using ARMeilleure.Translation.PTC;
using Avalonia;
using FFmpeg.AutoGen;
using Ryujinx.Ava.Application.Module;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.System;
using Ryujinx.Common.SystemInfo;
using Ryujinx.Configuration;
using Ryujinx.Modules;
using SixLabors.ImageSharp.Formats.Jpeg;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Ryujinx.Ava
{
    internal class Program
    {
        public static string Version           { get; private set; }
        public static string ConfigurationPath { get; private set; }

        [DllImport("libX11")]
        private static extern int XInitThreads();

        // NOTE: Initialization code. Don't use any Avalonia, third-party APIs or any SynchronizationContext-reliant code before AppMain is called:
        //       Things aren't initialized yet and stuff might break.
        public static void Main(string[] args)
        {
            Initialize(args);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new X11PlatformOptions
                {
                    EnableMultiTouch = true,
                    UseDBusMenu      = true,
                    EnableIme        = true,
                    UseEGL           = false,
                    UseGpu           = false
                })
                .With(new Win32PlatformOptions
                {
                    EnableMultitouch       = true,
                    UseWgl                 = false,
                    AllowEglInitialization = false
                })
                .UseSkia()
                .LogToTrace();
        }

        private static void Initialize(string[] args)
        {
            // Parse Arguments
            string launchPath        = null;
            string baseDirectoryPath = null;
            for (int i = 0; i < args.Length; ++i)
            {
                string arg = args[i];

                if (arg == "-r" || arg == "--root-data-dir")
                {
                    if (i + 1 >= args.Length)
                    {
                        Logger.Error?.Print(LogClass.Application, $"Invalid option '{arg}'");

                        continue;
                    }

                    baseDirectoryPath = args[++i];
                }
                else if (launchPath == null)
                {
                    launchPath = arg;
                }
            }

            // Delete backup files after updating.
            Task.Run(Updater.CleanupUpdate);

            Version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

            Console.Title = $"Ryujinx Console {Version}";

            // Assign Event Handlers
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => ProcessUnhandledException(e.ExceptionObject as Exception, e.IsTerminating);
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => Exit();

            // Fix the ffmpeg path for Linux
            if (OperatingSystem.IsLinux())
            {
                _ = XInitThreads();

                ffmpeg.RootPath = "/lib";
            }

            // Initialize AppDataManager
            AppDataManager.Initialize(baseDirectoryPath);

            // Initialize the configuration
            ConfigurationState.Initialize();

            // Initialize the logger system
            LoggerModule.Initialize();

            // Initialize Discord integration
            DiscordIntegrationModule.Initialize();

            // Set ImageSharp JPEG quality
            SixLabors.ImageSharp.Configuration.Default.ImageFormatsManager.SetEncoder(JpegFormat.Instance, new JpegEncoder { Quality = 100 });

            ReloadConfig();

            PrintSystemInfo();

            ForceDedicatedGpu.Nvidia();
        }

        private static void ReloadConfig()
        {
            string localConfigurationPath   = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.json");
            string appDataConfigurationPath = Path.Combine(AppDataManager.BaseDirPath,            "Config.json");

            // Now load the configuration as the other subsystems are now registered
            ConfigurationPath = File.Exists(localConfigurationPath) ? localConfigurationPath : File.Exists(appDataConfigurationPath) ? appDataConfigurationPath : null;

            if (ConfigurationPath == null)
            {
                // No configuration, we load the default values and save it to disk
                ConfigurationPath = appDataConfigurationPath;

                ConfigurationState.Instance.LoadDefault();
                ConfigurationState.Instance.ToFileFormat().SaveConfig(ConfigurationPath);
            }
            else
            {
                if (ConfigurationFileFormat.TryLoad(ConfigurationPath, out ConfigurationFileFormat configurationFileFormat))
                {
                    ConfigurationState.Instance.Load(configurationFileFormat, ConfigurationPath);
                }
                else
                {
                    ConfigurationState.Instance.LoadDefault();

                    Logger.Warning?.PrintMsg(LogClass.Application, $"Failed to load config! Loading the default config instead.\nFailed config location {ConfigurationPath}");
                }
            }
        }

        private static void PrintSystemInfo()
        {
            Logger.Notice.Print(LogClass.Application, $"Ryujinx Version: {Version}");

            SystemInfo.Gather().Print();

            IReadOnlyCollection<LogLevel> enabledLogs = Logger.GetEnabledLevels();

            Logger.Notice.Print(LogClass.Application, $"Logs Enabled: {(enabledLogs.Count == 0 ? "<None>" : string.Join(", ", enabledLogs))}");

            if (AppDataManager.Mode == AppDataManager.LaunchMode.Custom)
            {
                Logger.Notice.Print(LogClass.Application, $"Launch Mode: Custom Path {AppDataManager.BaseDirPath}");
            }
            else
            {
                Logger.Notice.Print(LogClass.Application, $"Launch Mode: {AppDataManager.Mode}");
            }
        }

        private static void ProcessUnhandledException(Exception ex, bool isTerminating)
        {
            Ptc.Close();
            PtcProfiler.Stop();

            string message = $"Unhandled exception caught: {ex}";

            Logger.Error?.PrintMsg(LogClass.Application, message);

            if (Logger.Error == null)
            {
                Logger.Notice.PrintMsg(LogClass.Application, message);
            }

            if (isTerminating)
            {
                Exit();
            }
        }

        public static void Exit()
        {
            DiscordIntegrationModule.Exit();

            Ptc.Dispose();
            PtcProfiler.Dispose();

            Logger.Shutdown();
        }
    }
}