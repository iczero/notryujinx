using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Input;
using MessageBoxSlim.Avalonia;
using Ryujinx.Ava.Ui.Models;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    public static class ContentDialogHelper
    {
        private static UserResults ShowContentDialog(Window window, string title, string primaryText, string secondaryText, string primaryButton,
            string secondaryButton, string closeButton, int iconSymbol)
        {
            UserResults result = UserResults.None;

            ContentDialog contentDialog = new ContentDialog
            {
                Title = title,
                PrimaryButtonText = primaryText,
                SecondaryButtonText = secondaryText,
                IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(primaryButton),
                IsSecondaryButtonEnabled = !string.IsNullOrWhiteSpace(secondaryButton),
                CloseButtonText = closeButton,
                Content = CreateDialogTextContent(primaryText, secondaryText, iconSymbol),
                // Todo check proper responses
                PrimaryButtonCommand = MiniCommand.Create(() =>
                {
                    result = primaryButton.ToLower() == "yes" ? UserResults.Yes : UserResults.Ok;
                }),
                SecondaryButtonCommand = MiniCommand.Create(() =>
                {
                    result = UserResults.No;
                }),
                CloseButtonCommand = MiniCommand.Create(() =>
                {
                    result = UserResults.Cancel;
                }),
            };
            contentDialog.ShowAsync(ContentDialogPlacement.Popup).Wait();

            return result;
        }

        private static Grid CreateDialogTextContent(string primaryText, string secondaryText, int symbol = 0xF4A3)
        {
            Grid content = new Grid();
            content.RowDefinitions = new RowDefinitions() {new RowDefinition(), new RowDefinition()};
            content.ColumnDefinitions = new ColumnDefinitions() {new ColumnDefinition(GridLength.Auto), new ColumnDefinition()};

            SymbolIcon icon = new SymbolIcon {Symbol = (Symbol)symbol};
            Grid.SetColumn(icon, 0);
            Grid.SetRowSpan(icon, 2);
            Grid.SetRow(icon, 0);

            Label primaryLabel = new Label(){Content =  primaryText};
            Label secondaryLabel = new Label(){Content =  primaryText};
            Grid.SetColumn(primaryLabel, 1);
            Grid.SetRow(primaryLabel, 0);
            Grid.SetColumn(secondaryLabel, 1);
            Grid.SetRow(secondaryLabel, 1);
            
            content.Children.Add(icon);
            content.Children.Add(primaryLabel);
            content.Children.Add(secondaryLabel);

            return content;
        }

        public static void CreateInfoDialog(Window window, string title, string primary, string secondaryText)
        {
            ShowContentDialog(window, title, primary, secondaryText, "OK", "", "Close",
                0xF4A3);
        }

        internal static async Task<UserResults> CreateConfirmationDialog(Window window, string primary, string secondaryText)
        {
            UserResults result = ShowContentDialog(window, "Ryujinx - Confirmation", primary, secondaryText, "Yes", "",
                "No",
                0xF4A3);

            return result;
        }
    }
}