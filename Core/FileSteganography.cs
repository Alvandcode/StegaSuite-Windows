using System;
using System.Text;

namespace StegaSuite.Core;

/// <summary>
/// Generic binary-file LSB engine (magic SGF1), port of FileSteganography.kt.
/// Skips a small header ("safe offset") so the carrier file stays openable.
/// Bit order: MSB-first, 1 bit per carrier byte.
/// </summary>
public static class FileSteganography
{
    private static readonly byte[] Magic = new byte[] { (byte)'S', (byte)'G', (byte)'F', (byte)'1' };
    private const int HeaderSize = 12;
    private const long MaxPayload = 50L * 1024 * 1024;

    public static long CapacityBytes(byte[] data)
    {
        long safe = GetSafeOffset(data);
        return (data.LongLength - safe) / 8L;
    }

    public static long MaxPayloadBytes(byte[] data) => CapacityBytes(data) - HeaderSize - 50;

    public static byte[] Hide(byte[] carrierData, byte[] payload, string fileName, string? password, IProgress<double>? progress = null)
    {
        string safeName = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
        byte[] nameBytes = Encoding.UTF8.GetBytes(safeName);
        if (nameBytes.Length > 1024) throw new ArgumentException("Filename too long (max 1024 bytes)");

        byte[] inner = new byte[4 + nameBytes.Length + payload.Length];
        WriteInt32BE(inner, 0, nameBytes.Length);
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
        else
        {
            encPayload = StegaCrypto.Encrypt(inner, password);
        }

        byte[] packet = new byte[4 + 8 + encPayload.Length];
        Buffer.BlockCopy(Magic, 0, packet, 0, 4);
        WriteInt64BE(packet, 4, encPayload.LongLength);
        Buffer.BlockCopy(encPayload, 0, packet, 12, encPayload.Length);

        long safeStart = GetSafeOffset(carrierData);
        long capacityBits = (carrierData.LongLength - safeStart) * 8L;
        if ((long)packet.Length * 8L > capacityBits)
            throw new ArgumentException($"File too large! Capacity: {CapacityBytes(carrierData) / 1024}KB, Needed: {packet.Length / 1024}KB");

        byte[] output = (byte[])carrierData.Clone();
        long bitIndex = 0;
        long totalBits = (long)packet.Length * 8L;
        for (long i = safeStart; i < output.LongLength; i++)
        {
            if (bitIndex >= totalBits) break;
            int packetByte = packet[bitIndex / 8] & 0xFF;
            int bit = (packetByte >> (7 - (int)(bitIndex % 8))) & 1;
            output[i] = (byte)((output[i] & 0xFE) | bit);
            bitIndex++;
            if (progress != null && (i & 65535) == 0 && totalBits > 0)
                progress.Report((double)bitIndex / totalBits);
        }
        progress?.Report(1.0);
        return output;
    }

    public static ExtractResult Extract(byte[] carrierData, string? password, IProgress<double>? progress = null)
    {
        long safeStart = GetSafeOffset(carrierData);
        IProgress<double>? bulk = progress == null ? null
            : new Progress<double>(p => progress.Report(0.05 + 0.95 * p));
        int[] magicBits = ReadBits(carrierData, safeStart, 4 * 8);
        byte[] magic = BitsToBytes(magicBits);
        if (!EqualsBytes(magic, Magic))
            throw new ArgumentException("No hidden file found in this file");

        int[] headerBits = ReadBits(carrierData, safeStart, HeaderSize * 8);
        byte[] head = BitsToBytes(headerBits);
        long len = ReadInt64BE(head, 4);
        if (len < 0 || len > MaxPayload) throw new ArgumentException("Suspicious length");
        long needed = (HeaderSize + len) * 8L;
        if (needed > (carrierData.LongLength - safeStart) * 8L) throw new ArgumentException("Data corrupted");

        int[] all = ReadBits(carrierData, safeStart, (int)needed, bulk);
        byte[] encPayload = BitsToBytes(Sub(all, HeaderSize * 8, all.Length - HeaderSize * 8));

        byte[] inner;
        if (string.IsNullOrEmpty(password))
        {
            if (encPayload.Length < 4) throw new ArgumentException("Data corrupted");
            byte[] innerPart = new byte[encPayload.Length - 4];
            Buffer.BlockCopy(encPayload, 0, innerPart, 0, innerPart.Length);
            int stored = ReadInt32BE(encPayload, encPayload.Length - 4);
            int calc = unchecked((int)Crc32.Compute(innerPart));
            if (stored != calc) throw new ArgumentException("File corrupted or wrong password");
            inner = innerPart;
        }
        else
        {
            inner = StegaCrypto.Decrypt(encPayload, password);
        }

        if (inner.Length < 4) throw new ArgumentException("Internal data corrupted");
        int nameLen = ReadInt32BE(inner, 0);
        if (nameLen < 0 || nameLen > 1024 || inner.Length < 4 + nameLen)
            throw new ArgumentException("Filename corrupted");
        string name = Encoding.UTF8.GetString(inner, 4, nameLen);
        byte[] data = new byte[inner.Length - 4 - nameLen];
        Buffer.BlockCopy(inner, 4 + nameLen, data, 0, data.Length);
        return new ExtractResult(data, name);
    }

    public static long GetSafeOffset(byte[] data)
    {
        if (data.Length < 4) return 0L;
        int b0 = data[0] & 0xFF, b1 = data[1] & 0xFF, b2 = data[2] & 0xFF, b3 = data[3] & 0xFF;
        if (b0 == 0xFF && b1 == 0xD8) return FindJpegContentStart(data);          // JPEG
        if (b0 == 0x25 && b1 == 0x25) return FindPdfContentStart(data);           // %PDF? (kept identical to Kotlin)
        if (b0 == 0x50 && b1 == 0x4B && b2 == 0x03 && b3 == 0x04) return 30L;      // ZIP
        if (b0 == 0x52 && b1 == 0x69 && b2 == 0x63 && b3 == 0x68) return 12L;      // "Rich"
        if (b0 == 0x1A && b1 == 0x45 && b2 == 0xDF && b3 == 0xA3) return 4L;       // Matroska/WebM
        if (b0 == 0x47 && b1 == 0x49 && b2 == 0x46) return 13L;                   // GIF
        if (b0 == 0x49 && b1 == 0x49 && b2 == 0x2A && b3 == 0x00) return 8L;       // TIFF LE
        if (b0 == 0x4D && b1 == 0x4D && b2 == 0x00 && b3 == 0x2A) return 8L;       // TIFF BE
        return 4L;
    }

    private static long FindJpegContentStart(byte[] data)
    {
        int i = 2;
        while (i < data.Length - 1)
        {
            if ((data[i] & 0xFF) == 0xFF)
            {
                int marker = data[i + 1] & 0xFF;
                if (marker == 0xD9) return i + 2;
                if (marker == 0xDA) return i + 2;
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    if (i + 3 < data.Length)
                    {
                        int len = (((data[i + 2] & 0xFF) << 8) | (data[i + 3] & 0xFF));
                        i += 2 + len; continue;
                    }
                }
                if (marker == 0xE1 || marker == 0xE0 || marker == 0xFE || (marker >= 0xE2 && marker <= 0xEF))
                {
                    if (i + 3 < data.Length)
                    {
                        int len = (((data[i + 2] & 0xFF) << 8) | (data[i + 3] & 0xFF));
                        i += 2 + len; continue;
                    }
                }
                i += 2;
            }
            else i++;
        }
        return i;
    }

    private static long FindPdfContentStart(byte[] data)
    {
        long headerEnd = Math.Min(1024L, data.LongLength);
        long i = 0;
        while (i < headerEnd - 1)
        {
            if (data[i] == (byte)'%' && data[i + 1] == (byte)'E' && i + 4 < data.Length &&
                data[i + 2] == (byte)'O' && data[i + 3] == (byte)'F')
                return i;
            i++;
        }
        return headerEnd;
    }

    internal static int[] ReadBits(byte[] data, long startOffset, int count, IProgress<double>? progress = null)
    {
        var res = new int[count];
        int idx = 0;
        long bi = startOffset;
        while (bi < data.LongLength && idx < count)
        {
            int b = data[bi] & 0xFF;
            // NOTE: FileSteganography.kt writes each carrier byte's LSB as ONE data bit
            // in packet order, so reading back takes LSB of each successive byte.
            res[idx++] = b & 1;
            bi++;
            if (progress != null && (idx & 8191) == 0 && count > 0)
                progress.Report((double)idx / count);
        }
        progress?.Report(1.0);
        return res;
    }

    internal static byte[] BitsToBytes(int[] bits)
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

    internal static int[] Sub(int[] a, int from, int len)
    {
        var r = new int[len];
        Array.Copy(a, from, r, 0, len);
        return r;
    }

    internal static bool EqualsBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    internal static void WriteInt32BE(byte[] buf, int off, int v)
    {
        buf[off] = (byte)(v >> 24); buf[off + 1] = (byte)(v >> 16);
        buf[off + 2] = (byte)(v >> 8); buf[off + 3] = (byte)v;
    }
    internal static void WriteInt64BE(byte[] buf, int off, long v)
    {
        for (int i = 0; i < 8; i++) buf[off + i] = (byte)(v >> (56 - 8 * i));
    }
    internal static int ReadInt32BE(byte[] buf, int off) =>
        (buf[off] << 24) | ((buf[off + 1] & 0xFF) << 16) | ((buf[off + 2] & 0xFF) << 8) | (buf[off + 3] & 0xFF);
    internal static long ReadInt64BE(byte[] buf, int off)
    {
        long v = 0;
        for (int i = 0; i < 8; i++) v = (v << 8) | (uint)(buf[off + i] & 0xFF);
        return v;
    }
}
