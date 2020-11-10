using System;
using System.Collections.Generic;
using Android.Content;
using Android.OS;
using Symbol.XamarinEMDK;
using Symbol.XamarinEMDK.Barcode;

namespace TSARScanner
{
    public class BarcodeScanner : Java.Lang.Object, EMDKManager.IEMDKListener
    {
        private static EMDKManager emdkManager = null;
        private static BarcodeManager barcodeManager = null;
        private static Scanner scanner = null;

        private static IOnScannerEvent mUIactivity = null;
        private static IOnScannerEventRunnable mEventRunnable;
        private static Handler mScanHandler;
        private static BarcodeScanner mBarcodeScanner;

        private const int I_ON_DATA = 0;
        private const int I_ON_STATUS = 1;

        public static BarcodeScanner GetInstance(Context context)
        {
            if (mBarcodeScanner == null)
                mBarcodeScanner = new BarcodeScanner(context);

            return mBarcodeScanner;
        }

        private BarcodeScanner(Context context)
        {
            EMDKResults results = EMDKManager.GetEMDKManager(context, this);

            if (results.StatusCode != EMDKResults.STATUS_CODE.Success)
            {
                Console.WriteLine("EMDKManager Request Failed");
            }

            mScanHandler = new Handler(Looper.MainLooper);
            mEventRunnable = new IOnScannerEventRunnable();
        }

        public static void ReleaseEMDK()
        {
            if (emdkManager != null)
            {
                emdkManager.Release();
                emdkManager = null;
            }

            mBarcodeScanner = null;
        }

        public static void RegisterUIObject(IOnScannerEvent UIactivity)
        {
            mUIactivity = UIactivity;

            if (mUIactivity.ToString().Contains("MainActivity") && emdkManager != null)
                EnableScanner();
        }

        public static void UnregisterUIObject()
        {
            mUIactivity = null;
        }

        public void OnOpened(EMDKManager emdkManager)
        {
            BarcodeScanner.emdkManager = emdkManager;

            InitScanner();

            if (mUIactivity != null && mUIactivity.ToString().Contains("MainActivity"))
                EnableScanner();
        }

        public void OnClosed()
        {
            if (emdkManager != null)
            {
                emdkManager.Release();
                emdkManager = null;
            }

            mBarcodeScanner = null;
        }

        private void InitScanner()
        {
            try
            {
                //Get the feature object such as BarcodeManager object for accessing the feature.
                barcodeManager = (BarcodeManager)emdkManager.GetInstance(EMDKManager.FEATURE_TYPE.Barcode);
                scanner = barcodeManager.GetDevice(BarcodeManager.DeviceIdentifier.Default);


                //Attach the Data Event handler to get the data callbacks.
                scanner.Data += OnData;

                scanner.TriggerType = Scanner.TriggerTypes.Hard;
                scanner.Enable();

                //EMDK: Configure the scanner settings
                SetScannerParameters();
            }
            catch (ScannerException e)
            {
                Console.WriteLine(e.StackTrace);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }

        }
        public void SetScannerParameters()
        {
            try
            {
                ScannerConfig config = scanner.GetConfig();
                config.SkipOnUnsupported = ScannerConfig.SkipOnUnSupported.None;
                config.ScanParams.DecodeLEDFeedback = true;
                config.ReaderParams.ReaderSpecific.ImagerSpecific.PicklistEx = ScannerConfig.PicklistEx.Software;
                config.DecoderParams.Code39.Enabled = true;
                config.DecoderParams.Code128.Enabled = true;
                config.DecoderParams.QrCode.Enabled = true;
                scanner.SetConfig(config);
            }
            catch (ScannerException ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        public static void DeinitScanner()
        {
            if (scanner != null)
            {
                try
                {
                    if (scanner.IsReadPending)
                        scanner.CancelRead();

                    scanner.Disable();
                    scanner.Data -= OnData;
                }
                catch (ScannerException e)
                {
                    Console.WriteLine(e.Message);
                }

                scanner = null;
            }

            if (barcodeManager != null)
                barcodeManager = null;

        }

        public static void EnableScanner()
        {
            if (scanner != null)
            {
                if (!scanner.IsEnabled)
                    scanner.Enable();

                if (!scanner.IsReadPending)
                    scanner.Read();
            }
            
        }
        public static void DisableScanner()
        {
            if (scanner != null)
            {
                if (scanner.IsEnabled)
                    scanner.Disable();
               
                if (scanner.IsReadPending)
                    scanner.CancelRead();
            }
        }

        public static void OnData(object sender, Scanner.DataEventArgs e)
        {
            DisableScanner();
            ScanDataCollection scanDataCollection = e.P0;

            if (scanDataCollection != null && scanDataCollection.Result == ScannerResults.Success)
            {
                IList<ScanDataCollection.ScanData> scanData = scanDataCollection.GetScanData();
                if (scanData != null && scanData.Count > 0)
                {
                    ScanDataCollection.ScanData scannedData = scanData[0];
                    CallIOnScannerEvent(I_ON_DATA, scannedData.Data, null);
                }
            }
        }

        private static void CallIOnScannerEvent(int interfaceId, string data, string status)
        {
            if (mUIactivity != null)
            {
                mEventRunnable.SetDetails(interfaceId, data, status);
                mScanHandler.Post(mEventRunnable);
            }
        }

        public class IOnScannerEventRunnable : Java.Lang.Object, Java.Lang.IRunnable
        {
            private int mInterfaceId = 0;
            internal string mBarcodeData = "";
            internal string mBarcodeStatus = "";

            public virtual void SetDetails(int id, string scannedData, string statusStr)
            {
                mInterfaceId = id;
                mBarcodeData = scannedData;
                mBarcodeStatus = statusStr;
            }

            public void Run()
            {
                if (mUIactivity != null)
                {
                    switch (mInterfaceId)
                    {
                        case I_ON_DATA:
                            mUIactivity.OnDataScanned(mBarcodeData);
                            break;
                        case I_ON_STATUS:
                            mUIactivity.OnStatusUpdate(mBarcodeStatus); 
                            break;
                    }
                }
            }
        }

        // This would show the status of the scanner but it is not currently being used.
        // The different variables such as mBarcodeStatus that pertain to the SCANNER status (i.e. "Scanner is Idle.")
        // are not being used but are included for potential future implementation
        public void OnStatus(StatusData statusData)
        {
            string statusStr = "";
            StatusData.ScannerStates state = statusData.State;

            if (state == StatusData.ScannerStates.Idle)
            {
                statusStr = "Scanner is idle and ready to submit read.";
                try
                {
                    if (scanner.IsEnabled && !scanner.IsReadPending)
                    {
                        scanner.Read();
                    }
                }
                catch (ScannerException e1)
                {
                    statusStr = e1.Message;
                }
            }
            if (state == StatusData.ScannerStates.Waiting)
            {
                statusStr = "Waiting for Trigger Press to scan";
            }
            if (state == StatusData.ScannerStates.Scanning)
            {
                statusStr = "Scanning in progress...";
            }
            if (state == StatusData.ScannerStates.Disabled)
            {
                statusStr = "Scanner disabled";
            }
            if (state == StatusData.ScannerStates.Error)
            {
                statusStr = "Error occurred during scanning";
            }

            CallIOnScannerEvent(I_ON_STATUS, null, statusStr);
        }
    }

}