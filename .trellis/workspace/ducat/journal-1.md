# Journal - ducat (Part 1)

> AI development session journal
> Started: 2026-06-01

---



## Session 1: STS2 AI 选牌建议 mod MVP:骨架 → 进游戏实测 → 语言自适应

**Date**: 2026-06-01
**Task**: STS2 AI 选牌建议 mod MVP:骨架 → 进游戏实测 → 语言自适应
**Branch**: `master`

### Summary

搭建并进游戏验证了 STS2 AI 选牌建议 mod 的最小纵切:ModInitializer 入口 + Harmony 钩 NCardRewardSelectionScreen.ShowScreen 拿候选卡 + 运行时读 run 状态(DebugOnlyGetState/GetMe)+ OpenAI 兼容 LLM 调用层(零三方 SDK)+ 右上角浮层「获取建议」按钮,主线程读状态、离线程调 LLM、编组回主线程渲染。provider=opencode-go/deepseek-v4-flash。实测踩坑全部修复并固化进 spec:PCK 引擎版本门禁(打包后戳回 4.5.1)、manifest 双 schema/命名、Cloudflare 浏览器 UA、reasoning 模型 max_tokens、DLL 改动需重启游戏;建议正文与候选卡名按游戏 locale(zh_Hans)自动本地化。归档 06-01-mvp-skeleton 与 00-bootstrap-guidelines。下一步:建议质量 A+B+C(真实卡牌机制 + 运行时流派检测 + 策展打法速查),数据新鲜度按层抗过期。

### Main Changes

(Add details)

### Git Commits

| Hash | Message |
|------|---------|
| `73d95d6` | (see git log) |
| `86389dd` | (see git log) |
| `47c622c` | (see git log) |
| `7608053` | (see git log) |
| `5fe22e9` | (see git log) |

### Testing

- [OK] (Add test results)

### Status

[OK] **Completed**

### Next Steps

- None - task complete


## Session 2: Advice quality: card mechanics + LLM archetype + draggable collapse; public AGPL release

**Date**: 2026-06-02
**Task**: Advice quality: card mechanics + LLM archetype + draggable collapse; public AGPL release
**Branch**: `master`

### Summary

Injected real card mechanics and LLM-inferred deck archetype into card-pick advice; added a SKIP (take-nothing) graded option. Reworked the overlay to collapse into a draggable corner button that the expanded panel follows. Published the repo publicly under AGPL-3.0 with LICENSE + THIRD_PARTY_LICENSES, and captured the new conventions (sparse tags, reflected members, char-id normalization, manifest-scan gotcha) in the spec.

### Main Changes

(Add details)

### Git Commits

| Hash | Message |
|------|---------|
| `8969cc9` | (see git log) |
| `9c1d260` | (see git log) |
| `3152f2b` | (see git log) |
| `b049681` | (see git log) |

### Testing

- [OK] (Add test results)

### Status

[OK] **Completed**

### Next Steps

- None - task complete


## Session 3: Clamp draggable collapse button + panel within viewport; grow upward when bottom-pinned

**Date**: 2026-06-02
**Task**: Clamp draggable collapse button + panel within viewport; grow upward when bottom-pinned
**Branch**: `master`

### Summary

Added ClampToViewport so the collapsed draggable button and the expanded panel stay fully on-screen (8px margin), using real rendered size (max of Size and GetCombinedMinimumSize) rather than the GrowVertical placeholder height. Re-clamp after SetContent so a bottom-pinned panel grows UPWARD when advice arrives instead of running off the bottom edge. Captured the conventions in the spec.

### Main Changes

(Add details)

### Git Commits

| Hash | Message |
|------|---------|
| `7e2694d` | (see git log) |
| `9689adc` | (see git log) |

### Testing

- [OK] (Add test results)

### Status

[OK] **Completed**

### Next Steps

- None - task complete
