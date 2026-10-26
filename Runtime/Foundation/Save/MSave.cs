using System;
using System.Collections.Generic;
using System.Text;

namespace MFramework
{
    /// <summary>
    /// 同步完整快照存档服务，提供 SHA-256 完整性校验、结构迁移、备份恢复与可选的并发版本保护（CAS）。
    /// </summary>
    /// <typeparam name="T">包含业务数据及随机状态等内容的完整快照类型。</typeparam>
    public sealed class MSave<T>
    {
        private readonly IMSaveStorage _storage;
        private readonly IMSaveSerializer<T> _serializer;
        private readonly Dictionary<int, IMSaveMigration> _migrations = new Dictionary<int, IMSaveMigration>();

        /// <summary>创建存档服务；不会立即读取磁盘，也不会自动注册为全局框架模块。</summary>
        /// <param name="storage">持久化后端，不能为 null。</param>
        /// <param name="serializer">当前结构版本的序列化策略，不能为 null。</param>
        /// <param name="schemaVersion">当前业务结构版本，必须大于零。</param>
        /// <param name="contentVersion">内容版本元数据，例如配置表版本。</param>
        /// <param name="migrations">可选的逐版本迁移步骤；同一起始版本不能重复。</param>
        public MSave(
            IMSaveStorage storage,
            IMSaveSerializer<T> serializer,
            int schemaVersion = 1,
            string contentVersion = "",
            IEnumerable<IMSaveMigration> migrations = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "结构版本号必须大于或等于 1。");
            }

            if (contentVersion == null)
            {
                throw new ArgumentNullException(nameof(contentVersion));
            }

            if (new UTF8Encoding(false, true).GetByteCount(contentVersion) > MSaveEnvelope.MaximumContentVersionBytes)
            {
                throw new ArgumentException("内容版本的 UTF-8 字节长度不能超过 1024 字节。", nameof(contentVersion));
            }

            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            if (migrations == null)
            {
                return;
            }

            foreach (IMSaveMigration migration in migrations)
            {
                if (migration == null || migration.FromSchemaVersion < 1 || migration.FromSchemaVersion >= schemaVersion)
                {
                    throw new ArgumentException("迁移起始版本必须大于零并小于当前结构版本。", nameof(migrations));
                }

                if (_migrations.ContainsKey(migration.FromSchemaVersion))
                {
                    throw new ArgumentException("迁移起始版本不能重复。", nameof(migrations));
                }

                _migrations.Add(migration.FromSchemaVersion, migration);
            }
        }

        /// <summary>当前业务结构版本；读取到更高版本时拒绝覆盖。</summary>
        public int SchemaVersion { get; }

        /// <summary>后续 Save 使用的内容版本。</summary>
        public string ContentVersion { get; }

        /// <summary>读取主档并在内存副本上按需执行版本迁移。</summary>
        /// <param name="slot">1 至 64 个 ASCII 字母、数字、下划线或短横线组成的槽名。</param>
        /// <returns>包含数据、元数据或明确故障状态的回执；主档缺失而备份存在时返回 RecoveryRequired。</returns>
        public MSaveResult<T> Load(string slot)
        {
            MSaveSlot.Validate(slot);
            lock (_storage.GetSynchronizationRoot(slot))
            {
                try
                {
                    byte[] main = _storage.Read(slot, MSaveSource.Main);
                    if (main == null && _storage.Read(slot, MSaveSource.Backup) != null)
                    {
                        return Failure(MSaveStatus.RecoveryRequired, "主档缺失但备份存在，请显式读取或恢复备份。");
                    }

                    return Decode(main, MSaveSource.Main);
                }
                catch (Exception exception)
                {
                    return StorageFailure(exception, false);
                }
            }
        }

        /// <summary>
        /// 显式读取备份并签发恢复凭据，不修改任何磁盘文件。
        /// </summary>
        /// <param name="slot">合法槽名。</param>
        /// <returns>Source 为 Backup 的回执；成功时可调用 <see cref="RestoreBackup(string)"/> 或 <see cref="RestoreBackup(MSaveRecoveryToken)"/> 恢复。</returns>
        public MSaveResult<T> LoadBackup(string slot)
        {
            MSaveSlot.Validate(slot);
            lock (_storage.GetSynchronizationRoot(slot))
            {
                try
                {
                    byte[] main = _storage.Read(slot, MSaveSource.Main);
                    CheckRecoverableMain(main);
                    byte[] backup = _storage.Read(slot, MSaveSource.Backup);
                    MSaveResult<T> result = Decode(backup, MSaveSource.Backup);
                    if (!result.Succeeded)
                    {
                        return result;
                    }

                    return new MSaveResult<T>(
                        MSaveStatus.Success,
                        result.Value,
                        result.Metadata,
                        "已读取备份，尚未恢复主档。",
                        source: MSaveSource.Backup,
                        appliedMigrations: result.AppliedMigrations,
                        recoveryToken: new MSaveRecoveryToken(
                            this,
                            slot,
                            MSaveEnvelope.Fingerprint(main),
                            MSaveEnvelope.Fingerprint(backup)));
                }
                catch (MSaveFormatException exception)
                {
                    return Failure(exception.Status, exception.Message, exception, MSaveSource.Backup);
                }
                catch (Exception exception)
                {
                    return StorageFailure(exception, false, source: MSaveSource.Backup);
                }
            }
        }

        /// <summary>
        /// 直接保存或覆盖指定槽位的完整快照（适合单人独立游戏的便捷保存入口，无需手动传递上一版本的 Metadata）。
        /// </summary>
        /// <param name="slot">合法槽名。</param>
        /// <param name="value">要保存的完整快照。</param>
        /// <returns>保存结果回执。</returns>
        public MSaveResult<T> SaveOrOverwrite(string slot, T value)
        {
            MSaveSlot.Validate(slot);
            lock (_storage.GetSynchronizationRoot(slot))
            {
                try
                {
                    byte[] main = _storage.Read(slot, MSaveSource.Main);
                    long currentRevision = 0;
                    if (main == null)
                    {
                        if (_storage.Read(slot, MSaveSource.Backup) != null)
                        {
                            return Failure(MSaveStatus.RecoveryRequired, "备份存在，不能把缺失的主档当作新槽覆盖。");
                        }
                    }
                    else
                    {
                        MSaveResult<T> current = Decode(main, MSaveSource.Main);
                        if (!current.Succeeded)
                        {
                            return current;
                        }

                        currentRevision = current.Metadata.Revision;
                    }

                    return Publish(slot, value, currentRevision + 1, ContentVersion, main != null, main != null);
                }
                catch (Exception exception)
                {
                    return StorageFailure(exception, false);
                }
            }
        }

        /// <summary>
        /// 将完整快照作为下一修订发布。默认 expected=null 仅允许创建全新槽；
        /// 更新已有存档时传入最近一次 Load 或 Save 成功结果的 Metadata（乐观并发控制）。
        /// </summary>
        /// <param name="slot">合法槽名。</param>
        /// <param name="value">应用已经构造完成的完整快照。</param>
        /// <param name="expected">预期主档的元数据凭据；null 表示创建新槽。</param>
        /// <returns>成功、冲突或明确失败回执。</returns>
        public MSaveResult<T> Save(string slot, T value, MSaveMetadata expected = null)
        {
            MSaveSlot.Validate(slot);
            lock (_storage.GetSynchronizationRoot(slot))
            {
                try
                {
                    byte[] main = _storage.Read(slot, MSaveSource.Main);
                    long currentRevision = 0;
                    Guid currentCommitId = Guid.Empty;
                    if (main == null)
                    {
                        if (_storage.Read(slot, MSaveSource.Backup) != null)
                        {
                            return Failure(MSaveStatus.RecoveryRequired, "备份存在，不能把缺失的主档当作新槽覆盖。");
                        }
                    }
                    else
                    {
                        MSaveResult<T> current = Decode(main, MSaveSource.Main);
                        if (!current.Succeeded)
                        {
                            return current;
                        }

                        currentRevision = current.Metadata.Revision;
                        currentCommitId = current.Metadata.CommitId;
                    }

                    if (currentRevision != (expected?.Revision ?? 0) || currentCommitId != (expected?.CommitId ?? Guid.Empty))
                    {
                        return Failure(MSaveStatus.Conflict, "主档提交凭据已变化，请重新读取。");
                    }

                    return Publish(slot, value, currentRevision + 1, ContentVersion, main != null, main != null);
                }
                catch (Exception exception)
                {
                    return StorageFailure(exception, false);
                }
            }
        }

        /// <summary>
        /// 便捷方法：直接读取指定槽位的备份并将其恢复为主档（适合无需两步确认的独立游戏流程）。
        /// </summary>
        /// <param name="slot">合法槽名。</param>
        /// <returns>恢复结果回执。</returns>
        public MSaveResult<T> RestoreBackup(string slot)
        {
            MSaveResult<T> backup = LoadBackup(slot);
            if (!backup.Succeeded)
            {
                return backup;
            }

            return RestoreBackup(backup.RecoveryToken);
        }

        /// <summary>
        /// 使用 <see cref="LoadBackup"/> 签发的恢复凭据，将已检查的备份恢复为新的主档修订，同时保留该有效备份。
        /// </summary>
        /// <param name="token">本实例最近一次 LoadBackup 成功结果提供的恢复凭据。</param>
        /// <returns>恢复成功或明确故障。</returns>
        public MSaveResult<T> RestoreBackup(MSaveRecoveryToken token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (!ReferenceEquals(token.Owner, this))
            {
                throw new ArgumentException("恢复凭据只能交还签发它的存档服务实例。", nameof(token));
            }

            lock (_storage.GetSynchronizationRoot(token.Slot))
            {
                try
                {
                    byte[] main = _storage.Read(token.Slot, MSaveSource.Main);
                    byte[] backup = _storage.Read(token.Slot, MSaveSource.Backup);
                    if (!MSaveEnvelope.Equal(token.MainFingerprint, MSaveEnvelope.Fingerprint(main)) ||
                        !MSaveEnvelope.Equal(token.BackupFingerprint, MSaveEnvelope.Fingerprint(backup)))
                    {
                        return Failure(MSaveStatus.Conflict, "主档或备份已变化，请重新读取备份后再恢复。");
                    }

                    MSaveMetadata mainMetadata = CheckRecoverableMain(main);
                    MSaveResult<T> previous = Decode(backup, MSaveSource.Backup);
                    if (!previous.Succeeded)
                    {
                        return previous;
                    }

                    long revision = Math.Max(mainMetadata?.Revision ?? 0, previous.Metadata.Revision);
                    return Publish(
                        token.Slot,
                        previous.Value,
                        revision + 1,
                        previous.Metadata.ContentVersion,
                        main != null,
                        false);
                }
                catch (MSaveFormatException exception)
                {
                    return Failure(exception.Status, exception.Message, exception);
                }
                catch (Exception exception)
                {
                    return StorageFailure(exception, false);
                }
            }
        }

        private MSaveResult<T> Publish(
            string slot,
            T value,
            long revision,
            string contentVersion,
            bool replaceExisting,
            bool preservePrevious)
        {
            var metadata = new MSaveMetadata(SchemaVersion, contentVersion, revision, Guid.NewGuid());
            byte[] bytes;
            try
            {
                byte[] payload = _serializer.Serialize(value);
                if (payload == null)
                {
                    throw new InvalidOperationException("序列化器返回了 null。");
                }

                _serializer.Deserialize((byte[])payload.Clone());
                bytes = new MSaveEnvelope(metadata, payload).Encode();
            }
            catch (Exception exception)
            {
                return Failure(MSaveStatus.SerializationFailure, "新快照序列化或验证失败，未写入存储。", exception);
            }

            try
            {
                _storage.Commit(slot, bytes, replaceExisting, preservePrevious);
                return new MSaveResult<T>(MSaveStatus.Success, value, metadata, "快照已确认发布。");
            }
            catch (Exception exception)
            {
                return StorageFailure(exception, true, metadata);
            }
        }

        private MSaveMetadata CheckRecoverableMain(byte[] main)
        {
            if (main == null)
            {
                return null;
            }

            try
            {
                MSaveEnvelope envelope = MSaveEnvelope.Decode(main);
                if (envelope.Metadata.SchemaVersion > SchemaVersion)
                {
                    throw new MSaveFormatException(MSaveStatus.UnsupportedVersion, "主档来自更高结构版本，禁止恢复覆盖。");
                }

                return envelope.Metadata;
            }
            catch (MSaveFormatException exception) when (exception.Status == MSaveStatus.Corrupt)
            {
                return null;
            }
        }

        private MSaveResult<T> Decode(byte[] bytes, MSaveSource source)
        {
            if (bytes == null)
            {
                return Failure(MSaveStatus.NotFound, "指定快照不存在。", source: source);
            }

            MSaveEnvelope envelope;
            try
            {
                envelope = MSaveEnvelope.Decode(bytes);
            }
            catch (MSaveFormatException exception)
            {
                return Failure(exception.Status, exception.Message, exception, source);
            }

            if (envelope.Metadata.SchemaVersion > SchemaVersion)
            {
                return Failure(MSaveStatus.UnsupportedVersion, "存档来自更高结构版本，当前程序无法读取或覆盖。", source: source);
            }

            byte[] payload = envelope.Payload;
            int applied = 0;
            for (int version = envelope.Metadata.SchemaVersion; version < SchemaVersion; version++)
            {
                if (!_migrations.TryGetValue(version, out IMSaveMigration migration))
                {
                    return Failure(MSaveStatus.UnsupportedVersion, $"缺少结构版本 {version} 到 {version + 1} 的迁移步骤。", source: source);
                }

                try
                {
                    payload = migration.Migrate((byte[])payload.Clone());
                    if (payload == null)
                    {
                        throw new InvalidOperationException("迁移返回了 null。");
                    }

                    applied++;
                }
                catch (Exception exception)
                {
                    return Failure(MSaveStatus.MigrationFailure, $"结构版本 {version} 的迁移失败，原档未改动。", exception, source);
                }
            }

            try
            {
                T value = _serializer.Deserialize((byte[])payload.Clone());
                return new MSaveResult<T>(
                    MSaveStatus.Success,
                    value,
                    envelope.Metadata,
                    source: source,
                    appliedMigrations: applied);
            }
            catch (Exception exception)
            {
                return Failure(
                    applied == 0 ? MSaveStatus.Corrupt : MSaveStatus.MigrationFailure,
                    "快照反序列化验证失败，原档未改动。",
                    exception,
                    source);
            }
        }

        private static MSaveResult<T> Failure(
            MSaveStatus status,
            string message,
            Exception exception = null,
            MSaveSource source = MSaveSource.Main)
            => new MSaveResult<T>(status, message: message, exception: exception, source: source);

        private static MSaveResult<T> StorageFailure(
            Exception exception,
            bool duringCommit,
            MSaveMetadata metadata = null,
            MSaveSource source = MSaveSource.Main)
        {
            MSaveStatus status = exception is MSaveStorageException storageException
                ? storageException.Status
                : (duringCommit ? MSaveStatus.CommitUnknown : MSaveStatus.IoFailure);
            return new MSaveResult<T>(
                status,
                metadata: metadata,
                message: exception.Message,
                exception: exception,
                source: source);
        }
    }
}
