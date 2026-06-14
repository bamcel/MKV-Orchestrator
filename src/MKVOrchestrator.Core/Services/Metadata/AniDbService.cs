using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class AniDbService
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    public async Task<IReadOnlyList<TvdbSeriesSearchResult>> SearchSeriesAsync(string query, string preferredLanguage, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<TvdbSeriesSearchResult>();

        var url = "https://anidb.net/api/anime-titles.xml.gz";
        using var response = await Client.GetAsync(url, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);

        Stream xmlStream = stream;
        if (response.Content.Headers.ContentType?.MediaType?.Contains("gzip", StringComparison.OrdinalIgnoreCase) == true
            || response.Content.Headers.ContentDisposition?.FileName?.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) == true)
        {
            xmlStream = new GZipStream(stream, CompressionMode.Decompress);
        }

        var document = await XDocument.LoadAsync(xmlStream, LoadOptions.None, token);
        var queryText = Normalize(query);
        var results = new List<TvdbSeriesSearchResult>();

        foreach (var anime in document.Descendants().Where(e => e.Name.LocalName == "anime"))
        {
            var aidText = anime.Attribute("aid")?.Value;
            if (!int.TryParse(aidText, out var aid) || aid <= 0) continue;

            var titles = anime.Elements().Where(e => e.Name.LocalName == "title")
                .Select(t => new
                {
                    Text = (t.Value ?? string.Empty).Trim(),
                    Lang = (t.Attribute(XNamespace.Xml + "lang")?.Value ?? string.Empty).Trim(),
                    Type = (t.Attribute("type")?.Value ?? string.Empty).Trim()
                })
                .Where(t => !string.IsNullOrWhiteSpace(t.Text))
                .ToList();

            if (!titles.Any(t => Normalize(t.Text).Contains(queryText))) continue;

            var chosen = PickTitle(titles.Select(t => (t.Text, t.Lang, t.Type)).ToList(), preferredLanguage);
            if (string.IsNullOrWhiteSpace(chosen)) continue;

            results.Add(new TvdbSeriesSearchResult
            {
                Id = aid,
                Name = chosen,
                Year = string.Empty,
                Overview = "AniDB title match. Select to load regular episodes and specials from the AniDB HTTP API.",
                Provider = "AniDB",
                Format = "Anime"
            });

            if (results.Count >= 50) break;
        }

        return results;
    }

    public async Task<IReadOnlyList<TvdbEpisode>> GetEpisodesAsync(string clientName, string clientVersion, int animeId, string preferredLanguage, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(clientName)) throw new InvalidOperationException("Enter an AniDB client name in Settings before loading AniDB episodes.");
        var clientVer = string.IsNullOrWhiteSpace(clientVersion) ? "1" : Regex.Replace(clientVersion.Trim(), "[^0-9]", string.Empty);
        if (string.IsNullOrWhiteSpace(clientVer)) clientVer = "1";

        // AniDB's HTTP API endpoint is published as HTTP on port 9001. Use HTTP here because HTTPS can fail or return
        // incomplete behavior depending on the runtime/network path.
        var url = $"http://api.anidb.net:9001/httpapi?request=anime&client={Uri.EscapeDataString(clientName.Trim().ToLowerInvariant())}&clientver={Uri.EscapeDataString(clientVer)}&protover=1&aid={animeId}";
        using var response = await Client.GetAsync(url, token);
        var xml = await response.Content.ReadAsStringAsync(token);
        response.EnsureSuccessStatusCode();

        var document = XDocument.Parse(xml);
        var errorText = document.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("error", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(errorText))
        {
            throw new InvalidOperationException("AniDB returned an error: " + errorText);
        }

        var episodesElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "episodes");
        var episodes = new List<TvdbEpisode>();

        if (episodesElement is not null)
        {
            foreach (var ep in episodesElement.Elements().Where(e => e.Name.LocalName == "episode"))
            {
                var epNoElement = ep.Elements().FirstOrDefault(e => e.Name.LocalName == "epno");
                var typeText = (epNoElement?.Attribute("type")?.Value ?? "1").Trim();
                var epNoText = (epNoElement?.Value ?? string.Empty).Trim();

                // AniDB type 1 is the regular episode list. Other epno types are non-regular items, so expose them
                // under Specials to keep the mkvrename scope selector useful and predictable.
                var seasonNumber = typeText == "1" ? 1 : 0;
                var episodeNumber = ParseEpisodeNumber(epNoText);
                if (episodeNumber <= 0) continue;

                var titleElements = ep.Elements().Where(e => e.Name.LocalName == "title").ToList();
                var title = PickAniDbEpisodeTitle(titleElements, preferredLanguage);
                if (string.IsNullOrWhiteSpace(title)) title = seasonNumber == 0 ? $"Special {episodeNumber:00}" : $"Episode {episodeNumber:00}";

                episodes.Add(new TvdbEpisode
                {
                    Id = BuildEpisodeId(animeId, seasonNumber, episodeNumber),
                    SeasonNumber = seasonNumber,
                    EpisodeNumber = episodeNumber,
                    Name = title,
                    Provider = "AniDB",
                    ScopeName = seasonNumber == 0 ? "Specials / OVAs" : "Main Series"
                });
            }
        }

        if (episodes.Count == 0)
        {
            var episodeCountText = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "episodecount")?.Value;
            if (int.TryParse(episodeCountText, out var episodeCount) && episodeCount > 0)
            {
                for (var i = 1; i <= episodeCount; i++)
                {
                    episodes.Add(new TvdbEpisode
                    {
                        Id = BuildEpisodeId(animeId, 1, i),
                        SeasonNumber = 1,
                        EpisodeNumber = i,
                        Name = $"Episode {i:00}",
                        Provider = "AniDB",
                        ScopeName = "Main Series"
                    });
                }
            }
        }

        return episodes
            .GroupBy(e => new { e.SeasonNumber, e.EpisodeNumber })
            .Select(g => g.First())
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .ToList();
    }

    private static int BuildEpisodeId(int animeId, int seasonNumber, int episodeNumber)
    {
        return (animeId * 100000) + (seasonNumber * 10000) + episodeNumber;
    }

    private static int ParseEpisodeNumber(string value)
    {
        var match = Regex.Match(value ?? string.Empty, @"\d+");
        return match.Success && int.TryParse(match.Value, out var number) ? number : 0;
    }

    private static string PickAniDbEpisodeTitle(List<XElement> titles, string preferredLanguage)
    {
        var preferred = NormalizeLanguage(preferredLanguage);
        var exact = titles.FirstOrDefault(t => NormalizeLanguage(t.Attribute(XNamespace.Xml + "lang")?.Value ?? string.Empty) == preferred)?.Value;
        if (!string.IsNullOrWhiteSpace(exact)) return exact.Trim();
        var english = titles.FirstOrDefault(t => NormalizeLanguage(t.Attribute(XNamespace.Xml + "lang")?.Value ?? string.Empty) == "en")?.Value;
        if (!string.IsNullOrWhiteSpace(english)) return english.Trim();
        var romanized = titles.FirstOrDefault(t => NormalizeLanguage(t.Attribute(XNamespace.Xml + "lang")?.Value ?? string.Empty) == "x-jat")?.Value;
        if (!string.IsNullOrWhiteSpace(romanized)) return romanized.Trim();
        return titles.FirstOrDefault()?.Value?.Trim() ?? string.Empty;
    }

    private static string PickTitle(List<(string Text, string Lang, string Type)> titles, string preferredLanguage)
    {
        var preferred = NormalizeLanguage(preferredLanguage);
        var exact = titles.FirstOrDefault(t => NormalizeLanguage(t.Lang) == preferred && t.Type.Equals("main", StringComparison.OrdinalIgnoreCase)).Text;
        if (!string.IsNullOrWhiteSpace(exact)) return exact;
        var romanizedMain = titles.FirstOrDefault(t => NormalizeLanguage(t.Lang) == "x-jat" && t.Type.Equals("main", StringComparison.OrdinalIgnoreCase)).Text;
        if (!string.IsNullOrWhiteSpace(romanizedMain)) return romanizedMain;
        var english = titles.FirstOrDefault(t => NormalizeLanguage(t.Lang) == "en" && t.Type.Equals("main", StringComparison.OrdinalIgnoreCase)).Text;
        if (!string.IsNullOrWhiteSpace(english)) return english;
        var main = titles.FirstOrDefault(t => t.Type.Equals("main", StringComparison.OrdinalIgnoreCase)).Text;
        if (!string.IsNullOrWhiteSpace(main)) return main;
        return titles.FirstOrDefault().Text;
    }

    private static string NormalizeLanguage(string language)
    {
        var value = (language ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "eng" => "en",
            "jpn" => "ja",
            "jpn-romaji" or "romaji" or "x_jat" => "x-jat",
            "spa" => "es",
            "fre" or "fra" => "fr",
            "ger" or "deu" => "de",
            "kor" => "ko",
            "chi" => "zh",
            _ => value
        };
    }

    private static string Normalize(string value)
    {
        return Regex.Replace((value ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }
}
