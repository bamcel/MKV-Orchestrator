using System.Net.Http;
using System.Text;
using System.Text.Json;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class AniListService
{
    private const string Endpoint = "https://graphql.anilist.co";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<IReadOnlyList<TvdbSeriesSearchResult>> SearchSeriesAsync(string query, string preferredLanguage, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<TvdbSeriesSearchResult>();

        const string graphQl = @"
query ($search: String) {
  Page(page: 1, perPage: 25) {
    media(search: $search, type: ANIME, sort: SEARCH_MATCH) {
      id
      title { romaji english native }
      description(asHtml: false)
      seasonYear
      format
      episodes
    }
  }
}";

        var payload = JsonSerializer.Serialize(new
        {
            query = graphQl,
            variables = new { search = query }
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync(Endpoint, content, token);
        var json = await response.Content.ReadAsStringAsync(token);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("Page", out var page)
            || !page.TryGetProperty("media", out var media)
            || media.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<TvdbSeriesSearchResult>();
        }

        var results = new List<TvdbSeriesSearchResult>();
        foreach (var item in media.EnumerateArray())
        {
            var id = ReadInt(item, "id");
            if (id <= 0) continue;

            var title = item.TryGetProperty("title", out var titleElement)
                ? PickTitle(titleElement, preferredLanguage)
                : string.Empty;
            if (string.IsNullOrWhiteSpace(title)) continue;

            var year = ReadInt(item, "seasonYear");
            var format = ReadString(item, "format");
            var episodeCount = ReadInt(item, "episodes");
            var overview = ReadString(item, "description");
            if (!string.IsNullOrWhiteSpace(format) || episodeCount > 0)
            {
                var suffix = $"{format}".Trim();
                if (episodeCount > 0) suffix = string.IsNullOrWhiteSpace(suffix) ? $"{episodeCount} episodes" : $"{suffix} • {episodeCount} episodes";
                overview = string.IsNullOrWhiteSpace(overview) ? suffix : $"{suffix}\n{overview}";
            }

            results.Add(new TvdbSeriesSearchResult
            {
                Id = id,
                Name = title,
                Year = year > 0 ? year.ToString() : string.Empty,
                Overview = StripHtml(overview),
                Provider = "AniList",
                Format = format
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<TvdbEpisode>> GetEpisodesAsync(int mediaId, string preferredLanguage, CancellationToken token)
    {
        const string graphQl = @"
query ($id: Int) {
  Media(id: $id, type: ANIME) {
    id
    title { romaji english native }
    seasonYear
    format
    episodes
  }
}";

        var payload = JsonSerializer.Serialize(new
        {
            query = graphQl,
            variables = new { id = mediaId }
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync(Endpoint, content, token);
        var json = await response.Content.ReadAsStringAsync(token);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("Media", out var media)
            || media.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<TvdbEpisode>();
        }

        var episodeCount = ReadInt(media, "episodes");
        if (episodeCount <= 0) episodeCount = 1;
        var format = ReadString(media, "format");
        var seasonNumber = IsSpecialFormat(format) ? 0 : 1;

        var episodes = new List<TvdbEpisode>();
        for (var i = 1; i <= episodeCount; i++)
        {
            episodes.Add(new TvdbEpisode
            {
                Id = (mediaId * 10000) + i,
                SeasonNumber = seasonNumber,
                EpisodeNumber = i,
                Name = $"Episode {i:00}",
                Provider = "AniList",
                ScopeName = seasonNumber == 0 ? "Specials / OVAs" : "Main Series"
            });
        }
        return episodes;
    }

    private static bool IsSpecialFormat(string format)
    {
        return format.Equals("SPECIAL", StringComparison.OrdinalIgnoreCase)
            || format.Equals("OVA", StringComparison.OrdinalIgnoreCase)
            || format.Equals("ONA", StringComparison.OrdinalIgnoreCase)
            || format.Equals("MUSIC", StringComparison.OrdinalIgnoreCase);
    }

    private static string PickTitle(JsonElement title, string preferredLanguage)
    {
        var language = (preferredLanguage ?? string.Empty).Trim().ToLowerInvariant();
        if (language is "eng" or "en")
        {
            var english = ReadString(title, "english");
            if (!string.IsNullOrWhiteSpace(english)) return english;
        }
        if (language is "jpn" or "ja")
        {
            var native = ReadString(title, "native");
            if (!string.IsNullOrWhiteSpace(native)) return native;
        }

        var romaji = ReadString(title, "romaji");
        if (!string.IsNullOrWhiteSpace(romaji)) return romaji;
        var fallbackEnglish = ReadString(title, "english");
        if (!string.IsNullOrWhiteSpace(fallbackEnglish)) return fallbackEnglish;
        return ReadString(title, "native");
    }

    private static string StripHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(value, "<.*?>", string.Empty).Replace("&quot;", "\"").Replace("&#039;", "'").Trim();
    }

    private static int ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return number;
        return 0;
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null) return string.Empty;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }
}
