// Card-reward hook structure adapted from sts2-advisor's GamePatches (MIT), trimmed to the
// single NCardRewardSelectionScreen.ShowScreen postfix needed for the MVP.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using Sts2AiAdvisor.Game;
using Sts2AiAdvisor.Ui;

namespace Sts2AiAdvisor;

internal static class GamePatches
{
    private static AdvisorOverlay? _overlay;

    public static void SetOverlay(AdvisorOverlay overlay) => _overlay = overlay;

    /// <summary>Manually patch NCardRewardSelectionScreen.ShowScreen (only hook needed for the MVP).</summary>
    public static void ApplyManualPatches(Harmony harmony)
    {
        try
        {
            MethodInfo? target = AccessTools.Method(typeof(NCardRewardSelectionScreen), "ShowScreen");
            if (target == null)
            {
                ModLog.Warn("Could not find NCardRewardSelectionScreen.ShowScreen — card-reward hook unavailable.");
                return;
            }
            MethodInfo postfix = typeof(GamePatches).GetMethod(
                nameof(OnCardRewardOpened), BindingFlags.Static | BindingFlags.Public)!;
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            ModLog.Info("Patched NCardRewardSelectionScreen.ShowScreen.");
        }
        catch (Exception ex)
        {
            ModLog.Error("Failed to patch NCardRewardSelectionScreen.ShowScreen", ex);
        }
    }

    /// <summary>
    /// Postfix on ShowScreen. Captures the offered cards and shows the overlay.
    /// The screen is reused for pile viewers / removal / events, so we skip those:
    /// genuine post-combat rewards have a small option set (guard: Count &gt; 5 → skip).
    /// </summary>
    public static void OnCardRewardOpened(IReadOnlyList<CardCreationResult> options)
    {
        try
        {
            if (options == null)
            {
                ModLog.Info("ShowScreen fired with null options — ignoring.");
                return;
            }
            if (options.Count > 5)
            {
                ModLog.Info($"ShowScreen with {options.Count} cards — likely a pile viewer, skipping.");
                return;
            }

            GameStateReader.LastCardOptions = options;

            var ids = new List<string>(options.Count);
            foreach (CardCreationResult result in options)
            {
                string? id = result?.Card?.Id?.Entry;
                if (id != null) ids.Add(id);
            }
            ModLog.Info($"Card reward detected — options: [{string.Join(", ", ids)}]");

            // We are on the game thread here; safe to touch Godot nodes.
            _overlay?.Show();
        }
        catch (Exception ex)
        {
            ModLog.Error("OnCardRewardOpened error", ex);
        }
    }
}
