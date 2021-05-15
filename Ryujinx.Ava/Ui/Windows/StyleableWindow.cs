using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
using Avalonia.Styling;
using System;
using System.IO;
using System.Reflection;

namespace Ryujinx.Ava.Ui.Windows
{
    public class StyleableWindow : Window, IStyleable
    {
        public StyleableWindow()
        {
            ExtendClientAreaToDecorationsHint = false;
            TransparencyLevelHint             = WindowTransparencyLevel.None;

            this.GetObservable(WindowStateProperty)
                .Subscribe(x =>
                {
                    PseudoClasses.Set(":maximized",  x == WindowState.Maximized);
                    PseudoClasses.Set(":fullscreen", x == WindowState.FullScreen);
                });

            this.GetObservable(IsExtendedIntoWindowDecorationsProperty)
                .Subscribe(x =>
                {
                    if (!x)
                    {
                        SystemDecorations     = SystemDecorations.Full;
                        TransparencyLevelHint = WindowTransparencyLevel.None;
                    }
                });

            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Ryujinx.Ava.Assets.Images.Logo_Ryujinx.png");

            Icon = new WindowIcon(stream);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.PreferSystemChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;
        }
    }
}