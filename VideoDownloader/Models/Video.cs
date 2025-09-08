using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VideoDownloader
{
    public sealed class Video
    {
        private static int _nextId = 1;

        public int Id { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public int PageNum { get; set; }
        public string? Details { get; set; }
        public string? CoverImage { get; set; }
        public string? Studio { get; set; }
        public DateOnly? Date { get; set; }
        public List<Performer> Performers { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string? DownloadedFile { get; set; }
        public bool ScrapeComplete { get; set; } = false;
        public bool StashComplete { get; set; } = false;
        public bool Ignore { get; set; } = false;

        public Video() { }

        public Video(string title, string url, int pageNum)
        {
            Id = _nextId++;
            Title = title;
            Url = url;
            PageNum = pageNum;
        }

        public Video(string title, string url, DateOnly? date, int pageNum)
        {
            Id = _nextId++;
            Title = title;
            Url = url;
            Date = date;
            PageNum = pageNum;
        }
    }
}
