using System;

namespace MFramework
{
    /// <summary>描述一次完整快照操作的结果；业务必须检查结果，不能把所有失败当作首次运行。</summary>
    public enum MSaveStatus
    {
        /// <summary>操作已完成；保存时表示后端确认发布成功。</summary>
        Success,
        /// <summary>指定的主档或备份确实不存在，不表示读权限不足或数据损坏。</summary>
        NotFound,
        /// <summary>信封、完整性摘要或反序列化内容损坏；禁止直接覆盖。</summary>
        Corrupt,
        /// <summary>信封或数据结构版本比当前实现新，或者缺少迁移步骤；禁止覆盖。</summary>
        UnsupportedVersion,
        /// <summary>期望提交凭据或恢复指纹已过期；需重新读取后由业务决定。</summary>
        Conflict,
        /// <summary>主档缺失但备份存在；必须显式检查并恢复，不能当新档创建。</summary>
        RecoveryRequired,
        /// <summary>访问或提交前 I/O 失败；内置文件后端保证未进入发布阶段。</summary>
        IoFailure,
        /// <summary>进入发布阶段后无法确定提交结果；先重新 Load 核对业务回执，不能盲目重试。</summary>
        CommitUnknown,
        /// <summary>平台明确不支持所需的持久化操作。</summary>
        UnsupportedStorage,
        /// <summary>序列化新快照失败；尚未写入存储。</summary>
        SerializationFailure,
        /// <summary>迁移步骤抛出异常或返回无效数据；原始存档保持不变。</summary>
        MigrationFailure
    }

    /// <summary>明确选择本次读取的数据来源；备份永远不会自动替代主档。</summary>
    public enum MSaveSource
    {
        /// <summary>读取当前已提交主档。</summary>
        Main,
        /// <summary>读取上一次有效主档的备份；不代表已经恢复为主档。</summary>
        Backup
    }

    /// <summary>受完整性摘要保护的存档元数据。</summary>
    public sealed class MSaveMetadata
    {
        internal MSaveMetadata(int schemaVersion, string contentVersion, long revision, Guid commitId)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Revision = revision;
            CommitId = commitId;
        }

        /// <summary>磁盘中数据结构的版本；读取迁移不会更改此原始版本。</summary>
        public int SchemaVersion { get; }
        /// <summary>应用声明的内容版本，例如配置表版本；兼容政策由应用负责。</summary>
        public string ContentVersion { get; }
        /// <summary>本槽快照修订号，从 1 开始。</summary>
        public long Revision { get; }
        /// <summary>每次发布生成的唯一提交标识；和 Revision 共同参与 CAS。</summary>
        public Guid CommitId { get; }
    }

    /// <summary>
    /// 显式读取备份时产生的恢复凭据，绑定当时主档与备份的完整字节指纹。
    /// </summary>
    public sealed class MSaveRecoveryToken
    {
        internal MSaveRecoveryToken(object owner, string slot, byte[] mainFingerprint, byte[] backupFingerprint)
        {
            Owner = owner;
            Slot = slot;
            MainFingerprint = mainFingerprint;
            BackupFingerprint = backupFingerprint;
        }

        /// <summary>此凭据绑定的槽名。</summary>
        public string Slot { get; }
        internal object Owner { get; }
        internal byte[] MainFingerprint { get; }
        internal byte[] BackupFingerprint { get; }
    }

    /// <summary>存档操作的不可变回执；只有 Succeeded 为真时才可以使用 Value。</summary>
    /// <typeparam name="T">由应用定义的完整快照类型。</typeparam>
    public sealed class MSaveResult<T>
    {
        internal MSaveResult(MSaveStatus status, T value = default, MSaveMetadata metadata = null,
            string message = "", Exception exception = null, MSaveSource source = MSaveSource.Main,
            int appliedMigrations = 0, MSaveRecoveryToken recoveryToken = null)
        {
            Status = status;
            Value = value;
            Metadata = metadata;
            Message = message;
            Exception = exception;
            Source = source;
            AppliedMigrations = appliedMigrations;
            RecoveryToken = recoveryToken;
        }

        /// <summary>明确的结果类别。</summary>
        public MSaveStatus Status { get; }
        /// <summary>是否已确定操作成功；CommitUnknown 绝不算成功。</summary>
        public bool Succeeded => Status == MSaveStatus.Success;
        /// <summary>成功时的数据；失败时为 default。</summary>
        public T Value { get; }
        /// <summary>可用的存档元数据。</summary>
        public MSaveMetadata Metadata { get; }
        /// <summary>用于诊断的中文说明。</summary>
        public string Message { get; }
        /// <summary>底层异常（如有）。</summary>
        public Exception Exception { get; }
        /// <summary>本次数据来源。</summary>
        public MSaveSource Source { get; }
        /// <summary>读取时在内存副本上执行的迁移步骤数。</summary>
        public int AppliedMigrations { get; }
        /// <summary>仅 LoadBackup 成功时提供的恢复凭据；其他结果为 null。</summary>
        public MSaveRecoveryToken RecoveryToken { get; }
    }

    /// <summary>存储后端使用的故障异常，明确区分发布前失败与提交结果未知。</summary>
    public sealed class MSaveStorageException : Exception
    {
        /// <summary>创建后端异常。已经尝试发布但结果不明确时必须使用 CommitUnknown。</summary>
        /// <param name="status">只允许 IoFailure、UnsupportedStorage 或 CommitUnknown。</param>
        /// <param name="message">故障说明。</param>
        /// <param name="innerException">底层异常，可为 null。</param>
        /// <exception cref="ArgumentOutOfRangeException">status 不是允许的存储故障。</exception>
        public MSaveStorageException(MSaveStatus status, string message, Exception innerException = null)
            : base(message, innerException)
        {
            if (status != MSaveStatus.IoFailure && status != MSaveStatus.UnsupportedStorage &&
                status != MSaveStatus.CommitUnknown)
            {
                throw new ArgumentOutOfRangeException(nameof(status), "存储异常状态只允许 IoFailure、UnsupportedStorage 或 CommitUnknown。");
            }

            Status = status;
        }

        /// <summary>调用者应采用的故障类别。</summary>
        public MSaveStatus Status { get; }
    }
}
