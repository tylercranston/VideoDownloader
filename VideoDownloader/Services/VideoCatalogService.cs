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
                _log.LogInformation("Using cached catalog ({Count} videos)", all.Count);
                if (!_config.VideoCatalog.ResumeScrape)
                {
                    return all;
                }
            }
        }

        _log.LogInformation("Building catalog by crawling pages {Start}..{End}", _config.VideoCatalog.StartPage, _config.VideoCatalog.EndPage);
        
        for (int i = _config.VideoCatalog.StartPage; i <= _config.VideoCatalog.EndPage && !ct.IsCancellationRequested; i++)
        {
            all.AddRange(await _listCrawler.CrawlVideoLinksOnPageAsync(i, ct));
            await _repo.SaveAsync(cacheFile, all, ct);
        }

        if (_config.VideoCatalog.StopAfterCatalog)
        {
            _log.LogInformation("Stopping after catalog as configured.");
            Environment.Exit(0);
        }
        return all;
    }
}