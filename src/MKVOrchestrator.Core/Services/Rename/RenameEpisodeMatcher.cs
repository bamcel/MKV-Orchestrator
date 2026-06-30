using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services.Rename;

public sealed record AbsoluteEpisodeMatch(int AbsoluteEpisode, TvdbEpisode Episode)
{
    public string StatusText => $"Episode {AbsoluteEpisode} = S{Episode.SeasonNumber:00}E{Episode.EpisodeNumber:00}";
}

public static class RenameEpisodeMatcher
{
    public static bool TryMatchAbsoluteEpisode(
        IEnumerable<TvdbEpisode> providerEpisodes,
        int? absoluteEpisode,
        out AbsoluteEpisodeMatch match)
    {
        match = null!;
        if (!absoluteEpisode.HasValue || absoluteEpisode.Value <= 0)
        {
            return false;
        }

        var orderedRegularEpisodes = providerEpisodes
            .Where(episode => episode.SeasonNumber > 0)
            .OrderBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .ThenBy(episode => episode.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (absoluteEpisode.Value > orderedRegularEpisodes.Count)
        {
            return false;
        }

        match = new AbsoluteEpisodeMatch(absoluteEpisode.Value, orderedRegularEpisodes[absoluteEpisode.Value - 1]);
        return true;
    }
}
