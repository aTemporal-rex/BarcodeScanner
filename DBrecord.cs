using System;

namespace TSARScanner
{
    public class DBrecord
    {
        public string DealNameString { get; set; }
        public string StageString { get; set; }
        public string PurchaseOrderString { get; set; }
        public double TimeOutputString { get; set; }
        public DateTime DueDate { get; set; }
        public string DueTimeString { get; set; }
        public DateTime CreationTime { get; set; }
        public double TotalInProgressSecondsWait { get; set; }
        public double TotalInProgressSecondsProduction { get; set; }
        public double TotalInProgressSecondsComplete { get; set; }
        public string HotOrder { get; set; }

    }
}