# Third-Party Notices

MFramework 的源码与文档采用 [Apache License 2.0](LICENSE.md)，Copyright 2026 MSTUDIO。第三方内容的来源与适用许可证列于下方。

MFramework depends on Unity Addressables and Unity UGUI. Their licenses and notices are distributed with the corresponding Unity packages and editor installation.

## PCG 随机算法

`Runtime/Foundation/Random/MRandom.cs` 的 PCG-XSH-RR 64/32 核心、播种与无偏区间抽样依据 [PCG C minimal implementation](https://github.com/imneme/pcg-c-basic)，原作 Copyright 2014 Melissa O'Neill，采用 Apache License 2.0。

MSTUDIO 于 2026 年改写为 C#，增加显式状态版本与恢复校验、命名流派生、53 位 double 抽样、中文说明及框架接口。PCG 官方固定输出向量用于兼容性测试。完整许可证见 [ThirdParty~/PCG/LICENSE.txt](ThirdParty~/PCG/LICENSE.txt)。请在再分发时保留上述原作者署名、修改说明及许可证。
