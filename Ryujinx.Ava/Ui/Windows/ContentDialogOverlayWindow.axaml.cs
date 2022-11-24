using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace Ryujinx.Ava.Ui.Windows
{
    public partial class ContentDialogOverlayWindow : StyleableWindow
    {
        // public event EventHandler Ready;

        public ContentDialogOverlayWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            ExtendClientAreaToDecorationsHint = true;
            TransparencyLevelHint = WindowTransparencyLevel.Transparent;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SystemDecorations = SystemDecorations.Full;
            ExtendClientAreaTitleBarHeightHint = 0;
            // Background = Brushes.Aquamarine;
            Background = Brushes.Transparent;
            CanResize = false;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // Ready.Invoke(this, EventArgs.Empty);
            ContentDialog.ShowAsync();
        }
    }
}