using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
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

                await page.WaitForXPathAsync(_config.VideoScrape.SceneDownloadButtonSelector);
                var downloadButton = await page.XPathAsync(_config.VideoScrape.SceneDownloadButtonSelector);
                await downloadButton[0].ClickAsync();

                var downloadLinks = await page.XPathAsync(_config.VideoScrape.SceneDownloadLinkSelector);

                string chosenLink = null;

                foreach (var quality in _config.VideoScrape.PreferredQualities)
                {
                    foreach (var downloadLink in downloadLinks)
                    {
                        if ((await downloadLink.EvaluateFunctionAsync<string>("el => el.href")).Contains(quality))
                        {
                            chosenLink = await downloadLink.EvaluateFunctionAsync<string>("el => el.href");
                        }
                    }

                    if (chosenLink != null)
                        break;
                }

                if (chosenLink != null)
                {
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
                }
                else
                {
                    new Exception(String.Format($"No matching quality link found. Page={video.PageNum}, Video={video.Title}, Url={video.Url}"));
                }
                break;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    _log.LogWarning(ex, "Operation failed after {Attempts} attempts", maxRetries);
                    throw;
                }
                else
                {
                    _log.LogWarning(ex, "Attempt {Attempt} failed, retrying...", attempt);
                    
                    await _browserFactory.DisposeAsync();
                    await Task.Delay(1000);
                }
            }
        }

        string downloadedFile = await WaitForNewFileAsync(_config.Config.DownloadPath, ct);
        string targetDir = _config.Config.MovePath;

        string fileName = Path.GetFileNameWithoutExtension(downloadedFile);
        string extension = Path.GetExtension(downloadedFile);

        string destinationPath = Path.Combine(targetDir, fileName + extension);

        int counter = 1;
        while (File.Exists(destinationPath))
        {
            destinationPath = Path.Combine(targetDir, $"{fileName} ({counter}){extension}");
            counter++;
        }

        _log.LogInformation($"{video.Id}: Moving {downloadedFile} to {destinationPath}");
        File.Move(downloadedFile, destinationPath, overwrite: false);

        video.DownloadedFile = destinationPath;

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