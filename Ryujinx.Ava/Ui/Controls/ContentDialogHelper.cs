using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Input;
using MessageBoxSlim.Avalonia;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.Windows;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    public static class ContentDialogHelper
    {
        private async static Task<UserResults> ShowContentDialog(StyleableWindow window, string title, string primaryText, string secondaryText, string primaryButton,
            string secondaryButton, string closeButton, int iconSymbol)
        {
            UserResults result = UserResults.None;

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
                    result = primaryButton.ToLower() == "yes" ? UserResults.Yes : UserResults.Ok;
                });
                contentDialog.SecondaryButtonCommand = MiniCommand.Create(() =>
                {
                    result = UserResults.No;
                });
                contentDialog.CloseButtonCommand = MiniCommand.Create(() =>
                {
                    result = UserResults.Cancel;
                });
                await contentDialog.ShowAsync(ContentDialogPlacement.Popup);
            };

            return result;
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

        public static void CreateInfoDialog(StyleableWindow window, string title, string primary, string secondaryText)
        {
            ShowContentDialog(window, title, primary, secondaryText, "OK", "", "Close",
                0xF4A3);
        }

        internal static async Task<UserResults> CreateConfirmationDialog(StyleableWindow window, string primary, string secondaryText)
        {
            return await ShowContentDialog(window, "Ryujinx - Confirmation", primary, secondaryText, "Yes", "",
                "No",
                0xF4A3);
        }
    }
}