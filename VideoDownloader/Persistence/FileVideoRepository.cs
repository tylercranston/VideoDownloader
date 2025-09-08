using Microsoft.Extensions.Logging;
using System.Text.Json;
using VideoDownloader;

namespace VideoDownloader
{
    public interface IVideoRepository
    {
        Task<List<Video>> LoadAsync(string path, CancellationToken ct);
        Task SaveAsync(string path, List<Video> videos, CancellationToken ct);
    }

    public sealed class FileVideoRepository : IVideoRepository
    {
        private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        private readonly ILogger<FileVideoRepository> _log;
        public FileVideoRepository(ILogger<FileVideoRepository> log) => _log = log;

        public async Task<List<Video>> LoadAsync(string path, CancellationToken ct)
        {
            if (!File.Exists(path)) return new List<Video>();
            await using var fs = File.OpenRead(path);
            var list = await JsonSerializer.DeserializeAsync<List<Video>>(fs, _json, ct) ?? new();
            _log.LogInformation($"Loaded {list.Count} videos from {Path.GetFullPath(path)}");
            ct.ThrowIfCancellationRequested();
            return list;
        }

        public async Task SaveAsync(string path, List<Video> videos, CancellationToken ct)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, videos, _json);
            _log.LogInformation($"Saved {videos.Count} videos to {Path.GetFullPath(path)}");
            ct.ThrowIfCancellationRequested();
        }
    }
}