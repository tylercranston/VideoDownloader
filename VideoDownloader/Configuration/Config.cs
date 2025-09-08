using System.Text.Json.Serialization;

namespace VideoDownloader;

public record RootConfig
{
    public required string Name { get; init; }
    public ConfigSection Config { get; init; } = new();
    public BrowserSection Browser { get; init; } = new();
    public VideoCatalogSection VideoCatalog { get; init; } = new();
    public VideoDownloaderSection VideoDownloader { get; init; } = new();
    public VideoScrapeSection VideoScrape { get; init; } = new();
    public StashSection Stash { get; init; } = new();
}


public record ConfigSection
{
    public string VideoCachePath { get; init; } = string.Empty;
    public string VideoCacheFileName { get; init; } = string.Empty;
    public int StartVideo { get; init; } = -1;
    public int EndVideo { get; init; } = -1;
    public int BrowserRestartDelay { get; init; } = 5000;
    public int QuitAfter { get; init; } = 0;
}

public record BrowserSection
{
    public bool Headless { get; init; } = false;
    public string[] Args { get; init; } = Array.Empty<string>();
    public bool ExistingPage { get; init; } = true;
    public string? UserAgent { get; init; }
    public List<HeaderItem> Headers { get; init; } = new();
    public List<CookieItem> Cookies { get; init; } = new();

}

 public record VideoCatalogSection
{
    public string PagesUrl {  get; init; } = string.Empty;
    public string PagesButton { get; init; } = string.Empty;
    public string VideoListTitle { get; init; } = string.Empty;
    public string VideoListLink { get; init; } = string.Empty;
    public string VideoListDate { get; init; } = string.Empty;
    public string AllowedHrefPrefix { get; init; } = string.Empty;
    public bool ScrapeDate { get; init; } = false;
    public string DateFormat { get; init; } = string.Empty;
    public bool DateRemoveSuffix { get; init; } = false;
    public int StartPage { get; init; } = 1;
    public int EndPage { get; init; } = 99;
    public int VideosPerPage { get; init; } = 0;
    public bool ForceRefreshCatalog { get; init; } = false;
    public bool ResumeScrape { get; init; } = false;
    public bool StopAfterCatalog { get; init; } = false;
    public int WaitAfterPageLoadMs { get; init; } = 1000;
}

public record VideoDownloaderSection
{
    public string DownloadPath { get; init; } = string.Empty;
    public string MovePath { get; init; } = string.Empty;
    public bool DownloadPopup { get; init; } = false;
    public DownloadType DownloadType { get; init; } = DownloadType.SingleLink;
    public string SceneDownloadPopupSelector { get; init; } = string.Empty;
    public string SceneDownloadLinkSelector { get; init; } = string.Empty;
    public PreferredQualityType PreferredQualityType { get; init; } = PreferredQualityType.Title;
    public string[] PreferredQualities { get; init; } = Array.Empty<string>();
    public bool DeleteAfterDownload { get; init; } = false;
}

public record VideoScrapeSection
{
    public string SceneTitleSelector { get; init; } = string.Empty;
    public string SceneLinkSelector { get; init; } = string.Empty;
    public string SceneDetailsSelector {  get; init; } = string.Empty;
    public string SceneDateSelector { get; init; } = string.Empty;
    public string ScenePerformersSelector { get; init; } = string.Empty;
    public string SceneStudioSelector { get; init; } = string.Empty;
    public string SceneTagsSelector { get; init; } = string.Empty;
    public string SceneCoverImageSelector { get; init; } = string.Empty;
    public string PerformerName { get; init; } = string.Empty;
    public string PerformerCoverImage { get; init; } = string.Empty;
    public string PerformerDetails { get; init; } = string.Empty;
    public string PerformerCountry { get; init; } = string.Empty;
    public string PerformerGender { get; init; } = string.Empty;
    public string PerformerEthnicity { get; init; } = string.Empty;
    public string PerformerHairColor { get; init; } = string.Empty;
    public string PerformerEyeColor { get; init; } = string.Empty;
    public string PerformerCircumcised { get; init; } = string.Empty;
    public string PerformerHeight { get; init; } = string.Empty;
    public string PerformerWeight { get; init; } = string.Empty;
    public string PerformerDickSize { get; init; } = string.Empty;
    public string QualityLinkSelector { get; init; } = string.Empty;
    public string DateFormat { get; init; } = "yyyy-MM-dd"; // "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "MMM d, yyyy", "d MMM yyyy"
    public bool ScrapeScene { get; init; } = true;
    public bool ScrapePerformers { get; init; } = true;
    public bool ScrapeComplete { get; init; } = false;
    public int WaitAfterPageLoadMs { get; init; } = 2000;
}

public record StashSection
{
    public string StashPath { get; init; } = string.Empty;
    public string StashUrl { get; init; } = string.Empty;
    public string StashApiKey { get; init; } = string.Empty;
    public string StashStudioId { get; init; } = string.Empty;
    public string SceneUrlSearch { get; init; } = string.Empty;
    public string SceneUrlReplace { get; init; } = string.Empty;
    public string SceneCoverImageSearch { get; init; } = string.Empty;
    public string SceneCoverImageReplace { get; init; } = string.Empty;
    public string PerformerUrlSearch { get; init; } = string.Empty;
    public string PerformerUrlReplace { get; init; } = string.Empty;
    public string PerformerCoverImageSearch { get; init; } = string.Empty;
    public string PerformerCoverImageReplace { get; init; } = string.Empty;
    public bool PerformerWeightConvert { get; init; } = false;
    public bool PerformerHeightConvert { get; init; } = false;
    public bool ProcessComplete { get; init; } = false;

}

public class CookieItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = "/";
}

public class HeaderItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DownloadType
{
    SingleLink,
    MultiLink
}

public enum PreferredQualityType
{
    Title,
    Url
}
