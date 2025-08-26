using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using VideoDownloader;
using static System.Formats.Asn1.AsnWriter;

namespace VideoDownloader;

public interface IVideoDownloader
{
    Task<Video?> DownloadVideoAsync(IPage workingPage, Video video, RootConfig cfg, CancellationToken ct);
}

public sealed class VideoDownloader : IVideoDownloader
{
    private readonly VideoScrapeSection _v;
    private readonly IBrowserFactory _browserFactory;
    private readonly ILogger<VideoDownloader> _log;


    public VideoDownloader(IOptions<RootConfig> cfg, IBrowserFactory browserFactory, ILogger<VideoDownloader> log)
    {
        _browserFactory = browserFactory;
        _v = cfg.Value.VideoScrape;
        _log = log;
    }

    public async Task<Video?> DownloadVideoAsync(IPage browserPage, Video video, RootConfig cfg, CancellationToken ct)
    {
        await browserPage.GoToAsync(video.Url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });

        await browserPage.WaitForXPathAsync(_v.SceneDownloadButtonSelector);
        var downloadButton = await browserPage.XPathAsync(_v.SceneDownloadButtonSelector);
        await downloadButton[0].ClickAsync();

        var downloadLinks = await browserPage.XPathAsync(_v.SceneDownloadLinkSelector);

        string chosenLink = null;

        foreach (var quality in cfg.VideoScrape.PreferredQualities)
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
                await browserPage.GoToAsync(chosenLink);
            }
            catch (NavigationException ex) when (ex.Message.Contains("net::ERR_ABORTED"))
            {
                _log.LogInformation($"{video.Id}: Downloading '{video.Title}'");
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

        string downloadedFile = await WaitForNewFileAsync(cfg.Config.DownloadPath, ct);
        string targetDir = cfg.Config.MovePath;

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