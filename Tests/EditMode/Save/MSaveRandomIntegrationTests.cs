using System;
using System.IO;
using NUnit.Framework;

namespace MFramework.Tests.EditMode
{
    /// <summary>验证保存、Unity JSON 与随机流共同使用时的数据边界，使用中性计数器而非游戏业务。</summary>
    public sealed class MSaveRandomIntegrationTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "MFramework-Joint-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void UnityJsonPreservesUnsignedRandomStateWithoutFloatingPointLoss()
        {
            var serializer = new MJsonUtilitySaveSerializer<Checkpoint>();
            var original = new Checkpoint
            {
                RandomState = new MRandomState(MRandom.AlgorithmId, MRandom.StateVersion,
                    ulong.MaxValue, ulong.MaxValue),
                RequestId = "request-with-64-bit-state"
            };

            Checkpoint restored = serializer.Deserialize(serializer.Serialize(original));

            Assert.AreEqual(ulong.MaxValue, restored.RandomState.State);
            Assert.AreEqual(ulong.MaxValue, restored.RandomState.Increment);
            Assert.AreEqual(original.RequestId, restored.RequestId);
            Assert.AreEqual(new MRandom(original.RandomState).NextUInt(), new MRandom(restored.RandomState).NextUInt());
        }

        [Test]
        public void CommittedReceiptAndRandomProgressRecoverTogetherWithANewService()
        {
            var save = NewSave();
            MRandom random = MRandom.CreateStream(42UL, "sample");
            MSaveResult<Checkpoint> initial = save.Save("slot", new Checkpoint
            {
                RandomState = random.CaptureState(), RequestId = "initial"
            });
            Assert.IsTrue(initial.Succeeded, initial.Message);

            // 从副本生成候选结果，只有提交成功才能更新正式会话的随机流。
            MRandom candidate = new MRandom(random.CaptureState());
            var next = new Checkpoint
            {
                RequestId = "operation-1", Result = candidate.NextInt(100), RandomState = candidate.CaptureState()
            };
            MSaveResult<Checkpoint> committed = save.Save("slot", next, initial.Metadata);
            Assert.IsTrue(committed.Succeeded, committed.Message);
            random.RestoreState(next.RandomState);

            // 新服务模拟重新打开程序；重试根据回执读取固定结果，不再执行一次抽样。
            MSaveResult<Checkpoint> loaded = NewSave().Load("slot");
            Assert.IsTrue(loaded.Succeeded, loaded.Message);
            Assert.AreEqual("operation-1", loaded.Value.RequestId);
            Assert.AreEqual(next.Result, loaded.Value.Result);
            Assert.AreEqual(committed.Metadata.CommitId, loaded.Metadata.CommitId);
            Assert.AreEqual(random.NextUInt(), new MRandom(loaded.Value.RandomState).NextUInt());

            MSaveResult<Checkpoint> backup = save.LoadBackup("slot");
            Assert.IsTrue(backup.Succeeded, backup.Message);
            Assert.AreEqual("initial", backup.Value.RequestId);
            Assert.AreEqual("operation-1", save.Load("slot").Value.RequestId,
                "读取备份不应隐式替换当前主档。");
        }

        [Test]
        public void FailedCandidateWriteLeavesCommittedReceiptAndLiveRandomUnchanged()
        {
            var save = NewSave();
            MRandom random = MRandom.CreateStream(42UL, "sample");
            MRandomState before = random.CaptureState();
            MSaveResult<Checkpoint> initial = save.Save("slot", new Checkpoint
            {
                RequestId = "initial", RandomState = before
            });
            Assert.IsTrue(initial.Succeeded, initial.Message);

            MRandom candidate = new MRandom(before);
            var next = new Checkpoint
            {
                RequestId = "must-not-be-published", Result = candidate.NextInt(100), RandomState = candidate.CaptureState()
            };
            var failure = new MSave<Checkpoint>(new RejectWrites(new MFileSaveStorage(_directory)),
                new MJsonUtilitySaveSerializer<Checkpoint>());
            MSaveResult<Checkpoint> result = failure.Save("slot", next, initial.Metadata);

            Assert.AreEqual(MSaveStatus.IoFailure, result.Status);
            Assert.AreEqual("initial", save.Load("slot").Value.RequestId);
            Assert.AreEqual(initial.Metadata.CommitId, save.Load("slot").Metadata.CommitId);
            Assert.AreEqual(before.State, random.CaptureState().State);
            Assert.AreEqual(new MRandom(before).NextUInt(), random.NextUInt());
        }

        private MSave<Checkpoint> NewSave()
            => new MSave<Checkpoint>(new MFileSaveStorage(_directory), new MJsonUtilitySaveSerializer<Checkpoint>());

        [Serializable]
        public sealed class Checkpoint
        {
            public string RequestId;
            public int Result;
            public MRandomState RandomState;
        }

        private sealed class RejectWrites : IMSaveStorage
        {
            private readonly IMSaveStorage _inner;
            public RejectWrites(IMSaveStorage inner) { _inner = inner; }
            public object GetSynchronizationRoot(string slot) => _inner.GetSynchronizationRoot(slot);
            public byte[] Read(string slot, MSaveSource source) => _inner.Read(slot, source);
            public void Commit(string slot, byte[] snapshot, bool replaceExisting, bool preservePrevious)
                => throw new MSaveStorageException(MSaveStatus.IoFailure, "测试注入：发布前写入失败。");
        }
    }
}
