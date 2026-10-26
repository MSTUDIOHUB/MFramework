---
title: Save 可靠本地存档
---

# Save：可靠的完整快照存档

`MSave<T>` 提供同步的本地完整快照保存、SHA-256 完整性校验、结构迁移、备份恢复与可选的并发更新保护（CAS）。基础实现位于 `MStudio.MFramework.Foundation`，不依赖 Unity；`MJsonUtilitySaveSerializer<T>` 是 Runtime 程序集中的 Unity 适配器。

## 能力与边界

- 每次保存一个完整 `T`，避免分别写入金币、装备和随机状态导致部分更新。
- `schemaVersion`、`contentVersion`、`Revision`、`CommitId`、长度及业务 payload 一起受 SHA-256 摘要保护。
- 同时提供**面向独立游戏的便捷入口**（`SaveOrOverwrite`、`RestoreBackup(slot)`）与**面向严谨并发校验的 CAS 入口**（`Save(slot, value, expected)`、`LoadBackup` + `RestoreBackup(token)`）。
- 正常更新保留前一份有效主档为备份。主档损坏、主档缺失或版本更高都不会触发静默回档。
- 迁移只修改内存副本，完整迁移并验证后才返回业务对象；不会自动回写旧档。

## 方式一：独立游戏便捷接入（推荐新手与单机小项目）

如果你不需要在业务层手动维护 `MSaveMetadata` 并发凭据，可以直接使用 `SaveOrOverwrite` 与 `RestoreBackup(slot)`：

```csharp
using System;
using System.IO;
using MFramework;
using UnityEngine;

[Serializable]
public sealed class PlayerSnapshot
{
    public int Gold;
    public MRandomState LootRandomState;
}

var storage = new MFileSaveStorage(Path.Combine(Application.persistentDataPath, "Saves"));
var serializer = new MJsonUtilitySaveSerializer<PlayerSnapshot>();
var saves = new MSave<PlayerSnapshot>(storage, serializer, schemaVersion: 1);

// 1. 直接保存或覆盖更新（内部自动递增修订号并保留上一份好主档为 .bak 备份）
MSaveResult<PlayerSnapshot> saveResult = saves.SaveOrOverwrite("player_01", new PlayerSnapshot
{
    Gold = 100,
    LootRandomState = MRandom.CreateStream(42UL, "loot").CaptureState()
});

// 2. 读取存档
MSaveResult<PlayerSnapshot> loaded = saves.Load("player_01");
if (loaded.Succeeded)
{
    Debug.Log($"当前金币：{loaded.Value.Gold}");
}
else if (loaded.Status == MSaveStatus.Corrupt || loaded.Status == MSaveStatus.RecoveryRequired)
{
    // 3. 主档损坏或丢失时，可一行代码将备份恢复为主档
    MSaveResult<PlayerSnapshot> restored = saves.RestoreBackup("player_01");
}
```

## 方式二：带 CAS 并发凭据与两步确认恢复的严谨接入

在需要严格防止旧状态覆盖新状态、或在恢复备份前先向玩家展示将要回退的进度时，使用 `Save(slot, value, expected)` 与 `LoadBackup` + `RestoreBackup(token)`：

```csharp
MSaveResult<PlayerSnapshot> loaded = saves.Load("player_01");
if (loaded.Status == MSaveStatus.NotFound)
{
    // expected 默认为 null，仅允许在主档和备份都不存在时创建全新存档
    MSaveResult<PlayerSnapshot> created = saves.Save("player_01", initialSnapshot);
}
else if (loaded.Succeeded)
{
    // 更新时传入 loaded.Metadata 进行 CAS 校验（同时比对 Revision 与 CommitId）
    MSaveResult<PlayerSnapshot> written = saves.Save("player_01", nextSnapshot, loaded.Metadata);
}
```

两步确认恢复备份：

```csharp
MSaveResult<PlayerSnapshot> candidate = saves.LoadBackup("player_01");
if (candidate.Succeeded)
{
    // 展示 candidate.Value 供玩家确认后，传入绑定了双指纹的 RecoveryToken 执行恢复
    MSaveResult<PlayerSnapshot> restored = saves.RestoreBackup(candidate.RecoveryToken);
}
```

## 结果状态速查表

| 状态 | 含义 | 应用应如何处理 |
| --- | --- | --- |
| `Success` | 操作已经确定成功 | 使用 `Value` 和新 `Metadata` |
| `NotFound` | 指定目标确实不存在 | Load 主档时可创建新档；LoadBackup 时表示没有备份 |
| `RecoveryRequired` | 主档不存在，但备份存在 | 调用 `RestoreBackup` 或 `LoadBackup` 恢复，不得当新档直接覆盖 |
| `Corrupt` | 完整性、信封或当前 payload 验证失败 | 保留原档，提示玩家恢复备份 |
| `UnsupportedVersion` | 未来结构/信封版本，或缺少迁移步骤 | 禁止覆盖，提示升级客户端或补充迁移器 |
| `Conflict` | 主档提交凭据或恢复指纹已变化 | 重新 `Load` 读取最新状态 |
| `IoFailure` | 读取故障或发布前写入失败 | 检查目录、权限、空间和大小上限；旧提交未受影响 |
| `UnsupportedStorage` | 平台缺少所需原子文件替换能力 | 改用适配目标平台的 `IMSaveStorage` 实现 |
| `CommitUnknown` | 发布阶段失去确定结果 | 重新 `Load` 核对 `CommitId` 和快照内业务回执 |
| `SerializationFailure` | 新数据无法序列化或回读验证 | 修正快照或序列化器，原存档未写入 |
| `MigrationFailure` | 迁移抛异常、返回 null 或迁移结果无法读取 | 修复迁移实现，磁盘原档未变化 |

## 结构迁移

`schemaVersion` 是快照字段结构版本；迁移步骤从 `FromSchemaVersion` 严格前进到 `FromSchemaVersion + 1`：

```csharp
public sealed class UpgradeV1ToV2 : IMSaveMigration
{
    public int FromSchemaVersion => 1;

    public byte[] Migrate(byte[] payload)
    {
        return ConvertOldPayloadToVersion2(payload);
    }
}
```

## 文件布局与进阶防御

每个槽在磁盘上对应：
- `player_01.msave`：当前完整主档
- `player_01.msave.bak`：上一次正常更新保留的有效主档
- `player_01.<Guid>.tmp`：发布前写入的临时文件
