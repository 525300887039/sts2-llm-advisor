// adapted from sts2-advisor (MIT)
using System.Collections.Generic;

namespace Sts2AiAdvisor.Game;

/// <summary>Snapshot of the current run, serialized cleanly to JSON for the LLM prompt.</summary>
public sealed class GameState
{
    public string Character { get; set; } = "unknown";
    public int ActNumber { get; set; }
    public int Floor { get; set; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int Gold { get; set; }
    public int AscensionLevel { get; set; }

    public List<CardInfo> DeckCards { get; set; } = new();
    public List<RelicInfo> Relics { get; set; } = new();
    public List<CardInfo> OfferedCards { get; set; } = new();
}
