using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Ryujinx.Ava.Ui.Windows;
using System;
using System.IO;

namespace Ryujinx.Ava.Ui.Controls
{
    public class ProfileWindow : StyleableWindow
    {
        public event EventHandler OkResponse;
        public string FileName { get; private set; }

        public TextBox ProfileBox { get; private set; }
        public TextBlock Error { get; private set; }

        public bool IsOkPressed { get; set; }
        
        public ProfileWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            ProfileBox = this.FindControl<TextBox>("ProfileBox");
            Error = this.FindControl<TextBlock>("Error");
        }

        private void OkButton_OnClick(object? sender, RoutedEventArgs e)
        {
            bool validFileName = true;

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                if (ProfileBox.Text.Contains(invalidChar))
                {
                    validFileName = false;
                }
            }

            if (validFileName && !string.IsNullOrEmpty(ProfileBox.Text))
            {
                FileName = $"{ProfileBox.Text}.json";

                OkResponse?.Invoke(this, EventArgs.Empty);

                IsOkPressed = true;

                Close();
            }
            else
            {
                Error.Text = "The file name contains invalid characters. Please try again.";
            }
        }

        private void Cancel_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}