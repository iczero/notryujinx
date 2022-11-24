using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace Ryujinx.Ava.Ui.Windows
{
    public partial class ContentDialogOverlayWindow : StyleableWindow
    {
        public event EventHandler DialogOpened;

        public ContentDialogOverlayWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            ExtendClientAreaToDecorationsHint = true;
            TransparencyLevelHint = WindowTransparencyLevel.Transparent;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SystemDecorations = SystemDecorations.None;
            ExtendClientAreaTitleBarHeightHint = 0;
            Background = Brushes.Transparent;
            CanResize = false;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            DialogOpened?.Invoke(this, EventArgs.Empty);
        }
    }
}