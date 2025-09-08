using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDownloader
{
    public static partial class Helpers
    {
        public static int CentimetersToInches(string centimeters)
        {
            if (!double.TryParse(centimeters, out var cm))
                throw new ArgumentException("Input must be a valid number.", nameof(centimeters));

            double inches = cm / 2.54;

            // Round to nearest whole number
            return (int)Math.Round(inches);
        }
    }
}
