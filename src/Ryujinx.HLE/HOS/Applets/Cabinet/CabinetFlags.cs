namespace Ryujinx.HLE.HOS.Applets
{
    enum CabinetFlags : byte
    {
        Canceled = 0,

        HasTagInfo = 1 << 1,
        HasRegisterInfo = 1 << 2,

        HasCompleteInfo = HasTagInfo | HasRegisterInfo,
    }
}
