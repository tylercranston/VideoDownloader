using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VideoDownloader
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var studio = Environment.GetEnvironmentVariable("VideoDownloaderStudio");
            
            if (string.IsNullOrWhiteSpace(studio))
            {
                throw new Exception("Environment variable 'VideoDownloaderStudio' is not set.");
            }

            var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile($"appsettings.{studio}.json", optional: false, reloadOnChange: true)
                   .AddEnvironmentVariables();
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<RootConfig>(ctx.Configuration);

                // Core services
                services.AddSingleton<IBrowserFactory, BrowserFactory>();
                //services.AddSingleton<ILoginService, LoginService>();
                services.AddSingleton<IVideoRepository, FileVideoRepository>();
                services.AddSingleton<IVideoCatalogService, VideoCatalogService>();
                services.AddSingleton<IVideoRepository, FileVideoRepository>();
                services.AddSingleton<IVideoListCrawler, VideoListCrawler>();
                services.AddSingleton<IVideoScraper, VideoScraper>();
                services.AddSingleton<IVideoDownloader, VideoDownloader>();
                services.AddHttpClient<IStashService, StashService>();
                services.AddHostedService<App>();
            })
            .ConfigureLogging(b => b.SetMinimumLevel(LogLevel.Information))
            .Build();

            await host.RunAsync();
        }
    }
}
