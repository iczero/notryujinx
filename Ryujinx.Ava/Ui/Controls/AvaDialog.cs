using Avalonia.Controls;
using Avalonia.Media.Imaging;
using MessageBoxSlim.Avalonia;
using MessageBoxSlim.Avalonia.DTO;
using MessageBoxSlim.Avalonia.Enums;
using MessageBoxSlim.Avalonia.Interfaces;
using Ryujinx.Common.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    internal class AvaDialog
    {
        private static bool _isChoiceDialogOpen;
        private readonly IMessageBox<UserResult> _dialog;
        private readonly Window _owner;

        internal AvaDialog(string title, string mainText, string secondaryText, Window owner,
            ButtonEnum buttonsType = ButtonEnum.Ok)
        {
            _owner = owner;
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream? stream = assembly.GetManifestResourceStream("Ryujinx.Ava.Ui.Resources.Logo_Ryujinx.png");
            _dialog = BoxedMessage.Create(new MessageBoxParams
            {
                ContentTitle = title,
                ContentMessage = $"{mainText}\n{secondaryText}",
                Icon = new Bitmap(stream),
                Buttons = buttonsType,
                CanResize = false
            });
        }

        public async Task<UserResult> Run()
        {
            return await _dialog.ShowDialogAsync(_owner);
        }

        public void Close()
        {
            _dialog.Close();
        }

        internal static async void CreateInfoDialog(string mainText, string secondaryText, Window owner)
        {
            await new AvaDialog("Ryujinx - Info", mainText, secondaryText, owner).Run();
        }

        internal static async void CreateUpdaterInfoDialog(string mainText, string secondaryText, Window owner)
        {
            await new AvaDialog("Ryujinx - Updater", mainText, secondaryText, owner).Run();
        }

        internal static UpdateWaitWindow CreateWaitingDialog(string mainText, string secondaryText, Window owner)
        {
            return new(mainText, secondaryText);
        }

        internal static async void CreateWarningDialog(string mainText, string secondaryText, Window owner)
        {
            await new AvaDialog("Ryujinx - Warning", mainText, secondaryText, owner).Run();
        }

        internal static async void CreateErrorDialog(string errorMessage, Window owner)
        {
            Logger.Error?.Print(LogClass.Application, errorMessage);

            await new AvaDialog("Ryujinx - Error", "Ryujinx has encountered an error", errorMessage, owner).Run();
        }

        internal static AvaDialog CreateConfirmationDialog(string mainText, string secondaryText, Window owner)
        {
            return new("Ryujinx - Confirmation", mainText, secondaryText, owner, ButtonEnum.Yes | ButtonEnum.No);
        }

        internal static async Task<bool> CreateChoiceDialog(string title, string mainText, string secondaryText,
            Window owner)
        {
            if (_isChoiceDialogOpen)
            {
                return false;
            }

            _isChoiceDialogOpen = true;

            UserResult response =
                await new AvaDialog(title, mainText, secondaryText, owner, ButtonEnum.Yes | ButtonEnum.No).Run();

            _isChoiceDialogOpen = false;

            return response == UserResult.Yes;
        }

        internal static async Task<bool> CreateExitDialog(Window owner)
        {
            return await CreateChoiceDialog("Ryujinx - Exit", "Are you sure you want to stop emulation?",
                "All unsaved data will be lost!", owner);
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