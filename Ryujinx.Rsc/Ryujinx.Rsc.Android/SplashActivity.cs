using Android.App;
using Android.Content;
using Application = Android.App.Application;

namespace Ryujinx.Rsc.Android
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : Activity
    {
        protected override void OnResume()
        {
            App.PreviewerDetached = true;
            
            base.OnResume();

            StartActivity(new Intent(Application.Context, typeof(MainActivity)));
        }
    }
}
