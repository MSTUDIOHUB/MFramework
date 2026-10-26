---
title: 使用手册
---

# 使用手册

各模块页面提供接入方法、职责边界和最小使用示例。请根据项目需要选择模块。

## 基础流程

1. [Core](core.md)：建立唯一入口，明确服务注册和生命周期顺序。
2. [Event](event.md)：让不应互相持有的系统发布和接收事实。
3. [Action](action.md)：组合延迟、等待、顺序和并行动作。
4. [FSM](state-machine.md)：表达角色或流程的互斥状态。

## Demo 支撑

1. [Addressables](addressables.md)：加载资源并明确谁负责释放。
2. [SceneFlow](scene-flow.md)：用明确阶段切换 Additive 内容场景，并在加载/激活/卸载前后执行项目命令。
3. [Pool](pool.md)：复用敌人、佣兵和特效实例。
4. [Config](config.md)：按类型和稳定 ID 查询配置，并通过字段路径定位缺失引用。
5. [Audio](audio.md)：管理一条 BGM 与有上限的音效并发。
6. [Platform](platform.md)：隔离小游戏 SDK 与普通 Unity 代码。
7. [Extensions](extensions.md)：保留少量含义明确的常用扩展。

[UGUI](ugui.md) 已提供页面、弹窗、覆盖层、统一返回和五种语义按钮的基础实现。

## 可靠数据与可复现性

1. [Save](save.md)：保存完整快照、检查提交结果、拒绝旧版本写入，显式选择备份恢复并迁移旧结构。
2. [Random](random.md)：使用可注入随机流，捕获并恢复状态，避免表现随机影响玩法序列。

存档核心与随机源属于独立 Foundation 程序集。先阅读返回值、失败行为和适用限制，再接入应用；框架不替项目定义物品、技能、结算或联网权威规则。

使用 IL2CPP、小游戏或代码裁剪时，请阅读 [IL2CPP / AOT 接入注意事项](reflection-aot.md)，并在目标设备验证自定义模块和第三方 SDK。
