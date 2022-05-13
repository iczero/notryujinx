using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Rsc.ViewModels;
using Ryujinx.Rsc.Views;
using Ryujinx.Rsc.Common.Configuration;
using System;
using System.IO;

namespace Ryujinx.Rsc
{
    public partial class App : Application
    {
        public static bool PreviewerDetached { get; set; }
        public static string GameDirectory { get; set; }
        
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public static void LoadConfiguration()
        {
            if (PreviewerDetached)
            {
                var basePath = OperatingSystem.IsAndroid() ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : null;
                // Setup base data directory.
                AppDataManager.Initialize(basePath);

                // Initialize the configuration.
                ConfigurationState.Initialize();

                // Initialize the logger system.
                LoggerModule.Initialize();

                string localConfigurationPath   = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.json");
                string appDataConfigurationPath = Path.Combine(AppDataManager.BaseDirPath,            "Config.json");

                // Now load the configuration as the other subsystems are now registered
                ConfigurationPath = File.Exists(localConfigurationPath)
                    ? localConfigurationPath
                    : File.Exists(appDataConfigurationPath)
                        ? appDataConfigurationPath
                        : null;

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

                if (OperatingSystem.IsAndroid())
                {
                    ConfigurationState.Instance.Ui.GameDirs.Value.Clear();
                    ConfigurationState.Instance.Ui.GameDirs.Value.Add(GameDirectory);
                }
            }
        }

        public static string ConfigurationPath { get; set; }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}