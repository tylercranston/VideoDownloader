using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using System;
using System.IO;
using System.Xml.Linq;

namespace VideoDownloader;

public interface IVideoScraper
{
    Task<Video> EnrichAsync(Video video, CancellationToken ct);
}

public sealed class VideoScraper : IVideoScraper
{
    private readonly IBrowserFactory _browserFactory;
    private readonly RootConfig _config;
    private readonly ILogger<VideoScraper> _log;

    public VideoScraper(IBrowserFactory browserFactory, IOptions<RootConfig> config, ILogger<VideoScraper> log)
    {
        _browserFactory = browserFactory;
        _config = config.Value;
        _log = log;
    }

    public async Task<Video> EnrichAsync(Video video, CancellationToken ct)
    {
        int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var page = await _browserFactory.GetPageAsync(ct);

                _log.LogInformation(string.Format($"{video.Id}: Scraping '{video.Title}' ..."));

                // Open video page
                await page.GoToAsync(video.Url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
                });

                await Task.Delay(_config.VideoScrape.WaitAfterPageLoadMs);

                // Update Title
                video.Title = await GetSingleTextAsync(page, _config.VideoScrape.SceneTitleSelector, true, ct);

                // Update Details
                video.Details = await GetSingleTextAsync(page, _config.VideoScrape.SceneDetailsSelector, true, ct);

                // Update Cover Image
                video.CoverImage = await GetSingleTextAsync(page, _config.VideoScrape.SceneCoverImageSelector, true, ct);

                // Update Date
                var dateText = await GetSingleTextAsync(page, _config.VideoScrape.SceneDateSelector, true, ct);
                video.Date = TryParseDate(dateText);

                // Update Tags
                video.Tags = (await GetManyTextAsync(page, _config.VideoScrape.SceneTagsSelector, false, ct)).ToList();

                // Update Studio
                video.Studio = await GetSingleTextAsync(page, _config.VideoScrape.SceneStudioSelector, false, ct);

                // Update Performers
                video.Performers.Clear();
                var performers = await page.XPathAsync(_config.VideoScrape.ScenePerformersSelector);
                foreach (var performer in performers)
                {
                    _log.LogDebug(await performer.EvaluateFunctionAsync<string>("el => el.outerHTML"));
                    var name = await (await performer.GetPropertyAsync("innerText")).JsonValueAsync<string>();
                    var href = await (await performer.GetPropertyAsync("href")).JsonValueAsync<string>();

                    video.Performers.Add(new Performer(name, href));
                }

                // Update performer details from performer page
                var performersList = video.Performers;
                foreach (var performer in performersList)
                {
                    // Open performer page
                    await page.GoToAsync(performer.Url, new NavigationOptions
                    {
                        WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
                    });

                    await Task.Delay(_config.VideoScrape.WaitAfterPageLoadMs);

                    string coverimage = await GetSingleTextAsync(page, _config.VideoScrape.PerformerCoverImage, true, ct);

                    var uri = new Uri(coverimage);

                    performer.CoverImage = uri.GetLeftPart(UriPartial.Path);
                }
                video.Performers = performersList;

                // Set scrape complete
                video.ScrapeComplete = true;

                _log.LogInformation($"{video.Id}: Scraping complete ({video.Title} | Date={video.Date} | Performers={video.Performers.Count} | Tags={video.Tags.Count})");

            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    _log.LogWarning(ex, "Operation failed after {Attempts} attempts", maxRetries);
                    throw;
                }
                else
                {
                    _log.LogWarning(ex, "Attempt {Attempt} failed, retrying...", attempt);
                    var page = await _browserFactory.GetPageAsync(ct);
                    if (!page.IsClosed)
                    {
                        await page.CloseAsync();
                    }
                    await Task.Delay(1000);
                }
            }
        }

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