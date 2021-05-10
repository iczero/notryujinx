using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MessageBoxSlim.Avalonia;
using System;

namespace Ryujinx.Ava.Ui.Controls
{
    public class InputDialog : Window
    {
        public string Message { get; set; }
        public string Input { get; set; }
        public string SubMessage { get; set; }

        public uint MaxLength { get; }

        public InputDialog(string title, string message, string input = "", string subMessage = "",
            uint maxLength = Int32.MaxValue)
        {
            Message = message;
            Input = input;
            SubMessage = subMessage;
            Title = title;
            MaxLength = maxLength;

            DataContext = this;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public InputDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OKButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Close(UserResult.Ok);
        }

        private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Close(UserResult.Cancel);
        }
    }
}