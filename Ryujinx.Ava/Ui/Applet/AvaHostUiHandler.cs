using Avalonia.Controls;
using Avalonia.Threading;
using MessageBoxSlim.Avalonia;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.HLE;
using Ryujinx.HLE.HOS.Applets;
using Ryujinx.HLE.HOS.Services.Am.AppletOE.ApplicationProxyService.ApplicationProxy.Types;
using System;
using System.Threading;

namespace Ryujinx.Ava.Ui.Applet
{
    internal class AvaHostUiHandler : IHostUiHandler
    {
        private readonly Window _parent;

        public AvaHostUiHandler(Window parent)
        {
            _parent = parent;
        }

        public bool DisplayMessageDialog(ControllerAppletUiArgs args)
        {
            string playerCount = args.PlayerCountMin == args.PlayerCountMax
                ? $"exactly {args.PlayerCountMin}"
                : $"{args.PlayerCountMin}-{args.PlayerCountMax}";

            string message = $"Application requests <b>{playerCount}</b> player(s) with:\n\n"
                             + $"<tt><b>TYPES:</b> {args.SupportedStyles}</tt>\n\n"
                             + $"<tt><b>PLAYERS:</b> {string.Join(", ", args.SupportedPlayers)}</tt>\n\n"
                             + (args.IsDocked ? "Docked mode set. <tt>Handheld</tt> is also invalid.\n\n" : "")
                             + "<i>Please reconfigure Input now and then press OK.</i>";

            return DisplayMessageDialog("Controller Applet", message);
        }

        public bool DisplayMessageDialog(string title, string message)
        {
            ManualResetEvent dialogCloseEvent = new(false);

            bool okPressed = false;

            Dispatcher.UIThread.Post(async () =>
            {
                AvaDialog msgDialog = null;

                try
                {
                    msgDialog = new AvaDialog(title, message, "", _parent);

                    UserResult response = await msgDialog.Run();

                    if (response == UserResult.Ok)
                    {
                        okPressed = true;
                    }

                    dialogCloseEvent.Set();
                }
                catch (Exception ex)
                {
                    AvaDialog.CreateErrorDialog($"Error displaying Message Dialog: {ex}", _parent);

                    dialogCloseEvent.Set();
                }
            });

            dialogCloseEvent.WaitOne();

            return okPressed;
        }

        public bool DisplayInputDialog(SoftwareKeyboardUiArgs args, out string userText)
        {
            ManualResetEvent dialogCloseEvent = new(false);

            bool okPressed = false;
            bool error = false;
            string inputText = args.InitialText ?? "";

            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    SwkbdAppletWindow swkbdDialog = new(args.HeaderText, args.SubtitleText)
                    {
                        Title = "Software Keyboard", Message = inputText
                    };

                    swkbdDialog.Input.Text = inputText;
                    swkbdDialog.Input.Watermark = args.GuideText;
                    swkbdDialog.OkButton.Content = args.SubmitText;

                    swkbdDialog.SetInputLengthValidation(args.StringLengthMin, args.StringLengthMax);

                    await swkbdDialog.ShowDialog(_parent);

                    if (swkbdDialog.IsOkPressed)
                    {
                        inputText = swkbdDialog.Input.Text;
                        okPressed = true;
                    }
                }
                catch (Exception ex)
                {
                    error = true;
                    AvaDialog.CreateErrorDialog($"Error displaying Software Keyboard: {ex}", _parent);
                }
                finally
                {
                    dialogCloseEvent.Set();
                }
            });

            dialogCloseEvent.WaitOne();

            userText = error ? null : inputText;

            return error || okPressed;
        }

        public void ExecuteProgram(Switch device, ProgramSpecifyKind kind, ulong value)
        {
            device.Configuration.UserChannelPersistence.ExecuteProgram(kind, value);
            ((MainWindow)_parent).AppHost?.Exit();
        }

        public bool DisplayErrorAppletDialog(string title, string message, string[] buttons)
        {
            ManualResetEvent dialogCloseEvent = new(false);

            bool showDetails = false;

            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    ErrorAppletWindow msgDialog = new(_parent, buttons, message)
                    {
                        Title = title, WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    msgDialog.Width = 400;

                    object response = await msgDialog.Run();

                    if (response != null)
                    {
                        if (buttons.Length > 1)
                        {
                            if ((int)response != buttons.Length - 1)
                            {
                                showDetails = true;
                            }
                        }
                    }

                    dialogCloseEvent.Set();

                    msgDialog.Close();
                }
                catch (Exception ex)
                {
                    dialogCloseEvent.Set();
                    AvaDialog.CreateErrorDialog($"Error displaying ErrorApplet Dialog: {ex}", _parent);
                }
            });

            dialogCloseEvent.WaitOne();

            return showDetails;
        }
    }
}