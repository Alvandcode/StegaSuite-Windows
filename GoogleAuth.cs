using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StegaSuite;

/// <summary>
/// Global (per-machine) app settings, e.g. the Google OAuth client ID.
/// </summary>
public static class AppConfig
{
    public static string ConfigPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StegaSuite", "config.json");

    public static string GoogleClientId { get; set; } = "";
    public static string GoogleClientSecret { get; set; } = "";

    /// <summary>Last-used global prefs (language/theme/window geometry).</summary>
    public static int Lang { get; set; }
    public static int Theme { get; set; }
    public static double WinW { get; set; }
    public static double WinH { get; set; }
    public static double WinLeft { get; set; }
    public static double WinTop { get; set; }
    public static bool WinMax { get; set; }

    /// <summary>False during SelfTest so automated runs never touch real settings.</summary>
    public static bool PersistenceEnabled { get; set; } = true;

    public static void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
            var root = doc.RootElement;
            if (root.TryGetProperty("googleClientId", out var v))
                GoogleClientId = v.GetString()?.Trim() ?? "";
            if (root.TryGetProperty("googleClientSecret", out var s))
                GoogleClientSecret = s.GetString()?.Trim() ?? "";
            if (root.TryGetProperty("lang", out var l) && l.TryGetInt32(out int li)) Lang = li;
            if (root.TryGetProperty("theme", out var t) && t.TryGetInt32(out int ti)) Theme = ti;
            if (root.TryGetProperty("winW", out var w) && w.TryGetDouble(out double ww)) WinW = ww;
            if (root.TryGetProperty("winH", out var h) && h.TryGetDouble(out double hh)) WinH = hh;
            if (root.TryGetProperty("winLeft", out var x) && x.TryGetDouble(out double xx)) WinLeft = xx;
            if (root.TryGetProperty("winTop", out var y) && y.TryGetDouble(out double yy)) WinTop = yy;
            if (root.TryGetProperty("winMax", out var m) && (m.ValueKind == JsonValueKind.True || m.ValueKind == JsonValueKind.False))
                WinMax = m.GetBoolean();
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            if (!PersistenceEnabled) return;
            string? dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["googleClientId"] = GoogleClientId.Trim(),
                    ["googleClientSecret"] = GoogleClientSecret.Trim(),
                    ["lang"] = Lang,
                    ["theme"] = Theme,
                    ["winW"] = WinW,
                    ["winH"] = WinH,
                    ["winLeft"] = WinLeft,
                    ["winTop"] = WinTop,
                    ["winMax"] = WinMax,
                }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

/// <summary>
/// Real Google sign-in for an installed (desktop) app: OAuth 2.0 authorization-code
/// flow with PKCE and a loopback redirect (http://127.0.0.1:port), opened in the
/// system browser. No client secret, no external packages — pure HttpClient +
/// a minimal TcpListener-based loopback receiver (works without admin rights,
/// unlike HttpListener which needs a URL reservation).
/// Requires a "Desktop"-type OAuth client ID from Google Cloud Console and internet.
/// </summary>
public static class GoogleAuth
{
    public const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    public const string UserinfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";

    public sealed class GoogleAccount
    {
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Challenge(string verifier)
    {
        using var sha = SHA256.Create();
        return Base64Url(sha.ComputeHash(Encoding.UTF8.GetBytes(verifier)));
    }

    public static string NewState() => Base64Url(RandomNumberGenerator.GetBytes(16));

    public static string BuildAuthUrl(string clientId, string redirectUri, string state, string challenge)
    {
        var q = new StringBuilder(AuthEndpoint);
        q.Append("?client_id=").Append(Uri.EscapeDataString(clientId));
        q.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        q.Append("&response_type=code");
        q.Append("&scope=").Append(Uri.EscapeDataString("openid email profile"));
        q.Append("&state=").Append(Uri.EscapeDataString(state));
        q.Append("&code_challenge=").Append(Uri.EscapeDataString(challenge));
        q.Append("&code_challenge_method=S256");
        q.Append("&prompt=select_account");
        return q.ToString();
    }

    /// <summary>Opens the browser, waits for the loopback callback, exchanges the code
    /// and returns the verified Google profile. Throws on cancel/timeout/denial/error.
    /// clientSecret is optional and only sent when Google demands it for the client.</summary>
    public static async Task<GoogleAccount> SignInAsync(
        string clientId, string okTitle, string okBody, CancellationToken ct,
        IProgress<string>? progress = null, string? clientSecret = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("missing client ID");
        using var receiver = new LoopbackReceiver();
        string state = NewState();
        string verifier = NewVerifier();
        string url = BuildAuthUrl(clientId, receiver.Url, state, Challenge(verifier));
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { throw new InvalidOperationException("cannot open browser: " + ex.Message); }
        try { progress?.Report("browser"); } catch { }

        var (code, recvState, error) = await receiver.WaitForCodeAsync(okTitle, okBody, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException("google: " + error);
        if (string.IsNullOrEmpty(code))
            throw new InvalidOperationException("no code received");
        if (!string.Equals(recvState, state, StringComparison.Ordinal))
            throw new InvalidOperationException("state mismatch");
        try { progress?.Report("code"); } catch { }

        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var formFields = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = receiver.Url,
            ["grant_type"] = "authorization_code",
        };
        if (!string.IsNullOrEmpty(clientSecret)) formFields["client_secret"] = clientSecret;
        var form = new System.Net.Http.FormUrlEncodedContent(formFields);
        string tokenBody;
        try
        {
            using var resp = await http.PostAsync(TokenEndpoint, form, ct).ConfigureAwait(false);
            tokenBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException("token: " + DescribeOAuthError(tokenBody));
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex) { throw new InvalidOperationException("network: " + ex.Message); }

        string access;
        try
        {
            using var doc = JsonDocument.Parse(tokenBody);
            access = doc.RootElement.GetProperty("access_token").GetString() ?? "";
        }
        catch { throw new InvalidOperationException("bad token response"); }
        if (access.Length == 0) throw new InvalidOperationException("bad token response");
        try { progress?.Report("token"); } catch { }

        try
        {
            using var req = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Get, UserinfoEndpoint);
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);
            using var uresp = await http.SendAsync(req, ct).ConfigureAwait(false);
            string ubody = await uresp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!uresp.IsSuccessStatusCode)
                throw new InvalidOperationException("userinfo: " + DescribeOAuthError(ubody));
            using var udoc = JsonDocument.Parse(ubody);
            var root = udoc.RootElement;
            bool verified = root.TryGetProperty("email_verified", out var ev)
                && ev.ValueKind == JsonValueKind.True;
            string email = root.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
            string name = root.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
            if (!verified || email.Length == 0)
                throw new InvalidOperationException("email not verified by Google");
            return new GoogleAccount { Email = email, Name = name };
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex) { throw new InvalidOperationException("network: " + ex.Message); }
    }

    /// <returns>"error-code :: description" for precise diagnostics.</returns>
    private static string DescribeOAuthError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            string code = doc.RootElement.TryGetProperty("error", out var e)
                ? e.GetString() ?? "unknown" : "unknown";
            string desc = doc.RootElement.TryGetProperty("error_description", out var d)
                ? d.GetString() ?? "" : "";
            if (desc.Length == 0) desc = body.Substring(0, Math.Min(160, body.Length));
            return code + " :: " + desc;
        }
        catch { }
        return "unknown :: " + (string.IsNullOrEmpty(body) ? "empty response" : body.Substring(0, Math.Min(160, body.Length)));
    }

    /// <summary>Minimal loopback HTTP receiver over TcpListener (no admin needed).</summary>
    internal sealed class LoopbackReceiver : IDisposable
    {
        private readonly TcpListener _listener;
        public string Url { get; }

        public LoopbackReceiver()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/";
        }

        public async Task<(string? Code, string? State, string? Error)> WaitForCodeAsync(
            string okTitle, string okBody, CancellationToken ct)
        {
            using var reg = ct.Register(() => { try { _listener.Stop(); } catch { } });
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false); }
            catch { ct.ThrowIfCancellationRequested(); throw new OperationCanceledException(ct); }
            using (client)
            using (var stream = client.GetStream())
            {
                var sb = new StringBuilder();
                var buf = new byte[4096];
                while (!sb.ToString().Contains("\r\n\r\n"))
                {
                    int n;
                    try { n = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false); }
                    catch { break; }
                    if (n == 0) break;
                    sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                    if (sb.Length > 32768) break;
                }
                string query = "";
                var lines = sb.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length > 0)
                {
                    var parts = lines[0].Split(' ');
                    if (parts.Length >= 2)
                    {
                        int q = parts[1].IndexOf('?');
                        if (q >= 0 && q + 1 < parts[1].Length) query = parts[1].Substring(q + 1);
                    }
                }
                var qd = ParseQuery(query);
                string html = "<html><head><meta charset=\"utf-8\"><title>" + okTitle + "</title></head>"
                    + "<body style=\"font-family:sans-serif;text-align:center;padding-top:60px\"><h2>"
                    + okBody + "</h2></body></html>";
                byte[] body = Encoding.UTF8.GetBytes(html);
                byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: "
                    + body.Length + "\r\nConnection: close\r\n\r\n");
                try
                {
                    await stream.WriteAsync(head, 0, head.Length, ct).ConfigureAwait(false);
                    await stream.WriteAsync(body, 0, body.Length, ct).ConfigureAwait(false);
                }
                catch { }
                qd.TryGetValue("code", out string? code);
                qd.TryGetValue("state", out string? state);
                qd.TryGetValue("error", out string? error);
                return (code, state, error);
            }
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in query.Split('&'))
            {
                if (pair.Length == 0) continue;
                int eq = pair.IndexOf('=');
                string k = eq < 0 ? pair : pair.Substring(0, eq);
                string v = eq < 0 ? "" : pair.Substring(eq + 1);
                try { d[Uri.UnescapeDataString(k.Replace('+', ' '))] = Uri.UnescapeDataString(v.Replace('+', ' ')); }
                catch { d[k] = v; }
            }
            return d;
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { }
        }
    }
}
