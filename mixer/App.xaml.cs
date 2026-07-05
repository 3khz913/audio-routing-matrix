using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using mixer.Services;
using mixer.ViewModels;

namespace mixer
{
    public partial class App : Application
    {
        public static WaveLinkService WaveLinkService { get; private set; } = null!;
        public static MidiService MidiService { get; private set; } = null!;
        public static MidiMappingStorage MidiMappingStorage { get; private set; } = null!;

        private Process? _serverProcess;

        protected override async void OnStartup(StartupEventArgs e)
        {
            // شبكة الأمان
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            Logger.Info("Application starting.");

            // إنهاء أي عملية Node.js سابقة على المنفذ 8765
            await Task.Run(() => KillExistingNodeProcess());

            // تشغيل سيرفر Node.js في الخلفية
            StartServer();

            // تهيئة الخدمات الأساسية
            WaveLinkService = new WaveLinkService();
            MidiService = new MidiService();
            MidiMappingStorage = new MidiMappingStorage();

            // بدء محاولة الاتصال بالسيرفر
            _ = WaveLinkService.ConnectAsync();

            // إنشاء ViewModel الرئيسي
            var mainVM = new MainViewModel(WaveLinkService, MidiService, MidiMappingStorage);

            // إنشاء النافذة الرئيسية وعرضها
            var window = new MainWindow { DataContext = mainVM };
            window.Show();

            base.OnStartup(e);
        }

        private void KillExistingNodeProcess()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c netstat -ano | findstr :8765 | findstr LISTENING",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        var pid = parts[^1];
                        try
                        {
                            var procToKill = Process.GetProcessById(int.Parse(pid));
                            if (procToKill.ProcessName.ToLower().Contains("node"))
                            {
                                procToKill.Kill();
                                Logger.Info($"Killed existing Node.js process (PID: {pid}) on port 8765.");
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to kill existing Node process: {ex.Message}");
            }
        }

        private void StartServer()
        {
            Task.Run(() =>
            {
                try
                {
                    var nodeExe = FindNodeExecutable();
                    if (nodeExe == null)
                    {
                        Logger.Error("Node.js executable not found. Server will not start.");
                        return;
                    }

                    var serverDir = GetServerDirectory();

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = nodeExe,
                        Arguments = "server.js",
                        WorkingDirectory = serverDir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    _serverProcess = new Process { StartInfo = startInfo };

                    _serverProcess.OutputDataReceived += (_, args) =>
                    {
                        if (!string.IsNullOrWhiteSpace(args.Data))
                            Logger.Debug($"[server] {args.Data}");
                    };
                    _serverProcess.ErrorDataReceived += (_, args) =>
                    {
                        if (!string.IsNullOrWhiteSpace(args.Data))
                            Logger.Error($"[server] {args.Data}");
                    };

                    _serverProcess.Start();
                    _serverProcess.BeginOutputReadLine();
                    _serverProcess.BeginErrorReadLine();

                    Logger.Info($"Server started (PID: {_serverProcess.Id})");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to start Node.js server", ex);
                }
            });
        }

        private void StopServer()
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                try
                {
                    _serverProcess.Kill();
                    _serverProcess.WaitForExit(3000);
                    Logger.Info("Server stopped.");
                }
                catch (Exception ex)
                {
                    Logger.Error("Error stopping server", ex);
                }
            }
        }

        private string? FindNodeExecutable()
        {
            // 1. البحث في مجلد runtime بجانب التطبيق المنشور
            var appDir = GetApplicationDirectory();
            var portableNode = Path.Combine(appDir, "runtime", "node.exe");
            if (File.Exists(portableNode))
            {
                Logger.Info($"Using portable Node.js: {portableNode}");
                return portableNode;
            }

            // 2. أثناء التطوير: البحث في النظام
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "node",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd();
                proc?.WaitForExit();
                var firstLine = output?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                        .FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(firstLine) && File.Exists(firstLine))
                {
                    Logger.Info($"Using system Node.js: {firstLine}");
                    return firstLine;
                }
            }
            catch { }

            Logger.Error("Node.js executable not found.");
            return null;
        }

        private string GetServerDirectory()
        {
            var appDir = GetApplicationDirectory();
            var serverDir = Path.Combine(appDir, "server");
            if (Directory.Exists(serverDir))
                return serverDir;

            // أثناء التطوير: مجلد server بجانب المشروع
            var devServer = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "server"));
            if (Directory.Exists(devServer))
                return devServer;

            throw new DirectoryNotFoundException($"Server directory not found. Expected: {serverDir}");
        }

        private static string GetApplicationDirectory()
        {
            var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(assemblyPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            StopServer();
            WaveLinkService?.Dispose();
            MidiService?.Dispose();
            Logger.Info("Application exiting.");
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error("Unhandled UI exception", e.Exception);
            MessageBox.Show(
                "An unexpected error occurred. Details were written to the log file.\n\n" + e.Exception.Message,
                "mixer - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Error("Unhandled AppDomain exception", ex);
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        }
    }
}