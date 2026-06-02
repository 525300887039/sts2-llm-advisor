// Rewritten from STS2-Agent's GameStateService route (AGPL) + cross-checked against
// sts2-advisor's GameStateReader (MIT). Original code from neither repo was copied verbatim.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace Sts2AiAdvisor.Game;

/// <summary>
/// Reads the current run into a plain <see cref="GameState"/>. MUST be called on the game thread.
/// Defensive throughout: a menu / non-combat / not-in-run context yields an empty state.
/// </summary>
internal static class GameStateReader
{
    private static readonly object Gate = new();
    private static IReadOnlyList<CardCreationResult>? _lastCardOptionsField;
    private static bool _cardsDumped;

    /// <summary>Card-reward options stashed by the Harmony hook (thread-safe).</summary>
    public static IReadOnlyList<CardCreationResult>? LastCardOptions
    {
        get { lock (Gate) return _lastCardOptionsField; }
        set { lock (Gate) _lastCardOptionsField = value; }
    }

    /// <summary>Build a snapshot of the current run. Returns an empty state (never null) when not in a run.</summary>
    public static GameState ReadCurrentState()
    {
        var state = new GameState();
        try
        {
            state.Locale = ReadLocale();
            var runManager = RunManager.Instance;
            if (runManager == null)
            {
                ModLog.Info("ReadCurrentState: RunManager is null — not in a run.");
                return state;
            }

            RunState? runState = runManager.DebugOnlyGetState();
            if (runState == null)
            {
                ModLog.Info("ReadCurrentState: RunState is null — likely at the menu.");
                state.OfferedCards = ReadOfferedCards();
                return state;
            }

            Player? player = LocalContext.GetMe(runState);
            if (player == null)
            {
                ModLog.Info("ReadCurrentState: no local player.");
                state.OfferedCards = ReadOfferedCards();
                return state;
            }

            state.Character = ReadCharacter(player);
            state.AscensionLevel = SafeInt(() => runState.AscensionLevel);
            state.Floor = SafeInt(() => runState.TotalFloor);
            state.ActNumber = SafeInt(() => runState.CurrentActIndex) + 1;
            state.CurrentHP = SafeInt(() => player.Creature?.CurrentHp ?? 0);
            state.MaxHP = SafeInt(() => player.Creature?.MaxHp ?? 0);
            state.Gold = SafeInt(() => player.Gold);
            state.DeckCards = ReadDeck(player);
            state.Relics = ReadRelics(player);
            state.OfferedCards = ReadOfferedCards();
        }
        catch (Exception ex)
        {
            ModLog.Error("ReadCurrentState failed", ex);
        }
        return state;
    }

    private static string ReadCharacter(Player player)
    {
        try
        {
            string raw = player.Character?.Id?.Entry?.ToLowerInvariant() ?? "unknown";
            // Game ids look like "character.regent" / "CHARACTER.REGENT" — normalize to the short name
            // ("regent") so it matches the archetype guide keys.
            int dot = raw.LastIndexOf('.');
            return dot >= 0 && dot < raw.Length - 1 ? raw.Substring(dot + 1) : raw;
        }
        catch (Exception ex)
        {
            ModLog.Error("ReadCharacter failed", ex);
            return "unknown";
        }
    }

    private static List<CardInfo> ReadDeck(Player player)
    {
        var list = new List<CardInfo>();
        try
        {
            var cards = player.Deck?.Cards;
            if (cards == null) return list;
            foreach (CardModel card in cards)
            {
                if (card != null) list.Add(CardModelToInfo(card));
            }
        }
        catch (Exception ex)
        {
            ModLog.Error("ReadDeck failed", ex);
        }
        return list;
    }

    private static List<RelicInfo> ReadRelics(Player player)
    {
        var list = new List<RelicInfo>();
        try
        {
            var relics = player.Relics;
            if (relics == null) return list;
            foreach (RelicModel relic in relics)
            {
                if (relic != null) list.Add(RelicModelToInfo(relic));
            }
        }
        catch (Exception ex)
        {
            ModLog.Error("ReadRelics failed", ex);
        }
        return list;
    }

    private static List<CardInfo> ReadOfferedCards()
    {
        var list = new List<CardInfo>();
        try
        {
            var options = LastCardOptions;
            if (options == null) return list;
            foreach (CardCreationResult result in options)
            {
                CardModel? card = result?.Card;
                if (card != null) list.Add(CardModelToInfo(card));
            }
        }
        catch (Exception ex)
        {
            ModLog.Error("ReadOfferedCards failed", ex);
        }
        return list;
    }

    private static CardInfo CardModelToInfo(CardModel card)
    {
        var info = new CardInfo
        {
            Id = card.Id?.Entry ?? "unknown",
            Name = card.Title ?? card.Id?.Entry ?? "unknown",
            Type = SafeString(() => card.Type.ToString()),
            Rarity = SafeString(() => card.Rarity.ToString()),
            Cost = SafeInt(() => card.EnergyCost?.Canonical ?? 0),
            // Mechanics (best-effort, reflection-based so unverified members degrade gracefully).
            Description = CardReflection.ReadDescription(card),
            Keywords = CardReflection.ReadStringList(card, "Keywords"),
            Tags = CardReflection.ReadStringList(card, "Tags"),
            TargetType = CardReflection.ReadTargetType(card),
            Damage = CardReflection.ReadDynamicValue(card, "damage"),
            Block = CardReflection.ReadDynamicValue(card, "block"),
        };
        info.Upgraded = card.Id?.Entry?.EndsWith("+", StringComparison.Ordinal) == true;
        return info;
    }

    private static RelicInfo RelicModelToInfo(RelicModel relic)
    {
        return new RelicInfo
        {
            Id = relic.Id?.Entry ?? "unknown",
            Name = relic.Title?.ToString() ?? relic.Id?.Entry ?? "unknown",
            Rarity = SafeString(() => relic.Rarity.ToString()),
        };
    }

    /// <summary>Current game UI locale via Godot's TranslationServer (game thread only). "" on failure.</summary>
    private static string ReadLocale()
    {
        try
        {
            string locale = Godot.TranslationServer.GetLocale() ?? "";
            ModLog.Info($"Detected game locale: '{locale}'");
            return locale;
        }
        catch (Exception ex)
        {
            ModLog.Error("ReadLocale failed", ex);
            return "";
        }
    }

    /// <summary>
    /// One-time dev aid (config <c>dumpCards: true</c>): dump the real card DB — id / tags / keywords /
    /// description — so the archetype tag vocabulary can be re-grounded on the installed game version.
    /// MUST be called on the game thread (touches game models). Writes cards_dump.txt beside the DLL.
    /// </summary>
    public static void DumpAllCards()
    {
        if (_cardsDumped) return;
        _cardsDumped = true;
        try
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (object card in CardReflection.EnumerateAllCards())
            {
                string id = CardReflection.ReadId(card);
                if (string.IsNullOrEmpty(id)) continue;
                List<string> tags = CardReflection.ReadStringList(card, "Tags");
                List<string> kw = CardReflection.ReadStringList(card, "Keywords");
                string desc = CardReflection.ReadDescription(card);
                sb.Append(id)
                  .Append(" | tags=[").Append(string.Join(",", tags)).Append(']')
                  .Append(" | keywords=[").Append(string.Join(",", kw)).Append(']')
                  .Append(" | ").Append(desc)
                  .AppendLine();
                n++;
            }
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            string path = Path.Combine(dir, "cards_dump.txt");
            File.WriteAllText(path, sb.ToString());
            ModLog.Info($"DumpAllCards: wrote {n} cards to {path}");
        }
        catch (Exception ex)
        {
            ModLog.Error("DumpAllCards failed", ex);
        }
    }

    private static int SafeInt(Func<int> getter)
    {
        try { return getter(); } catch { return 0; }
    }

    private static string SafeString(Func<string> getter)
    {
        try { return getter() ?? ""; } catch { return ""; }
    }
}
