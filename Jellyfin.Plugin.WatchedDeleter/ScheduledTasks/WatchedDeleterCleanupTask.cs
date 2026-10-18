using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WatchedDeleter.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.WatchedDeleter.ScheduledTasks;

/// <summary>
/// Scheduled task that deletes episodes once their grace period has expired.
/// </summary>
public class WatchedDeleterCleanupTask : IScheduledTask
{
    private readonly EpisodeCleanupService _cleanupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchedDeleterCleanupTask"/> class.
    /// </summary>
    /// <param name="cleanupService">The cleanup service.</param>
    public WatchedDeleterCleanupTask(EpisodeCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
    }

    /// <inheritdoc />
    public string Name => "Delete watched episodes";

    /// <inheritdoc />
    public string Key => "WatchedDeleterCleanup";

    /// <inheritdoc />
    public string Description =>
        "Deletes episodes from the selected libraries when they have been watched and the configured delay has passed. Series and seasons that no longer contain episodes are removed as well.";

    /// <inheritdoc />
    public string Category => "Watched Deleter";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromMinutes(15).Ticks
            }
        };
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        return _cleanupService.RunAsync(progress, cancellationToken);
    }
}
