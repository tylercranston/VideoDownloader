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
        var videos = new List<Video>();

        int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                videos.Clear();

                var page = await _browserFactory.GetPageAsync(ct);

                var pageUrl = string.Format(_config.VideoCatalog.PagesUrl, pageNum);
                await page.GoToAsync(pageUrl, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
                await PageExtensions.ScrollToEndAsync(page);

                await Task.Delay(1000);

                var videoTitles = await page.XPathAsync(_config.VideoCatalog.VideoListTitle);
                var videoAnchors = await page.XPathAsync(_config.VideoCatalog.VideoListLink);

                if (videoTitles.Length != videoAnchors.Length)
                {
                    throw new Exception(String.Format("Titles and Links on page must be equal (Page:{0}, Titles: {1}, Links: {2})", pageNum, videoTitles.Length, videoAnchors.Length));
                }

                if (_config.VideoCatalog.VideosPerPage != 0 && videoTitles.Length != _config.VideoCatalog.VideosPerPage && pageNum != _config.VideoCatalog.EndPage)
                {
                    throw new Exception(String.Format("Number of videos on page not found (Page:{0}, Expected: {1}, Found: {2})", pageNum, _config.VideoCatalog.VideosPerPage, videoTitles.Length));
                }

                for (int i = 0; i < videoAnchors.Length; i++)
                {
                    var title = await (await videoTitles[i].GetPropertyAsync("innerText")).JsonValueAsync<string>();
                    var href = await (await videoAnchors[i].GetPropertyAsync("href")).JsonValueAsync<string>();

                    if (!string.IsNullOrEmpty(_config.VideoCatalog.AllowedHrefPrefix) && !href.StartsWith(_config.VideoCatalog.AllowedHrefPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    videos.Add(new Video(title.Trim(), href, pageNum));
                }
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    _log.LogWarning(ex, "Operation failed on page {Page} after {Attempts} attempts", pageNum, maxRetries);
                    throw;
                }
                else
                {
                    _log.LogWarning(ex, "Attempt {Attempt} failed on page {Page}, retrying...", attempt, pageNum);
                    var page = await _browserFactory.GetPageAsync(ct);
                    if (!page.IsClosed)
                    {
                        await page.CloseAsync();
                    }
                    await Task.Delay(1000);
                }
            }
        }
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