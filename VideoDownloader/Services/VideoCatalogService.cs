using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System.Collections.Generic;
using VideoDownloader;

namespace VideoDownloader;

public interface IVideoCatalogService
{
    Task<List<Video>> GetAllAsync(IPage workingPage, RootConfig cfg, string cacheFile, CancellationToken ct);
}

public sealed class VideoCatalogService : IVideoCatalogService
{
    private readonly IVideoListCrawler _listCrawler;
    private readonly IVideoRepository _repo;
    private readonly ILogger<VideoCatalogService> _log;

    public VideoCatalogService(IVideoListCrawler listCrawler, IVideoRepository repo, ILogger<VideoCatalogService> log)
    {
        _listCrawler = listCrawler;
        _repo = repo;
        _log = log;
    }

    public async Task<List<Video>> GetAllAsync(IPage browserPage, RootConfig cfg, string cacheFile, CancellationToken ct)
    {
        var all = new List<Video>();
        
        if (!cfg.VideoList.ForceRefreshCatalog)
        {
            all = await _repo.LoadAsync(cacheFile, ct);
            if (all.Count > 0)
            {
                _log.LogInformation("Using cached catalog ({Count} videos)", all.Count);
                if (!cfg.VideoList.ResumeScrape)
                {
                    return all;
                }
            }
        }

        _log.LogInformation("Building catalog by crawling pages {Start}..{End}", cfg.VideoList.StartPage, cfg.VideoList.EndPage);
        
        for (int i = cfg.VideoList.StartPage; i <= cfg.VideoList.EndPage && !ct.IsCancellationRequested; i++)
        {
            all.AddRange(await _listCrawler.CrawlVideoLinksOnPageAsync(browserPage, i, ct));

            await _repo.SaveAsync(cacheFile, all, ct);

            await Task.Delay(1000);
        }

        await _repo.SaveAsync(cacheFile, all, ct);
        return all;
    }
}