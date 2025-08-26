namespace VideoDownloader;

public record RootConfig
{
    public string Name { get; init; }
    public ConfigSection Config { get; init; } = new();

    public VideoListSection VideoList { get; init; } = new();
    public VideoScrapeSection VideoScrape { get; init; } = new();

    public StashSection Stash { get; init; } = new();
    public List<CookieItem> Cookies { get; init; } = new();
}


public record ConfigSection
{
    public string? WsEndpoint { get; init; }
    public bool ExistingPage { get; init; }
    public string? UserAgent { get; init; }
    public string DownloadPath { get; init; }
    public string MovePath { get; init; }
    public string StashPath { get; init; }
    public bool Headless { get; init; } = true;
    public string VideoCachePath { get; init; }
    public int StartVideo { get; init; } = -1;
    public int EndVideo { get; init; } = -1;
}

public record VideoListSection
{
    public string PagesUrl {  get; init; } = string.Empty;
    public string PagesButton { get; init; } = string.Empty;
    public string VideoListTitle { get; init; } = string.Empty;
    public string VideoListLink { get; init; } = string.Empty;
    public string AllowedHrefPrefix { get; init; } = string.Empty;
    public int StartPage { get; init; }
    public int EndPage { get; init; }
    public int VideosPerPage { get; init; } = 0;
    public bool ForceRefreshCatalog { get; init; } = false;
    public bool ResumeScrape { get; init; } = false;
}

public record VideoScrapeSection
{
    public string SceneDownloadButtonSelector { get; init; } = string.Empty;
    public string SceneDownloadLinkSelector { get; init; } = string.Empty;
    public string SceneTitleSelector { get; init; } = string.Empty;
    public string SceneLinkSelector { get; init; } = string.Empty;
    public string SceneDetailsSelector {  get; init; } = string.Empty;
    public string SceneDateSelector { get; init; } = string.Empty;
    public string ScenePerformersSelector { get; init; } = string.Empty;
    public string SceneStudioSelector { get; init; } = string.Empty;
    public string SceneTagsSelector { get; init; } = string.Empty;
    public string SceneCoverImageSelector { get; init; } = string.Empty;
    public string PerformerCoverImage { get; init; } = string.Empty;
    public string QualityLinkSelector { get; init; } = string.Empty;
    public bool ScrapeComplete { get; init; } = false;
    public string[] PreferredQualities { get; init; } = new[] { "1080p", "720p", "540p", "480p", "360p", "240p", "160p" };
}

public record StashSection
{
    public string StashUrl { get; init; } = string.Empty;
    public string StashStudioId { get; init; } = string.Empty;
    public string SceneUrlSearch { get; init; } = string.Empty;
    public string SceneUrlReplace { get; init; } = string.Empty;
    public string PerformerUrlSearch { get; init; } = string.Empty;
    public string PerformerUrlReplace { get; init; } = string.Empty;
    public bool AddComplete { get; init; } = false;

}


public class CookieItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = "/";
}