using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 使用 Unity JsonUtility 的快照序列化适配器；T 必须是符合 Unity 字段序列化规则的普通可序列化类。
    /// 不支持直接保存 MonoBehaviour、ScriptableObject、字典或任意多态对象图；业务约束需另行验证。
    /// </summary>
    /// <typeparam name="T">带 Serializable 标记的普通快照类。</typeparam>
    public sealed class MJsonUtilitySaveSerializer<T> : IMSaveSerializer<T> where T : class
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly Action<T> _validate;

        /// <summary>创建适配器，并拒绝 JsonUtility 明确不能完整表示的根对象类型。</summary>
        /// <param name="validate">可选业务校验器，在写入前与读取后执行；无效数据应抛出异常。</param>
        /// <exception cref="ArgumentException">T 不是带 Serializable 标记的具体普通数据类，或属于明显不支持的根类型。</exception>
        public MJsonUtilitySaveSerializer(Action<T> validate = null)
        {
            Type type = typeof(T);
            if (type == typeof(string) || type.IsArray || type.IsAbstract ||
                typeof(UnityEngine.Object).IsAssignableFrom(type) || typeof(Delegate).IsAssignableFrom(type) ||
                typeof(IDictionary).IsAssignableFrom(type) || typeof(IList).IsAssignableFrom(type) ||
                !Attribute.IsDefined(type, typeof(SerializableAttribute), false))
                throw new ArgumentException("快照根类型必须是带 Serializable 标记的具体普通数据类，不能是字符串、集合、委托或 UnityEngine.Object。", nameof(T));
            _validate = validate;
        }

        /// <inheritdoc />
        public byte[] Serialize(T value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            _validate?.Invoke(value);
            return Utf8.GetBytes(JsonUtility.ToJson(value));
        }

        /// <inheritdoc />
        public T Deserialize(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            T value = JsonUtility.FromJson<T>(Utf8.GetString(payload));
            if (value == null) throw new InvalidOperationException("JSON 未生成有效快照对象。");
            _validate?.Invoke(value);
            return value;
        }
    }
}
