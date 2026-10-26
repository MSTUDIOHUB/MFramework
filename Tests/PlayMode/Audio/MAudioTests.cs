using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MFramework.Tests.PlayMode
{
    internal sealed class MAudioTests
    {
        private GameObject _host;
        private AudioClip _firstClip;
        private AudioClip _secondClip;
        private MAudio _audio;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("MAudio Test Host");
            _host.AddComponent<AudioListener>();
            _firstClip = AudioClip.Create("First Test Clip", 44100, 1, 44100, false);
            _secondClip = AudioClip.Create("Second Test Clip", 44100, 1, 44100, false);
            _audio = new MAudio(_host, 2);
        }

        [TearDown]
        public void TearDown()
        {
            _audio?.Dispose();

            if (_host != null)
            {
                Object.Destroy(_host);
            }

            if (_firstClip != null)
            {
                Object.Destroy(_firstClip);
            }

            if (_secondClip != null)
            {
                Object.Destroy(_secondClip);
            }
        }

        [Test]
        public void VolumeAndMuteSettings_AreIndependentAndClamped()
        {
            _audio.MusicVolume = 2f;
            _audio.SfxVolume = -1f;
            _audio.MusicMuted = true;
            _audio.SfxMuted = false;

            Assert.That(_audio.MusicVolume, Is.EqualTo(1f));
            Assert.That(_audio.SfxVolume, Is.EqualTo(0f));
            Assert.That(_audio.MusicMuted, Is.True);
            Assert.That(_audio.SfxMuted, Is.False);
        }

        [Test]
        public void NonFiniteVolumeInputs_AreRejected()
        {
            var id = new MAudioId("tests.audio.non-finite");
            _audio.Register(id, _firstClip);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => _audio.MusicVolume = float.NaN);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _audio.SfxVolume = float.PositiveInfinity);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _audio.PlaySfx(id, float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => _audio.Register(
                    new MAudioId("tests.audio.non-finite-interval"),
                    _secondClip,
                    float.NaN));
        }

        [Test]
        public void PlaySfx_RespectsPerIdConcurrencyAndMinimumInterval()
        {
            var limitedId = new MAudioId("tests.audio.limited");
            _audio.Register(limitedId, _firstClip, 10f, 1);

            Assert.That(_audio.PlaySfx(limitedId), Is.True);
            Assert.That(_audio.PlaySfx(limitedId), Is.False);

            _audio.StopAllSfx();
            Assert.That(_audio.PlaySfx(limitedId), Is.False, "停止声部后，最短播放间隔仍然有效。");
        }

        [Test]
        public void PlaySfx_WhenAllVoicesAreBusy_ReturnsFalse()
        {
            var firstId = new MAudioId("tests.audio.first");
            var secondId = new MAudioId("tests.audio.second");
            var thirdId = new MAudioId("tests.audio.third");
            _audio.Register(firstId, _firstClip);
            _audio.Register(secondId, _secondClip);
            _audio.Register(thirdId, _firstClip);

            Assert.That(_audio.PlaySfx(firstId), Is.True);
            Assert.That(_audio.PlaySfx(secondId), Is.True);
            Assert.That(_audio.PlaySfx(thirdId), Is.False);
        }

        [Test]
        public void PlayBgm_IgnoresDuplicateAndReplacesCurrentTrack()
        {
            var firstId = new MAudioId("tests.bgm.first");
            var secondId = new MAudioId("tests.bgm.second");
            _audio.Register(firstId, _firstClip);
            _audio.Register(secondId, _secondClip);

            Assert.That(_audio.PlayBgm(firstId), Is.True);
            Assert.That(_audio.PlayBgm(firstId), Is.False);
            Assert.That(_audio.CurrentBgmId, Is.EqualTo(firstId));

            Assert.That(_audio.PlayBgm(secondId), Is.True);
            Assert.That(_audio.CurrentBgmId, Is.EqualTo(secondId));

            _audio.StopBgm();
            Assert.That(_audio.CurrentBgmId, Is.Null);
        }

        [Test]
        public void ModuleShutdown_DisposesAudioAndCanBeRepeated()
        {
            _audio.Initialize(null);
            _audio.Shutdown();

            Assert.DoesNotThrow(() => _audio.Shutdown());
            Assert.Throws<System.ObjectDisposedException>(
                () => _audio.Register(new MAudioId("tests.audio.after-shutdown"), _firstClip));
        }

        [UnityTest]
        public IEnumerator StopAndDispose_WhenAudioSourcesAreDestroyedExternally_RemainSafe()
        {
            AudioSource[] sources = _host.GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                Object.Destroy(sources[i]);
            }

            yield return null;

            Assert.DoesNotThrow(() => _audio.StopBgm());
            Assert.DoesNotThrow(() => _audio.StopAllSfx());
            Assert.DoesNotThrow(() => _audio.Dispose());
        }

        [UnityTest]
        public IEnumerator StopAndDispose_WhenHostIsDestroyedExternally_RemainSafe()
        {
            Object.Destroy(_host);
            yield return null;
            _host = null;

            Assert.DoesNotThrow(() => _audio.StopBgm());
            Assert.DoesNotThrow(() => _audio.StopAllSfx());
            Assert.DoesNotThrow(() => _audio.Dispose());
        }

        [UnityTest]
        public IEnumerator Resume_SfxStartedDuringPauseOnlyAddsItsOwnPausedDuration()
        {
            var id = new MAudioId("tests.audio.started-during-pause");
            AudioClip shortClip = AudioClip.Create("Pause Timing Clip", 4410, 1, 44100, false);

            try
            {
                _audio.Register(id, shortClip, 0f, 1);
                _audio.Pause();
                yield return new WaitForSecondsRealtime(0.35f);

                Assert.That(_audio.PlaySfx(id), Is.True);
                yield return new WaitForSecondsRealtime(0.1f);
                _audio.Resume();
                yield return new WaitForSecondsRealtime(0.15f);

                Assert.That(
                    _audio.PlaySfx(id),
                    Is.True,
                    "暂停期间启动的 SFX 只应补偿它实际经历的暂停时间。");
            }
            finally
            {
                if (shortClip != null)
                {
                    Object.Destroy(shortClip);
                }
            }
        }
    }
}
