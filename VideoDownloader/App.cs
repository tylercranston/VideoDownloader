using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using System.Runtime.CompilerServices;
using VideoDownloader;
using static System.Formats.Asn1.AsnWriter;

namespace VideoDownloader;

public sealed class App : BackgroundService
{
    private readonly IBrowserFactory _browser;
    private readonly IVideoCatalogService _catalog;
    private readonly IVideoRepository _repo;
    private readonly IVideoScraper _scraper;
    private readonly IVideoDownloader _downloader;
    private readonly IStashService _stash;
    private readonly RootConfig _config;
    private readonly ILogger<App> _log;

    public App(
        IBrowserFactory browser,
        IVideoCatalogService catalog,
        IVideoRepository repo,
        IVideoScraper scraper,
        IVideoDownloader downloader,
        IStashService stash,
        IOptions<RootConfig> config,
        ILogger<App> log)
    {
        _browser = browser;
        _catalog = catalog;
        _repo = repo;
        _scraper = scraper;
        _downloader = downloader;
        _stash = stash;
        _config = config.Value;
        _log = log;
    }


    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var cacheFile = Path.Combine(_config.Config.VideoCachePath, String.Format("{0}.json", _config.Name));

        // Exit application if download folder is not empty
        EnsureCleanDownloadFolder(_config.VideoDownloader.DownloadPath);

        // Catalog all videos
        var videos = await _catalog.GetAllAsync(cacheFile, ct);
        _log.LogInformation($"Catalog contains {videos.Count} videos");

        // Process videos
        await ProcessVideosAsync(videos, cacheFile, ct);
    }

    private async Task ProcessVideosAsync(List<Video> videos, string cacheFile, CancellationToken ct)
    {
        (int startVideo, int endVideo) = GetVideoRange(videos.Count);

        for (var i = startVideo; i <= endVideo && !ct.IsCancellationRequested; i++)
        {
            var index = i - 1;

            // Process single video
            await ProcessSingleVideoAsync(videos, index, cacheFile, ct);
        }

    }

    private async Task ProcessSingleVideoAsync(List<Video> videos, int index, string cacheFile, CancellationToken ct)
    {
        var video = videos[index];

        // 1) Download         
        if (string.IsNullOrWhiteSpace(video.DownloadedFile))
        {
            video = await _downloader.DownloadVideoAsync(video, ct);
            // Save state
            videos[index] = video;
            await _repo.SaveAsync(cacheFile, videos, ct);
        }

        // 2) Scrape
        if (!video.ScrapeComplete || _config.VideoScrape.ScrapeComplete)
        {
            video = await _scraper.EnrichAsync(video, ct);
            // Save state
            videos[index] = video;
            await _repo.SaveAsync(cacheFile, videos, ct);
        }

        // 3) Stash
        if (!video.StashComplete || _config.Stash.ProcessComplete)
        {
            video = await _stash.CreateSceneAsync(video, ct);
            // Save state
            videos[index] = video;
            await _repo.SaveAsync(cacheFile, videos, ct);
        }

        return;
    }

    private void EnsureCleanDownloadFolder(string downloadPath)
    {
        if (Directory.Exists(downloadPath))
        {
            if (Directory.EnumerateFileSystemEntries(downloadPath).Any())
                throw new Exception($"Download path '{downloadPath}' is not empty. Please clear it before running.");
        }
        else
        {
            Directory.CreateDirectory(downloadPath);
        }
    }

    private (int start, int end) GetVideoRange(int total)
    {
        int start = _config.Config.StartVideo != -1 ? _config.Config.StartVideo : 1;
        int end = _config.Config.EndVideo != -1 ? _config.Config.EndVideo : total;
        return (start, end);
    }
}