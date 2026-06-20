using System.Text.Json.Serialization;

namespace SweetPlayer.Services.Scraping;

/// <summary>
/// TMDB搜索结果项
/// </summary>
public class TmdbSearchResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("original_title")]
    public string? OriginalTitle { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("vote_average")]
    public double? VoteAverage { get; set; }

    [JsonPropertyName("media_type")]
    public string? MediaType { get; set; }

    public int? Year => !string.IsNullOrEmpty(ReleaseDate) && ReleaseDate.Length >= 4
        ? int.TryParse(ReleaseDate[..4], out var year) ? year : null
        : null;
}

/// <summary>
/// TMDB搜索响应
/// </summary>
public class TmdbSearchResponse
{
    [JsonPropertyName("results")]
    public List<TmdbSearchResult> Results { get; set; } = new();

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }
}

/// <summary>
/// TMDB影片详情
/// </summary>
public class TmdbMovieDetail
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("original_title")]
    public string? OriginalTitle { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string? BackdropPath { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("vote_average")]
    public double? VoteAverage { get; set; }

    [JsonPropertyName("genres")]
    public List<TmdbGenre> Genres { get; set; } = new();

    [JsonPropertyName("credits")]
    public TmdbCredits? Credits { get; set; }

    public int? Year => !string.IsNullOrEmpty(ReleaseDate) && ReleaseDate.Length >= 4
        ? int.TryParse(ReleaseDate[..4], out var year) ? year : null
        : null;

    public string GenresString => string.Join(", ", Genres.Select(g => g.Name));
}

/// <summary>
/// TMDB类型
/// </summary>
public class TmdbGenre
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// TMDB演职人员信息
/// </summary>
public class TmdbCredits
{
    [JsonPropertyName("cast")]
    public List<TmdbCast> Cast { get; set; } = new();

    [JsonPropertyName("crew")]
    public List<TmdbCrew> Crew { get; set; } = new();
}

/// <summary>
/// TMDB演员
/// </summary>
public class TmdbCast
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("character")]
    public string? Character { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

/// <summary>
/// TMDB剧组成员
/// </summary>
public class TmdbCrew
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("job")]
    public string Job { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;
}
