// adapted from sts2-advisor (MIT)
namespace Sts2AiAdvisor.Game;

/// <summary>Plain relic snapshot, serialized into the LLM prompt.</summary>
public sealed class RelicInfo
{
    public string Id { get; set; } = "unknown";
    public string Name { get; set; } = "unknown";
    public string Rarity { get; set; } = "";
}
