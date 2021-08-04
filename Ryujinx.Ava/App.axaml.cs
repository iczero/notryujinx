using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using IX.System.IO;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common;
using Ryujinx.Configuration;
using System;
using System.Linq;

namespace Ryujinx.Ava
{
    public class App : Avalonia.Application
    {
        public static StyleManager StyleManager { get; set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                StyleManager       = new StyleManager();
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
            ApplyConfiguredTheme();
        }

        private void ThemeChanged_Event(object sender, ReactiveEventArgs<string> e)
        {
            ApplyConfiguredTheme();
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
            var currentStyles = this.Styles;

            IStyle newStyles = null;

            switch (baseStyle.ToLower())
            {
                case "dark":
                    theme.RequestedTheme = "Dark";
                    newStyles = (Styles)AvaloniaXamlLoader.Load(new Uri($"avares://Ryujinx.Ava/Assets/Styles/BaseDark.xaml", UriKind.Absolute));
                    break;
                case "light":
                    theme.RequestedTheme = "Light";
                    newStyles = (Styles)AvaloniaXamlLoader.Load(new Uri($"avares://Ryujinx.Ava/Assets/Styles/BaseLight.xaml", UriKind.Absolute));
                    break;
            }

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

            theme.Owner?.NotifyHostedResourcesChanged(ResourcesChangedEventArgs.Empty);
        }
    }
}