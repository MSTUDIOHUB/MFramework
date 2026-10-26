namespace MFramework
{
    /// <summary>
    /// 可替换的完整快照后端。MSave 会在 GetSynchronizationRoot 返回的锁内完成读取、CAS 与发布。
    /// 实现不得在 Read 中隐藏错误，也不得在 Commit 中用不安全的删除后移动模拟原子替换。
    /// </summary>
    public interface IMSaveStorage
    {
        /// <summary>获取稳定的进程内槽锁；访问同一物理槽的所有后端实例必须返回同一对象。</summary>
        /// <param name="slot">经过 MSave 验证的槽名。</param>
        /// <returns>非 null 的锁对象，其生命周期必须覆盖同槽所有操作。</returns>
        object GetSynchronizationRoot(string slot);

        /// <summary>读取已发布快照的完整字节；只在目标确实不存在时返回 null。</summary>
        /// <param name="slot">槽名。</param>
        /// <param name="source">显式选择主档或备份。</param>
        /// <returns>独立字节数组，或代表不存在的 null；不得返回内部可变缓冲区。</returns>
        /// <exception cref="MSaveStorageException">存储读取失败，不能伪装成不存在。</exception>
        byte[] Read(string slot, MSaveSource source);

        /// <summary>
        /// 持久化并验证完整临时快照后一次性发布。调用方持有槽锁，且已经校验待替换主档。
        /// 正常返回代表确定发布成功；不确定是否发布必须抛出 CommitUnknown。
        /// </summary>
        /// <param name="slot">槽名。</param>
        /// <param name="snapshot">包含摘要的完整快照字节；实现不得修改此数组。</param>
        /// <param name="replaceExisting">true 时必须替换现有主档，false 时必须仅创建不存在的主档。</param>
        /// <param name="preservePrevious">true 时保留原有效主档为备份；恢复损坏主档时必须为 false，以保留有效备份。</param>
        /// <exception cref="MSaveStorageException">发布前故障、平台不支持或提交结果未知。</exception>
        void Commit(string slot, byte[] snapshot, bool replaceExisting, bool preservePrevious);
    }

    /// <summary>业务快照序列化策略；实现应验证业务所要求的数据形状，不能静默忽略损坏数据。</summary>
    /// <typeparam name="T">完整业务快照类型。</typeparam>
    public interface IMSaveSerializer<T>
    {
        /// <summary>序列化完整快照；返回 null 会作为序列化失败处理。</summary>
        /// <param name="value">业务快照；是否允许 null 由序列化器决定。</param>
        /// <returns>独立的序列化数据。</returns>
        byte[] Serialize(T value);

        /// <summary>反序列化并验证当前结构版本的数据；损坏时应抛出异常。</summary>
        /// <param name="payload">只属于本次调用的数据副本，可以修改而不影响磁盘快照。</param>
        /// <returns>业务快照。</returns>
        T Deserialize(byte[] payload);
    }

    /// <summary>单步迁移，严格从 FromSchemaVersion 转到 FromSchemaVersion + 1；不执行任何存储写入。</summary>
    public interface IMSaveMigration
    {
        /// <summary>此步骤接受的旧数据结构版本，必须大于零。</summary>
        int FromSchemaVersion { get; }

        /// <summary>在独立副本上迁移旧结构；抛出异常或返回 null 会使读取返回 MigrationFailure。</summary>
        /// <param name="payload">旧版本序列化数据的副本；可原地修改。</param>
        /// <returns>下一结构版本的序列化数据。</returns>
        byte[] Migrate(byte[] payload);
    }
}
