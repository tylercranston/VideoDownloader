using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using VideoDownloader;
using static System.Net.Mime.MediaTypeNames;

namespace VideoDownloader;

public interface IVideoListCrawler
{
    Task<List<Video>> CrawlVideoLinksOnPageAsync(int pageNum, CancellationToken ct);
}


public sealed class VideoListCrawler : IVideoListCrawler
{
    private readonly IBrowserFactory _browserFactory;
    private readonly RootConfig _config;
    private readonly ILogger<VideoListCrawler> _log;

    public VideoListCrawler(IBrowserFactory browserFactory, IOptions<RootConfig> config, ILogger<VideoListCrawler> log)
    {
        _browserFactory = browserFactory;
        _config = config.Value;
        _log = log;
    }


    public async Task<List<Video>> CrawlVideoLinksOnPageAsync(int pageNum, CancellationToken ct)
    {
        IElementHandle[] videoTitles = Array.Empty<IElementHandle>();
        IElementHandle[] videoAnchors = Array.Empty<IElementHandle>();
        IElementHandle[] videoDates = Array.Empty<IElementHandle>();

        int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var page = await _browserFactory.GetPageAsync(ct);

                var pageUrl = string.Format(_config.VideoCatalog.PagesUrl, pageNum);
                await page.GoToAsync(pageUrl, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
                await Task.Delay(_config.VideoCatalog.WaitAfterPageLoadMs);
                await PageExtensions.ScrollToEndAsync(page);

                await Task.Delay(_config.VideoCatalog.WaitAfterPageLoadMs);

                videoTitles = await page.XPathAsync(_config.VideoCatalog.VideoListTitle);
                videoAnchors = await page.XPathAsync(_config.VideoCatalog.VideoListLink);

                if (_config.VideoCatalog.ScrapeDate)
                {
                    videoDates = await page.XPathAsync(_config.VideoCatalog.VideoListDate);
                }

                if (videoTitles.Length != videoAnchors.Length || _config.VideoCatalog.ScrapeDate && videoTitles.Length != videoDates.Length)
                {
                    string errorMessage;
                    if (_config.VideoCatalog.ScrapeDate)
                    {
                        errorMessage = $"Titles and Links and Dates on page must be equal (Page:{pageNum}, Titles: {videoTitles.Length}, Links: {videoAnchors.Length}, Dates: {videoDates.Length})";
                    }
                    else
                    {
                        errorMessage = $"Titles and Links on page must be equal (Page:{pageNum}, Titles: {videoTitles.Length}, Links: {videoAnchors.Length})";
                    }
                        throw new Exception(errorMessage);
                }

                if (_config.VideoCatalog.VideosPerPage != 0 && videoTitles.Length != _config.VideoCatalog.VideosPerPage && pageNum != _config.VideoCatalog.EndPage)
                {
                    throw new Exception($"Number of videos on page not found (Page: {pageNum}, Expected: {_config.VideoCatalog.VideosPerPage}, Found: {videoTitles.Length})");
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
        ct.ThrowIfCancellationRequested();

        var videos = new List<Video>();

        for (int i = 0; i <= videoTitles.Length - 1; i++)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var title = await (await videoTitles[i].GetPropertyAsync("innerText")).JsonValueAsync<string>();
                    var href = await (await videoAnchors[i].GetPropertyAsync("href")).JsonValueAsync<string>();
                    DateOnly? date;

                    if (_config.VideoCatalog.ScrapeDate)
                    {
                        var dateText = await (await videoDates[i].GetPropertyAsync("innerText")).JsonValueAsync<string>();
                        date = Helpers.TryParseDate(dateText, _config.VideoCatalog.DateFormat, _config.VideoCatalog.DateRemoveSuffix);
                    }
                    else
                    {
                        date = null;
                    }

                    if (!string.IsNullOrEmpty(_config.VideoCatalog.AllowedHrefPrefix) && !href.StartsWith(_config.VideoCatalog.AllowedHrefPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (_config.VideoCatalog.ScrapeDate)
                    {
                        videos.Add(new Video(title.Trim(), href, date, pageNum));
                    }
                    else
                    {
                        videos.Add(new Video(title.Trim(), href, pageNum));
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
        }
        ct.ThrowIfCancellationRequested();

        return videos;
    }
}

public static class PageExtensions
{
    public static async Task ScrollToEndAsync(this IPage page, int scrollHeight = 500, int delayMs = 1000, int maxScrolls = 50)
    {
        int scrollCount = 0;
        double lastHeight = 0;
        int scrollCurrent = 0;

        while (scrollCount < maxScrolls)
        {
            // Get current scroll height
            double newHeight = await page.EvaluateExpressionAsync<double>("document.body.scrollHeight");

            if (newHeight == lastHeight)
                break; // no more content

            lastHeight = newHeight;

            while (scrollCurrent < newHeight)
            {
                scrollCurrent += scrollHeight;

                // Scroll to the bottom
                await page.EvaluateFunctionAsync("(y) => { window.scrollTo(0, y); }", scrollCurrent);

                // Wait for content to load
                await Task.Delay(delayMs);

            }
            scrollCount++;
        }
    }




}