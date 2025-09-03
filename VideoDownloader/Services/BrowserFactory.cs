using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using VideoDownloader;
using static System.Net.Mime.MediaTypeNames;

namespace VideoDownloader;

public interface IBrowserFactory : IAsyncDisposable
{
    Task<IBrowser> GetBrowserAsync(CancellationToken ct);
    Task<IPage> GetPageAsync(CancellationToken ct);
}


public sealed class BrowserFactory : IBrowserFactory
{
    private IBrowser? _browser;
    private IPage? _page;
    private readonly RootConfig _config;
    private readonly ILogger<BrowserFactory> _log;

    public BrowserFactory(IOptions<RootConfig> config, ILogger<BrowserFactory> log)
    {
        _config = config.Value;
        _log = log;
    }


    public async Task<IBrowser> GetBrowserAsync(CancellationToken ct)
    {
        if (_browser != null) return _browser;

        _log.LogInformation("Launching Chromium browser");

        BrowserFetcher browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = _config.Browser.Headless,
            DefaultViewport = null,
            Args = _config.Browser.Args
        });

        return _browser;
    }

    public async Task<IPage> GetPageAsync(CancellationToken ct)
    {
        // If we already created it and it’s still open, reuse it
        if (_page is { IsClosed: false })
        {
            return _page;
        }

        var browser = await GetBrowserAsync(ct);

        // Create page or use existing
        if (_config.Browser.ExistingPage)
        {
            var pages = await browser.PagesAsync();
            _page = pages.Length > 0 ? pages[0] : await browser.NewPageAsync();
        }
        else
        {
            _page = await browser.NewPageAsync();
        }

        // Set user agent if configured
        if (!string.IsNullOrWhiteSpace(_config.Browser.UserAgent))
            await _page.SetUserAgentAsync(_config.Browser.UserAgent);

        // Optional login
        //if (_config.Login is not null)
        //{
        //    _log.LogInformation("Signing in...");
        //    await _login.SignInAsync(page, _config.Login, ct);
        //}

        return _page;
    }


    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            _log.LogInformation("Closing Chromium browser");
            try { await _browser.CloseAsync(); } catch { /* swallow on shutdown */ }
            try { await _browser.DisposeAsync(); } catch { /* swallow on shutdown */ }
            _browser = null;
            _page = null;
        }
    }
}