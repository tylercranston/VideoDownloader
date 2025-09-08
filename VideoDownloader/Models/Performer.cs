using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDownloader
{
    public sealed class Performer
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string? CoverImage { get; set; }
        public string? Details { get; set; }
        public string? Country { get; set; }
        public string? Gender { get; set; }
        public string? Ethnicity { get; set; }
        public string? HairColor { get; set; }
        public string? EyeColor { get; set; }
        public string? Circumcised { get; set; }
        public string? Height { get; set; }
        public string? Weight { get; set; }
        public string? DickSize { get; set; }

        public Performer() { }

        public Performer(string name, string url)
        {
            Name = name;
            Url = url;
        }
    }

    public enum PerformerGender
    {
        MALE,
        FEMALE,
        TRANSGENDER_MALE,
        TRANSGENDER_FEMALE,
        INTERSEX,
        NON_BINARY
    }

    public enum PerformerCircumcised
    {
        CUT,
        UNCUT
    }

}

