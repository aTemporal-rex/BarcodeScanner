using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace TSARScanner
{
    [Activity(Label = "Connection Failed", Theme = "@android:style/Theme.Material.NoActionBar", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class ConnectionFailedActivity : Activity, IOnScannerEvent
    {
        private Button retryButton = null,
                       authorizeButton = null;

        private TextView versionInfo = null;

        private ProgressBar progressBar = null;

        private RelativeLayout connectionFailedLayout = null;

        private readonly Utilities util = new Utilities();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            VersionTracking.Track();
            var currentAppVersion = VersionTracking.CurrentVersion;

            SetContentView(Resource.Layout.connection_failed);
            retryButton = FindViewById<Button>(Resource.Id.retryButton);
            authorizeButton = FindViewById<Button>(Resource.Id.authorizeButton);
            progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);
            connectionFailedLayout = FindViewById<RelativeLayout>(Resource.Id.connectionFailedLayout);
            versionInfo = FindViewById<TextView>(Resource.Id.versionInfo);

            versionInfo.Text = "Version " + currentAppVersion;

            retryButton.Click += OnClickRetryButton;
            authorizeButton.Click += OnClickAuthorizeButton;
        }

        private void OnClickAuthorizeButton(object sender, EventArgs e)
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(this);
            builder.SetTitle("RE-AUTHORIZE CONNECTION");
            builder.SetMessage("Please scan grant token.");
            builder.SetPositiveButton("OK", HandlerDialogAuthorize);

            AlertDialog dialog = builder.Create();
            dialog.SetCanceledOnTouchOutside(false); // Forces user to press OK to initialize scanner
            dialog.Show();
        }

        private async void OnClickRetryButton(object sender, EventArgs e)
        {
            try
            {
                await Task.Run(() => util.TestConnection());

                Intent mainIntent = new Intent(this, typeof(MainActivity));
                StartActivity(mainIntent);
            }
            catch (Exception ex)
            {
                AlertDialog.Builder builder = new AlertDialog.Builder(this);
                builder.SetTitle("ERROR");
                builder.SetMessage("Failed to connect. Ensure the device is connected to the internet and try again. If this issue persists please inform the office.\n\nEXCEPTION: " + ex.Message);
                builder.SetPositiveButton("Retry", HandlerDialogRetryConnection);
                builder.SetNegativeButton("Cancel", HandlerDialogRetryConnection);

                AlertDialog dialog = builder.Create();
                dialog.Show();
            }
        }

        private void HandlerDialogAuthorize(object sender, DialogClickEventArgs e)
        {
            AlertDialog alertDialog = sender as AlertDialog;

            Button btnClicked = alertDialog.GetButton(e.Which);
            if (btnClicked.Text == "OK")
            {
                alertDialog.Dispose();
                BarcodeScanner.EnableScanner();
            }
            else
            {
                alertDialog.Dispose();
            }
        }

        private void HandlerDialogRetryConnection(object sender, DialogClickEventArgs e)
        {
            AlertDialog alertDialog = sender as AlertDialog;

            Button btnClicked = alertDialog.GetButton(e.Which);
            if (btnClicked.Text == "Cancel")
            {
                alertDialog.Dispose();
            }
            else if (btnClicked.Text == "Retry")
            {
                try
                {
                    util.TestConnection();

                    Intent mainIntent = new Intent(this, typeof(MainActivity));
                    StartActivity(mainIntent);
                }
                catch (Exception ex)
                {
                    AlertDialog.Builder builder = new AlertDialog.Builder(this);
                    builder.SetTitle("ERROR");
                    builder.SetMessage("Failed to connect. Ensure the device is connected to the internet and try again. If this issue persists please inform the office.\n\nEXCEPTION: " + ex.Message);
                    builder.SetPositiveButton("Retry", HandlerDialogRetryConnection);
                    builder.SetNegativeButton("Cancel", HandlerDialogRetryConnection);

                    AlertDialog dialog = builder.Create();
                    dialog.Show();
                }
            }
            else
                alertDialog.Dispose();
        }

        protected override void OnPause()
        {
            base.OnPause();

            BarcodeScanner.UnregisterUIObject();
            BarcodeScanner.ReleaseEMDK();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            BarcodeScanner.DeinitScanner();
            BarcodeScanner.ReleaseEMDK();
        }

        protected override void OnResume()
        {
            base.OnResume();

            // Acquire the barcode manager resources
            BarcodeScanner.GetInstance(this);
            BarcodeScanner.RegisterUIObject(this);
        }

        public async void OnDataScanned(string scanData)
        {
            string scannedGrantToken = scanData;

            // ProgressBar for going from ConnectionFailedActivity to MainActivity
            util.LoadingReadyForScanView(connectionFailedLayout, progressBar, versionInfo);
            
            await Task.Delay(1); // This allows LoadingReadyForScanView to show a progress bar while going from connection_failed layout to scan_ready layout

            // Check to see if scanData matches the regex required for a Grant Token
            var regex = new Regex(@"^[0-9a-zA-Z]*\.[0-9a-zA-Z]*\.[0-9a-zA-Z-_]*$");
            if (regex.IsMatch(scanData))
            {
                // Authenticate if first time connecting to Zoho or if connection is somehow lost
                try
                {
                    util.CreateConnection(scannedGrantToken);

                    Intent intent = new Intent(this, typeof(MainActivity));
                    StartActivity(intent);
                }
                catch (Exception ex)
                {
                    AlertDialog.Builder builder = new AlertDialog.Builder(this);
                    builder.SetTitle("ERROR");
                    builder.SetMessage(ex.Message);
                    builder.SetPositiveButton("OK", HandlerDialogAuthorize);

                    AlertDialog dialog = builder.Create();
                    dialog.Show();

                    util.DismissLoadingReadyForScanView(connectionFailedLayout, progressBar, versionInfo);
                    BarcodeScanner.EnableScanner();
                }
            }
            else
            {
                AlertDialog.Builder builder = new AlertDialog.Builder(this);
                builder.SetTitle("ERROR");
                builder.SetMessage("Invalid Data. Not a Grant Token.");
                builder.SetPositiveButton("OK", HandlerDialogAuthorize);

                AlertDialog dialog = builder.Create();
                dialog.Show();

                util.DismissLoadingReadyForScanView(connectionFailedLayout, progressBar, versionInfo);
                BarcodeScanner.EnableScanner();
            }
        }

        public void OnStatusUpdate(string scanData)
        {
            Console.WriteLine(scanData);
        }
    }
}