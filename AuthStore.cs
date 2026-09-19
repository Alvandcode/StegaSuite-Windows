using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StegaSuite.Core;

namespace StegaSuite;

/// <summary>
/// Local per-user account store (fully offline).
/// Passwords and recovery codes are never stored in cleartext — only PBKDF2-HMAC-SHA256
/// verifiers. The backup file is AES-256-GCM-encrypted (via StegaCrypto) with a key
/// derived from the recovery code, so a forgotten password can be reset by restoring.
/// </summary>
public sealed class AuthException : Exception
{
    public string Key { get; }
    public AuthException(string key) : base(key) { Key = key; }
}

public sealed class UserRec
{
    public string Username { get; set; } = "";
    public string Salt { get; set; } = "";
    public int Iter { get; set; }
    public string Hash { get; set; } = "";
    public string RecSalt { get; set; } = "";
    public int RecIter { get; set; }
    public string RecHash { get; set; } = "";
    public string Created { get; set; } = "";
    public int Lang { get; set; }
    public int Theme { get; set; }
    public bool SeenBackupNotice { get; set; }
    public bool GoogleLinked { get; set; }
}

public static class AuthStore
{
    public static string StorePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StegaSuite", "users.json");

    private const int PassIter = 210_000;
    private const int RecIter = 210_000;
    private const int MinPass = 4;
    private const int MinUser = 2;
    private const int MaxUser = 32;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    // Unambiguous alphabet (no 0/O/1/I/L): 32 symbols -> 16 chars = 80-bit codes.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private static List<UserRec> Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return new List<UserRec>();
            var list = JsonSerializer.Deserialize<List<UserRec>>(File.ReadAllText(StorePath));
            return list ?? new List<UserRec>();
        }
        catch { return new List<UserRec>(); }
    }

    private static void Save(List<UserRec> users)
    {
        string? dir = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(users, JsonOpts));
    }

    public static bool HasUsers() => Load().Count > 0;

    public static UserRec? Get(string username) =>
        Load().FirstOrDefault(u => string.Equals(u.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase));

    internal static string Hash(string secret, byte[] salt, int iter) =>
        Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(secret, salt, iter, HashAlgorithmName.SHA256, 32));

    public static string NormCode(string? s) =>
        new string((s ?? "").ToUpperInvariant().Where(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')).ToArray());

    internal static string NewRecoveryCode()
    {
        byte[] rnd = RandomNumberGenerator.GetBytes(16);
        var sb = new StringBuilder(19);
        for (int i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append('-');
            sb.Append(CodeAlphabet[rnd[i] % CodeAlphabet.Length]);
        }
        return sb.ToString();
    }

    private static void CheckPassword(string pw)
    {
        if (string.IsNullOrEmpty(pw) || pw.Length < MinPass) throw new AuthException("passShort");
    }

    /// <returns>The one-time recovery code (show it to the user immediately).</returns>
    public static string Register(string username, string password, int lang = 0, int theme = 0)
    {
        username = (username ?? "").Trim();
        if (username.Length < MinUser || username.Length > MaxUser) throw new AuthException("authFillAll");
        CheckPassword(password);
        var users = Load();
        if (users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
            throw new AuthException("userExists");
        string code = CreateUser(users, username, password, lang, theme, googleLinked: false);
        Save(users);
        return code;
    }

    /// <summary>
    /// Passwordless account bound to a Gmail address (offline "Continue with Google").
    /// A random 256-bit password is set internally; entry is by email match on this
    /// device, recovery via recovery code + backup file.
    /// </summary>
    public static string RegisterGoogle(string email, int lang = 0, int theme = 0)
    {
        email = (email ?? "").Trim();
        if (email.Length <= "@gmail.com".Length
            || !email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            throw new AuthException("needGmail");
        var users = Load();
        if (users.Any(u => string.Equals(u.Username, email, StringComparison.OrdinalIgnoreCase)))
            throw new AuthException("userExists");
        string randomPw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string code = CreateUser(users, email, randomPw, lang, theme, googleLinked: true);
        Save(users);
        return code;
    }

    private static string CreateUser(List<UserRec> users, string username, string password,
        int lang, int theme, bool googleLinked)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        string code = NewRecoveryCode();
        byte[] recSalt = RandomNumberGenerator.GetBytes(16);
        users.Add(new UserRec
        {
            Username = username,
            Salt = Convert.ToBase64String(salt),
            Iter = PassIter,
            Hash = Hash(password, salt, PassIter),
            RecSalt = Convert.ToBase64String(recSalt),
            RecIter = RecIter,
            RecHash = Hash(NormCode(code), recSalt, RecIter),
            Created = DateTime.UtcNow.ToString("o"),
            Lang = lang,
            Theme = theme,
            GoogleLinked = googleLinked,
        });
        return code;
    }

    /// <returns>The account only if it was created via Google-link (else null → use password).</returns>
    public static UserRec? VerifyGoogle(string email)
    {
        var u = Get(email);
        return (u != null && u.GoogleLinked) ? u : null;
    }

    public static UserRec? Verify(string username, string password)
    {
        var u = Get(username);
        if (u == null || string.IsNullOrEmpty(password)) return null;
        string h = Hash(password, Convert.FromBase64String(u.Salt), u.Iter);
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(h), Convert.FromBase64String(u.Hash)) ? u : null;
    }

    public static bool VerifyRecovery(string username, string code)
    {
        var u = Get(username);
        if (u == null) return false;
        string norm = NormCode(code);
        if (norm.Length < 12) return false;
        string h = Hash(norm, Convert.FromBase64String(u.RecSalt), u.RecIter);
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(h), Convert.FromBase64String(u.RecHash));
    }

    public static void UpdatePassword(string username, string newPassword)
    {
        CheckPassword(newPassword);
        var users = Load();
        var u = users.FirstOrDefault(x => string.Equals(x.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new AuthException("userNotFound");
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        u.Salt = Convert.ToBase64String(salt);
        u.Iter = PassIter;
        u.Hash = Hash(newPassword, salt, PassIter);
        Save(users);
    }

    /// <summary>Links (or unlinks) real Google sign-in to an existing local account.</summary>
    public static void SetGoogleLinked(string username, bool linked)
    {
        var users = Load();
        var u = users.FirstOrDefault(x => string.Equals(x.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new AuthException("userNotFound");
        u.GoogleLinked = linked;
        Save(users);
    }

    /// <summary>Rotates the recovery code (must be logged in). Returns the new code to display once.</summary>
    public static string RotateRecovery(string username)
    {
        var users = Load();
        var u = users.FirstOrDefault(x => string.Equals(x.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new AuthException("userNotFound");
        string code = NewRecoveryCode();
        byte[] recSalt = RandomNumberGenerator.GetBytes(16);
        u.RecSalt = Convert.ToBase64String(recSalt);
        u.RecIter = RecIter;
        u.RecHash = Hash(NormCode(code), recSalt, RecIter);
        Save(users);
        return code;
    }

    public static void SavePrefs(string username, int lang, int theme)
    {
        try
        {
            var users = Load();
            var u = users.FirstOrDefault(x => string.Equals(x.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (u == null) return;
            u.Lang = Math.Max(0, Math.Min(4, lang));
            u.Theme = Math.Max(0, Math.Min(8, theme));
            Save(users);
        }
        catch { }
    }

    public static void SetNoticeSeen(string username)
    {
        try
        {
            var users = Load();
            var u = users.FirstOrDefault(x => string.Equals(x.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (u == null) return;
            u.SeenBackupNotice = true;
            Save(users);
        }
        catch { }
    }

    /// <summary>Exports an AES-GCM-encrypted backup (key = recovery code). Throws AuthException on bad code.</summary>
    public static void ExportBackup(string username, string code, string outPath)
    {
        var u = Get(username) ?? throw new AuthException("userNotFound");
        if (!VerifyRecovery(username, code)) throw new AuthException("wrongPass");
        var payload = new Dictionary<string, object>
        {
            ["app"] = "StegaSuite", ["kind"] = "account-backup", ["v"] = 1,
            ["username"] = u.Username, ["created"] = u.Created,
            ["recSalt"] = u.RecSalt, ["recIter"] = u.RecIter, ["recHash"] = u.RecHash,
            ["lang"] = u.Lang, ["theme"] = u.Theme, ["glink"] = u.GoogleLinked,
        };
        byte[] blob = StegaCrypto.Encrypt(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts)), NormCode(code));
        var wrapper = new Dictionary<string, object>
        {
            ["app"] = "StegaSuite", ["kind"] = "account-backup-file", ["v"] = 1,
            ["username"] = u.Username, ["data"] = Convert.ToBase64String(blob),
        };
        File.WriteAllText(outPath, JsonSerializer.Serialize(wrapper, JsonOpts));
    }

    /// <summary>Restores an account from backup and sets a NEW password. Returns the username.</summary>
    public static string ImportBackup(string path, string code, string newPassword)
    {
        CheckPassword(newPassword);
        Dictionary<string, JsonElement> wrap;
        try { wrap = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path)) ?? new(); }
        catch { throw new AuthException("restoreFail"); }
        if (!wrap.TryGetValue("kind", out var k) || k.GetString() != "account-backup-file"
            || !wrap.TryGetValue("data", out var d))
            throw new AuthException("restoreFail");
        byte[] plain;
        try { plain = StegaCrypto.Decrypt(Convert.FromBase64String(d.GetString() ?? ""), NormCode(code)); }
        catch { throw new AuthException("restoreFail"); }
        Dictionary<string, JsonElement> payload;
        try { payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Encoding.UTF8.GetString(plain)) ?? new(); }
        catch { throw new AuthException("restoreFail"); }
        if (!payload.TryGetValue("kind", out var pk) || pk.GetString() != "account-backup"
            || !payload.TryGetValue("username", out var un)) throw new AuthException("restoreFail");
        string username = un.GetString() ?? "";
        if (username.Length < MinUser) throw new AuthException("restoreFail");
        var users = Load();
        users.RemoveAll(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        users.Add(new UserRec
        {
            Username = username,
            Salt = Convert.ToBase64String(salt),
            Iter = PassIter,
            Hash = Hash(newPassword, salt, PassIter),
            RecSalt = payload.TryGetValue("recSalt", out var rs) ? rs.GetString() ?? "" : "",
            RecIter = payload.TryGetValue("recIter", out var ri) && ri.TryGetInt32(out int riv) ? riv : RecIter,
            RecHash = payload.TryGetValue("recHash", out var rh) ? rh.GetString() ?? "" : "",
            Created = payload.TryGetValue("created", out var c) ? c.GetString() ?? "" : "",
            Lang = payload.TryGetValue("lang", out var l) && l.TryGetInt32(out int lv) ? lv : 0,
            Theme = payload.TryGetValue("theme", out var t) && t.TryGetInt32(out int tv) ? tv : 0,
            GoogleLinked = payload.TryGetValue("glink", out var g) && g.ValueKind == JsonValueKind.True,
        });
        Save(users);
        return username;
    }
}
