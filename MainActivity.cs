using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using RestSharp;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace TSARScanner
{
    [Activity(Label = "TSAR Scanner", Theme = "@android:style/Theme.Material.NoActionBar", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class MainActivity : Activity, IOnScannerEvent
    {
        private TextView scanStatus = null,
                         dealName = null,
                         purchaseOrder = null,
                         time = null,
                         dueDate = null,
                         currentStage = null;

        private Button waitingButton = null,
                       productionButton = null,
                       doneButton = null,
                       mtrButton = null,
                       cancelButton = null;

        private ProgressBar loadingScanView = null;

        private LinearLayout buttonLayout = null;

        private RelativeLayout mainLayout = null,
                               loadingMainView = null;

        private readonly Utilities util = new Utilities();
        DBrecord record = new DBrecord();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            Xamarin.Forms.Forms.Init(this, savedInstanceState);

            try
            {
                util.TestConnection();

                SetContentView(Resource.Layout.scan_ready);
                scanStatus = FindViewById<TextView>(Resource.Id.scanStatus);
                loadingScanView = FindViewById<ProgressBar>(Resource.Id.loadingScanView);
                scanStatus.Visibility = ViewStates.Visible;
            }
            catch
            {
                Intent intent = new Intent(this, typeof(ConnectionFailedActivity));
                StartActivity(intent);
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            BarcodeScanner.GetInstance(this);
            BarcodeScanner.RegisterUIObject(this);

            // Make MTR and Cancel button clickable again after returning to activity_main and update the Linked MTR count
            // Also update the textviews on resume and put them in the if statement to avoid a null exception
            if (mtrButton != null)
            {
                util.EnableMtrAndCancelButton(mtrButton, cancelButton);
                mtrButton.Text = "MTR\n( " + Global.LinkedMTRs.Count + " )";
                util.UpdateTextViews(record, time, dueDate);
            }

            // Dismiss loading view when going back to activity_main from MTR activity
            if (loadingMainView != null)
            {
                util.DismissReloadStageView(loadingMainView, mainLayout);
            }
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

        public async void OnDataScanned(string scanData)
        {
            // Disable stage buttons while record is loading
            if (mainLayout != null)
                util.DisableRecordButtons(productionButton, doneButton, mtrButton, cancelButton);


            // ProgressBar for moving between scan_ready layout and activity_main layout
            util.LoadStageView(loadingScanView, scanStatus);


            // ProgressBar used for reloading the stage view (i.e. a record is scanned while already on the activity_main layout)
            if (loadingMainView != null)
                util.ReloadStageView(loadingMainView, mainLayout);


            // If ID scanned is valid then proceed as normal, otherwise show a dialog indicating the error and go to appropriate page.
            var regex = new Regex("^[0-9]*$");
            if (regex.IsMatch(scanData))
            {
                Global.ScannedId = scanData;

                try
                {
                    record = await Task.Run(() => util.GetRecord());

                    SetContentView(Resource.Layout.activity_main);

                    mainLayout = FindViewById<RelativeLayout>(Resource.Id.mainLayout);
                    loadingMainView = FindViewById<RelativeLayout>(Resource.Id.loadingMainView);
                    dealName = FindViewById<TextView>(Resource.Id.dealName);
                    purchaseOrder = FindViewById<TextView>(Resource.Id.purchaseOrder);
                    time = FindViewById<TextView>(Resource.Id.time);
                    dueDate = FindViewById<TextView>(Resource.Id.dueDate);

                    waitingButton = FindViewById<Button>(Resource.Id.waitingButton);
                    productionButton = FindViewById<Button>(Resource.Id.productionButton);
                    doneButton = FindViewById<Button>(Resource.Id.doneButton);
                    mtrButton = FindViewById<Button>(Resource.Id.mtrButton);
                    cancelButton = FindViewById<Button>(Resource.Id.cancelButton);
                    currentStage = FindViewById<TextView>(Resource.Id.currentStage);
                    buttonLayout = FindViewById<LinearLayout>(Resource.Id.buttonLayout);

                    Global.LinkedMTRs = util.GetMTRsLinkedToDeal();
                    Global.LinkedMTRs = Global.LinkedMTRs.OrderBy(x => x.Name).ToList();

                    mtrButton.Text = "MTR\n( " + Global.LinkedMTRs.Count + " )";

                    util.GetTimeInProgress();

                    util.DisplayRecord(record, dealName, purchaseOrder, time, dueDate, waitingButton, productionButton, doneButton, currentStage, buttonLayout);
                    util.DismissReloadStageView(loadingMainView, mainLayout);

                    productionButton.Click += OnClickStage;
                    doneButton.Click += OnClickStage;
                    cancelButton.Click += OnClickCancel;
                    mtrButton.Click += OnClickMTR;

                    BarcodeScanner.EnableScanner();
                }
                catch (Exception ex)
                {
                    // If failed to get record then check if the internet connection is the problem
                    try
                    {
                        // If internet is not the problem, then show the exception
                        // If internet was the problem and is fixed, then tell user to try again
                        await ShowDialogRecordException(ex.Message, "There was a problem encountered while acquiring record information. Please try again.");
                    }
                    catch (Exception e)
                    {
                        // If still no internet connection after attempting to resolve then show dialog indicating so and start the ConnectionFailedActivity
                        ShowDialogInternet(e); 
                    }
                }
                finally
                {
                    util.DismissLoadStageView(loadingScanView, scanStatus);

                    // If an incorrect code is scanned then loadingMainView will be null
                    if (loadingMainView != null)
                        util.DismissReloadStageView(loadingMainView, mainLayout);

                    if (mainLayout != null)
                        util.EnableRecordButtons(productionButton, doneButton, mtrButton, cancelButton);
                }
            }
            else
            {
                ShowDialogInvalidData();

                util.DismissLoadStageView(loadingScanView, scanStatus);

                if (loadingMainView != null)
                    util.DismissReloadStageView(loadingMainView, mainLayout);

                if (mainLayout != null)
                    util.EnableRecordButtons(productionButton, doneButton, mtrButton, cancelButton);
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private async void OnClickStage(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            IRestResponse restResponse;
            btn.Enabled = false;
            util.DisableMtrAndCancelButton(mtrButton, cancelButton);

            if (btn == productionButton)
            {
                util.ReloadStageView(loadingMainView, mainLayout);
                await Task.Delay(1); //This doesn't look important, but it is. FML.
                restResponse = await util.TransitionRecord(Constants.TRANSITIONID_JOBSTARTED);

                if (restResponse.IsSuccessful)
                {
                    util.DismissReloadStageView(loadingMainView, mainLayout);
                    util.DisableButton(waitingButton);
                    util.DisableButton(doneButton);
                    util.ActiveButton(btn);
                    record.StageString = Constants.PRODUCTION;

                    util.EnableMtrAndCancelButton(mtrButton, cancelButton);
                    util.UpdateTextViews(record, time, dueDate);
                }
                else if (!restResponse.IsSuccessful)
                {
                    // Check if internet connection is reason for failed stage update
                    // If internet is issue then attempt to re-establish connection and show appropriate dialog
                    await ShowDialogFailedStageUpdate();

                    util.DismissReloadStageView(loadingMainView, mainLayout);
                    btn.Enabled = true;
                    util.EnableMtrAndCancelButton(mtrButton, cancelButton);
                    util.UpdateTextViews(record, time, dueDate);
                }
            }
            else if (btn == doneButton)
            {
                BarcodeScanner.DisableScanner();
                AlertDialog.Builder builder = new AlertDialog.Builder(this); // Using an AlertDialog.Builder separately from an AlertDialog allows customization of buttons

                builder.SetTitle("CONFIRMATION");
                builder.SetMessage("Are you sure?");
                builder.SetPositiveButton("Done", HandlerDialogStage);
                builder.SetNeutralButton("Cancel", HandlerDialogStage);      // Using NeutralButton moves the cancel button to the left of the dialog to minimize misclicks

                AlertDialog dialog = builder.Create();
                dialog.SetCanceledOnTouchOutside(false);                     // Forces user to select either Done or Cancel
                dialog.Show();

                Button positiveButton = dialog.GetButton((int)DialogButtonType.Positive);
                positiveButton.SetTextColor(Color.ParseColor(Constants.COMPLETED_TEXT_COLOR));
            }

            
        }

        public async void OnClickCancel(object sender, EventArgs e)
        {
            Button btn = (Button)sender;

            // Disable cancel button after clicking to prevent multiple clicks
            btn.Clickable = false;

            util.ReloadStageView(loadingMainView, mainLayout);

            try
            {
                // Checking for internet connection before going back to ready for scan
                await util.CheckInternetConnection(); 

                Intent intent = new Intent(this, typeof(MainActivity));
                StartActivity(intent);
            }
            catch (Exception ex)
            {
                // If failure to get internet connection then show a dialog saying to inform the office
                ShowDialogInternet(ex); 
            }
        }

        public async void OnClickMTR(object sender, EventArgs e)
        {
            Button btn = (Button)sender;

            // Disable MTR button after clicking to prevent multiple clicks
            btn.Clickable = false;

            util.ReloadStageView(loadingMainView, mainLayout);

            try
            {
                // Checking for internet connection before going to MtrActivity
                await util.CheckInternetConnection(); 

                Intent intent = new Intent(this, typeof(MtrActivity));
                StartActivity(intent);
            }
            catch (Exception ex)
            {
                // If failure to get internet connection then show a dialog saying to inform the office
                ShowDialogInternet(ex); 
            }
        }

        // This method checks the internet connection when attempt to update stage fails and displays a dialog to user depending on the outcome
        public async Task ShowDialogFailedStageUpdate()
        {
            var current = Connectivity.NetworkAccess;

            var dlgAlert = new AlertDialog.Builder(this).Create();
            dlgAlert.SetTitle("ERROR");
            dlgAlert.SetButton("OK", HandlerDialogStage);

            try
            {
                if (current == NetworkAccess.Internet)      // If currently have a connection to internet then show dialog indicating stage update failure
                {
                    dlgAlert.SetMessage("Failed to update the stage. If this problem persists, please inform the office.");
                    dlgAlert.Show();
                }
                else                                        // If there is no internet connection then attempt to establish connection 
                {                                           // and inform the user to try to update stage again
                    await util.CheckInternetConnection();
                    dlgAlert.SetMessage("There was a problem encountered while updating the stage. Please try again.");
                    dlgAlert.Show();
                }
            }
            catch (Exception ex)
            {
                ShowDialogInternet(ex);
            }
        }

        // This dialog checks the internet connectivity when the scanner fails to get the record information
        // If connected to internet then it shows what the exception was.
        // If not connected to internet it attempts to reconnect and shows dialog indcating to retry action
        public async Task ShowDialogRecordException(string exceptionMessage, string dialogMessage)
        {
            var current = Connectivity.NetworkAccess;
            var dlgAlert = new AlertDialog.Builder(this).Create();

            dlgAlert.SetTitle("ERROR");
            dlgAlert.SetButton("OK", HandlerDialogRecordException);
            dlgAlert.SetCanceledOnTouchOutside(false);

            if (current == NetworkAccess.Internet)           // If currently connected to internet then show dialog with the GetRecord() exception
            {
                dlgAlert.SetMessage(exceptionMessage);
                dlgAlert.Show();
            }
            else                                            // If currently not connected to internet then attempt to establish connection
            {                                               // If successful in connecting then show dialog indicating to user to try again
                await util.CheckInternetConnection();

                dlgAlert.SetMessage(dialogMessage);
                dlgAlert.Show();
            }
        }

        public void ShowDialogInternet(Exception ex)
        {
            var dlgAlert = new AlertDialog.Builder(this).Create();
            dlgAlert.SetTitle("ERROR");
            dlgAlert.SetMessage(ex.Message);
            dlgAlert.SetButton("OK", HandlerDialogInternet);
            dlgAlert.SetCanceledOnTouchOutside(false);
            dlgAlert.Show();
        }

        public void ShowDialogInvalidData()
        {
            var dlgAlert = new AlertDialog.Builder(this).Create();
            dlgAlert.SetTitle("ERROR");
            dlgAlert.SetMessage("Invalid Data.");
            dlgAlert.SetButton("OK", HandlerDialogRecordException);
            dlgAlert.SetCanceledOnTouchOutside(false);
            dlgAlert.Show();
        }

        // Event handler for stage dialog buttons
        private async void HandlerDialogStage(object sender, DialogClickEventArgs e)
        {
            IRestResponse restResponse;
            AlertDialog alertDialog = sender as AlertDialog;

            Button btnClicked = alertDialog.GetButton(e.Which); // Gets which dialog button was clicked
            if (btnClicked.Text == "OK")
            {
                alertDialog.Dispose();
            }
            else if (btnClicked.Text == "Done")
            {
                alertDialog.Dispose();
                util.ReloadStageView(loadingMainView, mainLayout);
                await Task.Delay(1);
                restResponse = await util.TransitionRecord(Constants.TRANSITIONID_JOBCOMPLETED);

                if (restResponse.IsSuccessful)
                {
                    record.StageString = Constants.COMPLETE;
                    util.DisableButton(productionButton);
                    util.ActiveButton(doneButton);

                    util.DismissReloadStageView(loadingMainView, mainLayout);
                }
                else if (!restResponse.IsSuccessful)
                {
                    // Check if internet connection is reason for failed stage update
                    // If internet is issue then attempt to re-establish connection and show appropriate dialog
                    await ShowDialogFailedStageUpdate();

                    // Enable button again after unsuccessful stage update
                    doneButton.Enabled = true; 
                    util.DismissReloadStageView(loadingMainView, mainLayout);
                }
            }
            else if (btnClicked.Text == "Cancel")
            {
                doneButton.Enabled = true;
                alertDialog.Dispose();
            }

            BarcodeScanner.EnableScanner();
            util.EnableMtrAndCancelButton(mtrButton, cancelButton);
            util.UpdateTextViews(record, time, dueDate);
        }

        // Event handler for dialog button clicks for when the record scanned doesn't exist (Invalid ID is scanned)
        private void HandlerDialogRecordException(object sender, DialogClickEventArgs e)
        {
            AlertDialog alertDialog = sender as AlertDialog;

            Button btnClicked = alertDialog.GetButton(e.Which);
            if (btnClicked.Text == "OK")
            {
                alertDialog.Dispose();
                BarcodeScanner.EnableScanner(); // This is here so the user cannot keep scanning while the dialog is showing which would make multiple dialogs stack on top of each other
            }
        }

        public void HandlerDialogInternet(object sender, DialogClickEventArgs e)
        {
            AlertDialog alertDialog = sender as AlertDialog;

            Button btnClicked = alertDialog.GetButton(e.Which);
            if (btnClicked.Text == "OK")
            {
                alertDialog.Dispose();

                Intent intent = new Intent(this, typeof(ConnectionFailedActivity));
                StartActivity(intent);
            }
        }

        public override void OnBackPressed() { }

        public void OnStatusUpdate(string scanData)
        {
            Console.WriteLine(scanData);
        }
    }
}

