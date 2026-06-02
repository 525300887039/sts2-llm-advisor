using System;
using System.Collections.Generic;
using System.Linq;

namespace Sts2AiAdvisor.Game.Archetypes;

/// <summary>
/// Counts the deck's synergy tags and detects which character archetype(s) it leans toward.
/// Operates purely on <see cref="CardInfo"/> POCOs (no game objects / Godot nodes), so it is safe
/// to call OFF the game thread (e.g. inside the LLM advisor). Independent reimplementation; the
/// scoring shape is inspired by sts2-advisor (MIT).
/// </summary>
public static class DeckAnalyzer
{
    public static DeckAnalysis Analyze(string? character, IReadOnlyList<CardInfo>? deck)
    {
        var a = new DeckAnalysis { Character = character ?? "", TotalCards = deck?.Count ?? 0 };
        if (deck == null || deck.Count == 0) return a;

        int totalCost = 0, costCards = 0;
        foreach (CardInfo card in deck)
        {
            foreach (string tag in card.Tags ?? new List<string>())
            {
                string t = tag.Trim().ToLowerInvariant();
                if (t.Length == 0) continue;
                a.TagCounts[t] = a.TagCounts.TryGetValue(t, out int c) ? c + 1 : 1;
            }
            // Keywords (Exhaust/Sly/Ethereal/Retain/...) are far richer than tags in this game and are
            // a useful direction signal for the LLM.
            foreach (string kw in card.Keywords ?? new List<string>())
            {
                string k = kw.Trim().ToLowerInvariant();
                if (k.Length == 0) continue;
                a.KeywordCounts[k] = a.KeywordCounts.TryGetValue(k, out int kc) ? kc + 1 : 1;
            }

            int bucket = Math.Max(0, Math.Min(card.Cost, 5));
            a.EnergyCurve[bucket] = a.EnergyCurve.TryGetValue(bucket, out int e) ? e + 1 : 1;
            if (card.Cost >= 0) { totalCost += card.Cost; costCards++; }

            switch ((card.Type ?? "").ToLowerInvariant())
            {
                case "attack": a.AttackCount++; break;
                case "skill": a.SkillCount++; break;
                case "power": a.PowerCount++; break;
            }
        }
        a.AverageCost = costCards > 0 ? (float)totalCost / costCards : 0f;

        if (character != null
            && ArchetypeDefinitions.ByCharacter.TryGetValue(character.ToLowerInvariant(), out var archetypes))
        {
            foreach (Archetype arch in archetypes)
            {
                int core = arch.CoreTags.Sum(t => a.TagCounts.TryGetValue(t, out int v) ? v : 0);
                int support = arch.SupportTags.Sum(t => a.TagCounts.TryGetValue(t, out int v) ? v : 0);
                bool coreHit = core >= arch.CoreThreshold;
                bool supportHit = support >= arch.SupportThreshold;
                if (!coreHit && !(core >= 2 && supportHit)) continue;

                float strength = arch.CoreThreshold > 0 ? (float)core / (arch.CoreThreshold * 2f) : 0f;
                if (supportHit) strength += 0.2f;
                strength = Math.Min(1f, strength);

                a.DetectedArchetypes.Add(new ArchetypeMatch
                {
                    Archetype = arch,
                    Strength = strength,
                    CoreCount = core,
                    SupportCount = support,
                });
            }
            a.DetectedArchetypes.Sort((x, y) => y.Strength.CompareTo(x.Strength));
        }
        return a;
    }
}
