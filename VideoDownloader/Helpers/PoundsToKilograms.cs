using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDownloader
{
    public static partial class Helpers
    {
        public static int PoundsToKilograms(string pounds)
        {
            if (!double.TryParse(pounds, out var lbs))
                throw new ArgumentException("Input must be a valid number.", nameof(pounds));

            double kg = lbs * 0.45359237;

            // Round to nearest whole number
            return (int)Math.Round(kg);
        }
    }
}
