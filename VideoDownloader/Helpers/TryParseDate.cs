using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VideoDownloader
{
    public static partial class Helpers
    {
        public static DateOnly? TryParseDate(string raw, string dateFormat, bool removeSuffix = false)
        {
            if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(dateFormat))
            {
                return null;
            }

            if (removeSuffix)
            {
                raw = Regex.Replace(raw, @"\b(\d+)(st|nd|rd|th)\b", "$1");
            }

            if (DateTime.TryParseExact(raw, dateFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            {
                return DateOnly.FromDateTime(dt);
            }

            throw new FormatException($"Date '{raw}' does not match format '{dateFormat}'");

            //// Try a few common formats, add your site’s exact format if known
            //string[] fmts = { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "MMM d, yyyy", "d MMM yyyy" };
            //foreach (var f in fmts)
            //{
            //    if (DateTime.TryParseExact(raw, f, System.Globalization.CultureInfo.InvariantCulture,
            //                               System.Globalization.DateTimeStyles.None, out var dt))
            //        return DateOnly.FromDateTime(dt);
            //}

            //// Fallback to loose parse
            //if (DateTime.TryParse(raw, out var any))
            //    return DateOnly.FromDateTime(any);
            //return null;
        }
    }
}
