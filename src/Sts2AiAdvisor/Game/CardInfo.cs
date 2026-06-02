// adapted from sts2-advisor (MIT)
using System.Collections.Generic;

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

    /// <summary>Localized effect/rules text (e.g. "造成6点伤害。获得5格挡。"). Best-effort via reflection; "" if unavailable.</summary>
    public string Description { get; set; } = "";

    /// <summary>Card keywords (Exhaust, Sly, ...). Best-effort via reflection.</summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>Synergy/archetype tags (poison, shiv, block, ...) — drives archetype detection.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Targeting (Self, AnyEnemy, AllEnemies, ...). Best-effort via reflection.</summary>
    public string TargetType { get; set; } = "";

    /// <summary>Base damage if this card deals damage; null when not applicable/unknown.</summary>
    public int? Damage { get; set; }

    /// <summary>Base block if this card grants block; null when not applicable/unknown.</summary>
    public int? Block { get; set; }
}
