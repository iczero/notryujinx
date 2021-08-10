using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using IX.System.IO;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common;
using Ryujinx.Configuration;
using System;
using System.Linq;
using System.Diagnostics;

namespace Ryujinx.Ava
{
    public class App : Avalonia.Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();


            if (Program.PreviewerDetached)
            {
                ApplyConfiguredTheme();

                ConfigurationState.Instance.Ui.BaseStyle.Event += ThemeChanged_Event;
                ConfigurationState.Instance.Ui.CustomThemePath.Event += ThemeChanged_Event;
                ConfigurationState.Instance.Ui.EnableCustomTheme.Event += CustomThemeChanged_Event;
            }
        }

        private void CustomThemeChanged_Event(object sender, ReactiveEventArgs<bool> e)
        {
            try
            {
                ApplyConfiguredTheme();
            }
            catch (Exception)
            {
                ShowRestartDialog();
            }
        }

        private async void ShowRestartDialog()
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var result = await ContentDialogHelper.CreateConfirmationDialog(
                        (desktop.MainWindow as MainWindow).SettingsWindow,
                        LocaleManager.Instance["DialogThemeRestartMessage"],
                        LocaleManager.Instance["DialogThemeRestartSubMessage"], "Yes", "No", LocaleManager.Instance["DialogRestartRequiredMessage"]);

                    if (result == UserResult.Yes)
                    {
                        var path = Process.GetCurrentProcess().MainModule.FileName;
                        var info = new ProcessStartInfo() {FileName = path, UseShellExecute = false};
                        var proc = Process.Start(info);
                        desktop.Shutdown();
                        Environment.Exit(0);
                    }
                }
            });
        }

        private void ThemeChanged_Event(object sender, ReactiveEventArgs<string> e)
        {
            try
            {
                ApplyConfiguredTheme();
            }
            catch (Exception)
            {
                ShowRestartDialog();
            }
        }

        private void ApplyConfiguredTheme()
        {
            string baseStyle = ConfigurationState.Instance.Ui.BaseStyle;
            string themePath = ConfigurationState.Instance.Ui.CustomThemePath;
            bool enableCustomTheme = ConfigurationState.Instance.Ui.EnableCustomTheme;

            if (string.IsNullOrWhiteSpace(baseStyle))
            {
                ConfigurationState.Instance.Ui.BaseStyle.Value = "Dark";

                baseStyle = ConfigurationState.Instance.Ui.BaseStyle;
            }

            var theme = AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>();
            
            theme.RequestedTheme = baseStyle;
            
            var currentStyles = this.Styles;

            if (currentStyles.Count > 3)
            {
                currentStyles.RemoveRange(3, currentStyles.Count - 3);
            }

            IStyle newStyles = null;

            newStyles = (Styles)AvaloniaXamlLoader.Load(new Uri($"avares://Ryujinx.Ava/Assets/Styles/Base{baseStyle}.xaml", UriKind.Absolute));

            if (currentStyles.Count == 4)
            {
                currentStyles[3] = newStyles;
            }
            else
            {
                currentStyles.Add(newStyles);
            }

            if (enableCustomTheme)
            {
                if (!string.IsNullOrWhiteSpace(themePath))
                {
                    try
                    {
                        var themeContent = System.IO.File.ReadAllText(themePath);
                        var customStyle = AvaloniaRuntimeXamlLoader.Parse<IStyle>(themeContent);

                        if (currentStyles.Count == 5)
                        {
                            currentStyles[4] = customStyle;
                        }
                        else
                        {
                            currentStyles.Add(customStyle);
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }
        }
    }
}