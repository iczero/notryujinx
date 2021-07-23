using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using IX.System.Threading;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common.Logging;
using System;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    public static class ContentDialogHelper
    {
        private static bool _isChoiceDialogOpen;

        private async static Task<UserResult> ShowContentDialog(StyleableWindow window, string title, string primaryText, string secondaryText, string primaryButton,
            string secondaryButton, string closeButton, int iconSymbol)
        {
            UserResult result = UserResult.None;

            ContentDialog contentDialog = window.ContentDialog;

            if (contentDialog != null)
            {
                contentDialog.Title = title;
                contentDialog.PrimaryButtonText = primaryButton;
                contentDialog.SecondaryButtonText = secondaryButton;
                contentDialog.CloseButtonText = closeButton;
                contentDialog.Content = CreateDialogTextContent(primaryText, secondaryText, iconSymbol);
                // Todo check proper responses
                contentDialog.PrimaryButtonCommand = MiniCommand.Create(() =>
                {
                    result = primaryButton.ToLower() == "yes" ? UserResult.Yes : UserResult.Ok;
                });
                contentDialog.SecondaryButtonCommand = MiniCommand.Create(() =>
                {
                    result = UserResult.No;
                });
                contentDialog.CloseButtonCommand = MiniCommand.Create(() =>
                {
                    result = UserResult.Cancel;
                });
                await contentDialog.ShowAsync(ContentDialogPlacement.Popup);
            };

            return result;
        }

        private async static Task<UserResult> ShowDeferredContentDialog(StyleableWindow window, string title, string primaryText, string secondaryText, string primaryButton,
            string secondaryButton, string closeButton, int iconSymbol, ManualResetEvent deferResetEvent)
        {
            UserResult result = UserResult.None;

            ContentDialog contentDialog = window.ContentDialog;

            contentDialog.PrimaryButtonClick += DeferClose;

            if (contentDialog != null)
            {
                contentDialog.Title = title;
                contentDialog.PrimaryButtonText = primaryButton;
                contentDialog.SecondaryButtonText = secondaryButton;
                contentDialog.CloseButtonText = closeButton;
                contentDialog.Content = CreateDialogTextContent(primaryText, secondaryText, iconSymbol);
                // Todo check proper responses
                contentDialog.PrimaryButtonCommand = MiniCommand.Create(() =>
                {
                    result = primaryButton.ToLower() == "yes" ? UserResult.Yes : UserResult.Ok;
                });
                contentDialog.SecondaryButtonCommand = MiniCommand.Create(() =>
                {
                    result = UserResult.No;
                });
                contentDialog.CloseButtonCommand = MiniCommand.Create(() =>
                {
                    result = UserResult.Cancel;
                });
                await contentDialog.ShowAsync(ContentDialogPlacement.Popup);
            };

            return result;

            void DeferClose(ContentDialog sender, ContentDialogButtonClickEventArgs args)
            {
                var deferral = args.GetDeferral();

                contentDialog.PrimaryButtonClick -= DeferClose;

                Task.Run(()=>{
                    deferResetEvent.WaitOne();

                    deferral.Complete();
                });
            }
        }

        private static Grid CreateDialogTextContent(string primaryText, string secondaryText, int symbol = 0xF4A3)
        {
            Grid content = new Grid();
            content.RowDefinitions = new RowDefinitions() { new RowDefinition(), new RowDefinition() };
            content.ColumnDefinitions = new ColumnDefinitions() { new ColumnDefinition(GridLength.Auto), new ColumnDefinition() };

            content.MinHeight = 80;

            SymbolIcon icon = new SymbolIcon { Symbol = (Symbol)symbol, Margin = new Avalonia.Thickness(10) };
            icon.FontSize = 40;
            icon.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            Grid.SetColumn(icon, 0);
            Grid.SetRowSpan(icon, 2);
            Grid.SetRow(icon, 0);

            TextBlock primaryLabel = new TextBlock() { Text = primaryText,  Margin = new Avalonia.Thickness(5), TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 450 };
            TextBlock secondaryLabel = new TextBlock() { Text = secondaryText,  Margin = new Avalonia.Thickness(5), TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 450 };
            Grid.SetColumn(primaryLabel, 1);
            Grid.SetColumn(secondaryLabel, 1);
            Grid.SetRow(primaryLabel, 0);
            Grid.SetRow(secondaryLabel, 1);

            content.Children.Add(icon);
            content.Children.Add(primaryLabel);
            content.Children.Add(secondaryLabel);

            return content;
        }

        public static async Task<UserResult> CreateInfoDialog(StyleableWindow window, string primary, string secondaryText, string title = "Ryujinx - Info")
        {
            return await ShowContentDialog(window, title, primary, secondaryText, "OK", "", "Close",
                0xF4A3);
        }

        internal static async Task<UserResult> CreateConfirmationDialog(StyleableWindow window, string primary, string secondaryText, string acceptButtonText = "Yes", string cancelButtonText = "No", string title = "Ryujinx - Confirmation")
        {
            return await ShowContentDialog(window, "Ryujinx - Confirmation", primary, secondaryText, acceptButtonText, "",
                cancelButtonText,
                (int) Symbol.Help);
        }

        internal static UpdateWaitWindow CreateWaitingDialog(string mainText, string secondaryText)
        {
            return new(mainText, secondaryText);
        }


        internal static async void CreateUpdaterInfoDialog(StyleableWindow window, string primary, string secondaryText)
        {
            await ShowContentDialog(window, "Ryujinx - Updater", primary, secondaryText, "", "", "OK",
                0xF4A3);
        }
        
        internal static async void CreateWarningDialog(StyleableWindow window, string primary, string secondaryText)
        {
            await ShowContentDialog(window, "Ryujinx - Warning", primary, secondaryText, "", "", "OK",
                0xF4A3);
        }

        internal static async void CreateErrorDialog(StyleableWindow owner, string errorMessage, string secondaryErrorMessage = "")
        {
            Logger.Error?.Print(LogClass.Application, errorMessage);

            await ShowContentDialog(owner, "Ryujinx - Error", "Ryujinx has encountered an error", errorMessage, secondaryErrorMessage, "", "OK", 0xF3F2);
        }

        internal static async Task<bool> CreateChoiceDialog(StyleableWindow window, string title, string primary, string secondaryText)
        {
            if (_isChoiceDialogOpen)
            {
                return false;
            }

            _isChoiceDialogOpen = true;

            UserResult response =
                await ShowContentDialog(window, title, primary, secondaryText, "Yes", "", "No", (int) Symbol.Help);

            _isChoiceDialogOpen = false;

            return response == UserResult.Yes;
        }
        
        internal static async Task<bool> CreateExitDialog(StyleableWindow owner)
        {
            return await CreateChoiceDialog(owner, "Ryujinx - Exit", "Are you sure you want to stop emulation?",
                "All unsaved data will be lost!");
        }

        internal static async Task<string> CreateInputDialog(string title, string mainText, string subText,
            Window owner, uint maxLength = Int32.MaxValue, string input = "")
        {
            InputDialog dialog = new(title, mainText, input, subText, maxLength);

            if (await dialog.ShowDialog<UserResult>(owner) == UserResult.Ok)
            {
                return dialog.Input;
            }

            return string.Empty;
        }
    }
}