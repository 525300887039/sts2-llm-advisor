using System.Collections.Generic;

namespace Sts2AiAdvisor.Game.Archetypes;

/// <summary>
/// A character archetype pattern. A deck is considered "in" the archetype when it has enough
/// core-tagged cards (and optionally enough support-tagged cards).
/// Structure inspired by sts2-advisor (MIT); this is an independent reimplementation.
/// </summary>
public sealed class Archetype
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public IReadOnlyList<string> CoreTags { get; init; } = new List<string>();
    public IReadOnlyList<string> SupportTags { get; init; } = new List<string>();
    public int CoreThreshold { get; init; } = 3;
    public int SupportThreshold { get; init; } = 1;
}
