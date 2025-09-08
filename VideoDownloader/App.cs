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
    private List<Video> _videos = new();

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
        string cacheFile;
        if (!string.IsNullOrWhiteSpace(_config.Config.VideoCacheFileName))
        {
            cacheFile = Path.Combine(_config.Config.VideoCachePath, _config.Config.VideoCacheFileName);
        }
        else
        {
            cacheFile = Path.Combine(_config.Config.VideoCachePath, String.Format("{0}.json", _config.Name));
        }

        // Exit application if download folder is not empty
        EnsureCleanDownloadFolder(_config.VideoDownloader.DownloadPath);

        // Catalog all videos
        _videos = await _catalog.GetAllAsync(cacheFile, ct);
        _log.LogInformation($"Catalog contains {_videos.Count} videos");

        // Process videos
        await ProcessVideosAsync(cacheFile, ct);

        await _browser.DisposeAsync();
        Environment.Exit(0);
    }

    private async Task ProcessVideosAsync(string cacheFile, CancellationToken ct)
    {
        (int startVideo, int endVideo) = GetVideoRange(_videos.Count);

        int videoNum = 0;
        for (var i = startVideo; i <= endVideo && !ct.IsCancellationRequested; i++)
        {
            var index = i - 1;
            var video = _videos[index];

            if (!video.Ignore)
            {
                videoNum++;

                // Process single video
                await ProcessSingleVideoAsync(video, index, cacheFile, ct);

                if (videoNum == _config.Config.QuitAfter)
                {
                    _log.LogInformation($"QuitAfter is set to {_config.Config.QuitAfter}, exiting after {videoNum} video.");
                    break;
                }
            }
        }
    }

    private async Task ProcessSingleVideoAsync(Video video, int index, string cacheFile, CancellationToken ct)
    {
        if (!video.Ignore)
        {
            // 1) Download       
            if (string.IsNullOrWhiteSpace(video.DownloadedFile))
            {
                video = await _downloader.DownloadVideoAsync(video, ct);
                // Save state
                _videos[index] = video;
                await _repo.SaveAsync(cacheFile, _videos, ct);
            }

            // 2) Scrape
            if (!video.ScrapeComplete || _config.VideoScrape.ScrapeComplete)
            {
                video = await _scraper.EnrichAsync(video, ct);
                // Save state
                _videos[index] = video;
                await _repo.SaveAsync(cacheFile, _videos, ct);
            }

            // 3) Stash
            if (!video.StashComplete || _config.Stash.ProcessComplete)
            {
                video = await _stash.CreateSceneAsync(video, ct);
                // Save state
                _videos[index] = video;
                await _repo.SaveAsync(cacheFile, _videos, ct);
            }
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