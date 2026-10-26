using System;
using System.IO;
using NUnit.Framework;

namespace MFramework.Tests.EditMode
{
    internal sealed class MSaveTests
    {
        private string _directory;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "mframework-save-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void FileStorage_RoundTripsAndRequiresCompleteExpectedMetadata()
        {
            MSave<int> save = CreateFileSave();
            Assert.That(save.Load("player").Status, Is.EqualTo(MSaveStatus.NotFound));
            MSaveResult<int> first = save.Save("player", 7);
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.Metadata.Revision, Is.EqualTo(1));
            Assert.That(first.Metadata.CommitId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(save.Save("player", 8).Status, Is.EqualTo(MSaveStatus.Conflict));
            MSaveResult<int> second = save.Save("player", 9, first.Metadata);
            Assert.That(second.Succeeded, Is.True, second.Message);
            Assert.That(second.Metadata.Revision, Is.EqualTo(2));
            Assert.That(second.Metadata.CommitId, Is.Not.EqualTo(first.Metadata.CommitId));
            MSaveResult<int> loaded = CreateFileSave().Load("player");
            Assert.That(loaded.Value, Is.EqualTo(9));
            Assert.That(loaded.Metadata.ContentVersion, Is.EqualTo("content-1"));
            Assert.That(save.LoadBackup("player").Value, Is.EqualTo(7));
        }

        [Test]
        public void SaveOrOverwrite_AndDirectSlotRestoreBackup_WorkForSimpleIndieGames()
        {
            MSave<int> save = CreateFileSave();
            Assert.That(save.SaveOrOverwrite("player", 10).Succeeded, Is.True);
            Assert.That(save.SaveOrOverwrite("player", 25).Succeeded, Is.True);
            Assert.That(save.Load("player").Value, Is.EqualTo(25));

            File.WriteAllBytes(MainPath, new byte[] { 1, 2, 3 });
            Assert.That(save.Load("player").Status, Is.EqualTo(MSaveStatus.Corrupt));
            Assert.That(save.RestoreBackup("player").Succeeded, Is.True);
            Assert.That(save.Load("player").Value, Is.EqualTo(10));
        }

        [TestCase(12)] // schemaVersion
        [TestCase(20)] // revision
        [TestCase(32)] // commitId
        [TestCase(48)] // contentVersion
        [TestCase(57)] // payload
        public void CorruptedProtectedFields_AreNotTreatedAsMissingOrOverwritten(int offset)
        {
            MSave<int> save = CreateFileSave();
            MSaveResult<int> first = save.Save("player", 7);
            byte[] bytes = File.ReadAllBytes(MainPath);
            bytes[offset] ^= 1;
            File.WriteAllBytes(MainPath, bytes);
            Assert.That(save.Load("player").Status, Is.EqualTo(MSaveStatus.Corrupt));
            Assert.That(save.Save("player", 8, first.Metadata).Status, Is.EqualTo(MSaveStatus.Corrupt));
            Assert.That(File.ReadAllBytes(MainPath), Is.EqualTo(bytes));
        }

        [Test]
        public void InvalidSerializedPayload_WithValidEnvelope_ReturnsCorrupt()
        {
            var storage = new MemoryStorage();
            var writer = new MSave<byte[]>(storage, new BytesSerializer());
            Assert.That(writer.Save("player", new byte[] { 1 }).Succeeded, Is.True);
            var reader = new MSave<int>(storage, new IntSerializer());
            Assert.That(reader.Load("player").Status, Is.EqualTo(MSaveStatus.Corrupt));
            Assert.That(reader.Save("player", 1).Status, Is.EqualTo(MSaveStatus.Corrupt));
        }

        [Test]
        public void ExplicitRecovery_PreservesBackupAndRejectsOldCasTokenEvenWhenRevisionRepeats()
        {
            MSave<int> save = CreateFileSave();
            MSaveResult<int> first = save.Save("player", 10);
            MSaveResult<int> second = save.Save("player", 20, first.Metadata);
            byte[] originalBackup = File.ReadAllBytes(BackupPath);
            File.WriteAllBytes(MainPath, new byte[] { 1, 2, 3 });
            Assert.That(save.Load("player").Status, Is.EqualTo(MSaveStatus.Corrupt));
            MSaveResult<int> candidate = save.LoadBackup("player");
            Assert.That(candidate.Succeeded, Is.True);
            Assert.That(candidate.Source, Is.EqualTo(MSaveSource.Backup));
            Assert.That(candidate.Value, Is.EqualTo(10));
            Assert.That(File.ReadAllBytes(MainPath), Is.EqualTo(new byte[] { 1, 2, 3 }));
            MSaveResult<int> restored = save.RestoreBackup(candidate.RecoveryToken);
            Assert.That(restored.Succeeded, Is.True, restored.Message);
            Assert.That(restored.Metadata.Revision, Is.EqualTo(second.Metadata.Revision));
            Assert.That(restored.Metadata.CommitId, Is.Not.EqualTo(second.Metadata.CommitId));
            Assert.That(save.Save("player", 99, second.Metadata).Status, Is.EqualTo(MSaveStatus.Conflict));
            Assert.That(save.Load("player").Value, Is.EqualTo(10));
            Assert.That(File.ReadAllBytes(BackupPath), Is.EqualTo(originalBackup));
            Assert.That(save.RestoreBackup(candidate.RecoveryToken).Status, Is.EqualTo(MSaveStatus.Conflict));
        }

        [Test]
        public void MissingMainWithBackup_RequiresExplicitRecovery()
        {
            MSave<int> save = CreateFileSave();
            MSaveResult<int> saved = save.Save("player", 10);
            save.Save("player", 20, saved.Metadata);
            File.Delete(MainPath);
            Assert.That(save.Load("player").Status, Is.EqualTo(MSaveStatus.RecoveryRequired));
            Assert.That(save.Save("player", 0).Status, Is.EqualTo(MSaveStatus.RecoveryRequired));
            Assert.That(save.RestoreBackup("player").Succeeded, Is.True);
            Assert.That(save.Load("player").Value, Is.EqualTo(10));
        }

        [Test]
        public void CorruptBackup_CannotReplaceValidMain()
        {
            MSave<int> save = CreateFileSave();
            MSaveResult<int> first = save.Save("player", 1);
            save.Save("player", 2, first.Metadata);
            File.WriteAllBytes(BackupPath, new byte[] { 42 });
            MSaveResult<int> backup = save.LoadBackup("player");
            Assert.That(backup.Status, Is.EqualTo(MSaveStatus.Corrupt));
            Assert.That(backup.RecoveryToken, Is.Null);
            Assert.That(save.Load("player").Value, Is.EqualTo(2));
        }

        [Test]
        public void FutureSchema_RefusesLoadSaveAndRecoveryPreparation()
        {
            var storage = new MemoryStorage();
            var old = new MSave<int>(storage, new IntSerializer(), 1);
            MSaveResult<int> original = old.Save("player", 1);
            var newer = new MSave<int>(storage, new IntSerializer(), 2,
                migrations: new[] { new Migration(1, bytes => bytes) });
            Assert.That(newer.Save("player", 2, original.Metadata).Succeeded, Is.True);
            byte[] futureBytes = (byte[])storage.Main.Clone();
            Assert.That(old.Load("player").Status, Is.EqualTo(MSaveStatus.UnsupportedVersion));
            Assert.That(old.Save("player", 3, original.Metadata).Status, Is.EqualTo(MSaveStatus.UnsupportedVersion));
            Assert.That(old.LoadBackup("player").Status, Is.EqualTo(MSaveStatus.UnsupportedVersion));
            Assert.That(storage.Main, Is.EqualTo(futureBytes));
        }

        [Test]
        public void Migration_ExecutesInOrderOnCopiesWithoutWritingOriginal()
        {
            var storage = new MemoryStorage();
            new MSave<int>(storage, new IntSerializer()).Save("player", 3);
            byte[] original = (byte[])storage.Main.Clone();
            var current = new MSave<int>(storage, new IntSerializer(), 3, migrations: new[]
            {
                new Migration(1, bytes => BitConverter.GetBytes(BitConverter.ToInt32(bytes, 0) + 4)),
                new Migration(2, bytes => BitConverter.GetBytes(BitConverter.ToInt32(bytes, 0) * 2))
            });
            MSaveResult<int> read = current.Load("player");
            Assert.That(read.Value, Is.EqualTo(14));
            Assert.That(read.AppliedMigrations, Is.EqualTo(2));
            Assert.That(read.Metadata.SchemaVersion, Is.EqualTo(1));
            Assert.That(storage.Main, Is.EqualTo(original));
            Assert.That(current.Save("player", read.Value, read.Metadata).Succeeded, Is.True);
            Assert.That(current.Load("player").Metadata.SchemaVersion, Is.EqualTo(3));
        }

        [Test]
        public void WriteFailure_IsReportedAndLeavesCommittedSnapshotIntact()
        {
            var storage = new MemoryStorage();
            var save = new MSave<int>(storage, new IntSerializer());
            MSaveResult<int> original = save.Save("player", 1);
            storage.BeforePublishFailure = true;
            MSaveResult<int> failed = save.Save("player", 2, original.Metadata);
            Assert.That(failed.Status, Is.EqualTo(MSaveStatus.IoFailure));
            Assert.That(failed.Exception, Is.Not.Null);
            Assert.That(save.Load("player").Value, Is.EqualTo(1));
        }

        [Test]
        public void LostConfirmation_ReturnsCommitUnknownAndCanBeResolvedByReadBack()
        {
            var storage = new MemoryStorage { LoseConfirmation = true };
            var save = new MSave<int>(storage, new IntSerializer());
            MSaveResult<int> unknown = save.Save("player", 42);
            Assert.That(unknown.Status, Is.EqualTo(MSaveStatus.CommitUnknown));
            Assert.That(unknown.Succeeded, Is.False);
            MSaveResult<int> read = save.Load("player");
            Assert.That(read.Value, Is.EqualTo(42));
            Assert.That(read.Metadata.CommitId, Is.EqualTo(unknown.Metadata.CommitId));
            Assert.That(save.Save("player", 43).Status, Is.EqualTo(MSaveStatus.Conflict));
        }

        [TestCase("../escape")]
        [TestCase("a/b")]
        [TestCase("a\\b")]
        [TestCase(".")]
        [TestCase("")]
        [TestCase("存档")]
        public void InvalidSlot_RejectsPathTraversalBeforeTouchingStorage(string slot)
        {
            Assert.Throws<ArgumentException>(() => CreateFileSave().Save(slot, 1));
            Assert.That(Directory.Exists(_directory), Is.False);
        }

        private MSave<int> CreateFileSave()
            => new MSave<int>(new MFileSaveStorage(_directory), new IntSerializer(), contentVersion: "content-1");

        private string MainPath => Path.Combine(_directory, "player.msave");
        private string BackupPath => Path.Combine(_directory, "player.msave.bak");

        private sealed class IntSerializer : IMSaveSerializer<int>
        {
            public byte[] Serialize(int value) => BitConverter.GetBytes(value);
            public int Deserialize(byte[] payload)
            {
                if (payload.Length != 4) throw new InvalidDataException("需要恰好四个字节。");
                return BitConverter.ToInt32(payload, 0);
            }
        }

        private sealed class BytesSerializer : IMSaveSerializer<byte[]>
        {
            public byte[] Serialize(byte[] value) => (byte[])value.Clone();
            public byte[] Deserialize(byte[] payload) => (byte[])payload.Clone();
        }

        private sealed class Migration : IMSaveMigration
        {
            private readonly Func<byte[], byte[]> _apply;
            public Migration(int from, Func<byte[], byte[]> apply) { FromSchemaVersion = from; _apply = apply; }
            public int FromSchemaVersion { get; }
            public byte[] Migrate(byte[] payload) => _apply(payload);
        }

        private sealed class MemoryStorage : IMSaveStorage
        {
            private readonly object _sync = new object();
            public byte[] Main;
            private byte[] _backup;
            public bool BeforePublishFailure;
            public bool LoseConfirmation;
            public object GetSynchronizationRoot(string slot) => _sync;
            public byte[] Read(string slot, MSaveSource source)
                => (byte[])(source == MSaveSource.Main ? Main : _backup)?.Clone();
            public void Commit(string slot, byte[] snapshot, bool replaceExisting, bool preservePrevious)
            {
                if (BeforePublishFailure)
                    throw new MSaveStorageException(MSaveStatus.IoFailure, "注入发布前故障。");
                if (preservePrevious) _backup = Main;
                Main = (byte[])snapshot.Clone();
                if (LoseConfirmation) throw new IOException("已发布，但确认响应丢失。");
            }
        }
    }
}
