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
