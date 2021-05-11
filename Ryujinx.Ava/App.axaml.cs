using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Ui.Windows;

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
        }
    }
}