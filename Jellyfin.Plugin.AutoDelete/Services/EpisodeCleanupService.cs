using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AutoDelete.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AutoDelete.Services;

/// <summary>
/// Finds episodes that were watched longer ago than the configured delay and deletes them.
/// </summary>
public class EpisodeCleanupService
{
    private static readonly HashSet<string> VideoFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3gp", ".asf", ".avi", ".divx", ".flv", ".iso", ".m2ts", ".m2v", ".m4v", ".mkv",
        ".mov", ".mp4", ".mpeg", ".mpg", ".mts", ".ogv", ".rm", ".rmvb", ".strm", ".ts",
        ".vob", ".webm", ".wmv"
    };

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<EpisodeCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeCleanupService"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="logger">The logger.</param>
    public EpisodeCleanupService(
        ILibraryManager libraryManager,
        IUserManager userManager,
        IUserDataManager userDataManager,
        IFileSystem fileSystem,
        ILogger<EpisodeCleanupService> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _userDataManager = userDataManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Runs the cleanup once.
    /// </summary>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the cleanup.</returns>
    public Task RunAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var delay = configuration.GetDeleteDelay();

        _logger.LogInformation(
            "Auto-delete task starting (enabled: {Enabled}, dry run: {DryRun}, delay: {Delay})",
            configuration.Enabled,
            configuration.DryRun,
            delay);

        if (!configuration.Enabled)
        {
            _logger.LogInformation("Automatic deletion is disabled; nothing to do.");
            progress.Report(100);
            return Task.CompletedTask;
        }

        var libraryIds = GetSelectedLibraryIds(configuration);
        if (libraryIds.Count == 0)
        {
            _logger.LogWarning("No libraries are selected in the plugin configuration; nothing to do.");
            progress.Report(100);
            return Task.CompletedTask;
        }

        var libraryParents = libraryIds
            .Select(id => _libraryManager.GetItemById(id))
            .OfType<ICollectionFolder>()
            .Cast<BaseItem>()
            .ToList();

        if (libraryParents.Count == 0)
        {
            _logger.LogWarning("None of the configured libraries could be found; nothing to do.");
            progress.Report(100);
            return Task.CompletedTask;
        }

        var episodes = _libraryManager.GetItemList(
            new InternalItemsQuery
            {
                Recursive = true,
                IncludeItemTypes = new[] { BaseItemKind.Episode }
            },
            libraryParents);

        _logger.LogInformation(
            "Checking {EpisodeCount} episode(s) in {LibraryCount} selected library/libraries.",
            episodes.Count,
            libraryParents.Count);

        progress.Report(10);

        var candidates = FindCandidates(episodes, delay, cancellationToken);
        _logger.LogInformation(
            "{CandidateCount} episode(s) have been watched for longer than {Delay} and are eligible.",
            candidates.Count,
            delay);

        progress.Report(25);

        var processed = 0;
        var deletedEpisodes = 0;
        var affectedSeries = new HashSet<Guid>();
        var affectedSeasons = new HashSet<Guid>();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (configuration.DryRun)
            {
                _logger.LogInformation(
                    "[Dry run] Would delete episode \"{Episode}\" of series \"{Series}\" (watched {LastPlayed:u} by {User}, file: {Path})",
                    candidate.Episode.Name,
                    candidate.SeriesName,
                    candidate.LastPlayedUtc,
                    candidate.UserName,
                    candidate.Episode.Path);
            }
            else if (DeleteEpisode(candidate))
            {
                deletedEpisodes++;

                if (!candidate.SeriesId.Equals(Guid.Empty))
                {
                    affectedSeries.Add(candidate.SeriesId);
                }

                if (!candidate.SeasonId.Equals(Guid.Empty))
                {
                    affectedSeasons.Add(candidate.SeasonId);
                }
            }

            processed++;
            progress.Report(25 + (60.0 * processed / candidates.Count));
        }

        if (configuration.DryRun)
        {
            _logger.LogInformation("[Dry run] Finished. {Count} episode(s) would have been deleted.", candidates.Count);
            progress.Report(100);
            return Task.CompletedTask;
        }

        var removedSeasons = 0;
        if (configuration.RemoveEmptySeasons)
        {
            foreach (var seasonId in affectedSeasons)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (RemoveSeasonIfEmpty(seasonId))
                {
                    removedSeasons++;
                }
            }
        }

        var removedSeries = 0;
        if (configuration.RemoveEmptySeries)
        {
            foreach (var seriesId in affectedSeries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (RemoveSeriesIfEmpty(seriesId, configuration.DeleteSeriesFolder))
                {
                    removedSeries++;
                }
            }
        }

        _logger.LogInformation(
            "Auto-delete finished. Deleted {Episodes} episode(s), removed {Seasons} empty season(s) and {Series} empty series.",
            deletedEpisodes,
            removedSeasons,
            removedSeries);

        progress.Report(100);
        return Task.CompletedTask;
    }

    private static List<Guid> GetSelectedLibraryIds(PluginConfiguration configuration)
    {
        var result = new List<Guid>();

        if (configuration.Libraries is null)
        {
            return result;
        }

        foreach (var value in configuration.Libraries)
        {
            if (Guid.TryParse(value, out var id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private List<DeletionCandidate> FindCandidates(
        IReadOnlyList<BaseItem> episodes,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        var users = _userManager.GetUsers().ToList();
        var watchedBy = new Dictionary<Guid, (DateTime LastPlayedUtc, string UserName)>();

        // Build one "last played" timestamp per episode across all users, so that a
        // re-watch by any user restarts the countdown.
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<Guid, UserItemData> userData;
            try
            {
                userData = _userDataManager.GetUserDataBatch(episodes, user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read playback state for user {User}", user.Username);
                continue;
            }

            foreach (var episode in episodes)
            {
                if (!userData.TryGetValue(episode.Id, out var data)
                    || data is null
                    || !data.Played
                    || data.LastPlayedDate is null)
                {
                    continue;
                }

                var lastPlayed = AsUtc(data.LastPlayedDate.Value);
                if (watchedBy.TryGetValue(episode.Id, out var existing) && existing.LastPlayedUtc >= lastPlayed)
                {
                    continue;
                }

                watchedBy[episode.Id] = (lastPlayed, user.Username);
            }
        }

        var now = DateTime.UtcNow;
        var result = new List<DeletionCandidate>();

        foreach (var item in episodes)
        {
            if (string.IsNullOrEmpty(item.Path) || !_fileSystem.FileExists(item.Path))
            {
                continue;
            }

            if (!watchedBy.TryGetValue(item.Id, out var watched) || now - watched.LastPlayedUtc < delay)
            {
                continue;
            }

            if (item is not Episode episode)
            {
                continue;
            }

            result.Add(new DeletionCandidate(
                episode,
                episode.SeriesId,
                episode.SeasonId,
                episode.SeriesName,
                watched.LastPlayedUtc,
                watched.UserName));
        }

        return result;
    }

    private bool DeleteEpisode(DeletionCandidate candidate)
    {
        try
        {
            _logger.LogInformation(
                "Deleting watched episode \"{Episode}\" of series \"{Series}\" (watched {LastPlayed:u} by {User}, file: {Path})",
                candidate.Episode.Name,
                candidate.SeriesName,
                candidate.LastPlayedUtc,
                candidate.UserName,
                candidate.Episode.Path);

            _libraryManager.DeleteItem(candidate.Episode, new DeleteOptions { DeleteFileLocation = true });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete episode \"{Episode}\" ({Path})",
                candidate.Episode.Name,
                candidate.Episode.Path);
            return false;
        }
    }

    private bool RemoveSeasonIfEmpty(Guid seasonId)
    {
        var season = _libraryManager.GetItemById(seasonId) as Season;
        if (season is null || HasRemainingEpisodes(season))
        {
            return false;
        }

        var remainingVideos = GetVideoFiles(season.Path);
        if (remainingVideos is null || remainingVideos.Count > 0)
        {
            _logger.LogWarning(
                "Season \"{Season}\" has no episodes left in the library, but the folder still contains files. Leaving it alone.",
                season.Name);
            return false;
        }

        _logger.LogInformation("Removing empty season \"{Season}\" ({Path})", season.Name, season.Path);
        _libraryManager.DeleteItem(season, new DeleteOptions { DeleteFileLocation = false });
        DeleteFolderIfSafe(season.Path);
        return true;
    }

    private bool RemoveSeriesIfEmpty(Guid seriesId, bool deleteFolder)
    {
        var series = _libraryManager.GetItemById(seriesId) as Series;
        if (series is null || HasRemainingEpisodes(series))
        {
            return false;
        }

        var remainingVideos = GetVideoFiles(series.Path);
        if (remainingVideos is null || remainingVideos.Count > 0)
        {
            _logger.LogWarning(
                "Series \"{Series}\" has no episodes left in the library, but the folder still contains video files. Leaving it alone.",
                series.Name);
            return false;
        }

        _logger.LogInformation("Removing empty series \"{Series}\" ({Path})", series.Name, series.Path);
        _libraryManager.DeleteItem(series, new DeleteOptions { DeleteFileLocation = false });

        if (deleteFolder)
        {
            DeleteFolderIfSafe(series.Path);
        }

        return true;
    }

    private bool HasRemainingEpisodes(BaseItem parent)
    {
        var episodes = _libraryManager.GetItemList(
            new InternalItemsQuery
            {
                Recursive = true,
                IncludeItemTypes = new[] { BaseItemKind.Episode }
            },
            new List<BaseItem> { parent });

        return episodes.Any(episode => !string.IsNullOrEmpty(episode.Path) && _fileSystem.FileExists(episode.Path));
    }

    private IReadOnlyList<string>? GetVideoFiles(string? path)
    {
        if (string.IsNullOrEmpty(path) || !_fileSystem.DirectoryExists(path))
        {
            return Array.Empty<string>();
        }

        try
        {
            return _fileSystem
                .GetFilePaths(path, true)
                .Where(file => VideoFileExtensions.Contains(Path.GetExtension(file)))
                .ToList();
        }
        catch (Exception ex)
        {
            // Returning null means "unknown"; callers must not delete anything in that case.
            _logger.LogError(ex, "Could not enumerate the files in {Path}", path);
            return null;
        }
    }

    private bool IsLibraryPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return true;
        }

        var normalizedPath = Path.TrimEndingDirectorySeparator(path);

        foreach (var virtualFolder in _libraryManager.GetVirtualFolders())
        {
            if (virtualFolder.Locations is null)
            {
                continue;
            }

            foreach (var location in virtualFolder.Locations)
            {
                if (string.IsNullOrEmpty(location))
                {
                    continue;
                }

                var normalizedLocation = Path.TrimEndingDirectorySeparator(location);

                if (string.Equals(normalizedPath, normalizedLocation, StringComparison.OrdinalIgnoreCase)
                    || _fileSystem.ContainsSubPath(normalizedPath, normalizedLocation))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void DeleteFolderIfSafe(string? path)
    {
        if (string.IsNullOrEmpty(path) || !_fileSystem.DirectoryExists(path))
        {
            return;
        }

        if (IsLibraryPath(path))
        {
            _logger.LogWarning(
                "Not deleting \"{Path}\" because it is a library folder or a parent of one.",
                path);
            return;
        }

        if (IsSymbolicLink(path))
        {
            _logger.LogWarning(
                "Not deleting \"{Path}\" because it is a symbolic link. Remove it manually if you want it gone.",
                path);
            return;
        }

        try
        {
            Directory.Delete(path, true);
            _logger.LogInformation("Deleted folder {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete folder {Path}", path);
        }
    }

    private static bool IsSymbolicLink(string path)
    {
        try
        {
            return new DirectoryInfo(path).LinkTarget is not null;
        }
        catch (Exception)
        {
            // If we cannot tell, assume the worst and do not delete anything.
            return true;
        }
    }

    private sealed record DeletionCandidate(
        Episode Episode,
        Guid SeriesId,
        Guid SeasonId,
        string? SeriesName,
        DateTime LastPlayedUtc,
        string UserName);
}
