using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using ARMeilleure.Translation.PTC;
using Ryujinx.Common.Logging;
using System.IO;
using Application = Android.App.Application;

namespace Ryujinx.Rsc.Android
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : Activity
    {
        protected override void OnResume()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) == (int)Permission.Granted)
            {
                Load();

                base.OnResume();

                StartActivity(new Intent(Application.Context, typeof(MainActivity)));
            }
            else
            {
                RequestPermission();

                base.OnResume();
            }
        }

        private void RequestPermission()
        {
            ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.WriteExternalStorage }, 1);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            if ((grantResults.Length == 1) && (grantResults[0] == Permission.Granted))
            {
                Load();

                StartActivity(new Intent(Application.Context, typeof(MainActivity)));
            }
            else
            {
                Finish();
            }
        }

        private void Load()
        {
            if (!App.PreviewerDetached)
            {
                App.PreviewerDetached = true;

                var internalStorage =  Environment.ExternalStorageDirectory.AbsolutePath;

                var romPath = System.IO.Path.Combine(internalStorage, "ryujinx", "roms");
                var appPath = System.IO.Path.Combine(internalStorage, "ryujinx", "fs");

                Directory.CreateDirectory(romPath);

                App.GameDirectory = romPath;
                App.BaseDirectory = appPath;

                App.LoadConfiguration();

                Ryujinx.Common.Logging.Logger.AddTarget((new AsyncLogTargetWrapper(
                    new Logger(),
                    1000,
                    AsyncLogTargetOverflowAction.Discard)));

                System.AppDomain.CurrentDomain.UnhandledException += (object sender, System.UnhandledExceptionEventArgs e) => ProcessUnhandledException(e.ExceptionObject as System.Exception);

                Java.Lang.JavaSystem.LoadLibrary("c");
            }
        }

        private void ProcessUnhandledException(System.Exception exception)
        {
            Ptc.Close();
            PtcProfiler.Stop();

            string message = $"Unhandled exception caught: {exception}";

            Ryujinx.Common.Logging.Logger.Error?.PrintMsg(LogClass.Application, message);

            if (Ryujinx.Common.Logging.Logger.Error == null)
            {
                Ryujinx.Common.Logging.Logger.Notice.PrintMsg(LogClass.Application, message);
            }
        }
    }
}
