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
    private readonly IBrowserFactory _browserFactory;
    //private readonly ILoginService _login;
    private readonly IVideoCatalogService _catalog;
    private readonly IVideoRepository _repo;
    private readonly IVideoScraper _scraper;
    private readonly IVideoListCrawler _list;
    private readonly IVideoDownloader _downloader;
    private readonly IStashService _stash;
    //private readonly ISceneDownloader _downloader;
    private readonly RootConfig _cfg;
    private readonly ILogger<App> _log;


    public App(
    IBrowserFactory browserFactory,
    //ILoginService login,
    IVideoCatalogService catalog,
    IVideoRepository repo,
    IVideoScraper scraper,
    IVideoDownloader downloader,
    IStashService stash,
    //IVideoListCrawler list,
    //ISceneDownloader downloader,
    IOptions<RootConfig> cfg,
    ILogger<App> log)
    {
        _browserFactory = browserFactory;
        //_login = login;
        _catalog = catalog;
        _repo = repo;
        _scraper = scraper;
        _downloader = downloader;
        _stash = stash;
        //_list = list;
        //_downloader = downloader;
        _cfg = cfg.Value;
        _log = log;
    }


    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Throw exception if downloads folder is not empty
        string downloadPath = _cfg.Config.DownloadPath;
        if (Directory.Exists(downloadPath))
        {
            var hasFiles = Directory.EnumerateFileSystemEntries(downloadPath).Any();
            if (hasFiles)
            {
                throw new Exception($"Download path '{downloadPath}' is not empty. Please clear it before running.");
            }
        }
        else
        {
            Directory.CreateDirectory(downloadPath);
        }

        // Create page or use existing
        var browser = await _browserFactory.GetAsync(ct);
        var page = _cfg.Config.ExistingPage ? (await browser.PagesAsync())[0] : await browser.NewPageAsync();

        // Set user agent
        if (!string.IsNullOrWhiteSpace(_cfg.Config.UserAgent))
            await page.SetUserAgentAsync(_cfg.Config.UserAgent);

        // Optional login
        //if (_cfg.Login is not null)
        //{
        //    _log.LogInformation("Signing in...");
        //    await _login.SignInAsync(page, _cfg.Login, ct);
        //}

        var cacheFile = Path.Combine(_cfg.Config.VideoCachePath, String.Format("{0}.json", _cfg.Name));

        // Catalog all videos
        var videos = await _catalog.GetAllAsync(page, _cfg, cacheFile, ct);
        _log.LogInformation("Catalog contains {Count} videos", videos.Count);

        int startVideo;
        if (_cfg.Config.StartVideo != -1)
        {
            startVideo = _cfg.Config.StartVideo;
        }
        else
        {
            startVideo = 1;
        }

        int endVideo;
        if (_cfg.Config.EndVideo != -1)
        {
            endVideo = _cfg.Config.EndVideo;
        }
        else
        {
            endVideo = videos.Count;
        }

        for (var i = startVideo; i <= endVideo && !ct.IsCancellationRequested; i++)
        {
            try
            {
                var index = i - 1;
                var video = videos[index];

                // Download file            
                if (video.DownloadedFile == null)
                {
                    videos[index] = await _downloader.DownloadVideoAsync(page, video, _cfg, ct);
                    await _repo.SaveAsync(cacheFile, videos, ct);
                    video = videos[index];
                }

                // Scrape metadata
                if (!video.ScrapeComplete || _cfg.VideoScrape.ScrapeComplete)
                {
                    videos[index] = await _scraper.EnrichAsync(page, video, ct);
                    await _repo.SaveAsync(cacheFile, videos, ct);
                    video = videos[index];
                }

                if (!video.StashComplete || _cfg.Stash.AddComplete)
                {
                    var success = await _stash.CreateSceneAsync(video, _cfg, ct);
                    if (success)
                    {
                        video.StashComplete = true;
                        videos[index] = video;
                        await _repo.SaveAsync(cacheFile, videos, ct);
                    }
                }
            } catch (Exception ex)
            {
                _log.LogError("{Message}{NewLine}{StackTrace}", ex.Message, Environment.NewLine, ex.StackTrace);
            }
        }
    }
}