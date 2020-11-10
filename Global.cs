using System;
using System.Collections.Generic;

// These are the global values that need to be accessed by all files
namespace TSARScanner
{
    class Global
    {
        public static String ScannedId { get; set; }
        public static List<MTRrecord> LinkedMTRs { get; set; }
    }
}