using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace WinSpeed
{
    static class Program
    {
        public const string AppName = "WinSpeed";
        const string MutexName = @"Local\WinSpeed.SingleInstance";
        const string ActivateEventName = @"Local\WinSpeed.Activate";
        const string ExitEventName = @"Local\WinSpeed.Exit";

        public static string TargetFile => Path.Combine(Path.GetTempPath(), "WinSpeed.show");

        /// <summary>Reads (and removes) the page request left by a second instance.</summary>
        public static string TakeTarget()
        {
            try
            {
                if (!File.Exists(TargetFile)) return "";
                var t = File.ReadAllText(TargetFile).Trim();
                File.Delete(TargetFile);
                return t;
            }
            catch { return ""; }
        }

        static readonly string[] Pages = { "overview", "dashboard", "speedtest", "run-speedtest", "usage", "connection", "interfaces", "meter", "settings", "about" };

        public static string Version => typeof(Program).Assembly.GetName().Version.ToString(3);

        [STAThread]
        static int Main(string[] args)
        {
            bool exitRequest = args.Any(a => a.Equals("--exit", StringComparison.OrdinalIgnoreCase) || a.Equals("/exit", StringComparison.OrdinalIgnoreCase));
            bool fromStartup = args.Any(a => a.Equals("--startup", StringComparison.OrdinalIgnoreCase));

            // which page to show: --usage, --speedtest, --settings, ... (if already running, the running copy shows it)
            string target = args.Select(x => x.TrimStart('-', '/').ToLowerInvariant()).FirstOrDefault(x => Pages.Contains(x)) ?? "";

            using (var mutex = new Mutex(true, MutexName, out bool created))
            {
                if (!created)
                {
                    // Already running: ask the running copy to show itself (or quit).
                    try
                    {
                        if (!exitRequest) File.WriteAllText(TargetFile, target);
                        using (var ev = EventWaitHandle.OpenExisting(exitRequest ? ExitEventName : ActivateEventName)) ev.Set();
                        if (exitRequest)
                        {
                            try { mutex.WaitOne(8000); } catch (AbandonedMutexException) { }
                        }
                    }
                    catch { }
                    return 0;
                }
                if (exitRequest) return 0; // nothing running

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => Log(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject as Exception);

                using (var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName))
                using (var exit = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName))
                using (var app = new TrayApp(activate, exit, fromStartup))
                {
                    if (target.Length > 0) app.ShowTarget(target);
                    Application.Run(app);
                }
                mutex.ReleaseMutex();
            }
            return 0;
        }

        static readonly object logLock = new object();

        public static void Log(Exception ex)
        {
            if (ex == null) return;
            try
            {
                lock (logLock)
                {
                    var path = Path.Combine(Settings.DataDir, "error.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024) File.Delete(path);
                    File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}
