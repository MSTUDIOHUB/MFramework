---
title: Audio 模块
---

# Audio：一条 BGM 与有上限的音效

## 从项目问题开始

场景脚本各自创建 `AudioSource`，容易造成 BGM 叠播、短音效无限并发和音量设置不一致。`MAudio` 使用一个宿主对象，创建一条循环 BGM 声道和固定数量的 SFX 声道。

## 最小示例

```csharp
private static readonly MAudioId BattleBgm = new MAudioId("bgm.battle");
private static readonly MAudioId HitSfx = new MAudioId("sfx.hit");

// 先用稳定 ID 注册外部 Clip，再按同一个 ID 播放；MAudio 不拥有 Clip 的资源 Lease。
MAudio audio = new MAudio(audioHost, maxConcurrentSfx: 8);
audio.Register(BattleBgm, battleClip);
audio.Register(HitSfx, hitClip, minimumIntervalSeconds: 0.05f, maxConcurrentInstances: 3);

audio.PlayBgm(BattleBgm);
audio.PlaySfx(HitSfx, volumeScale: 0.8f);
```

## 当前能力

- 分别设置 `MusicVolume`、`SfxVolume`、`MusicMuted` 与 `SfxMuted`；
- 重复请求同一 BGM 时默认不重启；
- SFX 同时受全局声道数、单 ID 并发数和最短播放间隔约束；
- `Pause` 与 `Resume` 只恢复由当前服务暂停的声音；
- `Dispose` 停止播放、清空注册并移除服务创建的 `AudioSource`。

## 资源边界

`MAudio` 不加载也不释放 `AudioClip`。如果 Clip 由 Addressables 加载，调用方拥有对应的 `MAssetScope` 或 `MAssetLease<AudioClip>`：它必须覆盖注册和播放的完整期间；先停止并注销音频，再释放 Lease。不要把只在局部 `using` 中存活的 Lease 所含 Clip 注册给一个更长寿命的 `MAudio`。

`MAudio` 可以直接注册到 `MFrameworkApp`，框架关闭时会调用它的 `Shutdown`。宿主 GameObject 仍由调用方拥有；宿主意外先销毁后，停止和释放操作保持幂等，其他播放操作会用明确异常提示生命周期错误。

当前实现没有淡入淡出、AudioMixer 路由、3D 音效位置或复杂优先级。Demo 未证明需要前，不应提前加入这些能力。
