using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace StegaSuite.Core;

/// <summary>
/// Unified image steganography engine (magic SGP2, legacy SGP1 read-only),
/// port of PngSteganography.kt. Pixel RGB LSB, MSB-first bit order.
/// Output is always opaque (flattened on white) and PNG-encoded — same as Android.
/// </summary>
public static class PngSteganography
{
    private static readonly byte[] MagicV1 = new byte[] { (byte)'S', (byte)'G', (byte)'P', (byte)'1' };
    private static readonly byte[] MagicV2 = new byte[] { (byte)'S', (byte)'G', (byte)'P', (byte)'2' };
    private const int HeaderV1 = 12;
    private const int HeaderV2 = 12;
    private const long MaxPayload = 50L * 1024 * 1024;

    // ── Carrier detection (identical mapping to Android) ──
    public static CarrierType DetectCarrierType(string fileName, byte[] data)
    {
        if (data.Length >= 4 && AudioSteganography.IsWav(data)) return CarrierType.WAV;
        string lower = (fileName ?? "").ToLowerInvariant();
        if (lower.EndsWith(".png")) return CarrierType.PNG;
        if (lower.EndsWith(".bmp")) return CarrierType.BMP;
        if (lower.EndsWith(".tiff") || lower.EndsWith(".tif")) return CarrierType.TIFF;
        if (lower.EndsWith(".webp")) return CarrierType.WEBP;
        if (lower.EndsWith(".wav")) return CarrierType.WAV;
        return CarrierType.GENERIC; // mp3/flac/mp4/pdf/zip/txt/… all generic, like Android
    }

    public static string GetCarrierDescription(CarrierType type) => type switch
    {
        CarrierType.PNG => "PNG Image (lossless, recommended)",
        CarrierType.BMP => "BMP Image (lossless)",
        CarrierType.TIFF => "TIFF Image (lossless)",
        CarrierType.WEBP => "WebP Image (lossless)",
        CarrierType.WAV => "WAV Audio (lossless)",
        CarrierType.GENERIC => "Generic File (may lose data in lossy formats)",
        _ => "Unknown Format",
    };

    public static long CapacityBytesForType(CarrierType type, byte[] data)
    {
        switch (type)
        {
            case CarrierType.PNG:
            case CarrierType.BMP:
                try
                {
                    using var bmp = Decode(data);
                    return (long)bmp.Width * bmp.Height * 3L / 8L;
                }
                catch { return 0L; }
            case CarrierType.WAV:
                return AudioSteganography.CapacityBytes(data);
            default:
                return FileSteganography.CapacityBytes(data);
        }
    }

    public static long CapacityBytes(Bitmap bitmap) => (long)bitmap.Width * bitmap.Height * 3L / 8L;
    public static long MaxPayloadBytes(Bitmap bitmap) => CapacityBytes(bitmap) - HeaderV2 - 50;

    // ── High-level: bytes in / bytes out ──
    public static byte[] HideImage(byte[] carrierImageBytes, byte[] payload, string fileName, string? password, IProgress<double>? progress = null)
    {
        using var bmp = Decode(carrierImageBytes);
        using var output = Hide(bmp, payload, fileName, password, progress);
        progress?.Report(1.0);
        return EncodePng(output);
    }

    public static byte[] HideGeneric(byte[] carrierData, byte[] payload, string fileName, string? password, CarrierType carrierType, IProgress<double>? progress = null) =>
        carrierType == CarrierType.WAV
            ? AudioSteganography.Hide(carrierData, payload, fileName, password, progress)
            : FileSteganography.Hide(carrierData, payload, fileName, password, progress);

    public static ExtractResult ExtractGeneric(byte[] carrierData, string? password, CarrierType carrierType, IProgress<double>? progress = null) =>
        carrierType == CarrierType.WAV
            ? AudioSteganography.Extract(carrierData, password, progress)
            : FileSteganography.Extract(carrierData, password, progress);

    public static ExtractResult ExtractFromBytes(byte[] carrierData, string? password, CarrierType carrierType, IProgress<double>? progress = null)
    {
        if (carrierType == CarrierType.PNG || carrierType == CarrierType.BMP)
        {
            try
            {
                using var bmp = Decode(carrierData);
                var r = Extract(bmp, password, progress);
                progress?.Report(1.0);
                return r;
            }
            catch (ArgumentException) { /* fall through to generic */ }
        }
        var g = ExtractGeneric(carrierData, password, carrierType, progress);
        progress?.Report(1.0);
        return g;
    }

    // ── Bitmap-level hide/extract ──
    public static Bitmap Hide(Bitmap bitmap, byte[] payload, string fileName, string? password, IProgress<double>? progress = null)
    {
        string safeName = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
        byte[] nameBytes = Encoding.UTF8.GetBytes(safeName);
        if (nameBytes.Length > 1024) throw new ArgumentException("Filename too long");

        byte[] inner = new byte[4 + nameBytes.Length + payload.Length];
        FileSteganography.WriteInt32BE(inner, 0, nameBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, inner, 4, nameBytes.Length);
        Buffer.BlockCopy(payload, 0, inner, 4 + nameBytes.Length, payload.Length);

        byte[] enc;
        if (string.IsNullOrEmpty(password))
        {
            byte[] crc = Crc32.ComputeBigEndianBytes(inner);
            enc = new byte[inner.Length + 4];
            Buffer.BlockCopy(inner, 0, enc, 0, inner.Length);
            Buffer.BlockCopy(crc, 0, enc, inner.Length, 4);
        }
        else enc = StegaCrypto.Encrypt(inner, password);

        byte[] packet = new byte[4 + 8 + enc.Length];
        Buffer.BlockCopy(MagicV2, 0, packet, 0, 4);
        FileSteganography.WriteInt64BE(packet, 4, enc.LongLength);
        Buffer.BlockCopy(enc, 0, packet, 12, enc.Length);

        using var opaque = FlattenToOpaque(bitmap);
        long capacityBits = (long)opaque.Width * opaque.Height * 3L;
        if ((long)packet.Length * 8L > capacityBits)
            throw new ArgumentException($"File too large! Capacity: {CapacityBytes(opaque) / 1024}KB, Needed: {packet.Length / 1024}KB");

        var output = new Bitmap(opaque.Width, opaque.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, output.Width, output.Height);
        BitmapData srcData = opaque.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData dstData = output.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = srcData.Stride;
            int h = opaque.Height, w = opaque.Width;
            byte[] row = new byte[Math.Abs(stride)];
            long bitIndex = 0;
            long totalBits = (long)packet.Length * 8L;
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(IntPtr.Add(srcData.Scan0, y * stride), row, 0, row.Length);
                for (int x = 0; x < w; x++)
                {
                    if (bitIndex >= totalBits) break;
                    int o = x * 4;
                    // BGRA in memory: row[o]=B, [o+1]=G, [o+2]=R, [o+3]=A(255)
                    int r = row[o + 2], g = row[o + 1], b = row[o];
                    int[] ch = { r, g, b };
                    for (int c = 0; c < 3; c++)
                    {
                        if (bitIndex >= totalBits) break;
                        int packetByte = packet[bitIndex / 8] & 0xFF;
                        int bit = (packetByte >> (7 - (int)(bitIndex % 8))) & 1;
                        ch[c] = (ch[c] & 0xFE) | bit;
                        bitIndex++;
                    }
                    row[o] = (byte)ch[2]; row[o + 1] = (byte)ch[1]; row[o + 2] = (byte)ch[0];
                }
                Marshal.Copy(row, 0, IntPtr.Add(dstData.Scan0, y * stride), row.Length);
                if (progress != null && (y & 15) == 0 && totalBits > 0)
                    progress.Report((double)bitIndex / totalBits);
                if (bitIndex >= totalBits)
                {
                    // copy remaining rows untouched
                    for (int y2 = y + 1; y2 < h; y2++)
                    {
                        Marshal.Copy(IntPtr.Add(srcData.Scan0, y2 * stride), row, 0, row.Length);
                        Marshal.Copy(row, 0, IntPtr.Add(dstData.Scan0, y2 * stride), row.Length);
                    }
                    break;
                }
            }
        }
        finally
        {
            opaque.UnlockBits(srcData);
            output.UnlockBits(dstData);
        }
        return output;
    }

    public static ExtractResult Extract(Bitmap bitmap, string? password, IProgress<double>? progress = null)
    {
        // Small header reads report nothing; only the final bulk read drives progress.
        IProgress<double>? bulk = progress == null ? null
            : new Progress<double>(p => progress.Report(0.05 + 0.95 * p));
        int[] magicBits = ReadBits(bitmap, 4 * 8);
        byte[] magic = BitsToBytes(magicBits);

        if (FileSteganography.EqualsBytes(magic, MagicV1))
        {
            // Legacy V1: raw payload, no filename
            int[] headerBits = ReadBits(bitmap, HeaderV1 * 8);
            byte[] head = BitsToBytes(headerBits);
            long len = FileSteganography.ReadInt64BE(head, 4);
            if (len < 0 || len > int.MaxValue) throw new ArgumentException("Invalid length");
            long needed = (HeaderV1 + len) * 8L;
            if (needed > (long)bitmap.Width * bitmap.Height * 3L) throw new ArgumentException("Data corrupted");
            int[] all = ReadBits(bitmap, (int)needed, bulk);
            byte[] payload = BitsToBytes(FileSteganography.Sub(all, HeaderV1 * 8, all.Length - HeaderV1 * 8));
            byte[] data = string.IsNullOrEmpty(password) ? payload : StegaCrypto.Decrypt(payload, password);
            return new ExtractResult(data, "recovered_file");
        }

        if (!FileSteganography.EqualsBytes(magic, MagicV2))
            throw new ArgumentException("No hidden file found");

        int[] headerBits2 = ReadBits(bitmap, HeaderV2 * 8);
        byte[] head2 = BitsToBytes(headerBits2);
        long len2 = FileSteganography.ReadInt64BE(head2, 4);
        if (len2 < 0 || len2 > MaxPayload) throw new ArgumentException("Suspicious length");
        long needed2 = (HeaderV2 + len2) * 8L;
        if (needed2 > (long)bitmap.Width * bitmap.Height * 3L) throw new ArgumentException("Data corrupted");
        int[] all2 = ReadBits(bitmap, (int)needed2, bulk);
        byte[] encPayload = BitsToBytes(FileSteganography.Sub(all2, HeaderV2 * 8, all2.Length - HeaderV2 * 8));

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
        byte[] data2 = new byte[inner.Length - 4 - nameLen];
        Buffer.BlockCopy(inner, 4 + nameLen, data2, 0, data2.Length);
        return new ExtractResult(data2, name);
    }

    // ── Helpers ──
    public static Bitmap Decode(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        using var tmp = new Bitmap(ms);
        var clone = new Bitmap(tmp.Width, tmp.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(clone))
        {
            g.DrawImage(tmp, 0, 0, tmp.Width, tmp.Height);
        }
        return clone;
    }

    public static byte[] EncodePng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static Bitmap FlattenToOpaque(Bitmap src)
    {
        var flat = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(flat))
        {
            g.Clear(Color.White);
            g.DrawImage(src, 0, 0, src.Width, src.Height);
        }
        return flat;
    }

    private static int[] ReadBits(Bitmap bitmap, int count, IProgress<double>? progress = null)
    {
        var result = new int[count];
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            byte[] row = new byte[Math.Abs(stride)];
            int idx = 0;
            for (int y = 0; y < bitmap.Height && idx < count; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), row, 0, row.Length);
                for (int x = 0; x < bitmap.Width && idx < count; x++)
                {
                    int o = x * 4;
                    int r = row[o + 2], g = row[o + 1], b = row[o];
                    int[] chs = { r, g, b };
                    foreach (int c in chs)
                    {
                        if (idx >= count) break;
                        result[idx++] = c & 1;
                    }
                }
                if (progress != null && (idx & 4095) == 0 && count > 0)
                    progress.Report((double)idx / count);
            }
        }
        finally { bitmap.UnlockBits(data); }
        progress?.Report(1.0);
        return result;
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
}
