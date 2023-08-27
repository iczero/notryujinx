using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Am.AppletAE;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Ryujinx.HLE.HOS.Applets
{
    internal class CabinetApplet : IApplet
    {
        private readonly Horizon _system;

        private AppletSession _normalSession;
        private CabinetStartArguments _startArguments;

        public event EventHandler AppletStateChanged;

        public CabinetApplet(Horizon system)
        {
            _system = system;
        }

        public ResultCode Start(AppletSession normalSession, AppletSession interactiveSession)
        {
            _normalSession = normalSession;

            CommonArguments commonArguments = IApplet.ReadStruct<CommonArguments>(normalSession.Pop());
            Logger.Info?.PrintMsg(LogClass.ServiceAm, $"CabinetApplet version: 0x{commonArguments.AppletVersion:x8}");

            _startArguments = IApplet.ReadStruct<CabinetStartArguments>(normalSession.Pop());
            switch (_startArguments.Type)
            {
                case CabinetStartType.NicknameAndOwnerSettings:
                    {
                        // TODO: allow changing the owning Mii
                        ChangeNickname();

                        break;
                    }

                default:
                    throw new NotImplementedException($"CabinetStartType {_startArguments.Type} is not implemented.");
            }

            return ResultCode.Success;
        }

        public ResultCode GetResult()
        {
            return ResultCode.Success;
        }

        private void ChangeNickname()
        {
            string nickname = null;
            if (_startArguments.Flags.HasFlag(CabinetFlags.HasRegisterInfo))
            {
                nickname = Encoding.UTF8.GetString(_startArguments.RegisterInfo.Nickname.AsSpan()).TrimEnd('\0');
            }

            SoftwareKeyboardUiArgs inputParameters = new()
            {
                HeaderText = "Enter a new nickname for this amiibo.",
                GuideText = nickname,
                StringLengthMin = 1,
                StringLengthMax = 10
            };

            bool inputResult = _system.Device.UiHandler.DisplayInputDialog(inputParameters, out string newNickname);
            if (!inputResult)
            {
                ReturnCancel();
                return;
            }

            VirtualAmiibo.SetNickname(_startArguments.TagInfo.Uuid.AsSpan()[..9].ToArray(), newNickname);

            CabinetReturnValue returnValue = new()
            {
                Flags = CabinetFlags.HasCompleteInfo,
                DeviceHandle = (ulong)_system.NfpDevices[0].Handle,
                TagInfo = _startArguments.TagInfo,
                RegisterInfo = _startArguments.RegisterInfo
            };

            Span<byte> nicknameData = returnValue.RegisterInfo.Nickname.AsSpan();
            nicknameData.Clear();

            Encoding.UTF8.GetBytes(newNickname).CopyTo(nicknameData);

            _normalSession.Push(BuildResponse(returnValue));
            AppletStateChanged?.Invoke(this, null);
            _system.ReturnFocus();
        }

        private void ReturnCancel()
        {
            _normalSession.Push(BuildResponse());
            AppletStateChanged?.Invoke(this, null);
            _system.ReturnFocus();
        }

        private static byte[] BuildResponse(CabinetReturnValue result)
        {
            byte[] data = new byte[Unsafe.SizeOf<CabinetReturnValue>()];

            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            Marshal.StructureToPtr<CabinetReturnValue>(result, handle.AddrOfPinnedObject(), true);

            return data;
        }

        private static byte[] BuildResponse()
        {
            return BuildResponse(new CabinetReturnValue { Flags = CabinetFlags.Canceled });
        }
    }
}
