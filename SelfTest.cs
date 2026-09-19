using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace StegaSuite;

/// <summary>
/// Headless UI verification, run via `StegaSuite.exe --selftest` (or `dotnet run -- --selftest`).
/// Exercises 3 window sizes x 5 languages x 9 themes x 6 pages WITHOUT showing
/// any window (pure Measure/Arrange), and fails on any exception, NaN bounds,
/// collapsed key control, or wrong flow direction. Exit code 0 = healthy.
/// </summary>
public static class SelfTest
{
    private static readonly string[] Reqs =
    {
        "FCarrierH", "FPayload", "FPassH", "FPassHShow", "FCarrierE", "FPassE", "FPassEShow", "FWallet",
        "BtnGoHide", "BtnGoExtract", "Nav0", "Nav1", "Nav2", "Nav3", "Nav4", "Nav5",
        "TxtLog",
        "Lang0", "Lang1", "Lang2", "Lang3", "Lang4",
        "Theme0", "Theme1", "Theme2", "Theme3", "Theme4", "Theme5",
        "Theme6", "Theme7", "Theme8",
        "BtnBrowseCH", "BtnBrowseP", "BtnBrowseCE", "BtnStar", "BtnCopy",
        "ChkShowH", "ChkShowE", "BtnHistory",
        "BtnBackupExport", "BtnRotateRecovery", "BtnLogout", "FExportCode",
    };
    private static readonly string[] Texts =
    {
        "LblTitle", "LblSub", "LblStatus", "LblStatusCap",
        "LblCarrierH", "LblPayloadH", "LblPassH", "LblCarrierE", "LblPassE",
        "LblAboutTitle", "LblAboutDesc", "LblFeatures",
        "LblContactTitle", "LblSupportTitle", "LblSupportStar", "LblWalletCap",
        "LblLangCap", "LblDarkGroup", "LblLightGroup",
        "LblAccountCap", "LblAccountName", "LblExportCodeCap", "LblBackupHint",
    };

    private static bool EffectiveVisible(FrameworkElement el)
    {
        DependencyObject? d = el;
        while (d != null)
        {
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d);
        }
        return true;
    }

    public static int Run()
    {
        var problems = new List<string>();
        try
        {
            AppConfig.PersistenceEnabled = false; // never touch real settings
            var win = new MainWindow();
            win.ShowActivated = false;
            win.Left = -20000;
            win.Top = -20000;
            win.Show(); // creates the HWND off-screen so real layout runs (never visible)
            win.AuthBypass(); // skip the login gate: overlay would cover everything
            var sizes = new[] { new Size(880, 740), new Size(720, 600), new Size(1200, 860) };
            foreach (var s in sizes)
            {
                win.Width = s.Width;
                win.Height = s.Height;
                win.UpdateLayout();
                for (int li = 0; li < 5; li++)
                {
                    win.SetLang(li);
                    for (int ti = 0; ti < 9; ti++)
                    {
                        win.SetTheme(ti);
                        for (int pg = 0; pg < 6; pg++)
                        {
                            SetPage(win, pg);
                            win.UpdateLayout();
                            string tag = $"{s.Width:F0}x{s.Height:F0} L{li} T{ti} P{pg}";
                            foreach (var n in Reqs)
                            {
                                var el = win.FindName(n) as FrameworkElement;
                                if (el == null) { problems.Add($"{tag} missing:{n}"); continue; }
                                if (!EffectiveVisible(el)) continue;
                                if (double.IsNaN(el.ActualWidth) || double.IsNaN(el.ActualHeight)
                                    || el.ActualWidth < 30 || el.ActualHeight < 12)
                                    problems.Add($"{tag} {n} bad {el.ActualWidth:F0}x{el.ActualHeight:F0}");
                            }
                            foreach (var n in Texts)
                            {
                                var el = win.FindName(n) as TextBlock;
                                if (el == null) { problems.Add($"{tag} missing:{n}"); continue; }
                                if (!EffectiveVisible(el)) continue;
                                if (el.Text.Length == 0) problems.Add($"{tag} {n} empty text");
                                else if (el.ActualHeight <= 0) problems.Add($"{tag} {n} zero height");
                            }
                            var page = win.FindName("Page" + pg) as FrameworkElement;
                            if (page == null || page.Visibility != Visibility.Visible || page.ActualHeight < 50)
                                problems.Add($"{tag} page grid bad");
                        }
                    }
                }
            }
            // Flow direction must follow script direction
            win.SetLang(0);
            if (win.FlowDirection != FlowDirection.RightToLeft) problems.Add("fa must be RTL");
            win.SetLang(2);
            if (win.FlowDirection != FlowDirection.LeftToRight) problems.Add("en must be LTR");
            win.SetLang(1);
            if (win.FlowDirection != FlowDirection.RightToLeft) problems.Add("ar must be RTL");
            // Theme swap must actually re-skin the app (regression guard)
            win.SetTheme(0);
            var c0 = ((System.Windows.Media.SolidColorBrush)win.FindResource("WindowBrush")).Color;
            win.SetTheme(3);
            var c3 = ((System.Windows.Media.SolidColorBrush)win.FindResource("WindowBrush")).Color;
            if (c0 == c3) problems.Add("theme swap has no visual effect");
            win.Close();
        }
        catch (Exception ex)
        {
            problems.Add("FATAL: " + ex);
        }
        foreach (var p in problems.Count > 30 ? problems.GetRange(0, 30) : problems)
            Console.WriteLine("SELFTEST: " + p);
        if (problems.Count == 0)
        {
            Console.WriteLine("PASS: WPF selftest (3 sizes x 5 langs x 9 themes x 6 pages)");
            return 0;
        }
        Console.WriteLine($"FAIL: {problems.Count} selftest problems");
        return 1;
    }

    private static void SetPage(MainWindow win, int pg)
    {
        switch (pg)
        {
            case 0: win.SetPageHome(); break;
            case 1: win.SetPageExtract(); break;
            case 2: win.SetPageAbout(); break;
            case 3: win.SetPageContact(); break;
            case 4: win.SetPageSupport(); break;
            default: win.SetPageSettings(); break;
        }
    }
}
