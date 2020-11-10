using Android.App;
using Android.Graphics;
using Android.Net.Wifi;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.CData.ZohoCRM;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace TSARScanner
{
    class Utilities
    {
        private static ZohoCRMConnection connection;
        private static readonly string internalStoragePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        private static readonly string fileName = System.IO.Path.Combine(internalStoragePath, "OAuthSettingsProduction.txt");
        private static string Deals_Id;

        public void TestConnection(ZohoCRMConnectionStringBuilder connString = null)
        {
            if (connString is null)
            {
                connString = new ZohoCRMConnectionStringBuilder
                {
                    OAuthClientId = Constants.zohoClientID,
                    OAuthClientSecret = Constants.zohoClientSecret,
                    RTK = Constants.rtk,
                    InitiateOAuth = "REFRESH",
                    OAuthSettingsLocation = fileName,
                    Location = internalStoragePath
                };
            }

            try
            {
                ZohoCRMConnection conn = new ZohoCRMConnection(connString.ToString());

                if (conn.State == ConnectionState.Closed)
                {
                    conn.Open();
                }

                conn.TestConnection();
                conn.Close();
            }
            catch
            {
                throw;
            }
        }

        public void CreateConnection(String scannedGrantToken)
        {
            ZohoCRMConnectionStringBuilder connString = new ZohoCRMConnectionStringBuilder
            {
                OAuthClientId = Constants.zohoClientID,
                OAuthClientSecret = Constants.zohoClientSecret,
                RTK = Constants.rtk,
                InitiateOAuth = "REFRESH",
                OAuthSettingsLocation = fileName,
                Location = internalStoragePath,
                OAuthVerifier = scannedGrantToken
            };

            try
            {
                ZohoCRMConnection conn = new ZohoCRMConnection(connString.ToString());

                if (conn.State == ConnectionState.Closed)
                {
                    conn.Open();
                }

                conn.TestConnection();
                conn.Close();
            }
            catch
            {
                throw;
            }
        }

        public String GetConnectionString()
        {
            String internalStoragePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);

            return "InitiateOAuth=REFRESH;OAuthClientId=" + Constants.zohoClientID + "; OAuthClientSecret=" + Constants.zohoClientSecret + ";CallbackURL=" + Constants.CallbackURL + ";RTK =" + Constants.rtk +
                   ";OAuthSettingsLocation=" + fileName + ";Location=" + internalStoragePath;
        }

        public DBrecord GetRecord()
        {
            using (connection = new ZohoCRMConnection(GetConnectionString()))
            {
                // Try to get the record of the scanned ID. 
                // If the ID does not exist or is incorrect then throw exception.
                try
                {
                    ZohoCRMCommand cmd = new ZohoCRMCommand("SELECT AccountName_Name, Stage, PurchaseOrderNumber, CreatedTime, DueTime, DueDate, [HotOrder?] AS HotOrder, Id " +
                        "FROM ZohoCRM.Deals WHERE LookupKey = @LookupKey", connection);

                    cmd.Parameters.Add(new ZohoCRMParameter("@LookupKey", Global.ScannedId));

                    ZohoCRMDataReader reader = cmd.ExecuteReader();

                    // Checks if a record exists with that ID and if not it throws a new exception
                    if (reader.HasRows)
                    {
                        // Deals_Id is used for the MTRs (GetMTRsLinkedToDeal) and to refer to the deal URL 
                        Deals_Id = reader["Id"].ToString();

                        double[] timeInProgressValues = GetTimeInProgress();

                        DBrecord record = new DBrecord
                        {
                            DealNameString = reader["AccountName_Name"].ToString(),
                            StageString = reader["Stage"].ToString(),
                            PurchaseOrderString = reader["PurchaseOrderNumber"].ToString(),
                            CreationTime = (DateTime)reader["CreatedTime"],
                            DueTimeString = reader["DueTime"].ToString(),
                            HotOrder = reader["HotOrder"].ToString(),
                            TotalInProgressSecondsWait = timeInProgressValues[Constants.WAIT_INDEX],
                            TotalInProgressSecondsProduction = timeInProgressValues[Constants.PRODUCTION_INDEX],
                            TotalInProgressSecondsComplete = timeInProgressValues[Constants.COMPLETE_INDEX]
                        };

                        if (record.DueDate != DateTime.MinValue)
                        {
                            record.DueDate = (DateTime)reader["DueDate"];
                        }
                        else
                        {
                            record.DueDate = record.CreationTime.AddDays(2);

                            if (String.Equals(record.CreationTime.DayOfWeek.ToString(), "Friday"))
                                record.DueDate = record.DueDate.AddDays(2);
                            else if (String.Equals(record.CreationTime.DayOfWeek.ToString(), "Thursday"))
                                record.DueDate = record.DueDate.AddDays(2);

                            record.DueDate = record.DueDate.Date;
                        }

                        return record;
                    }
                    else
                    {
                        throw new Exception("No record found.");
                    }
                }
                catch
                {
                    throw;
                }
            }
        }

        public void DisplayRecord(DBrecord record, TextView dealName, TextView purchaseOrder, TextView time, TextView dueDate, Button waitingButton, Button productionButton, Button doneButton, TextView currentStage, LinearLayout buttonLayout)
        {
            // Reset text values
            dealName.Text = "";
            dueDate.Text = "Due ";
            purchaseOrder.Text = "PO# ";
            time.Text = "Time in Progress\n";

            dealName.Text += record.DealNameString;
            purchaseOrder.Text += record.PurchaseOrderString;

            DisplayDueDate(record, dueDate);
            DisplayTimeInProgress(record, time);
            if (record.StageString == Constants.WAITING || record.StageString == Constants.PRODUCTION || record.StageString == Constants.COMPLETE)
            {
                DisplayButtons(record, waitingButton, productionButton, doneButton);
            }
            else
            {
                currentStage.Text = record.StageString.Replace(" ", "\n");
                currentStage.Visibility = ViewStates.Visible;
                buttonLayout.Visibility = ViewStates.Gone;
            }
        }

        public void DisableButton(Button button)
        {
            button.Enabled = false;
            button.Alpha = 0.4f;
            button.SetTextColor(Color.ParseColor(Constants.BUTTON_TEXT_COLOR));
            button.SetBackgroundResource(Resource.Drawable.disable_button);
        }

        public void EnableButton(Button button)
        {
            button.Enabled = true;
            button.Alpha = 1.0f;
            button.SetTextColor(Color.ParseColor(Constants.BUTTON_TEXT_COLOR));
            button.SetBackgroundResource(Resource.Drawable.enable_button);
        }

        public void ActiveButton(Button button)
        {
            button.Enabled = false;
            button.Alpha = 1.0f;
            button.SetTextColor(Color.ParseColor(Constants.BUTTON_TEXT_COLOR));
            button.SetBackgroundResource(Resource.Drawable.active_button);
        }

        public void EnableRecordButtons(Button productionButton, Button doneButton, Button mtrButton, Button cancelButton)
        {
            productionButton.Clickable = true;
            doneButton.Clickable = true;
            mtrButton.Clickable = true;
            cancelButton.Clickable = true;
        }

        public void DisableRecordButtons(Button productionButton, Button doneButton, Button mtrButton, Button cancelButton)
        {
            productionButton.Clickable = false;
            doneButton.Clickable = false;
            mtrButton.Clickable = false;
            cancelButton.Clickable = false;
        }

        public void EnableMtrAndCancelButton(Button mtrButton, Button cancelButton)
        {
            mtrButton.Clickable = true;
            cancelButton.Clickable = true;
        }

        public void DisableMtrAndCancelButton(Button mtrButton, Button cancelButton)
        {
            mtrButton.Clickable = false;
            cancelButton.Clickable = false;
        }

        // Gets entire MTR collection from database
        public List<MTRrecord> GetMTRs()
        {
            List<MTRrecord> mtrs = new List<MTRrecord>();

            ZohoCRMCommand cmd = new ZohoCRMCommand("SELECT Id, MTRName, Size, Thickness, Grade FROM [CustomModule4]",
              connection);

            ZohoCRMDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                MTRrecord mtr = new MTRrecord()
                {
                    Id = reader["Id"].ToString(),
                    Name = reader["MTRName"].ToString(),
                    Size = reader["Size"].ToString(),
                    Thickness = reader["Thickness"].ToString(),
                    Grade = reader["Grade"].ToString()
                };

                mtrs.Add(mtr);
            }
            return mtrs;

        }

        //Pre-populate the RecyclerView list with values that already exist
        public List<MTRrecord> GetMTRsLinkedToDeal()
        {
            List<MTRrecord> linkedMTRs = new List<MTRrecord>();

            // Gets the Name, Id, Size, Grade, and Thickness from CustomModule4 and the Linked Id from LinkingModule3 for the Linked MTRs
            ZohoCRMCommand cmd = new ZohoCRMCommand("SELECT MTR.Id AS MTRId, Link.Id AS LinkId, MTR.MTRName, MTR.Size, MTR.Grade, MTR.Thickness " +
              "FROM ZohoCRM.CustomModule4 MTR, ZohoCRM.LinkingModule3 Link " +
              "WHERE MTR.Id = Link.MTRs_Id AND (Link.Deals_Id = @Deals_Id)", connection);

            cmd.Parameters.Add(new ZohoCRMParameter("@Deals_Id", Deals_Id));

            ZohoCRMDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                MTRrecord mtrRecord = new MTRrecord()
                {
                    Name = reader["MTRName"].ToString(),
                    Id = reader["MTRId"].ToString(),
                    LinkedId = reader["LinkId"].ToString(),
                    Size = reader["Size"].ToString(),
                    Thickness = reader["Thickness"].ToString(),
                    Grade = reader["Grade"].ToString()
                };

                linkedMTRs.Add(mtrRecord);
            }
            return linkedMTRs;

        }

        public void RemoveLink(String MTRs_LinkedId, String MTRs_Id)
        {
            ZohoCRMCommand cmd = new ZohoCRMCommand("DELETE FROM [LinkingModule3] WHERE Id = @MTRs_LinkedId AND Deals_Id = @Deals_Id " +
                "AND MTRs_Id = @MTRs_Id", connection);
            
            cmd.Parameters.Add(new ZohoCRMParameter("@MTRs_LinkedId", MTRs_LinkedId));
            cmd.Parameters.Add(new ZohoCRMParameter("@Deals_Id", Deals_Id));
            cmd.Parameters.Add(new ZohoCRMParameter("@MTRs_Id", MTRs_Id));

            cmd.ExecuteReader();
        }

        // Links MTRs to the scanned deal
        public string LinkMTRToDeal(String MTRs_Id)
        {
            int rowsAffected;
            ZohoCRMCommand cmd = new ZohoCRMCommand("INSERT INTO [LinkingModule3] (Deals_Id, MTRs_Id) VALUES (@Deals_Id , @MTRs_Id)", connection);

            cmd.Parameters.Add(new ZohoCRMParameter("@Deals_Id", Deals_Id));
            cmd.Parameters.Add(new ZohoCRMParameter("@MTRs_Id", MTRs_Id));

            rowsAffected = cmd.ExecuteNonQuery();
            cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT SCOPE_IDENTITY()";
            Object returnedValues = cmd.ExecuteScalar();
            return (String)returnedValues;
        }

        // Checks if the recyclerview has any pre-populated data and if it doesn't then it shows "NO DATA"
        public void CheckIfEmpty(RecyclerView recyclerView, TextView emptyView)
        {
            if (recyclerView.GetAdapter().ItemCount == 0)
            {
                recyclerView.Visibility = ViewStates.Gone;
                emptyView.Visibility = ViewStates.Visible;
            }
            else
            {
                recyclerView.Visibility = ViewStates.Visible;
                emptyView.Visibility = ViewStates.Gone;
            }
        }

        // Update the due date and time in progress textviews when a button is pressed/stage is changed.
        // This function sets the time in progress to 0 rather than doing any calculations since the time in progress is the time the current stage
        // has been in progress.
        public void UpdateTextViews(DBrecord record, TextView time, TextView dueDate)
        {
            DisplayTimeInProgress(record, time);
            DisplayDueDate(record, dueDate);
        }

        public void LoadStageView(ProgressBar loadingScanView, TextView scanStatus)
        {
            loadingScanView.Visibility = ViewStates.Visible;
            scanStatus.Visibility = ViewStates.Gone;
        }
        public void DismissLoadStageView(ProgressBar loadingScanView, TextView scanStatus)
        {
            loadingScanView.Visibility = ViewStates.Gone;
            scanStatus.Visibility = ViewStates.Visible;
        }
        public void ReloadStageView(RelativeLayout loadingMainView, RelativeLayout mainLayout)
        {
            loadingMainView.Visibility = ViewStates.Visible;
            mainLayout.Alpha = 0.5f;
        }

        public void DismissReloadStageView(RelativeLayout loadingMainView, RelativeLayout mainLayout)
        {
            loadingMainView.Visibility = ViewStates.Gone;
            mainLayout.Alpha = 1.0f;
        }

        public void LoadingReadyForScanView(RelativeLayout connectionFailedLayout, ProgressBar progressBar, TextView versionInfo)
        {
            progressBar.Visibility = ViewStates.Visible;
            connectionFailedLayout.Visibility = ViewStates.Gone;
            versionInfo.Visibility = ViewStates.Gone;
        }

        public void DismissLoadingReadyForScanView(RelativeLayout connectionFailedLayout, ProgressBar progressBar, TextView versionInfo)
        {
            progressBar.Visibility = ViewStates.Gone;
            connectionFailedLayout.Visibility = ViewStates.Visible;
            versionInfo.Visibility = ViewStates.Visible;
        }

        public double[] GetTimeInProgress()
        {
            DateTime inProgressStartTimeWait = DateTime.MinValue,
                     inProgressStartTimeProduction = DateTime.MinValue,
                     inProgressStartTimeComplete = DateTime.MinValue,
                     nextStageStartTimeWait,
                     nextStageStartTimeProduction,
                     nextStageStartTimeComplete;

            double totalInProgressSecondsWait = 0,
                   totalInProgressSecondsProduction = 0,
                   totalInProgressSecondsComplete = 0;

            bool addTimeWait = false,
                 addTimeProduction = false,
                 addTimeComplete = false;

            double[] allTotals = new double[3];

            ZohoCRMCommand cmd = new ZohoCRMCommand("SELECT Stage, LastModifiedTime FROM ZohoCRM.StageHistories WHERE (DealId = @Deals_Id) ORDER BY LastModifiedTime ASC", connection);

            cmd.Parameters.Add(new ZohoCRMParameter("@Deals_Id", Deals_Id));

            ZohoCRMDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (addTimeWait)
                {
                    nextStageStartTimeWait = (DateTime)reader["LastModifiedTime"];
                    totalInProgressSecondsWait += (nextStageStartTimeWait - inProgressStartTimeWait).TotalSeconds;
                    addTimeWait = false;
                }
                else if (addTimeProduction)
                {
                    nextStageStartTimeProduction = (DateTime)reader["LastModifiedTime"];
                    totalInProgressSecondsProduction += (nextStageStartTimeProduction - inProgressStartTimeProduction).TotalSeconds;
                    addTimeProduction = false;
                }
                else if (addTimeComplete)
                {
                    nextStageStartTimeComplete = (DateTime)reader["LastModifiedTime"];
                    totalInProgressSecondsComplete += (nextStageStartTimeComplete - inProgressStartTimeComplete).TotalSeconds;
                    addTimeComplete = false;
                }

                if (reader["Stage"].ToString() == Constants.WAIT_STAGE_ID)
                {
                    inProgressStartTimeWait = (DateTime)reader["LastModifiedTime"];
                    addTimeWait = true;
                }
                else if (reader["Stage"].ToString() == Constants.PROD_STAGE_ID)
                {
                    inProgressStartTimeProduction = (DateTime)reader["LastModifiedTime"];
                    addTimeProduction = true;
                }
                else if (reader["Stage"].ToString() == Constants.COMPLETE_STAGE_ID)
                {
                    inProgressStartTimeComplete = (DateTime)reader["LastModifiedTime"];
                    addTimeComplete = true;
                }
            }

            

            if (addTimeWait)
            {
                totalInProgressSecondsWait += (DateTime.Now - inProgressStartTimeWait).TotalSeconds;
            }
            else if (addTimeProduction)
            {
                totalInProgressSecondsProduction += (DateTime.Now - inProgressStartTimeProduction).TotalSeconds;
            }
            else if (addTimeComplete)
            {
                totalInProgressSecondsComplete += (DateTime.Now - inProgressStartTimeComplete).TotalSeconds;
            }

            allTotals[Constants.WAIT_INDEX] = totalInProgressSecondsWait;
            allTotals[Constants.PRODUCTION_INDEX] = totalInProgressSecondsProduction;
            allTotals[Constants.COMPLETE_INDEX] = totalInProgressSecondsComplete;

            return allTotals;
        }

        public void DisplayTimeInProgress(DBrecord record, TextView time)
        {
            TimeSpan spanWait = TimeSpan.FromSeconds(record.TotalInProgressSecondsWait),
                     spanProduction = TimeSpan.FromSeconds(record.TotalInProgressSecondsProduction),
                     spanComplete = TimeSpan.FromSeconds(record.TotalInProgressSecondsComplete);

            if (String.Equals(record.StageString, Constants.WAITING))
            {
                time.Text = "Time in Queue\n";
                if (spanWait.Days > 0 && spanWait.Days < 10)
                {
                    time.Text += spanWait.Days.ToString().PadLeft(2);
                    if (spanWait.Days > 1)
                    {
                        time.Text += " Days ";
                    }
                    else
                    {
                        time.Text += " Day ";
                    }
                    if (spanWait.Hours > 0)
                    {
                        time.Text += spanWait.Hours;
                        if (spanWait.Hours > 1)
                        {
                            time.Text += " Hours ";
                        }
                        else
                        {
                            time.Text += " Hour ";
                        }
                    }
                    if (spanWait.Minutes > 0)
                    {
                        time.Text += spanWait.Minutes;
                        if (spanWait.Minutes > 1)
                        {
                            time.Text += " Minutes ";
                        }
                        else
                        {
                            time.Text += " Minute ";
                        }
                    }
                    else
                        time.Text += "0 Minutes";
                }
                else
                {
                    time.Text += spanWait.Days.ToString().PadLeft(2);
                    time.Text += " Days ";
                    if (spanWait.Hours > 0)
                    {
                        time.Text += spanWait.Hours;
                        if (spanWait.Hours > 1)
                        {
                            time.Text += " Hours ";
                        }
                        else
                        {
                            time.Text += " Hour ";
                        }
                    }
                    else
                    {
                        if (spanWait.Minutes > 0)
                        {
                            time.Text += spanWait.Minutes;
                            if (spanWait.Minutes > 1)
                            {
                                time.Text += " Minutes ";
                            }
                            else
                            {
                                time.Text += " Minute ";
                            }
                        }
                        else
                            time.Text += "0 Minutes";
                    }
                }
            }
            else if (String.Equals(record.StageString, Constants.PRODUCTION))
            {
                time.Text = "Time in Progress\n";
                if (spanProduction.Days > 0 && spanProduction.Days < 10)
                {
                    time.Text += spanProduction.Days.ToString().PadLeft(2);
                    if (spanProduction.Days > 1)
                    {
                        time.Text += " Days ";
                    }
                    else
                    {
                        time.Text += " Day ";
                    }
                    if (spanProduction.Hours > 0)
                    {
                        time.Text += spanProduction.Hours;
                        if (spanProduction.Hours > 1)
                        {
                            time.Text += " Hours ";
                        }
                        else
                        {
                            time.Text += " Hour ";
                        }
                    }
                    if (spanProduction.Minutes > 0)
                    {
                        time.Text += spanProduction.Minutes;
                        if (spanProduction.Minutes > 1)
                        {
                            time.Text += " Minutes ";
                        }
                        else
                        {
                            time.Text += " Minute ";
                        }
                    }
                    else
                        time.Text += "0 Minutes";
                }
                else
                {
                    time.Text += spanProduction.Days.ToString().PadLeft(2);
                    time.Text += " Days ";
                    if (spanProduction.Hours > 0)
                    {
                        time.Text += spanProduction.Hours;
                        if (spanProduction.Hours > 1)
                        {
                            time.Text += " Hours ";
                        }
                        else
                        {
                            time.Text += " Hour ";
                        }
                    }
                    else
                    {
                        if (spanProduction.Minutes > 0)
                        {
                            time.Text += spanProduction.Minutes;
                            if (spanProduction.Minutes > 1)
                            {
                                time.Text += " Minutes ";
                            }
                            else
                            {
                                time.Text += " Minute ";
                            }
                        }
                        else
                            time.Text += "0 Minutes";
                    }
                }
            }
            else if (String.Equals(record.StageString, Constants.COMPLETE))
            {
                time.Text = "Time Since Completion\n";

                if (spanComplete.Days > 0 && spanComplete.Days < 10)
                {
                    time.Text += spanComplete.Days.ToString().PadLeft(2);
                    if (spanComplete.Days > 1)
                    {
                        time.Text += " Days ";
                    }
                    else
                    {
                        time.Text += " Day ";
                    }
                    if (spanComplete.Hours > 0)
                    {
                        time.Text += spanComplete.Hours;
                        if (spanComplete.Hours > 1)
                        {
                            time.Text += " Hours ";
                        }
                        else
                        {
                            time.Text += " Hour ";
                        }
                    }
                    if (spanComplete.Minutes > 0)
                    {
                        time.Text += spanComplete.Minutes;
                        if (spanComplete.Minutes > 1)
                        {
                            time.Text += " Minutes ";
                        }
                        else
                        {
                            time.Text += " Minute ";
                        }
                    }
                    else
                        time.Text += "0 Minutes";
                }
                else
                {
                    time.Text += spanComplete.Days.ToString().PadLeft(2);
                    time.Text += " Days ";

                    if (spanComplete.Hours > 0)
                    {
                        time.Text += spanComplete.Hours;
                        if (spanComplete.Hours > 1)
                        {
                            time.Text += " Hours ";
                        }
                        else
                        {
                            time.Text += " Hour ";
                        }
                    }
                    else
                    {
                        if (spanComplete.Minutes > 0)
                        {
                            time.Text += spanComplete.Minutes;
                            if (spanComplete.Minutes > 1)
                            {
                                time.Text += " Minutes ";
                            }
                            else
                            {
                                time.Text += " Minute ";
                            }
                        }
                        else
                            time.Text += "0 Minutes";
                    }
                }
            }
            else
                time.Text = "";

        }

        public void DisplayDueDate(DBrecord record, TextView dueDate)
        {
            // Get the difference between the due date and the current time
            // This is used to determine the color of the text output for the due date (Yellow < 2 HOURS, Red = PAST DUE)
            TimeSpan timeDifference = record.DueDate - DateTime.Now;

            dueDate.Text = "Due ";

            if (!String.Equals(record.StageString, Constants.COMPLETE))
            {
                if (record.HotOrder == "YES")
                {
                    dueDate.SetTextColor(Color.OrangeRed);
                    dueDate.Text += record.DueDate.ToString("ddd M/d") + " " + record.DueTimeString + " " + Constants.EMOJI_FIRE;
                }
                else if (timeDifference.TotalHours < Constants.DUE_DATE_EXCEEDED)
                {
                    dueDate.SetTextColor(Color.Red);
                    dueDate.Text += record.DueDate.ToString("ddd M/d") + " " + record.DueTimeString;
                }
                else if (timeDifference.TotalHours <= Constants.DUE_DATE_APPROACHING)
                {
                    dueDate.SetTextColor(Color.Yellow);
                    dueDate.Text += record.DueDate.ToString("ddd M/d") + " " + record.DueTimeString + " " + Constants.EMOJI_WARNING;
                }
                else
                    dueDate.Text += record.DueDate.ToString("ddd M/d") + " " + record.DueTimeString;
            }

            // If current stage is "DONE" then change due date text color to green and print Time Since Completed
            if (String.Equals(record.StageString, Constants.COMPLETE))
            {
                dueDate.SetTextColor(Color.ParseColor(Constants.COMPLETED_TEXT_COLOR));
                dueDate.Text += record.DueDate.ToString("ddd M/d") + " " + record.DueTimeString;
            }
        }

        public void DisplayButtons(DBrecord record, Button waitingButton, Button productionButton, Button doneButton)
        {
            TimeSpan spanProduction = TimeSpan.FromSeconds(record.TotalInProgressSecondsProduction);

            if (String.Equals(record.StageString, Constants.WAITING))
            {
                ActiveButton(waitingButton);
                EnableButton(productionButton);
                DisableButton(doneButton);
            }
            else if (String.Equals(record.StageString, Constants.PRODUCTION))
            {
                DisableButton(waitingButton);
                ActiveButton(productionButton);

                if (spanProduction.TotalMinutes > Constants.MIN_PRODUCTION_TIME)
                    EnableButton(doneButton);
                else
                    DisableButton(doneButton);
            }
            else
            {
                DisableButton(waitingButton);
                ActiveButton(doneButton);
                DisableButton(productionButton);
            }
        }

        public async Task<IRestResponse> TransitionRecord(String transitionId)
        {
            var client = new RestClient("https://www.zohoapis.com/crm/v2");
            var request = new RestRequest("/Deals/" + Deals_Id + "/actions/blueprint", Method.PUT);
            var cancellationTokenSource = new CancellationTokenSource();

            request.AddHeader("Authorization", "Zoho-oauthtoken " + GetOAuthAccessToken());
            String jsonTransition = "{\"blueprint\":[{\"transition_id\":\"" + transitionId + "\",\"data\":{ }}]}";
            request.AddJsonBody(jsonTransition);

            var restResponse = await client.ExecuteTaskAsync(request, cancellationTokenSource.Token);

            return restResponse;
        }

        private string GetOAuthAccessToken()
        {
            var data = new Dictionary<string, string>();
            foreach (var row in File.ReadAllLines(fileName))
                data.Add(row.Split('=')[0], string.Join("=", row.Split('=').Skip(1).ToArray()));

            Console.WriteLine(data["_persist_oauthaccesstoken"]);
            return data["_persist_oauthaccesstoken"];
        }

        public async Task CheckInternetConnection()
        {
            var current = Connectivity.NetworkAccess;
            if (current != NetworkAccess.Internet)
            {
                WifiManager wifi = Application.Context.GetSystemService("wifi") as WifiManager;
                if (wifi.IsWifiEnabled)
                {
                    wifi.SetWifiEnabled(false);
                    wifi.SetWifiEnabled(true);
                    await Task.Delay(5000); // This allows the app time to get a wifi connection before proceeding
                }
                else
                {
                    wifi.SetWifiEnabled(true);
                    await Task.Delay(5000);
                }

                current = Connectivity.NetworkAccess;
            }

            if (current != NetworkAccess.Internet)
            {
                throw new Exception("Failed to establish connection. Please inform the office.");
            }
            
        }
    }
}
