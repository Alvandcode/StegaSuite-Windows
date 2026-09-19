using System.Windows;

namespace StegaSuite;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        foreach (var a in e.Args)
        {
            if (a == "--selftest")
            {
                int code = SelfTest.Run();
                Shutdown(code);
                return;
            }
        }
        base.OnStartup(e);
    }
}
