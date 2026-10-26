using System;
using NUnit.Framework;

namespace MFramework.Tests.EditMode
{
    internal sealed class MJsonUtilitySaveSerializerTests
    {
        [Serializable]
        private sealed class Snapshot
        {
            public int Gold;
            public string Receipt;
        }

        [Test]
        public void JsonUtilityAdapter_RoundTripsPlainSerializableFields()
        {
            var serializer = new MJsonUtilitySaveSerializer<Snapshot>();
            var original = new Snapshot { Gold = 17, Receipt = "结算-001" };
            Snapshot restored = serializer.Deserialize(serializer.Serialize(original));
            Assert.That(restored.Gold, Is.EqualTo(17));
            Assert.That(restored.Receipt, Is.EqualTo("结算-001"));
            Assert.That(restored, Is.Not.SameAs(original));
        }

        [Test]
        public void JsonUtilityAdapter_RejectsNullSnapshotAndInvalidUtf8()
        {
            var serializer = new MJsonUtilitySaveSerializer<Snapshot>();
            Assert.Throws<ArgumentNullException>(() => serializer.Serialize(null));
            Assert.Throws<System.Text.DecoderFallbackException>(() => serializer.Deserialize(new byte[] { 0xff }));
        }

        [Test]
        public void JsonUtilityAdapter_RejectsUnsupportedRootAndMissingRequiredBusinessFields()
        {
            Assert.Throws<ArgumentException>(() => new MJsonUtilitySaveSerializer<System.Collections.Generic.Dictionary<string, int>>());
            Assert.Throws<ArgumentException>(() => new MJsonUtilitySaveSerializer<string>());
            var serializer = new MJsonUtilitySaveSerializer<Snapshot>(snapshot =>
            {
                if (string.IsNullOrEmpty(snapshot.Receipt))
                    throw new InvalidOperationException("业务要求结算回执不能为空。");
            });
            Assert.Throws<InvalidOperationException>(() => serializer.Deserialize(System.Text.Encoding.UTF8.GetBytes("{}")));
            Assert.Throws<InvalidOperationException>(() => serializer.Serialize(new Snapshot { Gold = 1 }));
        }
    }
}
