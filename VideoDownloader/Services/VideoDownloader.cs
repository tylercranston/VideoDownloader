using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using System.IO;
using VideoDownloader;
using static System.Formats.Asn1.AsnWriter;

namespace VideoDownloader;

public interface IVideoDownloader
{
    Task<Video?> DownloadVideoAsync( Video video, CancellationToken ct);
}

public sealed class VideoDownloader : IVideoDownloader
{
    private readonly IBrowserFactory _browserFactory;
    private readonly RootConfig _config;
    private readonly ILogger<VideoDownloader> _log;

    public VideoDownloader(IBrowserFactory browserFactory, IOptions<RootConfig> config, ILogger<VideoDownloader> log)
    {
        _browserFactory = browserFactory;
        _config = config.Value;
        _log = log;
    }

    public async Task<Video?> DownloadVideoAsync(Video video, CancellationToken ct)
    {
        int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var page = await _browserFactory.GetPageAsync(ct);

                _log.LogInformation($"{video.Id}: Downloading '{video.Title}'");

                await page.GoToAsync(video.Url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });

                if (_config.VideoDownloader.DownloadPopup)
                {
                    await page.WaitForXPathAsync(_config.VideoDownloader.SceneDownloadPopupSelector);

                    ct.ThrowIfCancellationRequested();

                    var downloadButton = await page.XPathAsync(_config.VideoDownloader.SceneDownloadPopupSelector);
                    await downloadButton[0].ClickAsync();
                }

                string chosenLink = null;

                await page.WaitForXPathAsync(_config.VideoDownloader.SceneDownloadLinkSelector);
                ct.ThrowIfCancellationRequested();

                if (_config.VideoDownloader.DownloadType == DownloadType.MultiLink)
                {
                    var downloadLinks = await page.XPathAsync(_config.VideoDownloader.SceneDownloadLinkSelector);

                    if (downloadLinks.Length > 0)
                    {
                        string query = null;
                        if (_config.VideoDownloader.PreferredQualityType == PreferredQualityType.Url)
                        {
                            query = "el => el.href";
                        }
                        else if (_config.VideoDownloader.PreferredQualityType == PreferredQualityType.Title)
                        {
                            query = "el => (el.textContent ?? '').trim()";
                        }

                        foreach (var quality in _config.VideoDownloader.PreferredQualities)
                        {
                            foreach (var downloadLink in downloadLinks)
                            {
                                var linkTitle = await downloadLink.EvaluateFunctionAsync<string>(query);

                                // Log the link title
                                _log.LogDebug("Evaluating link with title: {Title}", linkTitle);

                                if ((await downloadLink.EvaluateFunctionAsync<string>(query)).Contains(quality))
                                {
                                    chosenLink = await downloadLink.EvaluateFunctionAsync<string>("el => el.href");
                                }
                            }

                            if (chosenLink != null)
                                break;
                        }
                        
                        new Exception(String.Format($"No download links with preferred quality found. Page={video.PageNum}, Video={video.Title}, Url={video.Url}"));
                    }
                    else
                    {
                        new Exception(String.Format($"No download links found. Page={video.PageNum}, Video={video.Title}, Url={video.Url}"));
                    }
                }
                else if (_config.VideoDownloader.DownloadType == DownloadType.SingleLink)
                {
                    var downloadLink = await page.XPathAsync(_config.VideoDownloader.SceneDownloadLinkSelector);
                    if (downloadLink.Length > 0)
                    {
                        chosenLink = await downloadLink[0].EvaluateFunctionAsync<string>("el => el.href");
                    }
                    else
                    {
                        new Exception(String.Format($"No download link found. Page={video.PageNum}, Video={video.Title}, Url={video.Url}"));
                    }
                }
                ct.ThrowIfCancellationRequested();

                try
                {
                    await page.GoToAsync(chosenLink);
                }
                catch (NavigationException ex) when (ex.Message.Contains("net::ERR_ABORTED"))
                {
                    _log.LogInformation($"{video.Id}: Starting download");
                }
                catch (Exception ex)
                {
                    new Exception(ex.Message);
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

        string downloadedFile = await WaitForNewFileAsync(_config.VideoDownloader.DownloadPath, ct);
        string targetDir = _config.VideoDownloader.MovePath;

        string fileName = Path.GetFileNameWithoutExtension(downloadedFile);
        string extension = Path.GetExtension(downloadedFile);

        string destinationPath = Path.Combine(targetDir, fileName + extension);

        Directory.CreateDirectory(targetDir);

        int counter = 1;
        while (File.Exists(destinationPath))
        {
            destinationPath = Path.Combine(targetDir, $"{fileName} ({counter}){extension}");
            counter++;
        }

        _log.LogInformation($"{video.Id}: Moving {downloadedFile} to {destinationPath}");
        
        if (_config.VideoDownloader.DeleteAfterDownload)
        {
            File.Delete(downloadedFile);
        }
        else
        {
            File.Move(downloadedFile, destinationPath, overwrite: false);
        }

        video.DownloadedFile = destinationPath;

        ct.ThrowIfCancellationRequested();

        return video;
    }

    private async Task<string> WaitForNewFileAsync(string path, CancellationToken ct)
    {
        // Ensure the folder exists
        Directory.CreateDirectory(path);

        var known = new HashSet<string>(
            Directory.EnumerateFiles(path),
            StringComparer.OrdinalIgnoreCase);

        while (!ct.IsCancellationRequested)
        {
            var currentFiles = Directory.EnumerateFiles(path)
                .Where(f => !f.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase));

            foreach (var file in currentFiles)
            {
                if (!known.Contains(file))
                {
                    return file; // Exit as soon as a new file is found
                }
            }

            await Task.Delay(1000, ct); // wait 1 second between checks
        }

        throw new OperationCanceledException("No file detected before cancellation.");
    }
}