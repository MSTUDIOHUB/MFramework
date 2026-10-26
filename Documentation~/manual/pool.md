---
title: Pool 模块
---

# Pool：复用高频 GameObject

## 从项目问题开始

防御游戏会频繁创建敌人、佣兵、投射物和特效。不断 `Instantiate` 与 `Destroy` 会产生尖峰，也容易让对象重生时保留旧状态。`MPool` 统一注册、预热、取出、归还与清理流程。

闲置实例的实际存储、`Get/Release/Clear`、重复归还检查和最大容量由 Unity 2022.3 自带的
`UnityEngine.Pool.ObjectPool<GameObject>` 完成。MFramework 没有重写这只轮子，只增加多预制体 Key、
实例归属校验、Hierarchy 整理与 `IMPooledGameObject` 生命周期。

## 最小示例

```csharp
private static readonly MPoolKey EnemyKey = new MPoolKey("enemy.goblin");

// 使用稳定 Key 完成注册后才能预热和 Spawn；取出的实例仍归这个 pool 管理。
MPool pool = new MPool(inactiveRoot);
pool.Register(EnemyKey, enemyPrefab, maxInactive: 24);
pool.Prewarm(EnemyKey, 8);

GameObject enemy = pool.Spawn(
    EnemyKey,
    spawnPoint.position,
    Quaternion.identity);

// 使用结束后归还给创建它的同一个池，让实例进入下一次复用。
pool.Despawn(enemy);
```

需要在重生时重置状态的组件实现 `IMPooledGameObject`。`OnSpawned` 在激活并设置位置后调用，`OnDespawned` 在停用或销毁前调用。

## 所有权规则

- Key 使用区分大小写的稳定字符串；不要使用会被改名的场景显示名。
- `Despawn` 只能接收同一个 `MPool` 创建且当前处于活动状态的对象。
- `maxInactive` 为零表示不限制非活动实例数量；小游戏项目通常应设置合理上限。
- `MPool` 只管理 GameObject，不是通用 C# 对象池。

实例归属追踪由内部统一处理：Unity 6.4 及更新版本使用 `GetEntityId()` 返回的完整 `EntityId`；较早的 Unity 与当前团结版本使用 `GetInstanceID()` 返回的 `int`。公开的注册、生成与回收接口相同，使用者无需转换或保存实例 ID。

## 与 Addressables 的释放顺序

若注册的 Prefab 来自 `MAssetLease<GameObject>`，必须先 `Clear` 对应池，再释放 Prefab Lease：

```text
停止生成 -> 归还活动对象 -> MPool.Clear -> MAssetLease.Dispose
```

否则池仍可能持有已经被 Addressables 释放的 Prefab。
