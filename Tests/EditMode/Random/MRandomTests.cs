using System;
using NUnit.Framework;

namespace MFramework.Tests.EditMode
{
    public sealed class MRandomTests
    {
        // 官方 PCG Minimal C 示例：initstate = 42，initseq = 54。
        // https://www.pcg-random.org/using-pcg-c-basic.html
        private static readonly uint[] ReferenceOutputs =
        {
            0xa15c02b7U, 0x7b47f409U, 0xba1d3330U,
            0x83d2f293U, 0xbfa4784bU, 0xcbed606eU
        };

        [Test]
        public void NextUInt_MatchesPublishedPcgReferenceVector()
        {
            IMRandomSource random = new MRandom(42UL, 54UL);
            foreach (uint expected in ReferenceOutputs)
            {
                Assert.AreEqual(expected, random.NextUInt());
            }
        }

        [Test]
        public void CaptureAndRestore_ContinuePublishedSequenceWithoutAdvancing()
        {
            MRandom random = new MRandom(42UL);
            Assert.AreEqual(ReferenceOutputs[0], random.NextUInt());
            Assert.AreEqual(ReferenceOutputs[1], random.NextUInt());
            MRandomState saved = random.CaptureState();
            MRandomState secondCapture = random.CaptureState();

            AssertStateEqual(saved, secondCapture);
            Assert.AreEqual(ReferenceOutputs[2], random.NextUInt());
            random.RestoreState(saved);
            Assert.AreEqual(ReferenceOutputs[2], random.NextUInt());

            MRandom restored = new MRandom(saved);
            for (int i = 2; i < ReferenceOutputs.Length; i++)
            {
                Assert.AreEqual(ReferenceOutputs[i], restored.NextUInt());
            }
        }

        [Test]
        public void CapturedState_IsAnIndependentValue()
        {
            MRandom random = new MRandom(42UL);
            MRandomState copiedState = random.CaptureState();
            copiedState.State = 0UL;
            copiedState.Increment = 1UL;

            Assert.AreEqual(ReferenceOutputs[0], random.NextUInt());
            Assert.AreEqual(0U, new MRandom(copiedState).NextUInt());
        }

        [Test]
        public void InvalidStates_AreRejectedWithoutMutatingCurrentState()
        {
            MRandom random = new MRandom(42UL);
            MRandomState before = random.CaptureState();
            MRandomState[] invalidStates =
            {
                default(MRandomState),
                new MRandomState("other", MRandom.StateVersion, 1UL, 1UL),
                new MRandomState(MRandom.AlgorithmId, 0, 1UL, 1UL),
                new MRandomState(MRandom.AlgorithmId, MRandom.StateVersion + 1, 1UL, 1UL),
                new MRandomState(MRandom.AlgorithmId, MRandom.StateVersion, 1UL, 0UL),
                new MRandomState(MRandom.AlgorithmId, MRandom.StateVersion, 1UL, 2UL)
            };

            foreach (MRandomState invalid in invalidStates)
            {
                Assert.Throws<ArgumentException>(() => random.RestoreState(invalid));
                Assert.Throws<ArgumentException>(() => new MRandom(invalid));
                AssertStateEqual(before, random.CaptureState());
            }

            Assert.AreEqual(ReferenceOutputs[0], random.NextUInt());
        }

        [Test]
        public void InvalidIntegerRanges_DoNotConsumeOutput()
        {
            MRandom random = new MRandom(42UL);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(4, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(4, -4));
            Assert.AreEqual(ReferenceOutputs[0], random.NextUInt());
        }

        [Test]
        public void NextInt_UsesInclusiveLowerAndExclusiveUpperBounds()
        {
            MRandom random = new MRandom(42UL);
            Assert.AreEqual(-7, random.NextInt(-10, 10));
            Assert.AreEqual(0, random.NextInt(1));
            Assert.AreEqual(int.MinValue, random.NextInt(int.MinValue, int.MinValue + 1));
            Assert.AreEqual(int.MaxValue - 1, random.NextInt(int.MaxValue - 1, int.MaxValue));

            // 最大合法区间宽度是 2^32 - 1，不能先用 int 做减法。
            random = new MRandom(42UL);
            Assert.AreEqual(559678135, random.NextInt(int.MinValue, int.MaxValue));
            for (int i = 0; i < 4096; i++)
            {
                int value = random.NextInt(-3, 7);
                Assert.That(value, Is.GreaterThanOrEqualTo(-3).And.LessThan(7));
            }
        }

        [Test]
        public void NextInt_RejectsRemainderRegionInsteadOfReturningModuloBiasedValue()
        {
            // PCG 从 state=0/increment=1 出发的输出为 0、0、3837872008。
            // 区间宽度 1073741825 的拒绝阈值为 1073741821，前两个零必须被跳过。
            MRandom random = new MRandom(
                new MRandomState(MRandom.AlgorithmId, MRandom.StateVersion, 0UL, 1UL));

            Assert.AreEqual(616646533, random.NextInt(1073741825));
            Assert.AreEqual(13885033948157127959UL, random.CaptureState().State);
        }

        [Test]
        public void NextDouble_UsesFixed53BitMappingAndConsumesTwoOutputs()
        {
            MRandom random = new MRandom(42UL);
            Assert.AreEqual(0.6303102186438938, random.NextDouble());
            Assert.AreEqual(ReferenceOutputs[2], random.NextUInt());

            MRandom zero = new MRandom(
                new MRandomState(MRandom.AlgorithmId, MRandom.StateVersion, 0UL, 1UL));
            Assert.AreEqual(0.0, zero.NextDouble());
            for (int i = 0; i < 4096; i++)
            {
                Assert.That(random.NextDouble(), Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
        }

        [Test]
        public void NamedStream_MatchesFrozenDerivationVector()
        {
            // UTF-16LE FNV-1a64("loot") = 0xaa4b911f79fb9005。
            // 用独立 Python 参考程序按明确字节序和 PCG 整数规则计算后冻结。
            MRandom random = MRandom.CreateStream(42UL, "loot");
            Assert.AreEqual(0x0485fe05U, random.NextUInt());
            Assert.AreEqual(0x765d6625U, random.NextUInt());
            Assert.AreEqual(0xc069343bU, random.NextUInt());
        }

        [Test]
        public void CreatingAndUsingOtherStreams_DoesNotAdvanceExistingStreams()
        {
            MRandom parent = new MRandom(42UL);
            MRandom loot = MRandom.CreateStream(42UL, "loot");
            MRandom visual = MRandom.CreateStream(42UL, "visual");
            MRandom.CreateStream(42UL, "battle");
            for (int i = 0; i < 100; i++)
            {
                visual.NextDouble();
            }

            Assert.AreEqual(ReferenceOutputs[0], parent.NextUInt());
            Assert.AreEqual(0x0485fe05U, loot.NextUInt());
            Assert.AreEqual(0x0485fe05U, MRandom.CreateStream(42UL, "loot").NextUInt());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void NamedStream_RejectsMissingName(string name)
        {
            Assert.Throws<ArgumentException>(() => MRandom.CreateStream(42UL, name));
        }

        [Test]
        public void SequenceNumber_RejectsLossyHighestBitAndAcceptsLargestValidValue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MRandom(42UL, 1UL << 63));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MRandom(42UL, ulong.MaxValue));
            Assert.AreEqual(ulong.MaxValue, new MRandom(42UL, (ulong)long.MaxValue).CaptureState().Increment);
        }

        private static void AssertStateEqual(MRandomState expected, MRandomState actual)
        {
            Assert.AreEqual(expected.Algorithm, actual.Algorithm);
            Assert.AreEqual(expected.Version, actual.Version);
            Assert.AreEqual(expected.State, actual.State);
            Assert.AreEqual(expected.Increment, actual.Increment);
        }
    }
}
