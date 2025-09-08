using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDownloader
{
    public static partial class Helpers
    {
        public static int HeightToCentimeters(string height)
        {
            if (string.IsNullOrWhiteSpace(height))
                throw new ArgumentException("Height string cannot be null or empty.", nameof(height));

            // Example input: 5'10"
            // Split on the apostrophe (feet)
            var parts = height.Split('\'');
            if (parts.Length < 2)
                throw new FormatException("Invalid height format. Expected format: 5'10\"");

            if (!int.TryParse(parts[0], out int feet))
                throw new FormatException("Feet value is invalid.");

            // Remove the double-quote (inches) and parse
            string inchesPart = parts[1].Replace("\"", "").Trim();
            if (!int.TryParse(inchesPart, out int inches))
                inches = 0; // allow cases like "6'" without inches

            // Convert: 1 foot = 30.48 cm, 1 inch = 2.54 cm
            double cm = (feet * 30.48) + (inches * 2.54);

            return (int)Math.Round(cm);
        }
    }
}
