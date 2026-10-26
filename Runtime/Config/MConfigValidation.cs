using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 描述一条配置校验错误。
    /// </summary>
    public readonly struct MConfigValidationError
    {
        /// <summary>
        /// 创建一条配置校验错误。
        /// </summary>
        /// <param name="configType">配置类型。</param>
        /// <param name="configId">配置项标识；无法取得时可为空。</param>
        /// <param name="message">面向使用者的问题说明。</param>
        public MConfigValidationError(Type configType, string configId, string message)
            : this(configType, configId, null, message)
        {
        }

        /// <summary>
        /// 创建带来源字段路径的配置校验错误。
        /// </summary>
        /// <param name="configType">发生错误的来源配置注册类型。</param>
        /// <param name="configId">来源配置的注册 ID。</param>
        /// <param name="fieldPath">来源字段路径，例如 items[2].templateId；配置整体错误可传 null。</param>
        /// <param name="message">面向使用者的问题说明。</param>
        public MConfigValidationError(Type configType, string configId, string fieldPath, string message)
        {
            ConfigType = configType;
            ConfigId = configId;
            FieldPath = fieldPath;
            Message = message;
        }

        /// <summary>获取配置类型。</summary>
        public Type ConfigType { get; }

        /// <summary>获取配置项标识。</summary>
        public string ConfigId { get; }

        /// <summary>获取来源字段路径；未指定字段时为 null。</summary>
        public string FieldPath { get; }

        /// <summary>获取校验信息。</summary>
        public string Message { get; }

        /// <summary>返回便于阅读的校验错误文本。</summary>
        public override string ToString()
        {
            string location = ConfigType.Name + "[" + ConfigId + "]";
            if (!string.IsNullOrWhiteSpace(FieldPath))
            {
                location += "." + FieldPath;
            }

            return location + ": " + Message;
        }
    }

    /// <summary>
    /// 收集一个配置项的校验错误，并显式查询同一注册表中的引用目标。
    /// </summary>
    public sealed class MConfigValidationContext
    {
        private readonly Type _configType;
        private readonly string _configId;
        private readonly List<MConfigValidationError> _errors;
        private readonly MConfig _configs;

        internal MConfigValidationContext(
            Type configType,
            string configId,
            List<MConfigValidationError> errors,
            MConfig configs)
        {
            _configType = configType;
            _configId = configId;
            _errors = errors;
            _configs = configs;
        }

        /// <summary>
        /// 为当前配置项添加一条整体校验错误。
        /// </summary>
        /// <param name="message">具体的问题说明和修改建议。</param>
        public void AddError(string message)
        {
            AddErrorCore(null, message);
        }

        /// <summary>
        /// 为当前来源配置的指定字段添加一条校验错误。
        /// </summary>
        /// <param name="fieldPath">字段路径，例如 options[0].labelId。</param>
        /// <param name="message">具体的问题说明和修改建议。</param>
        public void AddError(string fieldPath, string message)
        {
            ValidateFieldPath(fieldPath);
            AddErrorCore(fieldPath, message);
        }

        /// <summary>
        /// 按精确注册类型与 ID 查询引用目标，不自动报告错误。
        /// </summary>
        public bool TryResolve<TConfig>(string id, out TConfig config)
            where TConfig : MConfigAsset
        {
            MConfigAsset value;
            bool found = TryResolve(typeof(TConfig), id, out value);
            config = found ? (TConfig)value : null;
            return found;
        }

        /// <summary>
        /// 使用显式 Type 与 ID 查询引用目标，不自动报告错误。
        /// </summary>
        public bool TryResolve(Type targetType, string id, out MConfigAsset config)
        {
            ValidateTargetType(targetType);
            if (string.IsNullOrWhiteSpace(id))
            {
                config = null;
                return false;
            }

            if (_configs.TryGet(targetType, id, out config) && config != null)
            {
                return true;
            }

            config = null;
            return false;
        }

        /// <summary>
        /// 校验一个显式声明的配置引用，并将缺失信息报告到当前来源字段。
        /// </summary>
        public bool RequireReference<TConfig>(string fieldPath, string id, bool optional = false)
            where TConfig : MConfigAsset
        {
            return RequireReference(typeof(TConfig), fieldPath, id, optional);
        }

        /// <summary>
        /// 使用调用方显式提供的目标类型校验引用，并报告可定位的缺失错误。
        /// </summary>
        public bool RequireReference(Type targetType, string fieldPath, string id, bool optional = false)
        {
            ValidateTargetType(targetType);
            ValidateFieldPath(fieldPath);
            if (string.IsNullOrWhiteSpace(id))
            {
                if (optional)
                {
                    return true;
                }

                AddErrorCore(fieldPath, $"指向 {targetType.FullName} 的必填引用未填写有效 Id。");
                return false;
            }

            MConfigAsset target;
            if (TryResolve(targetType, id, out target))
            {
                return true;
            }

            AddErrorCore(
                fieldPath,
                $"未找到类型为 {targetType.FullName}、Id 为 '{id}' 的目标配置（未注册或已被销毁）。");
            return false;
        }

        private void AddErrorCore(string fieldPath, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("校验错误说明不能为空。", nameof(message));
            }

            _errors.Add(new MConfigValidationError(_configType, _configId, fieldPath, message));
        }

        private static void ValidateFieldPath(string fieldPath)
        {
            if (string.IsNullOrWhiteSpace(fieldPath))
            {
                throw new ArgumentException("校验字段路径不能为空。", nameof(fieldPath));
            }
        }

        private static void ValidateTargetType(Type targetType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            if (!typeof(MConfigAsset).IsAssignableFrom(targetType) || targetType.ContainsGenericParameters)
            {
                throw new ArgumentException("引用目标类型必须是已封闭的 MConfigAsset 派生类型。", nameof(targetType));
            }
        }
    }

    /// <summary>
    /// 报告无效注册或重复的配置标识。
    /// </summary>
    public sealed class MConfigRegistrationException : Exception
    {
        /// <summary>
        /// 创建该异常。
        /// </summary>
        /// <param name="message">注册失败的原因。</param>
        public MConfigRegistrationException(string message)
            : base(message)
        {
        }
    }
}
