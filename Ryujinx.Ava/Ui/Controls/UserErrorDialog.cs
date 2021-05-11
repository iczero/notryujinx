using Avalonia.Controls;
using MessageBox.Avalonia;
using MessageBox.Avalonia.BaseWindows.Base;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;
using Ryujinx.Ava.Common;
using System.Collections.Generic;

namespace Ryujinx.Ava.Ui.Controls
{
    internal class UserErrorDialog
    {
        private const string SetupGuideUrl =
            "https://github.com/Ryujinx/Ryujinx/wiki/Ryujinx-Setup-&-Configuration-Guide";
        
        private readonly IMsBoxWindow<string> _messageBox;
        private readonly Window _owner;

        private readonly UserError _userError;

        private UserErrorDialog(UserError error, Window owner)
        {
            _userError = error;
            _owner = owner;
            string errorCode = GetErrorCode(error);

            bool isInSetupGuide = IsCoveredBySetupGuide(error);
            List<ButtonDefinition> buttonDefs = new() {new() {Name = "OK"}};

            if (isInSetupGuide)
            {
                buttonDefs.Add(new ButtonDefinition {Name = "Open the Setup Guide"});
            }

            _messageBox =
                MessageBoxManager.GetMessageBoxCustomWindow(new MessageBoxCustomParams
                {
                    Icon = Icon.Error,
                    ContentTitle = $"Ryujinx error ({errorCode})",
                    ContentHeader = $"{errorCode}: {GetErrorTitle(error)}",
                    ContentMessage =
                        GetErrorDescription(error) + (isInSetupGuide
                            ? "\nFor more information on how to fix this error, follow our Setup Guide."
                            : ""),
                    ButtonDefinitions = buttonDefs
                });
        }

        private string GetErrorCode(UserError error)
        {
            return $"RYU-{(uint)error:X4}";
        }

        private string GetErrorTitle(UserError error)
        {
            return error switch
            {
                UserError.NoKeys => "Keys not found",
                UserError.NoFirmware => "Firmware not found",
                UserError.FirmwareParsingFailed => "Firmware parsing error",
                UserError.ApplicationNotFound => "Application not found",
                UserError.Unknown => "Unknown error",
                _ => "Undefined error"
            };
        }

        private string GetErrorDescription(UserError error)
        {
            return error switch
            {
                UserError.NoKeys => "Ryujinx was unable to find your 'prod.keys' file",
                UserError.NoFirmware => "Ryujinx was unable to find any firmwares installed",
                UserError.FirmwareParsingFailed =>
                    "Ryujinx was unable to parse the provided firmware. This is usually caused by outdated keys.",
                UserError.ApplicationNotFound => "Ryujinx couldn't find a valid application at the given path.",
                UserError.Unknown => "An unknown error occured!",
                _ => "An undefined error occured! This shouldn't happen, please contact a dev!"
            };
        }

        private static bool IsCoveredBySetupGuide(UserError error)
        {
            return error switch
            {
                UserError.NoKeys or
                    UserError.NoFirmware or
                    UserError.FirmwareParsingFailed => true,
                _ => false
            };
        }

        private static string GetSetupGuideUrl(UserError error)
        {
            if (!IsCoveredBySetupGuide(error))
            {
                return null;
            }

            return error switch
            {
                UserError.NoKeys => SetupGuideUrl + "#initial-setup---placement-of-prodkeys",
                UserError.NoFirmware => SetupGuideUrl + "#initial-setup-continued---installation-of-firmware",
                _ => SetupGuideUrl
            };
        }

        public async void Run()
        {
            string result = await _messageBox.ShowDialog(_owner);

            if (result == "Open the Setup Guide")
            {
                OpenHelper.OpenUrl(GetSetupGuideUrl(_userError));
            }
        }

        public static void CreateUserErrorDialog(UserError error, Window owner)
        {
            new UserErrorDialog(error, owner).Run();
        }
    }
}