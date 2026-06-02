using System.Collections.Generic;
using Sts2AiAdvisor.Game;

namespace Sts2AiAdvisor.Llm;

/// <summary>Input to the advisor: the current run snapshot. Candidate cards live in <see cref="GameState.OfferedCards"/>.</summary>
public sealed record AdviceRequest(GameState State);

/// <summary>
/// Parsed advice for the offered cards plus a short overall summary.
/// <see cref="Archetype"/> is the deck direction the model inferred (raw). <see cref="DeckSummary"/>
/// is the short, locale-prefixed line shown above the advice (built from <see cref="Archetype"/>).
/// A card with <see cref="CardAdvice.CardId"/> == "SKIP" represents the "take no card" option.
/// </summary>
public sealed record AdviceResult(List<CardAdvice> Cards, string Summary, string DeckSummary = "", string Archetype = "");

/// <summary>Per-card advice. <see cref="Grade"/> is a free-form letter/score from the model.</summary>
public sealed record CardAdvice(string CardId, string Grade, string Reason, bool Recommended);
