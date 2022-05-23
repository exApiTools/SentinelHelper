using System.Collections.Generic;
using System.Text.Json.Serialization;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace SentinelHelper;

public class SentinelTypeSettings : ISettings
{
    //sigh
    [JsonIgnore] public ToggleNode Enable { get; set; }

    public ToggleNode DisplayMonsterCountInRange { get; set; } = new ToggleNode(true);
    public RangeNode<int> NearbyMonsterRange { get; set; } = new RangeNode<int>(70, 1, 300);
    public TextNode Condition { get; set; } = new TextNode("");
}

public class SentinelHelperSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(true);
    public ToggleNode IgnoreIdleMonsters { get; set; } = new ToggleNode(true);

    public Dictionary<SentinelType, SentinelTypeSettings> PerTypeSettings = new()
    {
        [SentinelType.Blue] = new SentinelTypeSettings(),
        [SentinelType.Red] = new SentinelTypeSettings(),
        [SentinelType.Yellow] = new SentinelTypeSettings()
    };
}