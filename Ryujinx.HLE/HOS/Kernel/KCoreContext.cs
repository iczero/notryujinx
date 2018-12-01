using Ryujinx.Common;

namespace Ryujinx.HLE.HOS.Kernel
{
    class KCoreContext
    {
        private KScheduler _scheduler;

        private HleCoreManager _coreManager;

        public bool ContextSwitchNeeded { get; private set; }

        public long LastContextSwitchTime { get; private set; }

        public long TotalIdleTimeTicks { get; private set; } //TODO

        public KThread CurrentThread  { get; private set; }
        public KThread SelectedThread { get; private set; }

        public KCoreContext(KScheduler scheduler, HleCoreManager coreManager)
        {
            this._scheduler   = scheduler;
            this._coreManager = coreManager;
        }

        public void SelectThread(KThread thread)
        {
            SelectedThread = thread;

            if (SelectedThread != CurrentThread)
            {
                ContextSwitchNeeded = true;
            }
        }

        public void UpdateCurrentThread()
        {
            ContextSwitchNeeded = false;

            LastContextSwitchTime = PerformanceCounter.ElapsedMilliseconds;

            CurrentThread = SelectedThread;

            if (CurrentThread != null)
            {
                long currentTime = PerformanceCounter.ElapsedMilliseconds;

                CurrentThread.TotalTimeRunning += currentTime - CurrentThread.LastScheduledTime;
                CurrentThread.LastScheduledTime = currentTime;
            }
        }

        public void ContextSwitch()
        {
            ContextSwitchNeeded = false;

            LastContextSwitchTime = PerformanceCounter.ElapsedMilliseconds;

            if (CurrentThread != null)
            {
                _coreManager.Reset(CurrentThread.Context.Work);
            }

            CurrentThread = SelectedThread;

            if (CurrentThread != null)
            {
                long currentTime = PerformanceCounter.ElapsedMilliseconds;

                CurrentThread.TotalTimeRunning += currentTime - CurrentThread.LastScheduledTime;
                CurrentThread.LastScheduledTime = currentTime;

                CurrentThread.ClearExclusive();

                _coreManager.Set(CurrentThread.Context.Work);

                CurrentThread.Context.Execute();
            }
        }
    }
}