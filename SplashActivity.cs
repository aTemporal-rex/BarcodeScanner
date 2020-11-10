using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.App;
using System.Threading.Tasks;

namespace TSARScanner
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : AppCompatActivity
    {
        Utilities util = new Utilities();
        public override void OnCreate(Bundle savedInstanceState, PersistableBundle persistentState)
        {
            base.OnCreate(savedInstanceState, persistentState);
        }

        // Launches the startup task
        protected override void OnResume()
        {
            base.OnResume();
            Task startupWork = new Task(() => { SimulateStartup(); });
            startupWork.Start();
        }

        // Show splash screen while application is loading
        async void SimulateStartup()
        {
            await Task.CompletedTask; // Simulate a bit of startup work.
            try
            {
                util.TestConnection();

                Intent intent = new Intent(this, typeof(MainActivity));
                StartActivity(intent);

            } catch
            {
                Intent intent = new Intent(this, typeof(ConnectionFailedActivity));
                StartActivity(intent);
            }
        }
        public override void OnBackPressed() { }
    }
}