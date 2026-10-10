using System.Configuration;
using System.Data;
using System.Windows;

namespace Aion2MapOverlay.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length >= 3 && e.Args[0] is "--verify" or "--self-check")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                if (e.Args[0] == "--self-check") SelfCheck.Run(e.Args[2], e.Args[1]);
                else Diagnostics.Run(e.Args.Skip(2).ToArray(), e.Args[1]);
            }
            catch (Exception error)
            {
                System.IO.Directory.CreateDirectory(e.Args[1]);
                System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "error.txt"), error.ToString());
                Shutdown(1); return;
            }
            Shutdown(); return;
        }
        base.OnStartup(e);
    }
}
