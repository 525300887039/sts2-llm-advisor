# 浮层收起小按钮拖动加屏幕边界限制

## Goal

给浮层收起后的可拖动小按钮("AI ▸")加屏幕内边界限制,避免被拖出可视区后**找不回来**。当前拖动无任何边界约束,可一路拖到屏幕外丢失(虽重启游戏会复位到右上角默认位,但当局内无法找回)。

## What I already know

- 小按钮 `_collapsedButton` 与面板 `_panel` 都锚定右上角(`AnchorLeft=AnchorRight=1`,`AnchorTop=AnchorBottom=0`),四个 `Offset*` 即相对视口右上角的位移。
- 拖动逻辑在 `src/Sts2AiAdvisor/Ui/AdvisorOverlay.cs` 的 `OnCollapsedGuiInput`:`InputEventMouseMotion` 时把 `mm.Relative.X/Y` 直接累加到 `OffsetLeft/Right/Top/Bottom`,**无边界检查**。
- 展开时 `SyncPanelToCollapsedButton()` 把 `_panel` 的右上角 offset 对齐到小按钮的 offset(面板保持原宽高,向左、向下展开)。
- 视口尺寸可由 `GetViewport().GetVisibleRect().Size` 或控件的 `GetViewportRect()` 取得(游戏线程内)。

## Assumptions (temporary)

- 边界以"控件完整保留在视口内"为准,留一个小边距(默认 8px)。
- 拖动时**实时**夹紧(拖不出界),比松手后回弹体验更顺。

## Open Questions

- (已解决)范围:**两者都夹** —— 小按钮拖动时夹、展开时面板也夹进视口。见 Decision。

## Requirements

- 拖动小按钮时,按钮矩形始终完整保留在视口内(含小边距 8px),拖不出界。
- **展开时**把 `_panel` 整体夹进视口:`SyncPanelToCollapsedButton` 对齐后,若面板任一边出界,则平移其 offset 使其完整可见。
- 视口尺寸变化(窗口缩放/分辨率切换)时不至于把按钮/面板留在界外(至少下次拖动/展开会被夹回)。

## Acceptance Criteria

- [ ] 把小按钮往任意方向(尤其左、上、下、右四角)拖到底,按钮始终完整可见、不丢失。
- [ ] 在屏幕**左上角附近**收起→展开,完整面板仍整体可见(不向左/上跑出屏幕)。
- [ ] 夹紧后点击小按钮仍能正常展开;展开跟随位置的行为不被破坏。
- [ ] `dotnet build -c Debug` 0 警告 0 错误。

## Definition of Done

- 进游戏实测:四个方向/四角拖动均不丢按钮。
- 构建 0/0;改动仅限 `AdvisorOverlay.cs`。
- 若行为有新约定,按 3.3 更新 spec。

## Decision (ADR-lite)

**Context**:小按钮夹在视口内即可防丢,但面板展开时右上角跟随按钮,按钮靠左/上边时面板会向左下凸出 400px+ 而隐形,只夹按钮不彻底。

**Decision**:两者都夹 —— 复用同一个 `ClampToViewport` 助手:拖动后夹小按钮,`SyncPanelToCollapsedButton` 末尾夹面板。

**Consequences**:多几行代码,但"任何元素都不会跑到屏幕外"语义完整。面板被夹后其右上角可能不再精确等于按钮位置(贴边时让位于"完整可见"),可接受。

## Out of Scope

- 拖动位置持久化到磁盘(重启游戏仍复位到默认右上角,本任务不改)。
- 面板标题栏自身的拖动(本任务只针对收起态小按钮;若决定连面板一起夹,仅夹"展开落点",不新增面板拖动)。
- 防抖/缓存、遗物/事件建议等其他 backlog 项。

## Technical Notes

- 夹紧数学(右上角锚):设视口宽 W、高 H、边距 m。按钮宽 bw、高 bh(由 offset 决定,`bw=OffsetRight-OffsetLeft`、`bh=OffsetBottom-OffsetTop`)。
  - 水平:右边缘 `W+OffsetRight` 需 `≤ W-m` ⇒ `OffsetRight ≤ -m`;左边缘 `W+OffsetLeft` 需 `≥ m` ⇒ `OffsetLeft ≥ m-W`。夹 `OffsetRight∈[m-W+bw, -m]`,再令 `OffsetLeft=OffsetRight-bw`。
  - 垂直:`OffsetTop∈[m, H-m-bh]`,`OffsetBottom=OffsetTop+bh`。
- 实现位置:在 `OnCollapsedGuiInput` 累加 offset 后,调用一个 `ClampToViewport(control)` 助手再赋值;展开路径若纳入范围,则在 `SyncPanelToCollapsedButton` 末尾对 `_panel` 同样夹一次。
- 线程:`GuiInput`/展开都在游戏主线程触发,直接读视口尺寸安全。
