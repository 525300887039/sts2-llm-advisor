using System.Collections.Generic;

namespace Sts2AiAdvisor.Game.Archetypes;

/// <summary>A detected archetype with a 0..1 strength score and the counts that produced it.</summary>
public sealed class ArchetypeMatch
{
    public Archetype Archetype { get; init; } = null!;
    public float Strength { get; init; }
    public int CoreCount { get; init; }
    public int SupportCount { get; init; }
}

/// <summary>Result of analysing a deck: tag histogram, energy curve, type counts, detected archetypes.</summary>
public sealed class DeckAnalysis
{
    public string Character { get; set; } = "";
    public int TotalCards { get; set; }
    public Dictionary<string, int> TagCounts { get; } = new();
    public Dictionary<string, int> KeywordCounts { get; } = new();
    public Dictionary<int, int> EnergyCurve { get; } = new();
    public int AttackCount { get; set; }
    public int SkillCount { get; set; }
    public int PowerCount { get; set; }
    public float AverageCost { get; set; }
    public List<ArchetypeMatch> DetectedArchetypes { get; } = new();
}
