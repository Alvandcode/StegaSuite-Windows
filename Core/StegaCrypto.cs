using System;
using System.Security.Cryptography;
using System.Text;

namespace StegaSuite.Core;

/// <summary>
/// AES-256-GCM + PBKDF2-HMAC-SHA256, 100% compatible with Android StegaSuite (StegaCrypto.kt).
/// Layout: MAGIC "STGS"(4) + VERSION(1) + salt(16) + nonce(12) + ciphertext+tag(16)
/// KDF: PBKDF2WithHmacSHA256, 600_000 iterations, 256-bit key. AAD = "STGS".
/// </summary>
public static class StegaCrypto
{
    private static readonly byte[] Magic = Encoding.UTF8.GetBytes("STGS");
    private const byte Version = 1;
    private const int Iterations = 600_000;
    private const int SaltLen = 16;
    private const int NonceLen = 12;
    private const int TagLen = 16;

    public static byte[] Encrypt(byte[] data, string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("پسورد خالی است / Password is empty");
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLen);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceLen);
        byte[] key = Derive(password, salt);
        try
        {
            byte[] ciphertext = new byte[data.Length];
            byte[] tag = new byte[TagLen];
            using (var aes = new AesGcm(key, TagLen))
            {
                aes.Encrypt(nonce, data, ciphertext, tag, Magic);
            }
            byte[] outBuf = new byte[4 + 1 + SaltLen + NonceLen + ciphertext.Length + TagLen];
            Buffer.BlockCopy(Magic, 0, outBuf, 0, 4);
            outBuf[4] = Version;
            Buffer.BlockCopy(salt, 0, outBuf, 5, SaltLen);
            Buffer.BlockCopy(nonce, 0, outBuf, 5 + SaltLen, NonceLen);
            Buffer.BlockCopy(ciphertext, 0, outBuf, 5 + SaltLen + NonceLen, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, outBuf, 5 + SaltLen + NonceLen + ciphertext.Length, TagLen);
            return outBuf;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static byte[] Decrypt(byte[] blob, string password)
    {
        if (blob.Length <= 33) throw new ArgumentException("داده رمز شده خراب است / Encrypted data is corrupted");
        for (int i = 0; i < 4; i++)
            if (blob[i] != Magic[i]) throw new ArgumentException("داده رمز شده نیست / Not encrypted data");
        if (blob[4] != Version) throw new ArgumentException("نسخه پشتیبانی نمی‌شود / Unsupported version");
        byte[] salt = new byte[SaltLen];
        byte[] nonce = new byte[NonceLen];
        Buffer.BlockCopy(blob, 5, salt, 0, SaltLen);
        Buffer.BlockCopy(blob, 21, nonce, 0, NonceLen);
        int encLen = blob.Length - 33; // ciphertext + tag
        if (encLen < TagLen) throw new ArgumentException("داده رمز شده خراب است / Encrypted data is corrupted");
        int ctLen = encLen - TagLen;
        byte[] ct = new byte[ctLen];
        byte[] tag = new byte[TagLen];
        Buffer.BlockCopy(blob, 33, ct, 0, ctLen);
        Buffer.BlockCopy(blob, 33 + ctLen, tag, 0, TagLen);
        byte[] key = Derive(password, salt);
        try
        {
            byte[] plain = new byte[ctLen];
            using (var aes = new AesGcm(key, TagLen))
            {
                aes.Decrypt(nonce, ct, tag, plain, Magic);
            }
            return plain;
        }
        catch (CryptographicException)
        {
            throw new ArgumentException("پسورد اشتباه است یا فایل خراب شده / Wrong password or corrupted file");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
}
