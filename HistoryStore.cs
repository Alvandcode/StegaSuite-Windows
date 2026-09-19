using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StegaSuite;

/// <summary>Per-user operation history (hide/extract), stored as JSON, capped at 100 entries.</summary>
public sealed class HistEntry
{
    public string Time { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Detail { get; set; } = "";
}

public static class HistoryStore
{
    public static string BaseDir { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StegaSuite", "history");

    private static string PathFor(string? user)
    {
        string raw = string.IsNullOrEmpty(user) ? "x" : user;
        var safe = new string(raw.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
        if (safe.Length > 32) safe = safe.Substring(0, 32);
        return Path.Combine(BaseDir, "history_" + safe + ".json");
    }

    public static List<HistEntry> Load(string? user)
    {
        try
        {
            string p = PathFor(user);
            if (!File.Exists(p)) return new List<HistEntry>();
            return JsonSerializer.Deserialize<List<HistEntry>>(File.ReadAllText(p)) ?? new List<HistEntry>();
        }
        catch { return new List<HistEntry>(); }
    }

    public static void Add(string? user, string kindKey, string detail)
    {
        try
        {
            if (string.IsNullOrEmpty(user) || user == "__selftest") return;
            var list = Load(user);
            list.Add(new HistEntry
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Kind = kindKey,
                Detail = detail,
            });
            while (list.Count > 100) list.RemoveAt(0);
            Directory.CreateDirectory(BaseDir);
            File.WriteAllText(PathFor(user),
                JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static void Clear(string? user)
    {
        try
        {
            string p = PathFor(user);
            if (File.Exists(p)) File.Delete(p);
        }
        catch { }
    }
}
