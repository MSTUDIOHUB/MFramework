using System;
using System.IO;

namespace MFramework
{
    /// <summary>
    /// 本地文件快照后端：写入同目录临时文件并刷新到磁盘后，通过原子替换（Replace/Move）发布为主档。
    /// </summary>
    public sealed class MFileSaveStorage : IMSaveStorage
    {
        private static readonly object GlobalSyncRoot = new object();

        /// <summary>创建文件后端；此时仅校验路径，不创建目录或文件。</summary>
        /// <param name="rootDirectory">由应用提供的可信根目录，Unity 中通常位于 persistentDataPath 下。</param>
        /// <param name="maximumSnapshotBytes">单个完整快照的最大字节数，默认 16 MiB，至少 80 字节。</param>
        /// <exception cref="ArgumentException">根路径为空或大小限制过小。</exception>
        public MFileSaveStorage(string rootDirectory, int maximumSnapshotBytes = 16 * 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("存档根目录不能为空。", nameof(rootDirectory));
            }

            if (maximumSnapshotBytes < 80)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSnapshotBytes), "单快照最大字节数不能小于 80 字节。");
            }

            RootDirectory = Path.GetFullPath(rootDirectory);
            MaximumSnapshotBytes = maximumSnapshotBytes;
        }

        /// <summary>规范化后的存档根目录绝对路径。</summary>
        public string RootDirectory { get; }

        /// <summary>完整快照大小上限；超限返回 IoFailure，并保留已有文件。</summary>
        public int MaximumSnapshotBytes { get; }

        /// <inheritdoc />
        public object GetSynchronizationRoot(string slot)
        {
            MSaveSlot.Validate(slot);
            return GlobalSyncRoot;
        }

        /// <inheritdoc />
        public byte[] Read(string slot, MSaveSource source)
        {
            MSaveSlot.Validate(slot);
            if (source != MSaveSource.Main && source != MSaveSource.Backup)
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }

            string path = GetPath(slot, source);
            try
            {
                if (!File.Exists(path))
                {
                    EnsureDirectoryPathIsNotBlockedByFile(path);
                    return null;
                }

                FileInfo info = new FileInfo(path);
                if (info.Length > MaximumSnapshotBytes)
                {
                    throw new IOException("存档快照文件超过后端配置的大小上限。");
                }

                return File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                throw new MSaveStorageException(MSaveStatus.IoFailure, "读取存档快照失败。", exception);
            }
        }

        /// <inheritdoc />
        public void Commit(string slot, byte[] snapshot, bool replaceExisting, bool preservePrevious)
        {
            MSaveSlot.Validate(slot);
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (preservePrevious && !replaceExisting)
            {
                throw new ArgumentException("首次创建主档时不能要求备份不存在的旧主档。", nameof(preservePrevious));
            }

            string temporaryPath = Path.Combine(RootDirectory, slot + "." + Guid.NewGuid().ToString("N") + ".tmp");
            bool publishing = false;
            try
            {
                if (snapshot.Length > MaximumSnapshotBytes)
                {
                    throw new IOException("新快照超过后端配置的大小上限。");
                }

                Directory.CreateDirectory(RootDirectory);
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(snapshot, 0, snapshot.Length);
                    stream.Flush(true);
                }

                publishing = true;
                string mainPath = GetPath(slot, MSaveSource.Main);
                if (replaceExisting && File.Exists(mainPath))
                {
                    File.Replace(
                        temporaryPath,
                        mainPath,
                        preservePrevious ? GetPath(slot, MSaveSource.Backup) : null);
                }
                else
                {
                    File.Move(temporaryPath, mainPath);
                }
            }
            catch (Exception exception) when (exception is PlatformNotSupportedException || exception is NotSupportedException)
            {
                throw new MSaveStorageException(
                    MSaveStatus.UnsupportedStorage,
                    "当前存储环境不支持所需的原子文件替换操作。",
                    exception);
            }
            catch (Exception exception)
            {
                throw new MSaveStorageException(
                    publishing ? MSaveStatus.CommitUnknown : MSaveStatus.IoFailure,
                    publishing ? "发布阶段发生异常，提交结果未知；请重新读取核对。" : "发布前写入临时文件失败，原主档未受影响。",
                    exception);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static void EnsureDirectoryPathIsNotBlockedByFile(string targetFilePath)
        {
            string directory = Path.GetDirectoryName(targetFilePath);
            while (!string.IsNullOrEmpty(directory))
            {
                if (File.Exists(directory))
                {
                    throw new IOException("存档目录路径中的某一级实际是普通文件。");
                }

                directory = Path.GetDirectoryName(directory);
            }
        }

        private string GetPath(string slot, MSaveSource source)
            => Path.Combine(RootDirectory, slot + (source == MSaveSource.Main ? ".msave" : ".msave.bak"));
    }
}
