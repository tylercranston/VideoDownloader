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

                if (_config.VideoScrape.ScrapeScene)
                {
                    // Open video page
                    await page.GoToAsync(video.Url, new NavigationOptions
                    {
                        WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
                    });

                    await Task.Delay(_config.VideoScrape.WaitAfterPageLoadMs);

                    ct.ThrowIfCancellationRequested();

                    // Update Title
                    video.Title = await GetSingleTextAsync(page, _config.VideoScrape.SceneTitleSelector, true, ct);

                    // Update Details
                    video.Details = await GetSingleTextAsync(page, _config.VideoScrape.SceneDetailsSelector, true, ct);

                    // Update Cover Image
                    {
                        video.CoverImage = await GetSingleTextAsync(page, _config.VideoScrape.SceneCoverImageSelector, true, ct);
                    }

                    if (!_config.VideoCatalog.ScrapeDate)
                    {
                        // Update Date
                        var dateText = await GetSingleTextAsync(page, _config.VideoScrape.SceneDateSelector, true, ct);
                        video.Date = Helpers.TryParseDate(dateText, _config.VideoScrape.DateFormat);
                    }

                    // Update Tags
                    video.Tags = (await GetManyTextAsync(page, _config.VideoScrape.SceneTagsSelector, false, ct)).ToList();

                    // Update Studio
                    video.Studio = await GetSingleTextAsync(page, _config.VideoScrape.SceneStudioSelector, false, ct);

                    // Update Performers
                    video.Performers = new List<Performer>();
                    var performers = await page.XPathAsync(_config.VideoScrape.ScenePerformersSelector);
                    foreach (var performer in performers)
                    {
                        _log.LogDebug(await performer.EvaluateFunctionAsync<string>("el => el.outerHTML"));
                        var name = await (await performer.GetPropertyAsync("innerText")).JsonValueAsync<string>();
                        var href = await (await performer.GetPropertyAsync("href")).JsonValueAsync<string>();

                        video.Performers.Add(new Performer(name, href));
                    }

                    ct.ThrowIfCancellationRequested();

                }

                if (_config.VideoScrape.ScrapePerformers)
                {

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

                        ct.ThrowIfCancellationRequested();

                        // Update Name
                        if (!string.IsNullOrWhiteSpace(_config.VideoScrape.PerformerName))
                            performer.Name = await GetSingleTextAsync(page, _config.VideoScrape.PerformerName, false, ct);

                        // Update Cover Image
                        performer.CoverImage = await GetSingleTextAsync(page, _config.VideoScrape.PerformerCoverImage, false, ct);

                        // Update Details
                        performer.Details = await GetSingleTextAsync(page, _config.VideoScrape.PerformerDetails, false, ct);

                        // Update Details
                        performer.Country = await GetSingleTextAsync(page, _config.VideoScrape.PerformerCountry, false, ct);

                        // Update Gender
                        performer.Gender = await GetSingleTextAsync(page, _config.VideoScrape.PerformerGender, false, ct);

                        // Update Ethnicity
                        performer.Ethnicity = await GetSingleTextAsync(page, _config.VideoScrape.PerformerEthnicity, false, ct);

                        // Update Hair Color
                        performer.HairColor = await GetSingleTextAsync(page, _config.VideoScrape.PerformerHairColor, false, ct);

                        // Update Eye Color
                        performer.EyeColor = await GetSingleTextAsync(page, _config.VideoScrape.PerformerEyeColor, false, ct);

                        // Update Circumcised
                        performer.Circumcised = await GetSingleTextAsync(page, _config.VideoScrape.PerformerCircumcised, false, ct);

                        // Update Height
                        performer.Height = await GetSingleTextAsync(page, _config.VideoScrape.PerformerHeight, false, ct);

                        // Update Weight
                        performer.Weight = await GetSingleTextAsync(page, _config.VideoScrape.PerformerWeight, false, ct);

                        // Update Dick Size
                        performer.DickSize = await GetSingleTextAsync(page, _config.VideoScrape.PerformerDickSize, false, ct);

                        ct.ThrowIfCancellationRequested();
                    }
                    video.Performers = performersList;
                }

                break;
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw;
                }
                else if (attempt == maxRetries)
                {
                    _log.LogWarning(ex, $"Operation failed after {attempt} attempts");
                    throw;
                }
                else
                {
                    _log.LogWarning(ex, $"Attempt {attempt} failed, retrying...");

                    await _browserFactory.DisposeAsync();
                    await Task.Delay(_config.Config.BrowserRestartDelay, ct);
                }
            }
        }

        // Set scrape complete
        video.ScrapeComplete = true;

        _log.LogInformation($"{video.Id}: Scraping complete ({video.Title} | Date={video.Date} | Performers={video.Performers.Count} | Tags={video.Tags.Count})");

        ct.ThrowIfCancellationRequested();

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
        var texts = await Task.WhenAll(
            nodes.Select(n =>
                n.EvaluateFunctionAsync<string>("el => (el.innerText || el.textContent || '').trim()"))
        );
        return string.Join(Environment.NewLine, texts.Where(t => !string.IsNullOrWhiteSpace(t)));
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
}