using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using VideoDownloader;

namespace VideoDownloader;

public interface IVideoListCrawler
{
    Task<List<Video>> CrawlVideoLinksOnPageAsync(IPage browserPage, int pageNum, CancellationToken ct);
}


public sealed class VideoListCrawler : IVideoListCrawler
{
    private readonly VideoListSection _v;
    private readonly RootConfig _c;
    private readonly IVideoRepository _repo;
    private readonly ILogger<VideoListCrawler> _log;


    public VideoListCrawler(IOptions<RootConfig> cfg, IVideoRepository repo, ILogger<VideoListCrawler> log)
    {
        _v = cfg.Value.VideoList;
        _c = cfg.Value;
        _repo = repo;
        _log = log;
    }


    public async Task<List<Video>> CrawlVideoLinksOnPageAsync(IPage browserPage, int pageNum, CancellationToken ct)
    {
        var pageUrl = string.Format(_v.PagesUrl, pageNum);
        await browserPage.GoToAsync(pageUrl, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
        await PageExtensions.ScrollToEndAsync(browserPage);

        await Task.Delay(1000);

        var videoTitles = await browserPage.XPathAsync(_v.VideoListTitle);
        var videoAnchors = await browserPage.XPathAsync(_v.VideoListLink);
        
        if (videoTitles.Length != videoAnchors.Length)
        {
            throw new Exception(String.Format("Titles and Links on page must be equal (Page:{0}, Titles: {1}, Links: {2})", pageNum, videoTitles.Length, videoAnchors.Length));
        }

        if (_v.VideosPerPage != 0 && videoTitles.Length != _v.VideosPerPage && pageNum != _v.EndPage)
        {
            throw new Exception(String.Format("Number of videos on page not found (Page:{0}, Expected: {1}, Found: {2})", pageNum, _v.VideosPerPage, videoTitles.Length));
        }
        
        var videos = new List<Video>(videoAnchors.Length);

        for (int i = 0; i < videoAnchors.Length; i++)
        {
            var title = await (await videoTitles[i].GetPropertyAsync("innerText")).JsonValueAsync<string>();
            var href = await (await videoAnchors[i].GetPropertyAsync("href")).JsonValueAsync<string>();

            if (!string.IsNullOrEmpty(_v.AllowedHrefPrefix) && !href.StartsWith(_v.AllowedHrefPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            videos.Add(new Video(title.Trim(), href, pageNum));
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