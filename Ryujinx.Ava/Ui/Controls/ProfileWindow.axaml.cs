using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.Windows;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    public class ProfileDialog : UserControl
    {
        public event EventHandler OkResponse;
        public string FileName { get; private set; }

        public TextBox ProfileBox { get; private set; }
        public TextBlock Error { get; private set; }

        public bool IsOkPressed { get; set; }
        
        public ProfileDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            ProfileBox = this.FindControl<TextBox>("ProfileBox");
            Error = this.FindControl<TextBlock>("Error");
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
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
            }
            else
            {
                Error.Text = "The file name contains invalid characters. Please try again.";
            }
        }

        public async static Task<string> ShowProfileDialog(StyleableWindow window)
        {
            ContentDialog contentDialog = window.ContentDialog;

            string name = string.Empty;
            
            ProfileDialog content = new ProfileDialog();

            if (contentDialog != null)
            {
                contentDialog.PrimaryButtonClick += DeferClose;
                
                contentDialog.Title = "Enter Profile Name";
                contentDialog.PrimaryButtonText = "OK";
                contentDialog.SecondaryButtonText = "";
                contentDialog.CloseButtonText = "Cancel";
                contentDialog.Content = content;

                await contentDialog.ShowAsync();
            }

            async void DeferClose(ContentDialog sender, ContentDialogButtonClickEventArgs args)
            {
                var deferral = args.GetDeferral();

                if (!string.IsNullOrEmpty(content.ProfileBox.Text))
                {
                    foreach (char invalidChar in Path.GetInvalidFileNameChars())
                    {
                        if (content.ProfileBox.Text.Contains(invalidChar))
                        {
                            break;
                        }
                    }

                    name = $"{content.ProfileBox.Text}.json";

                    deferral.Complete();

                    contentDialog.PrimaryButtonClick -= DeferClose;
                    
                    return;
                }

                content.Error.Text = "The file name contains invalid characters. Please try again.";

                args.Cancel = true;
            }

            return name;
        }
    }
}