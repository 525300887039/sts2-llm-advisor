// adapted from sts2-advisor (MIT)
namespace Sts2AiAdvisor.Game;

/// <summary>Plain card snapshot, serialized into the LLM prompt.</summary>
public sealed class CardInfo
{
    public string Id { get; set; } = "unknown";
    public string Name { get; set; } = "unknown";
    public int Cost { get; set; }
    public string Type { get; set; } = "";
    public string Rarity { get; set; } = "";
    public bool Upgraded { get; set; }
}
