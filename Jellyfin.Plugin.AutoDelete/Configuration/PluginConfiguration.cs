using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AutoDelete.Configuration;

/// <summary>
/// The unit used for the delete delay.
/// </summary>
public enum DelayUnit
{
    /// <summary>
    /// Minutes.
    /// </summary>
    Minutes,

    /// <summary>
    /// Hours.
    /// </summary>
    Hours,

    /// <summary>
    /// Days.
    /// </summary>
    Days
}

/// <summary>
/// The plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the scheduled task should do anything.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether files should only be logged and not deleted.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Gets or sets how long an episode is kept after it was watched.
    /// </summary>
    public double DeleteAfterValue { get; set; } = 24;

    /// <summary>
    /// Gets or sets the unit of <see cref="DeleteAfterValue"/>.
    /// </summary>
    public DelayUnit DeleteAfterUnit { get; set; } = DelayUnit.Hours;

    /// <summary>
    /// Gets or sets the IDs of the libraries that should be scanned for watched episodes.
    /// </summary>
    public List<string> Libraries { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether series without episodes should be removed.
    /// </summary>
    public bool RemoveEmptySeries { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the folder of an empty series should be deleted from disk.
    /// </summary>
    public bool DeleteSeriesFolder { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether seasons without episodes should be removed.
    /// </summary>
    public bool RemoveEmptySeasons { get; set; } = true;

    /// <summary>
    /// Gets the configured delay as a <see cref="TimeSpan"/>.
    /// </summary>
    /// <returns>The delay.</returns>
    public TimeSpan GetDeleteDelay()
    {
        var value = DeleteAfterValue > 0 ? DeleteAfterValue : 0;

        return DeleteAfterUnit switch
        {
            DelayUnit.Minutes => TimeSpan.FromMinutes(value),
            DelayUnit.Days => TimeSpan.FromDays(value),
            _ => TimeSpan.FromHours(value)
        };
    }
}
