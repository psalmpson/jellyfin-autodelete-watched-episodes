using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AutoDelete.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AutoDelete;

/// <summary>
/// Main plugin entry point.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The unique identifier of this plugin.
    /// </summary>
    public const string PluginId = "9386bc33-7cb3-4303-802a-c7f53827022f";

    private const string ConfigurationPageResource = "Jellyfin.Plugin.AutoDelete.Configuration.configPage.html";

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Auto Delete Watched Episodes";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse(PluginId);

    /// <inheritdoc />
    public override string Description =>
        "Deletes episodes a configurable amount of time after they have been watched, and removes series and seasons that no longer contain any episodes.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = ConfigurationPageResource
            }
        };
    }
}
