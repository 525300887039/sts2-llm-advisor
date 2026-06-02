// CanvasLayer/panel/button creation idiom adapted from sts2-advisor's OverlayManager (MIT);
// only the lightweight scaffolding was reused, not the full overlay implementation.
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Sts2AiAdvisor.Game;
using Sts2AiAdvisor.Llm;

namespace Sts2AiAdvisor.Ui;

/// <summary>
/// Top-right floating panel with a title, a content label, and a "获取建议" button.
/// Built entirely in code and attached deferred to the live SceneTree root.
/// Game-object reads happen on the game thread; the LLM HTTP call runs off-thread.
/// </summary>
public sealed class AdvisorOverlay
{
    private static readonly Color ClrBg = new(0.034f, 0.057f, 0.11f, 0.97f);
    private static readonly Color ClrHeader = new(0.92f, 0.78f, 0.35f);
    private static readonly Color ClrBody = new(0.85f, 0.85f, 0.85f);

    private readonly LlmConfig _config;
    private readonly ILlmAdvisor _advisor;

    private CanvasLayer? _layer;
    private PanelContainer? _panel;
    private VBoxContainer? _body;
    private Label? _content;
    private Button? _button;
    private Button? _collapseButton;
    private Button? _collapsedButton;
    private bool _busy;
    private bool _collapsed;
    private bool _dragging;
    private bool _dragMoved;
    private Vector2 _dragTotal;

    public AdvisorOverlay(LlmConfig config, ILlmAdvisor advisor)
    {
        _config = config;
        _advisor = advisor;
    }

    /// <summary>Build the overlay (once) and attach it deferred to the SceneTree root. Call on the game thread.</summary>
    public void EnsureBuilt()
    {
        if (_layer != null && GodotObject.IsInstanceValid(_layer))
            return;

        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
        {
            ModLog.Warn("EnsureBuilt: SceneTree not ready — overlay deferred.");
            return;
        }

        _layer = new CanvasLayer { Layer = 100 };

        _panel = new PanelContainer
        {
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = -420,
            OffsetRight = -20,
            OffsetTop = 20,
            OffsetBottom = 60,
            GrowVertical = Control.GrowDirection.End,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        var panelStyle = new StyleBoxFlat { BgColor = ClrBg };
        panelStyle.SetContentMarginAll(12);
        _panel.AddThemeStyleboxOverride("panel", panelStyle);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(vbox);

        // Title row: title label (expands) + a collapse toggle on the right.
        var titleRow = new HBoxContainer();
        var title = new Label { Text = "STS2 AI Advisor" };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", ClrHeader);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);

        _collapseButton = new Button { Text = "—", TooltipText = "收起为小按钮" };
        _collapseButton.Pressed += OnToggleCollapse;
        titleRow.AddChild(_collapseButton);
        vbox.AddChild(titleRow);

        // Collapsible body: the content label + the "获取建议" button.
        _body = new VBoxContainer();
        _body.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(_body);

        _content = new Label
        {
            Text = "进入选牌页后点击下方按钮获取建议。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 0),
        };
        _content.AddThemeColorOverride("font_color", ClrBody);
        _body.AddChild(_content);

        _button = new Button { Text = "获取建议" };
        _button.Pressed += OnButtonPressed;
        _body.AddChild(_button);

        _layer.AddChild(_panel);

        // Collapsed state: a single small button tucked into the top-right corner so it stops
        // blocking the game UI. Pressing it restores the full panel.
        _collapsedButton = new Button
        {
            Text = "AI ▸",
            TooltipText = "点击展开 · 按住拖动可移动",
            Visible = false,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = -92,
            OffsetRight = -20,
            OffsetTop = 20,
            OffsetBottom = 52,
        };
        // Drive click-vs-drag ourselves: a clean click expands; a drag moves the button.
        _collapsedButton.GuiInput += OnCollapsedGuiInput;
        _layer.AddChild(_collapsedButton);

        tree.Root.CallDeferred("add_child", _layer);
        ModLog.Info("Advisor overlay built and attached.");
    }

    /// <summary>Make the overlay visible (call on the game thread, e.g. from the card-reward hook).</summary>
    public void Show()
    {
        EnsureBuilt();
        if (_layer != null) _layer.Visible = true;
        ApplyCollapsedState();
    }

    /// <summary>Collapse the whole panel down to a single small corner button.</summary>
    private void OnToggleCollapse()
    {
        _collapsed = true;
        ApplyCollapsedState();
    }

    /// <summary>Restore the full panel from the collapsed button, at the button's current spot.</summary>
    private void OnExpand()
    {
        SyncPanelToCollapsedButton();
        _collapsed = false;
        ApplyCollapsedState();
    }

    /// <summary>
    /// Move the full panel so its top-right corner sits where the (possibly dragged) collapsed
    /// button is, so expanding keeps the position the user tucked it into. Both controls anchor to
    /// the top-right corner, so their offsets share a reference frame and copy across directly.
    /// The panel keeps its own width/height; it grows left and down from that corner.
    /// </summary>
    private void SyncPanelToCollapsedButton()
    {
        if (_panel == null || !GodotObject.IsInstanceValid(_panel)) return;
        if (_collapsedButton == null || !GodotObject.IsInstanceValid(_collapsedButton)) return;

        float width = _panel.OffsetRight - _panel.OffsetLeft;
        float height = _panel.OffsetBottom - _panel.OffsetTop;
        float right = _collapsedButton.OffsetRight;
        float top = _collapsedButton.OffsetTop;

        _panel.OffsetRight = right;
        _panel.OffsetLeft = right - width;
        _panel.OffsetTop = top;
        _panel.OffsetBottom = top + height;

        // Keep the whole panel on-screen: when the button was tucked near the left/top edge the
        // panel would otherwise grow off-screen (it extends left/down from that corner) and vanish.
        ClampToViewport(_panel);
    }

    /// <summary>
    /// Clamp a top-right-anchored control's offsets so its rect stays fully inside the viewport
    /// (with a small margin), so neither the draggable button nor the expanded panel can be lost
    /// off-screen. If the control is larger than the viewport, its top-left is pinned to the margin.
    /// Game-thread only (reads the live viewport); width/height are preserved.
    /// </summary>
    private static void ClampToViewport(Control c)
    {
        if (c == null || !GodotObject.IsInstanceValid(c)) return;

        const float margin = 8f;
        Vector2 vp = c.GetViewportRect().Size;
        // Use the real rendered size, not the offset-defined min: the panel grows to content height
        // (GrowVertical=End), so offset height understates it and a tall panel would clear the clamp.
        Vector2 size = c.GetCombinedMinimumSize();
        float w = Math.Max(c.Size.X, size.X);
        float h = Math.Max(c.Size.Y, size.Y);

        // Top-left in viewport coords (x is measured from the right edge for a right-anchored control).
        float left = vp.X + c.OffsetLeft;
        float top = c.OffsetTop;

        float maxLeft = vp.X - margin - w;
        left = maxLeft >= margin ? Math.Clamp(left, margin, maxLeft) : margin;

        float maxTop = vp.Y - margin - h;
        top = maxTop >= margin ? Math.Clamp(top, margin, maxTop) : margin;

        c.OffsetLeft = left - vp.X;
        c.OffsetRight = c.OffsetLeft + w;
        c.OffsetTop = top;
        c.OffsetBottom = top + h;
    }

    /// <summary>Show exactly one of {full panel, small collapsed button} per the collapsed flag.</summary>
    private void ApplyCollapsedState()
    {
        if (_panel != null && GodotObject.IsInstanceValid(_panel))
            _panel.Visible = !_collapsed;
        if (_collapsedButton != null && GodotObject.IsInstanceValid(_collapsedButton))
            _collapsedButton.Visible = _collapsed;
    }

    /// <summary>
    /// Make the collapsed button draggable. Hold-and-drag moves it (so it can be tucked out of the
    /// way of key info); a clean press/release with no real movement counts as a click and expands.
    /// We handle all left-button input here and swallow it, so the Button's own click never fires.
    /// While the left button is held, the viewport routes motion to this control even off-rect,
    /// so fast drags still track.
    /// </summary>
    private void OnCollapsedGuiInput(InputEvent ev)
    {
        if (_collapsedButton == null || !GodotObject.IsInstanceValid(_collapsedButton))
            return;

        const float clickSlop = 5f; // total movement under this many px is still treated as a click

        switch (ev)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                if (mb.Pressed)
                {
                    _dragging = true;
                    _dragMoved = false;
                    _dragTotal = Vector2.Zero;
                }
                else if (_dragging)
                {
                    bool moved = _dragMoved;
                    _dragging = false;
                    if (!moved)
                        OnExpand(); // a click, not a drag
                }
                _collapsedButton.AcceptEvent();
                break;

            case InputEventMouseMotion mm when _dragging:
                // Offsets are relative to the top-right anchor; shift all four by the mouse delta
                // to move the button without fighting the anchor system.
                _collapsedButton.OffsetLeft += mm.Relative.X;
                _collapsedButton.OffsetRight += mm.Relative.X;
                _collapsedButton.OffsetTop += mm.Relative.Y;
                _collapsedButton.OffsetBottom += mm.Relative.Y;
                ClampToViewport(_collapsedButton); // never let the button leave the screen
                _dragTotal += mm.Relative;
                if (_dragTotal.Length() > clickSlop)
                    _dragMoved = true;
                _collapsedButton.AcceptEvent();
                break;
        }
    }

    private void OnButtonPressed()
    {
        if (_busy)
            return;

        if (!_config.IsValid)
        {
            SetContent("请在 config.json 配置 apiKey。");
            return;
        }

        _busy = true;
        SetButtonEnabled(false);
        SetContent("思考中…");

        // Dev aid (config dumpCards=true): dump the real card DB once to ground the archetype tags.
        if (_config.DumpCards)
            GameStateReader.DumpAllCards();

        // We are already on the game thread (Godot fires Pressed there): read state inline.
        GameState state;
        try
        {
            state = GameStateReader.ReadCurrentState();
        }
        catch (Exception ex)
        {
            ModLog.Error("Reading game state failed", ex);
            SetContent("读取游戏状态失败：" + ex.Message);
            _busy = false;
            SetButtonEnabled(true);
            return;
        }

        if (state.OfferedCards.Count == 0)
        {
            SetContent("未检测到候选卡（请在选牌页打开后再试）。");
            _busy = false;
            SetButtonEnabled(true);
            return;
        }

        LogOffered(state);

        // Run the network call OFF the game thread.
        _ = Task.Run(async () =>
        {
            string text;
            try
            {
                AdviceResult result = await _advisor
                    .GetAdviceAsync(new AdviceRequest(state), CancellationToken.None)
                    .ConfigureAwait(false);
                text = FormatAdvice(result, state);
            }
            catch (Exception ex)
            {
                ModLog.Error("LLM advice request failed", ex);
                text = "获取建议失败：" + ex.Message;
            }

            // Marshal back to the game thread before touching any Godot node.
            try
            {
                await GameThread.InvokeAsync(() =>
                {
                    SetContent(text);
                    _busy = false;
                    SetButtonEnabled(true);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ModLog.Error("Marshalling advice back to game thread failed", ex);
                _busy = false;
            }
        });
    }

    private static string FormatAdvice(AdviceResult result, GameState state)
    {
        // Map offered cardId -> localized display name, so advice shows the in-game card name
        // (e.g. 战利品) instead of the raw English id (SPOILS_OF_BATTLE).
        var nameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (CardInfo c in state.OfferedCards)
        {
            if (!string.IsNullOrEmpty(c.Id) && !nameById.ContainsKey(c.Id))
                nameById[c.Id] = string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name;
        }
        // The "take no card" option comes back as cardId "SKIP" — show a localized label for it.
        bool zh = !string.IsNullOrEmpty(state.Locale) && state.Locale.Trim().ToLowerInvariant().StartsWith("zh");
        nameById["SKIP"] = zh ? "跳过(不拿任何牌)" : "Skip (take nothing)";

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.DeckSummary))
            sb.AppendLine(result.DeckSummary).AppendLine();
        if (!string.IsNullOrWhiteSpace(result.Summary))
            sb.AppendLine(result.Summary).AppendLine();

        foreach (CardAdvice c in result.Cards)
        {
            string display = c.CardId != null && nameById.TryGetValue(c.CardId, out string? localized)
                ? localized
                : c.CardId ?? "";
            string mark = c.Recommended ? "★ " : "  ";
            sb.Append(mark).Append('[').Append(c.Grade).Append("] ").AppendLine(display);
            if (!string.IsNullOrWhiteSpace(c.Reason))
                sb.Append("    ").AppendLine(c.Reason);
        }

        string text = sb.ToString().TrimEnd();
        return string.IsNullOrWhiteSpace(text) ? "(模型未返回建议)" : text;
    }

    /// <summary>Log offered card id=name pairs so we can confirm whether Name is localized.</summary>
    private static void LogOffered(GameState state)
    {
        var sb = new StringBuilder();
        foreach (CardInfo c in state.OfferedCards)
            sb.Append(c.Id).Append('=').Append(c.Name).Append("; ");
        ModLog.Info($"Offered id=name (locale '{state.Locale}'): {sb}");
    }

    private void SetContent(string text)
    {
        if (_content != null && GodotObject.IsInstanceValid(_content))
            _content.Text = text;
        ReclampPanelDeferred();
    }

    /// <summary>
    /// After the panel's content changes height (e.g. advice text arrives), re-clamp it so a panel
    /// pinned near the bottom of the screen grows UPWARD instead of pushing new text off the bottom
    /// edge. Deferred so the new text has been laid out before measuring. No-op while collapsed.
    /// </summary>
    private void ReclampPanelDeferred()
    {
        if (_collapsed || _panel == null || !GodotObject.IsInstanceValid(_panel) || !_panel.Visible)
            return;
        Callable.From(() => ClampToViewport(_panel)).CallDeferred();
    }

    private void SetButtonEnabled(bool enabled)
    {
        if (_button != null && GodotObject.IsInstanceValid(_button))
            _button.Disabled = !enabled;
    }
}
