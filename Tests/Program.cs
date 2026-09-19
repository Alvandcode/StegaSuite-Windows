using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using StegaSuite;
using StegaSuite.Core;

// Engine round-trip tests (UI is covered by the WPF app's own --selftest).
static class Tests
{
    static int pass = 0, fail = 0;

    static void Check(string name, Func<bool> fn)
    {
        try
        {
            if (fn()) { Console.WriteLine($"PASS: {name}"); pass++; }
            else { Console.WriteLine($"FAIL: {name} (returned false)"); fail++; }
        }
        catch (Exception ex) { Console.WriteLine($"FAIL: {name} ({ex.GetType().Name}: {ex.Message})"); fail++; }
    }

    static byte[] MakePng(int w, int h)
    {
        var rnd = new Random(42);
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                bmp.SetPixel(x, y, Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)));
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    static byte[] MakeBmp(int w, int h)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Teal);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Bmp);
        return ms.ToArray();
    }

    static byte[] MakeWav(int seconds = 2, int sampleRate = 44100)
    {
        int channels = 1, bits = 16;
        int dataLen = seconds * sampleRate * channels * (bits / 8);
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataLen);
        bw.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        bw.Write(16); bw.Write((short)1); bw.Write((short)channels);
        bw.Write(sampleRate); bw.Write(sampleRate * channels * (bits / 8));
        bw.Write((short)(channels * (bits / 8))); bw.Write((short)bits);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataLen);
        var rnd = new Random(7);
        for (int i = 0; i < dataLen; i++) bw.Write((byte)rnd.Next(256));
        return ms.ToArray();
    }

    static int Main()
    {
        Check("crypto round-trip (AES-256-GCM/PBKDF2)", () =>
        {
            byte[] plain = Encoding.UTF8.GetBytes("سلام StegaSuite! hello world 12345");
            byte[] blob = StegaCrypto.Encrypt(plain, "Str0ng!Pass");
            if (Encoding.ASCII.GetString(blob, 0, 4) != "STGS") return false;
            if (blob[4] != 1) return false;
            byte[] back = StegaCrypto.Decrypt(blob, "Str0ng!Pass");
            if (Encoding.UTF8.GetString(back) != "سلام StegaSuite! hello world 12345") return false;
            try { StegaCrypto.Decrypt(blob, "wrong"); return false; }
            catch (ArgumentException) { return true; }
        });

        Check("png hide/extract no-password", () =>
        {
            byte[] carrier = MakePng(200, 200);
            byte[] payload = Encoding.UTF8.GetBytes(new string('A', 5000));
            byte[] stego = PngSteganography.HideImage(carrier, payload, "doc.pdf", null);
            var type = PngSteganography.DetectCarrierType("out.png", stego);
            var r = PngSteganography.ExtractFromBytes(stego, null, type);
            return r.FileName == "doc.pdf" && r.Bytes.AsSpan().SequenceEqual(payload);
        });

        Check("png hide/extract password + FA filename", () =>
        {
            byte[] carrier = MakePng(200, 200);
            byte[] payload = new byte[8000]; new Random(3).NextBytes(payload);
            byte[] stego = PngSteganography.HideImage(carrier, payload, "گزارش محرمانه.docx", "رمز قوی 123!@#");
            var r = PngSteganography.ExtractFromBytes(stego, "رمز قوی 123!@#", CarrierType.PNG);
            return r.FileName == "گزارش محرمانه.docx" && r.Bytes.AsSpan().SequenceEqual(payload);
        });

        Check("png wrong password fails", () =>
        {
            byte[] carrier = MakePng(120, 120);
            byte[] stego = PngSteganography.HideImage(carrier, new byte[] { 1, 2, 3 }, "a.bin", "correct");
            try { PngSteganography.ExtractFromBytes(stego, "incorrect", CarrierType.PNG); return false; }
            catch (ArgumentException) { return true; }
        });

        Check("bmp hide/extract", () =>
        {
            byte[] carrier = MakeBmp(120, 120);
            byte[] payload = Encoding.UTF8.GetBytes("bmp payload test");
            byte[] stego = PngSteganography.HideImage(carrier, payload, "note.txt", null);
            var r = PngSteganography.ExtractFromBytes(stego, null, CarrierType.BMP);
            return r.FileName == "note.txt" && r.Bytes.AsSpan().SequenceEqual(payload);
        });

        Check("wav hide/extract password", () =>
        {
            byte[] carrier = MakeWav();
            byte[] payload = Encoding.UTF8.GetBytes("secret audio payload " + new string('z', 2000));
            byte[] stego = AudioSteganography.Hide(carrier, payload, "secret.zip", "audiopass");
            if (!AudioSteganography.IsWav(stego)) return false;
            var r = AudioSteganography.Extract(stego, "audiopass");
            return r.FileName == "secret.zip" && r.Bytes.AsSpan().SequenceEqual(payload);
        });

        Check("wav hide/extract no-password", () =>
        {
            byte[] carrier = MakeWav(1);
            byte[] payload = new byte[] { 9, 8, 7, 6, 5 };
            byte[] stego = AudioSteganography.Hide(carrier, payload, "x.bin", null);
            var r = AudioSteganography.Extract(stego, null);
            return r.Bytes.AsSpan().SequenceEqual(payload);
        });

        Check("generic hide/extract password", () =>
        {
            byte[] carrier = Encoding.UTF8.GetBytes(new string('Q', 20000));
            byte[] payload = Encoding.UTF8.GetBytes("generic secret");
            byte[] stego = FileSteganography.Hide(carrier, payload, "hidden.pdf", "p@ss");
            var r = FileSteganography.Extract(stego, "p@ss");
            return r.FileName == "hidden.pdf" && r.Bytes.AsSpan().SequenceEqual(payload);
        });

        Check("generic hide/extract no-password", () =>
        {
            byte[] carrier = new byte[20000]; new Random(11).NextBytes(carrier);
            byte[] payload = Encoding.UTF8.GetBytes("plain generic");
            byte[] stego = FileSteganography.Hide(carrier, payload, "f.txt", null);
            var r = FileSteganography.Extract(stego, null);
            return r.Bytes.AsSpan().SequenceEqual(payload);
        });

        Check("capacity enforcement (too big fails)", () =>
        {
            byte[] carrier = MakePng(20, 20);
            try { PngSteganography.HideImage(carrier, new byte[5000], "big.bin", null); return false; }
            catch (ArgumentException) { return true; }
        });

        Check("wire magic SGP2/SGA1/SGF1 + STGS", () =>
        {
            byte[] carrier = MakePng(100, 100);
            byte[] stego = PngSteganography.HideImage(carrier, new byte[] { 1 }, "a", null);
            using var bmp = PngSteganography.Decode(stego);
            var r = PngSteganography.Extract(bmp, null);
            byte[] wav = MakeWav(1);
            byte[] sw = AudioSteganography.Hide(wav, new byte[] { 1 }, "a", null);
            var rw = AudioSteganography.Extract(sw, null);
            byte[] car = new byte[5000];
            byte[] sg = FileSteganography.Hide(car, new byte[] { 1 }, "a", null);
            var rg = FileSteganography.Extract(sg, null);
            return r.Bytes.Length == 1 && rw.Bytes.Length == 1 && rg.Bytes.Length == 1;
        });

        Check("auth register/login + wrong password", () =>
        {
            AuthStore.StorePath = Path.Combine(Path.GetTempPath(), "stega_auth_test.json");
            try { File.Delete(AuthStore.StorePath); } catch { }
            string code = AuthStore.Register("ali", "s3cret!");
            if (code.Replace("-", "").Length != 16) return false;
            if (AuthStore.Verify("ali", "s3cret!") == null) return false;
            if (AuthStore.Verify("ali", "wrong") != null) return false;
            if (AuthStore.Verify("ALI", "s3cret!") == null) return false;
            if (AuthStore.Verify("nobody", "s3cret!") != null) return false;
            try { AuthStore.Register("ali", "other1"); return false; }
            catch (AuthException) { }
            try { AuthStore.Register("bob", "123"); return false; }
            catch (AuthException) { }
            return true;
        });

        Check("auth recovery code verify/normalize/rotate", () =>
        {
            string code = AuthStore.Register("sara", "pass99");
            if (!AuthStore.VerifyRecovery("sara", code)) return false;
            if (!AuthStore.VerifyRecovery("sara", code.ToLowerInvariant().Replace("-", " "))) return false;
            if (AuthStore.VerifyRecovery("sara", "WRONG-CODE-1234-5678")) return false;
            AuthStore.UpdatePassword("sara", "brandnew");
            if (AuthStore.Verify("sara", "brandnew") == null) return false;
            string code2 = AuthStore.RotateRecovery("sara");
            if (code2 == code || !AuthStore.VerifyRecovery("sara", code2)) return false;
            if (AuthStore.VerifyRecovery("sara", code)) return false;
            return true;
        });

        Check("auth backup export/import roundtrip", () =>
        {
            string code = AuthStore.Register("bakr", "oldpass1");
            string bak = Path.Combine(Path.GetTempPath(), "bakr_test.stegabak");
            AuthStore.ExportBackup("bakr", code, bak);
            if (!new FileInfo(bak).Exists) return false;
            string who = AuthStore.ImportBackup(bak, code, "newpass22");
            if (who != "bakr") return false;
            if (AuthStore.Verify("bakr", "newpass22") == null) return false;
            if (AuthStore.Verify("bakr", "oldpass1") != null) return false;
            if (!AuthStore.VerifyRecovery("bakr", code)) return false;
            try { AuthStore.ImportBackup(bak, "WRONGCODE00000000", "newpass33"); return false; }
            catch (AuthException) { }
            try { File.Delete(bak); } catch { }
            return true;
        });

        Check("auth prefs persist per user", () =>
        {
            AuthStore.SavePrefs("bakr", 2, 3);
            var u = AuthStore.Get("bakr");
            return u != null && u.Lang == 2 && u.Theme == 3;
        });

        Check("auth google link/login (offline)", () =>
        {
            string code = AuthStore.RegisterGoogle("test@gmail.com");
            if (code.Replace("-", "").Length != 16) return false;
            if (AuthStore.VerifyGoogle("TEST@gmail.com") == null) return false;
            if (AuthStore.VerifyGoogle("other@gmail.com") != null) return false;
            try { AuthStore.RegisterGoogle("not-an-email"); return false; }
            catch (AuthException) { }
            AuthStore.Register("plain", "pass1234");
            if (AuthStore.VerifyGoogle("plain") != null) return false; // password account: no bypass
            AuthStore.SetGoogleLinked("plain", true);
            if (AuthStore.VerifyGoogle("plain") == null) return false;
            AuthStore.SetGoogleLinked("plain", false);
            if (AuthStore.VerifyGoogle("plain") != null) return false;
            return true;
        });

        Check("google pkce vector (RFC 7636) + auth url", () =>
        {
            string ch = GoogleAuth.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");
            if (ch != "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM") return false;
            string url = GoogleAuth.BuildAuthUrl("CID", "http://127.0.0.1:9/", "ST", ch);
            if (!url.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?")) return false;
            foreach (var must in new[] { "client_id=CID", "response_type=code", "code_challenge_method=S256",
                "redirect_uri=" + Uri.EscapeDataString("http://127.0.0.1:9/"), "scope=" })
                if (!url.Contains(must)) return false;
            return true;
        });

        Check("appconfig save/load roundtrip", () =>
        {
            AppConfig.ConfigPath = Path.Combine(Path.GetTempPath(), "stega_cfg_test.json");
            try { File.Delete(AppConfig.ConfigPath); } catch { }
            AppConfig.GoogleClientId = "test-id-123";
            AppConfig.GoogleClientSecret = "test-secret-abc";
            AppConfig.Lang = 3; AppConfig.Theme = 7;
            AppConfig.WinW = 1024; AppConfig.WinH = 768;
            AppConfig.WinLeft = 50; AppConfig.WinTop = 60; AppConfig.WinMax = true;
            AppConfig.Save();
            AppConfig.GoogleClientId = "";
            AppConfig.GoogleClientSecret = "";
            AppConfig.Lang = 0; AppConfig.Theme = 0;
            AppConfig.WinW = 0; AppConfig.WinH = 0;
            AppConfig.WinLeft = 0; AppConfig.WinTop = 0; AppConfig.WinMax = false;
            AppConfig.Load();
            bool ok = AppConfig.GoogleClientId == "test-id-123"
                && AppConfig.GoogleClientSecret == "test-secret-abc"
                && AppConfig.Lang == 3 && AppConfig.Theme == 7
                && AppConfig.WinW == 1024 && AppConfig.WinH == 768
                && AppConfig.WinLeft == 50 && AppConfig.WinTop == 60 && AppConfig.WinMax;
            try { File.Delete(AppConfig.ConfigPath); } catch { }
            return ok;
        });

        Console.WriteLine($"--- {pass} passed, {fail} failed ---");
        return fail == 0 ? 0 : 1;
    }
}
