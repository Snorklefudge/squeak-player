using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SqueakPlayer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (_, args) =>
            {
                Log("Dispatcher", args.Exception);
                MessageBox.Show(args.Exception.ToString(), Loc.ErrorTitle);
                args.Handled = true; // keep the app alive so we can see what happened
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Log("AppDomain", args.ExceptionObject as Exception);
        }

        private static void Log(string source, Exception? ex)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Squeak");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "crash.log"),
                    $"{DateTime.Now:o} [{source}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { /* ignore logging failures */ }
        }
    }
}
