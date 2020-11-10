namespace TSARScanner
{
    internal class Constants
    {
        internal static string zohoClientID = "1000.BG3TWSNB0L7X90799ANV8VU0OKTCWH";
        internal static string zohoClientSecret = "4e856eafb245858d3fe14b6752e0e307a02ed8e63f";
        internal static string zohoScope = "ZohoCRM.modules.ALL,ZohoCRM.settings.ALL,ZohoCRM.users.ALL,ZohoCRM.org.ALL";
        internal static string rtk = "435A524541415355525041413153554243574A423539333100000000000000000000000000000000444E5337484E4A3800005057394358525441583038550000";
        internal static string CallbackURL = "http://localhost:33333";
        internal static double MIN_PRODUCTION_TIME = 0.01;
        internal static double DONE_TO_PROD_TIME = 5;
        internal static double TIMEOUT = 600000;
        internal static string WAITING = "Waiting in Queue";
        internal static string PRODUCTION = "In Production";
        internal static string COMPLETE = "Order Completed";
        internal static double DUE_DATE_APPROACHING = 2;
        internal static double DUE_DATE_EXCEEDED = 0;
        internal static string WAIT_STAGE_ID = "3139483000002381001";
        internal static string PROD_STAGE_ID = "3139483000002381003";
        internal static string COMPLETE_STAGE_ID = "3139483000002381005";
        internal static int WAIT_INDEX = 0;
        internal static int PRODUCTION_INDEX = 1;
        internal static int COMPLETE_INDEX = 2;
        internal static string COMPLETED_TEXT_COLOR = "#7fd15f";
        internal static string BUTTON_TEXT_COLOR = "#fff5f5f5";
        internal static string TRANSITIONID_JOBSTARTED = "3139483000002403025";
        internal static string TRANSITIONID_JOBCOMPLETED = "3139483000002403019";

        internal static string EMOJI_FIRE = "\U0001F525\U0001F525\U0001F525";
        internal static string EMOJI_WARNING = "\U000023F0\U000023F0\U000023F0";
        internal static string EMOJI_ERROR = "\U000026A0";
    }
}