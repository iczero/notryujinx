using System;

namespace Ryujinx.Ava.Ui.Models
{
    public class StatusUpdatedEventArgs : EventArgs
    {
        public bool   VSyncEnabled;
        public string AspectRatio;
        public string DockedMode;
        public string FifoStatus;
        public string GameStatus;
        public string GpuName;

        public StatusUpdatedEventArgs(bool vSyncEnabled, string dockedMode, string aspectRatio, string gameStatus, string fifoStatus, string gpuName)
        {
            VSyncEnabled = vSyncEnabled;
            DockedMode   = dockedMode;
            AspectRatio  = aspectRatio;
            GameStatus   = gameStatus;
            FifoStatus   = fifoStatus;
            GpuName      = gpuName;
        }
    }
}