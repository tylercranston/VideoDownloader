using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VideoDownloader;

namespace VideoDownloader;

public interface IStashService
{
    /// <summary>
    /// Creates a Scene in StashApp for the given Video. Returns true on success.
    /// Reads configuration from appsettings under "Stash": { "Endpoint": "http://host:port/graphql", "ApiKey": "...", "HeaderName": "ApiKey" }
    /// </summary>
    Task<Video> CreateSceneAsync(Video video, CancellationToken ct, string? details = null, DateOnly? date = null, IEnumerable<string>? extraUrls = null);
}


public sealed class StashService : IStashService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    private readonly string _endpoint;
    private readonly string? _apiKey;
    private readonly HttpClient _http;
    private readonly RootConfig _config;
    private readonly ILogger<StashService> _log;

    public StashService(HttpClient http, IOptions<RootConfig> config, ILogger<StashService> log)
    {

        _endpoint = config.Value.Stash.StashUrl;
        _apiKey = config.Value.Stash.StashApiKey;
        _http = http;
        _config = config.Value;
        _log = log;
    }


    public async Task<Video> CreateSceneAsync(Video video, CancellationToken ct, string? details = null, DateOnly? date = null, IEnumerable<string>? extraUrls = null)
    {
        _log.LogInformation(string.Format($"{video.Id}: Creating '{video.Title}' Scene in Stash ..."));

        // Scan all files
        const string scanMutation = @"
mutation MetadataScan($input: ScanMetadataInput!) {
  metadataScan(input: $input)
}";
        var scanVars = new
        {
            input = new
            {
                scanGenerateCovers = false,
                scanGeneratePreviews = false,
                scanGenerateImagePreviews = false,
                scanGenerateSprites = false,
                scanGeneratePhashes = true,
                scanGenerateThumbnails = false,
                scanGenerateClipPreviews = false
            }
        };
        await GraphQLAsync(scanMutation, scanVars, ct);

        // Find scene
        bool sceneFound = false;
        string sceneId = null;
        do
        {
            const string queryScene = @"
query FindScenes($filter: FindFilterType!) {
  findScenes(filter: $filter) {
    scenes {
      id
      title
      files {
        path
      }
    }
  }
}";
            var filePath = (Path.Combine(_config.Config.StashPath, Path.GetFileName(video.DownloadedFile))).Replace('\\', '/');
            var queryVars = new
            {
                filter = new
                {
                    q = filePath
                }
            };
            var querySceneResponse = await GraphQLAsync(queryScene, queryVars, ct);
            try
            {
                var scenesArray = querySceneResponse.RootElement.GetProperty("data").GetProperty("findScenes").GetProperty("scenes");
                sceneId = scenesArray.EnumerateArray().Where(p => p.GetProperty("files")[0].GetProperty("path").GetString().ToUpper() == filePath.ToUpper()).Select(p => p.GetProperty("id").GetString()).FirstOrDefault();
            } catch
            {
                sceneId = null;
            }
            if (sceneId != null)
            {
                sceneFound = true;
            }
        } while (sceneFound == false);

        // Find Tags
        var tagIds = new List<string>();
        foreach (var tag in video.Tags)
        {
            const string queryTagFind = @"
query FindTags($filter: FindFilterType!) {
  findTags(filter: $filter) {
    count
    tags {
      id
      name
    }
  }
}";
            var queryVars = new
            {
                filter = new
                {
                    q = tag
                }
            };
            var queryTagFindResponse = await GraphQLAsync(queryTagFind, queryVars, ct);
            string tagId = null;
            try
            {
                var tagsArray = queryTagFindResponse.RootElement.GetProperty("data").GetProperty("findTags").GetProperty("tags");
                tagId = tagsArray.EnumerateArray().Where(p => p.GetProperty("name").GetString().ToUpper() == tag.ToUpper()).Select(p => p.GetProperty("id").GetString()).FirstOrDefault();
            } catch
            {
                tagId = null;
            }

            // Create tag
            if (tagId == null)
            {
                const string queryTagCreate = @"
mutation TagCreate($input: TagCreateInput!) {
  tagCreate(input: $input) {
    id
  }
}";
                var queryTagCreateVars = new
                {
                    input = new
                    {
                        name = tag,
                    }
                };

                var queryTagCreateResponse = await GraphQLAsync(queryTagCreate, queryTagCreateVars, ct);
                tagIds.Add(queryTagCreateResponse.RootElement.GetProperty("data").GetProperty("tagCreate").GetProperty("id").ToString());
            } else
            {
                tagIds.Add(tagId);
            }
        }

        // Find performers
        var performerIds = new List<string>();
        foreach (var performer in video.Performers)
        {
            const string queryPerformerFind = @"
query FindPerformers($filter: FindFilterType!) {
  findPerformers(filter: $filter) {
    count
    performers {
      id
      name
    }
  }
}";
            var queryPerformerFindVars = new
            {
                filter = new
                {
                    q = performer.Name
                }
            };
            var queryPerformerFindResponse = await GraphQLAsync(queryPerformerFind, queryPerformerFindVars, ct);
            string? performerId = null;
            try
            {
                var performersArray = queryPerformerFindResponse.RootElement.GetProperty("data").GetProperty("findPerformers").GetProperty("performers");
                performerId = performersArray.EnumerateArray().Where(p => p.GetProperty("name").GetString().ToUpper() == performer.Name.ToUpper()).Select(p => p.GetProperty("id").GetString()).FirstOrDefault();
            }
            catch
            {
                performerId = null;
            }

            var performerUrl = performer.Url.Replace(_config.Stash.PerformerUrlSearch, _config.Stash.PerformerUrlReplace);
            var performerImage = performer.CoverImage.Replace(_config.Stash.PerformerCoverImageSearch, _config.Stash.PerformerCoverImageReplace);

            // Create Performer
            if (performerId == null)
            {
                const string queryPerformerCreate = @"
mutation PerformerCreate($input: PerformerCreateInput!) {
  performerCreate(input: $input) {
    id
  }
}";
                var queryPerformerCreateVars = new
                {
                    input = new
                    {
                        name = performer.Name,
                        url = performerUrl,
                        image = performerImage
                    }
                };

                var queryPerformerCreateResponse = await GraphQLAsync(queryPerformerCreate, queryPerformerCreateVars, ct);
                performerIds.Add(queryPerformerCreateResponse.RootElement.GetProperty("data").GetProperty("performerCreate").GetProperty("id").ToString());
            }
            // Update Performer
            else
            {
                const string queryPerformerUpdate = @"
mutation PerformerUpdate($input: PerformerUpdateInput!) {
  performerUpdate(input: $input) {
    id
  }
}";
                var queryPerformerUpdateVars = new
                {
                    input = new
                    {
                        id = performerId,
                        name = performer.Name,
                        url = performerUrl,
                        image = performerImage
                    }
                };
                var queryPerformerUpdateResponse = await GraphQLAsync(queryPerformerUpdate, queryPerformerUpdateVars, ct);
                performerIds.Add(performerId);
            }
        }

        // Find studio
        string? studioId = null;
        if (!string.IsNullOrEmpty(video.Studio))
        {
            const string queryStudioFind = @"
query FindStudios($filter: FindFilterType!) {
  findStudios(filter: $filter) {
    count
    studios {
      id
      name
    }
  }
}";
            var queryStudioFindVars = new
            {
                filter = new
                {
                    q = video.Studio
                }
            };
            var queryStudioFindResponse = await GraphQLAsync(queryStudioFind, queryStudioFindVars, ct);
            
            try
            {
                var studiosArray = queryStudioFindResponse.RootElement.GetProperty("data").GetProperty("findStudios").GetProperty("studios");
                studioId = studiosArray.EnumerateArray().Where(p => p.GetProperty("name").GetString().ToUpper() == video.Studio.ToUpper()).Select(p => p.GetProperty("id").GetString()).FirstOrDefault();
            }
            catch
            {
                studioId = null;
            }
            
            // Create studio
            if (studioId == null)
            {
                const string queryStudioCreate = @"
mutation StudioCreate($input:StudioCreateInput!) {
  studioCreate(input: $input) {
    id
  }
}";
                var queryStudioCreateVars = new
                {
                    input = new
                    {
                        name = video.Studio
                    }
                };

                var queryPerformerCreateResponse = await GraphQLAsync(queryStudioCreate, queryStudioCreateVars, ct);
                studioId = queryPerformerCreateResponse.RootElement.GetProperty("data").GetProperty("studioCreate").GetProperty("id").ToString();
            }
        } else
        {
            studioId = _config.Stash.StashStudioId;
        }

        // Update scene
        const string updateMutation = @"
mutation SceneUpdate($input: SceneUpdateInput!) {
  sceneUpdate(input: $input) {
    id
  }
}";
        var updateVars = new
        {
            input = new
            {
                id = sceneId,
                title = video.Title,
                details = video.Details,
                url = video.Url.Replace(_config.Stash.SceneUrlSearch, _config.Stash.SceneUrlReplace),
                date = video.Date.Value.ToString("yyyy-MM-dd"),
                tag_ids = tagIds,
                performer_ids = performerIds,
                studio_id = studioId,
                cover_image = video.CoverImage

            }
        };

        await GraphQLAsync(updateMutation, updateVars, ct);

        video.StashComplete = true;

        _log.LogInformation($"{video.Id}: Updated scene in Stash for '{video.Title}'");

        return video;
    }

    private async Task<JsonDocument?> GraphQLAsync(string query, object? variables, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        if (!string.IsNullOrWhiteSpace(_apiKey))
            req.Headers.TryAddWithoutValidation("ApiKey", _apiKey);

        var payload = JsonSerializer.Serialize(new { query, variables }, _json);
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception(String.Format("GraphQL HTTP {Status}: {Body}", (int)resp.StatusCode, text));
        }

        if (text.Contains("\"errors\"", StringComparison.OrdinalIgnoreCase))
        {
           throw new Exception(String.Format("GraphQL returned errors: {Body}", text));
        }

        try
        {
            return JsonDocument.Parse(text);
        }
        catch (Exception ex)
        {
            throw new Exception(string.Format("Failed to parse GraphQL JSON: {Body}", text));
        }
    }
}
