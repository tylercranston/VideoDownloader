using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using VideoDownloader;

namespace VideoDownloader;

public interface IBrowserFactory : IAsyncDisposable
{
    Task<IBrowser> GetAsync(CancellationToken ct);
}


public sealed class BrowserFactory : IBrowserFactory
{
    private readonly RootConfig _cfg;
    private readonly ILogger<BrowserFactory> _log;
    private IBrowser? _browser;


    public BrowserFactory(IOptions<RootConfig> cfg, ILogger<BrowserFactory> log)
    {
        _cfg = cfg.Value;
        _log = log;
    }


    public async Task<IBrowser> GetAsync(CancellationToken ct)
    {
        if (_browser != null) return _browser;


        if (!string.IsNullOrWhiteSpace(_cfg.Config.WsEndpoint))
        {
            _log.LogInformation("Connecting to existing browser at {Endpoint}", _cfg.Config.WsEndpoint);
            _browser = await Puppeteer.ConnectAsync(new ConnectOptions
            {
                BrowserWSEndpoint = _cfg.Config.WsEndpoint,
                DefaultViewport = null
            });
        }
        else
        {
            _log.LogInformation("Launching Chromium (Headless={Headless})", _cfg.Config.Headless);
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = _cfg.Config.Headless,
                DefaultViewport = null,
                Args = new[] { "--disable-dev-shm-usage", "--no-sandbox" }
            });
        }
        return _browser;
    }


    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            try { await _browser.CloseAsync(); }
            catch { /* swallow on shutdown */ }
        }
    }
}