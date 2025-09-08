using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using System.Collections.Generic;
using VideoDownloader;

namespace VideoDownloader;

public interface IVideoCatalogService
{
    Task<List<Video>> GetAllAsync(string cacheFile, CancellationToken ct);
}

public sealed class VideoCatalogService : IVideoCatalogService
{
    private readonly IVideoListCrawler _listCrawler;
    private readonly IVideoRepository _repo;
    private readonly RootConfig _config;
    private readonly ILogger<VideoCatalogService> _log;

    public VideoCatalogService(IVideoListCrawler listCrawler, IVideoRepository repo, IOptions<RootConfig> config, ILogger<VideoCatalogService> log)
    {
        _listCrawler = listCrawler;
        _repo = repo;
        _config = config.Value;
        _log = log;
    }

    public async Task<List<Video>> GetAllAsync(string cacheFile, CancellationToken ct)
    {
        var all = new List<Video>();
        
        if (!_config.VideoCatalog.ForceRefreshCatalog)
        {
            all = await _repo.LoadAsync(cacheFile, ct);
            if (all.Count > 0)
            {
                _log.LogInformation($"Using cached catalog ({all.Count} videos)");
            }
        }

        if (all.Count == 0 || _config.VideoCatalog.ResumeScrape || _config.VideoCatalog.ForceRefreshCatalog)
        {
            var startPage = _config.VideoCatalog.StartPage;
            var endPage = _config.VideoCatalog.EndPage;

            _log.LogInformation($"Building catalog by crawling pages {startPage}..{endPage}");

            for (int i = _config.VideoCatalog.StartPage; i <= _config.VideoCatalog.EndPage && !ct.IsCancellationRequested; i++)
            {
                all.AddRange(await _listCrawler.CrawlVideoLinksOnPageAsync(i, ct));
                await _repo.SaveAsync(cacheFile, all, ct);
            }
        }

        if (_config.VideoCatalog.StopAfterCatalog)
        {
            await _repo.SaveAsync(cacheFile, all, ct);
            _log.LogInformation("Stopping after catalog as configured.");
            Environment.Exit(0);
        }
        ct.ThrowIfCancellationRequested();

        return all;
    }
}