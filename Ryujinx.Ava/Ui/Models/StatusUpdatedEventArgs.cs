using System;

namespace Ryujinx.Ava.Ui.Models
{
    public class StatusUpdatedEventArgs : EventArgs
    {
        public string AspectRatio;
        public string DockedMode;
        public string FifoStatus;
        public string GameStatus;
        public string GpuName;
        public bool VSyncEnabled;

        public StatusUpdatedEventArgs(bool vSyncEnabled, string dockedMode, string aspectRatio, string gameStatus,
            string fifoStatus, string gpuName)
        {
            VSyncEnabled = vSyncEnabled;
            DockedMode = dockedMode;
            AspectRatio = aspectRatio;
            GameStatus = gameStatus;
            FifoStatus = fifoStatus;
            GpuName = gpuName;
        }
    }
}