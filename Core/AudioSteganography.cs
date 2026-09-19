using System;
using System.Collections.Generic;
using System.Text;

namespace StegaSuite.Core;

/// <summary>
/// WAV audio LSB engine (magic SGA1), port of AudioSteganography.kt.
/// 1 bit per audio sample (LSB of each channel sample). Bit order MSB-first.
/// </summary>
public static class AudioSteganography
{
    private static readonly byte[] Magic = new byte[] { (byte)'S', (byte)'G', (byte)'A', (byte)'1' };
    private const int HeaderSize = 12;
    private const long MaxPayload = 50L * 1024 * 1024;

    public static bool IsWav(byte[] data)
    {
        if (data.Length < 12) return false;
        return data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F' &&
               data[8] == (byte)'W' && data[9] == (byte)'A' && data[10] == (byte)'V' && data[11] == (byte)'E';
    }

    public static int FindDataChunkOffset(byte[] data)
    {
        if (data.Length < 12) return 44;
        int offset = 12;
        while (offset + 8 <= data.Length)
        {
            string chunkId = Encoding.ASCII.GetString(data, offset, 4);
            int chunkSize = BitConverter.ToInt32(data, offset + 4); // WAV is little-endian; x86/x64 is LE
            if (!BitConverter.IsLittleEndian) chunkSize = Reverse(chunkSize);
            if (chunkSize < 0 || (long)offset + 8 + chunkSize > data.Length) return 44;
            if (chunkId == "data") return offset + 8;
            offset += 8 + chunkSize + (chunkSize & 1);
        }
        return 44;
    }

    public static string GetAudioInfo(byte[] data)
    {
        if (!IsWav(data)) return "Not a WAV file";
        int channels = GetUInt16LE(data, 22);
        int sampleRate = GetInt32LE(data, 24);
        int bitsPerSample = GetUInt16LE(data, 34);
        int dataOffset = FindDataChunkOffset(data);
        long sampleDataSize = data.LongLength - dataOffset;
        long dur = (channels > 0 && sampleRate > 0 && bitsPerSample > 0)
            ? sampleDataSize * 8L / (channels * sampleRate * bitsPerSample) : 0L;
        return $"{sampleRate}Hz, {bitsPerSample}bit, {channels}ch, {dur}s";
    }

    public static long CapacityBytes(byte[] data)
    {
        if (!IsWav(data)) return 0L;
        int channels = GetUInt16LE(data, 22);
        int bitsPerSample = GetUInt16LE(data, 34);
        if (channels <= 0 || bitsPerSample <= 0) return 0L;
        int dataOffset = FindDataChunkOffset(data);
        long sampleDataSize = data.LongLength - dataOffset;
        int bytesPerSample = bitsPerSample / 8;
        if (bytesPerSample <= 0) return 0L;
        long totalSamples = sampleDataSize / (channels * bytesPerSample);
        return totalSamples * channels / 8L;
    }

    public static long MaxPayloadBytes(byte[] data) => CapacityBytes(data) - HeaderSize - 50;

    public static byte[] Hide(byte[] wavData, byte[] payload, string fileName, string? password, IProgress<double>? progress = null)
    {
        if (!IsWav(wavData)) throw new ArgumentException("Not a valid WAV file");
        string safeName = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
        byte[] nameBytes = Encoding.UTF8.GetBytes(safeName);
        if (nameBytes.Length > 1024) throw new ArgumentException("Filename too long");

        byte[] inner = new byte[4 + nameBytes.Length + payload.Length];
        FileSteganography.WriteInt32BE(inner, 0, nameBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, inner, 4, nameBytes.Length);
        Buffer.BlockCopy(payload, 0, inner, 4 + nameBytes.Length, payload.Length);

        byte[] encPayload;
        if (string.IsNullOrEmpty(password))
        {
            byte[] crc = Crc32.ComputeBigEndianBytes(inner);
            encPayload = new byte[inner.Length + 4];
            Buffer.BlockCopy(inner, 0, encPayload, 0, inner.Length);
            Buffer.BlockCopy(crc, 0, encPayload, inner.Length, 4);
        }
        else encPayload = StegaCrypto.Encrypt(inner, password);

        byte[] packet = new byte[4 + 8 + encPayload.Length];
        Buffer.BlockCopy(Magic, 0, packet, 0, 4);
        FileSteganography.WriteInt64BE(packet, 4, encPayload.LongLength);
        Buffer.BlockCopy(encPayload, 0, packet, 12, encPayload.Length);

        int channels = GetUInt16LE(wavData, 22);
        int bitsPerSample = GetUInt16LE(wavData, 34);
        if (channels <= 0 || bitsPerSample <= 0) throw new ArgumentException("Invalid WAV parameters");
        int bytesPerSample = bitsPerSample / 8;
        int dataOffset = FindDataChunkOffset(wavData);
        long totalSampleBytes = wavData.LongLength - dataOffset;
        long totalSamples = totalSampleBytes / (channels * bytesPerSample);
        long capacityBits = totalSamples * channels;
        if ((long)packet.Length * 8L > capacityBits)
            throw new ArgumentException($"File too large! Audio capacity: {CapacityBytes(wavData) / 1024}KB, Needed: {packet.Length / 1024}KB");

        byte[] output = (byte[])wavData.Clone();
        long bitIndex = 0;
        long totalBits = (long)packet.Length * 8L;
        for (long i = dataOffset; i < output.LongLength; i += bytesPerSample)
        {
            if (bitIndex >= totalBits) break;
            for (int ch = 0; ch < channels; ch++)
            {
                if (bitIndex >= totalBits) break;
                long sampleOffset = i + ch * bytesPerSample;
                if (sampleOffset + bytesPerSample > output.LongLength) break;
                long sample = 0;
                for (int b = 0; b < bytesPerSample; b++)
                    sample |= ((long)(output[sampleOffset + b] & 0xFF)) << (b * 8);
                int packetByte = packet[bitIndex / 8] & 0xFF;
                int dataBit = (packetByte >> (7 - (int)(bitIndex % 8))) & 1;
                sample = (sample & ~1L) | (uint)dataBit;
                for (int b = 0; b < bytesPerSample; b++)
                    output[sampleOffset + b] = (byte)((sample >> (b * 8)) & 0xFF);
                bitIndex++;
            }
            if (progress != null && ((i - dataOffset) & 65535) == 0 && totalBits > 0)
                progress.Report((double)bitIndex / totalBits);
        }
        progress?.Report(1.0);
        return output;
    }

    public static ExtractResult Extract(byte[] wavData, string? password, IProgress<double>? progress = null)
    {
        if (!IsWav(wavData)) throw new ArgumentException("Not a valid WAV file");
        int channels = GetUInt16LE(wavData, 22);
        int bitsPerSample = GetUInt16LE(wavData, 34);
        if (channels <= 0 || bitsPerSample <= 0) throw new ArgumentException("Invalid WAV parameters");
        int bytesPerSample = bitsPerSample / 8;
        int dataOffset = FindDataChunkOffset(wavData);

        var sampleBits = new List<int>();
        long scanTotal = wavData.LongLength - dataOffset;
        for (long i = dataOffset; i < wavData.LongLength; i += bytesPerSample)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                long sampleOffset = i + ch * bytesPerSample;
                if (sampleOffset + bytesPerSample > wavData.LongLength) break;
                long sample = 0;
                for (int b = 0; b < bytesPerSample; b++)
                    sample |= ((long)(wavData[sampleOffset + b] & 0xFF)) << (b * 8);
                sampleBits.Add((int)(sample & 1L));
            }
            if (progress != null && scanTotal > 0 && ((i - dataOffset) & 131071) == 0)
                progress.Report(0.05 + 0.90 * ((double)(i - dataOffset) / scanTotal));
        }
        progress?.Report(0.95);

        if (sampleBits.Count < HeaderSize * 8) throw new ArgumentException("No hidden file found in this audio");
        byte[] magic = BitsToBytes(sampleBits.GetRange(0, 4 * 8).ToArray());
        if (!FileSteganography.EqualsBytes(magic, Magic))
            throw new ArgumentException("No hidden file found in this audio");
        byte[] head = BitsToBytes(sampleBits.GetRange(0, HeaderSize * 8).ToArray());
        long len = FileSteganography.ReadInt64BE(head, 4);
        if (len < 0 || len > MaxPayload) throw new ArgumentException("Suspicious length");
        long needed = (HeaderSize + len) * 8L;
        if (needed > sampleBits.Count) throw new ArgumentException("Data corrupted");
        byte[] encPayload = BitsToBytes(sampleBits.GetRange(HeaderSize * 8, (int)needed - HeaderSize * 8).ToArray());

        byte[] inner;
        if (string.IsNullOrEmpty(password))
        {
            if (encPayload.Length < 4) throw new ArgumentException("Data corrupted");
            byte[] innerPart = new byte[encPayload.Length - 4];
            Buffer.BlockCopy(encPayload, 0, innerPart, 0, innerPart.Length);
            int stored = FileSteganography.ReadInt32BE(encPayload, encPayload.Length - 4);
            int calc = unchecked((int)Crc32.Compute(innerPart));
            if (stored != calc) throw new ArgumentException("File corrupted or wrong password");
            inner = innerPart;
        }
        else inner = StegaCrypto.Decrypt(encPayload, password);

        if (inner.Length < 4) throw new ArgumentException("Internal data corrupted");
        int nameLen = FileSteganography.ReadInt32BE(inner, 0);
        if (nameLen < 0 || nameLen > 1024 || inner.Length < 4 + nameLen)
            throw new ArgumentException("Filename corrupted");
        string name = Encoding.UTF8.GetString(inner, 4, nameLen);
        byte[] data = new byte[inner.Length - 4 - nameLen];
        Buffer.BlockCopy(inner, 4 + nameLen, data, 0, data.Length);
        return new ExtractResult(data, name);
    }

    private static byte[] BitsToBytes(int[] bits)
    {
        if (bits.Length % 8 != 0) throw new ArgumentException("Bit count must be multiple of 8");
        byte[] output = new byte[bits.Length / 8];
        for (int i = 0; i < output.Length; i++)
        {
            int v = 0;
            for (int j = 0; j < 8; j++) v = (v << 1) | bits[i * 8 + j];
            output[i] = (byte)v;
        }
        return output;
    }

    private static int GetUInt16LE(byte[] d, int off) => d[off] | (d[off + 1] << 8);
    private static int GetInt32LE(byte[] d, int off) =>
        d[off] | (d[off + 1] << 8) | (d[off + 2] << 16) | (d[off + 3] << 24);
    private static int Reverse(int v)
    {
        byte[] b = BitConverter.GetBytes(v);
        Array.Reverse(b);
        return BitConverter.ToInt32(b, 0);
    }
}
