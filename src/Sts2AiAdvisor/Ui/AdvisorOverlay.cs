// CanvasLayer/panel/button creation idiom adapted from sts2-advisor's OverlayManager (MIT);
// only the lightweight scaffolding was reused, not the full overlay implementation.
using System;
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
    private Label? _content;
    private Button? _button;
    private bool _busy;

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

        var title = new Label { Text = "STS2 AI Advisor" };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", ClrHeader);
        vbox.AddChild(title);

        _content = new Label
        {
            Text = "进入选牌页后点击下方按钮获取建议。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 0),
        };
        _content.AddThemeColorOverride("font_color", ClrBody);
        vbox.AddChild(_content);

        _button = new Button { Text = "获取建议" };
        _button.Pressed += OnButtonPressed;
        vbox.AddChild(_button);

        _layer.AddChild(_panel);
        tree.Root.CallDeferred("add_child", _layer);
        ModLog.Info("Advisor overlay built and attached.");
    }

    /// <summary>Make the overlay visible (call on the game thread, e.g. from the card-reward hook).</summary>
    public void Show()
    {
        EnsureBuilt();
        if (_panel != null) _panel.Visible = true;
        if (_layer != null) _layer.Visible = true;
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

        // Run the network call OFF the game thread.
        _ = Task.Run(async () =>
        {
            string text;
            try
            {
                AdviceResult result = await _advisor
                    .GetAdviceAsync(new AdviceRequest(state), CancellationToken.None)
                    .ConfigureAwait(false);
                text = FormatAdvice(result);
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

    private static string FormatAdvice(AdviceResult result)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.Summary))
            sb.AppendLine(result.Summary).AppendLine();

        foreach (CardAdvice c in result.Cards)
        {
            string mark = c.Recommended ? "★ " : "  ";
            sb.Append(mark).Append('[').Append(c.Grade).Append("] ").AppendLine(c.CardId);
            if (!string.IsNullOrWhiteSpace(c.Reason))
                sb.Append("    ").AppendLine(c.Reason);
        }

        string text = sb.ToString().TrimEnd();
        return string.IsNullOrWhiteSpace(text) ? "(模型未返回建议)" : text;
    }

    private void SetContent(string text)
    {
        if (_content != null && GodotObject.IsInstanceValid(_content))
            _content.Text = text;
    }

    private void SetButtonEnabled(bool enabled)
    {
        if (_button != null && GodotObject.IsInstanceValid(_button))
            _button.Disabled = !enabled;
    }
}
