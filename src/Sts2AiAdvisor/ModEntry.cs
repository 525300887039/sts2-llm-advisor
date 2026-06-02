// Bootstrap adapted from sts2-advisor's Plugin.cs (MIT) — SQLite/cloud/tier services dropped.
using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using Sts2AiAdvisor.Game;
using Sts2AiAdvisor.Llm;
using Sts2AiAdvisor.Ui;

namespace Sts2AiAdvisor;

[ModInitializer(nameof(Init))]
public static class ModEntry
{
    private const string HarmonyId = "com.sts2aiadvisor";

    private static bool _initialized;
    private static Harmony? _harmony;
    private static AdvisorOverlay? _overlay;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            ModLog.Info("STS2 AI Advisor v0.1.0 initializing...");

            // Capture the game thread for later marshalling. Init() runs on the game thread.
            GameThread.Initialize();

            // LLM layer (+ curated archetype cheat-sheet for richer prompts).
            LlmConfig config = LlmConfig.Load();
            ArchetypeGuide archetypeGuide = ArchetypeGuide.Load();
            ILlmAdvisor advisor = new OpenAiCompatibleAdvisor(config, archetypeGuide);

            // Overlay (built lazily when the SceneTree is ready / first card reward).
            _overlay = new AdvisorOverlay(config, advisor);
            GamePatches.SetOverlay(_overlay);

            // Harmony patches.
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(typeof(ModEntry).Assembly);
            GamePatches.ApplyManualPatches(_harmony);

            ModLog.Info("STS2 AI Advisor initialized.");
        }
        catch (Exception ex)
        {
            ModLog.Error("Initialization failed", ex);
        }
    }
}
