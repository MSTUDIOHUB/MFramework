using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 注册、查询并校验具有明确类型的 ScriptableObject 配置项。
    /// </summary>
    /// <remarks>
    /// MConfig 不负责加载文件、保存玩家进度或更新远端数据。请先通过选定的 Unity 资源方案
    /// 加载配置资源，再将它们显式注册到这里。
    /// </remarks>
    public sealed class MConfig : IMFrameworkModule
    {
        private readonly Dictionary<Type, Dictionary<string, MConfigAsset>> _entries =
            new Dictionary<Type, Dictionary<string, MConfigAsset>>();

        /// <summary>
        /// 使用调用时指定的泛型类型注册一个配置项。
        /// </summary>
        /// <typeparam name="TConfig">调用方查询时使用的配置类型。</typeparam>
        /// <param name="config">要注册的配置项。</param>
        /// <remarks>
        /// 注册键是 <typeparamref name="TConfig"/> 与区分大小写的 ID，不是对象的运行时类型。
        /// 查询和引用校验必须使用相同类型；不同注册类型可以使用相同 ID。
        /// 注册时不执行引用校验，请在所有相关配置注册完成后调用 <see cref="ValidateAll"/>。
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> 为 null。</exception>
        /// <exception cref="MConfigRegistrationException">配置标识为空或重复。</exception>
        public void Register<TConfig>(TConfig config)
            where TConfig : MConfigAsset
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrWhiteSpace(config.Id))
            {
                throw new MConfigRegistrationException(
                    $"配置类型 {typeof(TConfig).FullName} 的 Id 为空，无法注册。");
            }

            Dictionary<string, MConfigAsset> typedEntries = GetOrCreateEntries(typeof(TConfig));
            if (typedEntries.ContainsKey(config.Id))
            {
                throw new MConfigRegistrationException(
                    $"配置类型 {typeof(TConfig).FullName} 中存在重复的配置 Id '{config.Id}'。");
            }

            typedEntries.Add(config.Id, config);
        }

        /// <summary>
        /// 注册一组配置项。
        /// </summary>
        /// <typeparam name="TConfig">调用方查询时使用的配置类型。</typeparam>
        /// <param name="configs">按照枚举顺序注册的配置项。</param>
        /// <exception cref="ArgumentNullException"><paramref name="configs"/> 为 null。</exception>
        public void RegisterRange<TConfig>(IEnumerable<TConfig> configs)
            where TConfig : MConfigAsset
        {
            if (configs == null)
            {
                throw new ArgumentNullException(nameof(configs));
            }

            foreach (TConfig config in configs)
            {
                Register(config);
            }
        }

        /// <summary>
        /// 获取一个必须存在的配置项。
        /// </summary>
        /// <typeparam name="TConfig">已经注册的配置类型。</typeparam>
        /// <param name="id">配置项的稳定标识。</param>
        /// <returns>与类型和标识匹配的配置项。</returns>
        /// <exception cref="ArgumentException"><paramref name="id"/> 为空。</exception>
        /// <exception cref="KeyNotFoundException">请求的类型或标识尚未注册。</exception>
        public TConfig Get<TConfig>(string id)
            where TConfig : MConfigAsset
        {
            TConfig config;
            if (!TryGet(id, out config))
            {
                throw new KeyNotFoundException(
                    $"未找到类型为 {typeof(TConfig).FullName}、Id 为 '{id}' 的已注册配置项。");
            }

            return config;
        }

        /// <summary>
        /// 尝试获取一个配置项。
        /// </summary>
        /// <typeparam name="TConfig">已经注册的配置类型。</typeparam>
        /// <param name="id">配置项的稳定标识。</param>
        /// <param name="config">找到配置项时，接收匹配的配置项。</param>
        /// <returns>找到配置项时返回 <see langword="true"/>。</returns>
        /// <exception cref="ArgumentException"><paramref name="id"/> 为空。</exception>
        public bool TryGet<TConfig>(string id, out TConfig config)
            where TConfig : MConfigAsset
        {
            MConfigAsset value;
            if (TryGet(typeof(TConfig), id, out value))
            {
                config = (TConfig)value;
                return true;
            }

            config = null;
            return false;
        }

        /// <summary>
        /// 返回指定配置类型下全部已注册配置项的快照。
        /// </summary>
        /// <typeparam name="TConfig">已经注册的配置类型。</typeparam>
        /// <returns>调用方可以安全枚举的快照。</returns>
        public IReadOnlyList<TConfig> GetAll<TConfig>()
            where TConfig : MConfigAsset
        {
            Dictionary<string, MConfigAsset> typedEntries;
            if (!_entries.TryGetValue(typeof(TConfig), out typedEntries))
            {
                return Array.Empty<TConfig>();
            }

            List<TConfig> result = new List<TConfig>(typedEntries.Count);
            foreach (MConfigAsset entry in typedEntries.Values)
            {
                result.Add((TConfig)entry);
            }

            return result;
        }

        /// <summary>
        /// 在所有相关配置注册完成后，执行每个已注册配置项的校验入口并汇总错误。
        /// </summary>
        /// <returns>全部已报告校验错误的快照。</returns>
        public IReadOnlyList<MConfigValidationError> ValidateAll()
        {
            List<MConfigValidationError> errors = new List<MConfigValidationError>();

            foreach (KeyValuePair<Type, Dictionary<string, MConfigAsset>> typedGroup in _entries)
            {
                foreach (KeyValuePair<string, MConfigAsset> entry in typedGroup.Value)
                {
                    if (entry.Value == null)
                    {
                        errors.Add(new MConfigValidationError(
                            typedGroup.Key,
                            entry.Key,
                            "已注册的配置资产已被销毁或丢失。"));
                        continue;
                    }

                    MConfigValidationContext context =
                        new MConfigValidationContext(typedGroup.Key, entry.Key, errors, this);
                    entry.Value.Validate(context);
                }
            }

            return errors;
        }

        /// <summary>移除全部已注册配置项。</summary>
        public void Clear()
        {
            _entries.Clear();
        }

        /// <summary>配置项采用显式注册，因此初始化时不执行任何操作。</summary>
        /// <param name="app">持有该模块的框架应用。</param>
        public void Initialize(MFrameworkApp app)
        {
        }

        /// <summary>清除全部已注册配置项。</summary>
        public void Shutdown()
        {
            Clear();
        }

        internal bool TryGet(Type configType, string id, out MConfigAsset config)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("配置项 Id 不能为空或纯空白字符。", nameof(id));
            }

            Dictionary<string, MConfigAsset> typedEntries;
            if (_entries.TryGetValue(configType, out typedEntries) &&
                typedEntries.TryGetValue(id, out config))
            {
                return true;
            }

            config = null;
            return false;
        }

        private Dictionary<string, MConfigAsset> GetOrCreateEntries(Type configType)
        {
            Dictionary<string, MConfigAsset> typedEntries;
            if (!_entries.TryGetValue(configType, out typedEntries))
            {
                typedEntries = new Dictionary<string, MConfigAsset>(StringComparer.Ordinal);
                _entries.Add(configType, typedEntries);
            }

            return typedEntries;
        }
    }
}
