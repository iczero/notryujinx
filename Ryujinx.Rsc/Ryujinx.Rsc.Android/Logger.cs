using Android.Util;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ALog = Android.Util.Log; 

namespace Ryujinx.Rsc.Android
{
    public class Logger : ILogTarget
    {
        private DefaultLogFormatter _formatter;
        private string tag = "Ryujinx-Log";

        public string Name => "Android Logger";

        public Logger()
        {
            _formatter = new DefaultLogFormatter();
        }

        public void Dispose()
        {
        }

        public void Log(object sender, LogEventArgs args)
        {
            switch (args.Level)
            {
                case LogLevel.Debug:
                    ALog.Debug(tag, _formatter.Format(args));
                    break;
                case LogLevel.Stub:
                    ALog.Debug(tag, _formatter.Format(args));
                    break;
                case LogLevel.Info:
                    ALog.Info(tag, _formatter.Format(args));
                    break;
                case LogLevel.Warning:
                    ALog.Warn(tag, _formatter.Format(args));
                    break;
                case LogLevel.Error:
                    ALog.Error(tag, _formatter.Format(args));
                    break;
                case LogLevel.Guest:
                    ALog.Debug(tag, _formatter.Format(args));
                    break;
                case LogLevel.AccessLog:
                    ALog.Debug(tag, _formatter.Format(args));
                    break;
                case LogLevel.Notice:
                    ALog.Verbose(tag, _formatter.Format(args));
                    break;
                case LogLevel.Trace:
                    ALog.Verbose(tag, _formatter.Format(args));
                    break;
            }
        }
    }
}
