# 可恢复随机源

`MRandom` 提供固定算法、可捕获和恢复的随机流。它位于不依赖 UnityEngine 的 `MStudio.MFramework.Foundation` 程序集；游戏规则可以只依赖 `IMRandomSource`，在构造时接收随机来源。

```csharp
MRandom loot = MRandom.CreateStream(12345UL, "loot");
MRandom visual = MRandom.CreateStream(12345UL, "visual");

int index = loot.NextInt(10);        // 0 到 9，包含 0，不包含 10。
int offset = loot.NextInt(-3, 4);    // -3 到 3。
double chance = loot.NextDouble(); // [0, 1)，不会取到 1。
uint bits = visual.NextUInt();     // 完整 32 位无符号整数。
```

## 算法与调用约定

当前算法标识为 `pcg-xsh-rr-64-32`，状态版本为 `1`。实现使用 64 位状态的 PCG-XSH-RR 32 位输出，明确按照模 2^64 推进，即使宿主启用整数溢出检查也保持序列一致。`new MRandom(seed, sequence)` 按标准 PCG 播种规则初始化；`sequence` 仅接受 63 位流编号，默认值为 54，不会静默截断超范围输入。

测试直接使用 [PCG Minimal C 官方示例](https://www.pcg-random.org/using-pcg-c-basic.html) 中 `seed=42`、`sequence=54` 的固定向量：`A15C02B7 7B47F409 BA1D3330 83D2F293 BFA4784B CBED606E`。它验证的是已有参考值，并非让两个相同实现互相比较。

`NextInt` 使用拒绝采样消除取模偏差，因此一次整数抽样可能消耗多个底层输出。上下界相等、上界小于下界或单参数上界不大于零都会抛出 `ArgumentOutOfRangeException`，且不会推进状态。单值区间合法，例如 `NextInt(5, 6)` 返回 5，但仍会推进状态。

`NextDouble` 固定消耗两个输出，分别取 27 位和 26 位组成 53 位整数，除以 2^53，范围为 `[0, 1)`。相同状态与相同调用顺序才保证继续相同序列；把一次整数调用改成浮点调用，或更改调用次数，都会改变后续结果。

版本同时约束状态布局、播种、区间抽样、双精度映射和命名流派生规则。修改这些规则时必须提供明确的兼容策略，不应沿用当前版本悄悄改变结果。

## 独立命名流

`CreateStream(masterSeed, streamName)` 是静态工厂，不需要父生成器，也不会消耗任何已有流。名称按 UTF-16 码元的小端字节做固定 FNV-1a 64 位散列；派生种子为 `masterSeed XOR hash`，流编号为 `hash >> 1`。不使用 `string.GetHashCode`，不依赖机器字节序和运行时散列设置。

名称区分大小写，不裁剪、不做 Unicode 规范化；应用应把稳定名称当作存档协议的一部分。空名称及全空白名称会抛出 `ArgumentException`。相同种子和名称每次都会创建从起点开始的新实例，恢复已有进度时应使用保存的状态。

地图、战斗、掉落、强化与表现可以分别持有一个流。创建顺序以及消费表现随机数不会推进掉落流。这里的“独立”指没有共享可变状态：64 位散列存在碰撞可能，不承诺名称唯一映射、序列绝不重叠或密码学独立。

## 捕获、保存与恢复

```csharp
MRandom random = MRandom.CreateStream(12345UL, "loot");
MRandomState before = random.CaptureState();

// 在候选状态上生成结果，尚未改变正式会话的随机流。
MRandom candidate = new MRandom(before);
int result = candidate.NextInt(100);
MRandomState after = candidate.CaptureState();

// 由应用把 result、after、业务状态和命令回执放入同一个待提交快照。
// 只有保存协调器确认提交成功后，正式会话才采用 after。
// random.RestoreState(after);
```

`MRandomState` 是标记为 `[Serializable]` 的值类型，通过公开字段保存 `Algorithm`、`Version`、`State` 和 `Increment`。捕获不会消耗随机数，修改捕获副本也不会修改生成器。序列化器由应用选择：必须无损往返保存 `ulong`，不能将其转换为 JavaScript Number 或其他 53 位精度浮点数；文本格式需要时可用十进制字符串承载，再精确还原。

`new MRandom(state)` 和 `RestoreState(state)` 校验算法、版本和奇数增量。默认状态、未知算法、旧版或未来版本以及偶数增量会被拒绝，现有流保持不变。内部状态为零合法，不能当作“未初始化”。此入口不自动降级为新种子，也不检查存档完整性；后者由保存封装负责。

保存随机状态本身不保证“不重抽”。固定产物、消费后的随机状态、材料变化和请求回执必须处于同一提交边界。结果确认丢失时先核对持久化提交，不能直接重新执行抽样；读取已有结果时直接使用该结果。备份回退会回退随机状态，不构成防回档机制。

## 职责与限制

- 概率、保底、掉落表、属性区间和敌人选招策略属于游戏规则；当前模块不提供通用加权策略或 RPG 规则。
- 实例不是线程安全的，由调用方串行访问；不同会话分别持有实例。
- 这是非密码学伪随机数，不能生成密码、令牌或承担防作弊与服务器权威职责。
- 固定整数算法不等于整个游戏跨平台确定：浮点规则、集合迭代顺序和命令顺序仍需应用自己的验证。

EditMode 测试覆盖标准向量、状态恢复、拒绝采样、完整整数区间、双精度映射、无效状态不推进及命名流隔离。宿主接入应继续验证具体序列化器的无损往返，并把随机状态与实际保存快照联合测试。
