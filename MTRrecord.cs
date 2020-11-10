using System;

namespace TSARScanner
{
    public class MTRrecord : Java.Lang.Object
    {
        public String Id { get; set; }
        public String Name { get; set; }
        public String Thickness { get; set; }
        public String Size { get; set; }
        public String Grade { get; set; }
        public int Count { get; }
        public String LinkedId { get; set; }
    }
}