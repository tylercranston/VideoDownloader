using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using System;
using System.IO;
using System.Xml.Linq;

namespace VideoDownloader;

public interface IVideoScraper
{
    Task<Video> EnrichAsync(IPage browserPage, Video video, CancellationToken ct);
}

public sealed class VideoScraper : IVideoScraper
{
    private readonly VideoScrapeSection _v;
    private readonly RootConfig _c;
    private readonly ILogger<VideoScraper> _log;

    public VideoScraper(IOptions<RootConfig> cfg, ILogger<VideoScraper> log)
    {
        _v = cfg.Value.VideoScrape;
        _c = cfg.Value;
        _log = log;
    }

    public async Task<Video> EnrichAsync(IPage browserPage, Video video, CancellationToken ct)
    {
        _log.LogInformation(string.Format($"{video.Id}: Scraping '{video.Title}' ..."));

        await browserPage.GoToAsync(video.Url, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
        });

        await Task.Delay(5000);

        // Title
        video.Title = await GetSingleTextAsync(browserPage, _v.SceneTitleSelector, true, ct);

        // Details
        video.Details = await GetSingleTextAsync(browserPage, _v.SceneDetailsSelector, true, ct);

        // Cover Image
        video.CoverImage = await GetSingleTextAsync(browserPage, _v.SceneCoverImageSelector, true, ct);

        // Date
        var dateText = await GetSingleTextAsync(browserPage, _v.SceneDateSelector, true, ct);
        video.Date = TryParseDate(dateText);

        // Tags
        video.Tags = (await GetManyTextAsync(browserPage, _v.SceneTagsSelector, false, ct)).ToList();

        // Studio
        video.Studio = await GetSingleTextAsync(browserPage, _v.SceneStudioSelector, false, ct);

        // Performers
        video.Performers.Clear();
        var performers = await browserPage.XPathAsync(_v.ScenePerformersSelector);
        foreach (var performer in performers)
        {
            _log.LogDebug(await performer.EvaluateFunctionAsync<string>("el => el.outerHTML"));
            var name = await (await performer.GetPropertyAsync("innerText")).JsonValueAsync<string>();
            var href = await (await performer.GetPropertyAsync("href")).JsonValueAsync<string>();

            video.Performers.Add(new Performer(name, href));
        }

        var performersList = video.Performers;
        foreach (var performer in performersList)
        {
            await browserPage.GoToAsync(performer.Url, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
            });

            await Task.Delay(5000);

            string coverimage = await GetSingleTextAsync(browserPage, _v.PerformerCoverImage, true, ct);

            var uri = new Uri(coverimage);

            performer.CoverImage = uri.GetLeftPart(UriPartial.Path);
        }
        video.Performers = performersList;
        
        video.ScrapeComplete = true;

        _log.LogInformation($"{video.Id}: Scraping complete ({video.Title} | Date={video.Date} | Performers={video.Performers.Count} | Tags={video.Tags.Count})");

        return video;
    }

    private static async Task<string?> GetSingleTextAsync(IPage page, string xpath, bool wait, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(xpath)) return null;
        if (wait)
        {
            var elementHandle = await page.WaitForXPathAsync(xpath);
        }
        var nodes = await page.XPathAsync(xpath);
        if (nodes.Length == 0) return null;
        return await nodes[0].EvaluateFunctionAsync<string?>("el => (el.innerText || el.textContent || '').trim()");
    }

    private static async Task<IEnumerable<string>> GetManyTextAsync(IPage page, string xpath, bool wait, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(xpath)) return Array.Empty<string>();
        if (wait)
        {
            var elementHandle = await page.WaitForXPathAsync(xpath);
        }
        var nodes = await page.XPathAsync(xpath);
        var list = new List<string>(nodes.Length);
        foreach (var n in nodes)
        {
            var text = await n.EvaluateFunctionAsync<string?>("el => (el.innerText || el.textContent || '').trim()");
            if (!string.IsNullOrWhiteSpace(text))
                list.Add(text!);
        }
        return list;
    }

    private static DateOnly? TryParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Try a few common formats, add your site’s exact format if known
        string[] fmts = { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "MMM d, yyyy", "d MMM yyyy" };
        foreach (var f in fmts)
        {
            if (DateTime.TryParseExact(raw, f, System.Globalization.CultureInfo.InvariantCulture,
                                       System.Globalization.DateTimeStyles.None, out var dt))
                return DateOnly.FromDateTime(dt);
        }

        // Fallback to loose parse
        if (DateTime.TryParse(raw, out var any))
            return DateOnly.FromDateTime(any);

        return null;
    }
}