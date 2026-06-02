using System.Collections.Generic;

namespace Sts2AiAdvisor.Game.Archetypes;

/// <summary>
/// PROVISIONAL per-character archetype patterns. The tag vocabulary mirrors sts2-advisor
/// (MIT, last updated 2026-03) as a STARTING POINT — it must be re-grounded on the live game's
/// actual CardTag values: run once with `dumpCards: true` in config.json to produce cards_dump.txt,
/// then align these tag strings to what the installed version really emits.
///
/// Detection gaps degrade gracefully: the raw deck tags are ALSO sent to the LLM, so even an
/// imperfect table here does not produce wrong advice — the model still sees the real tags.
/// </summary>
public static class ArchetypeDefinitions
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<Archetype>> ByCharacter =
        new Dictionary<string, IReadOnlyList<Archetype>>
        {
            ["ironclad"] = new Archetype[]
            {
                new() { Id = "strength", DisplayName = "Strength", CoreTags = new[] { "strength", "scaling" }, SupportTags = new[] { "multi_hit", "vulnerable" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "exhaust", DisplayName = "Exhaust", CoreTags = new[] { "exhaust" }, SupportTags = new[] { "draw", "self_damage" }, CoreThreshold = 4, SupportThreshold = 2 },
                new() { Id = "block", DisplayName = "Block / Barricade", CoreTags = new[] { "block", "dexterity" }, SupportTags = new[] { "scaling", "weak" }, CoreThreshold = 4, SupportThreshold = 1 },
                new() { Id = "self_damage", DisplayName = "Self-Damage / Corruption", CoreTags = new[] { "self_damage" }, SupportTags = new[] { "strength", "exhaust" }, CoreThreshold = 3, SupportThreshold = 2 },
            },
            ["silent"] = new Archetype[]
            {
                new() { Id = "poison", DisplayName = "Poison", CoreTags = new[] { "poison", "poison_scaling" }, SupportTags = new[] { "weak", "scaling" }, CoreThreshold = 3, SupportThreshold = 1 },
                new() { Id = "shiv", DisplayName = "Shiv", CoreTags = new[] { "shiv", "shiv_synergy" }, SupportTags = new[] { "dexterity", "draw" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "discard", DisplayName = "Discard / Sly", CoreTags = new[] { "discard", "discard_synergy", "sly" }, SupportTags = new[] { "draw", "retain" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "dexterity", DisplayName = "Dexterity / Block", CoreTags = new[] { "dexterity", "block" }, SupportTags = new[] { "weak", "draw" }, CoreThreshold = 3, SupportThreshold = 2 },
            },
            ["defect"] = new Archetype[]
            {
                new() { Id = "lightning", DisplayName = "Lightning Orbs", CoreTags = new[] { "lightning", "orb" }, SupportTags = new[] { "focus", "evoke" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "frost", DisplayName = "Frost / Focus", CoreTags = new[] { "frost", "focus" }, SupportTags = new[] { "orb", "block" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "dark", DisplayName = "Dark Orbs", CoreTags = new[] { "dark", "orb" }, SupportTags = new[] { "focus", "evoke" }, CoreThreshold = 2, SupportThreshold = 2 },
                new() { Id = "all_orbs", DisplayName = "All Orbs / Focus", CoreTags = new[] { "focus", "orb" }, SupportTags = new[] { "lightning", "frost", "dark", "channel" }, CoreThreshold = 3, SupportThreshold = 3 },
                new() { Id = "zero_cost", DisplayName = "0-Cost", CoreTags = new[] { "zero_cost" }, SupportTags = new[] { "draw", "scaling" }, CoreThreshold = 4, SupportThreshold = 1 },
            },
            ["regent"] = new Archetype[]
            {
                new() { Id = "stellar", DisplayName = "Stellar / Stars", CoreTags = new[] { "stellar", "stars" }, SupportTags = new[] { "draw", "scaling", "zero_cost" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "authority", DisplayName = "Authority / Forge", CoreTags = new[] { "authority", "forge" }, SupportTags = new[] { "scaling", "damage" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "minion", DisplayName = "Minion / Summoner", CoreTags = new[] { "minion" }, SupportTags = new[] { "damage", "scaling", "block" }, CoreThreshold = 2, SupportThreshold = 2 },
                new() { Id = "cosmic", DisplayName = "Cosmic Damage", CoreTags = new[] { "cosmic", "aoe" }, SupportTags = new[] { "stellar", "stars", "scaling" }, CoreThreshold = 3, SupportThreshold = 2 },
            },
            ["necrobinder"] = new Archetype[]
            {
                new() { Id = "doom", DisplayName = "Doom / Debuff", CoreTags = new[] { "doom", "debuff" }, SupportTags = new[] { "block", "scaling" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "soul", DisplayName = "Soul / Exhaust Cycling", CoreTags = new[] { "soul", "exhaust" }, SupportTags = new[] { "draw", "scaling" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "minion", DisplayName = "Minion / Summoner", CoreTags = new[] { "minion", "summon" }, SupportTags = new[] { "damage", "scaling" }, CoreThreshold = 3, SupportThreshold = 2 },
                new() { Id = "death", DisplayName = "Death / Reaper", CoreTags = new[] { "death", "exhaust" }, SupportTags = new[] { "aoe", "damage" }, CoreThreshold = 3, SupportThreshold = 2 },
            },
        };
}
