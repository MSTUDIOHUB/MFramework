using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MFramework
{
    /// <summary>
    /// 使用已注册的 AudioClip 播放一路 BGM，以及数量受限的并发 SFX。
    /// </summary>
    /// <remarks>
    /// 调用方拥有宿主 GameObject；本服务会添加并持有自己的 AudioSource 组件，
    /// 并在 <see cref="Dispose"/> 时移除这些组件。本服务不负责加载或释放 AudioClip。
    /// </remarks>
    public sealed class MAudio : IMFrameworkModule, IDisposable
    {
        private readonly GameObject _host;
        private readonly AudioSource _bgmSource;
        private readonly SfxVoice[] _sfxVoices;
        private readonly Dictionary<MAudioId, AudioRegistration> _registrations =
            new Dictionary<MAudioId, AudioRegistration>();

        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        private bool _musicMuted;
        private bool _sfxMuted;
        private bool _isPaused;
        private bool _bgmRequested;
        private bool _bgmPausedByService;
        private bool _hasCurrentBgm;
        private MAudioId _currentBgmId;
        private double _pauseStartedAt;
        private bool _isDisposed;

        /// <summary>
        /// 创建音频服务，并把所需的 AudioSource 组件添加到宿主 GameObject。
        /// </summary>
        /// <param name="host">用于承载新建 AudioSource 组件的有效 GameObject。</param>
        /// <param name="maxConcurrentSfx">
        /// 固定的 SFX 声部数量，同时也是全局 SFX 并发上限。
        /// </param>
        /// <exception cref="ArgumentNullException">当 <paramref name="host"/> 为 null 时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 当 <paramref name="maxConcurrentSfx"/> 小于一时抛出。
        /// </exception>
        public MAudio(GameObject host, int maxConcurrentSfx = 8)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (maxConcurrentSfx < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentSfx),
                    "MAudio 至少需要 1 个 SFX 并发声道。");
            }

            _host = host;
            _bgmSource = CreateSource(host, true);
            _sfxVoices = new SfxVoice[maxConcurrentSfx];

            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                _sfxVoices[i] = new SfxVoice(CreateSource(host, false));
            }
        }

        /// <summary>
        /// 获取已注册的 AudioClip 数量。
        /// </summary>
        public int RegisteredCount => _registrations.Count;

        /// <summary>
        /// 获取全局可同时播放的 SFX 声部数量。
        /// </summary>
        public int MaxConcurrentSfx => _sfxVoices.Length;

        /// <summary>
        /// 获取或设置 BGM 音量，取值范围为零到一（包含边界）；超出范围的值会被截断。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">值不是有限数字。</exception>
        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                ThrowIfUnavailable();
                ValidateFinite(value, nameof(value));
                _musicVolume = Mathf.Clamp01(value);
                _bgmSource.volume = _musicVolume;
            }
        }

        /// <summary>
        /// 获取或设置 SFX 总线音量，取值范围为零到一（包含边界）；超出范围的值会被截断。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">值不是有限数字。</exception>
        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                ThrowIfUnavailable();
                ValidateFinite(value, nameof(value));
                _sfxVolume = Mathf.Clamp01(value);
                RefreshSfxVolumes();
            }
        }

        /// <summary>
        /// 获取或设置 BGM 总线是否静音；此操作不会改变 <see cref="MusicVolume"/>。
        /// </summary>
        public bool MusicMuted
        {
            get => _musicMuted;
            set
            {
                ThrowIfUnavailable();
                _musicMuted = value;
                _bgmSource.mute = value;
            }
        }

        /// <summary>
        /// 获取或设置 SFX 总线是否静音；此操作不会改变 <see cref="SfxVolume"/>。
        /// </summary>
        public bool SfxMuted
        {
            get => _sfxMuted;
            set
            {
                ThrowIfUnavailable();
                _sfxMuted = value;
                for (int i = 0; i < _sfxVoices.Length; i++)
                {
                    _sfxVoices[i].Source.mute = value;
                }
            }
        }

        /// <summary>
        /// 获取当前请求播放的 BGM ID；未指定 BGM 时返回 <see langword="null"/>。
        /// </summary>
        public MAudioId? CurrentBgmId => _hasCurrentBgm ? _currentBgmId : (MAudioId?)null;

        /// <summary>
        /// 注册 AudioClip。音频必须先注册，之后才能播放。
        /// </summary>
        /// <param name="id">玩法代码使用的稳定 ID。</param>
        /// <param name="clip">需要注册的 AudioClip；MAudio 不持有其资源所有权，也不会释放它。</param>
        /// <param name="minimumIntervalSeconds">
        /// 此 ID 两次成功播放 SFX 之间所需的最短真实时间间隔。
        /// </param>
        /// <param name="maxConcurrentInstances">
        /// 此 ID 可同时占用的最大 SFX 声部数；零表示只使用全局上限。
        /// </param>
        /// <exception cref="ArgumentException">当 <paramref name="id"/> 无效时抛出。</exception>
        /// <exception cref="ArgumentNullException">当 <paramref name="clip"/> 为 null 时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 当时间间隔或单 ID 并发上限为负数时抛出。
        /// </exception>
        /// <exception cref="InvalidOperationException">当 ID 已经注册时抛出。</exception>
        public void Register(
            MAudioId id,
            AudioClip clip,
            float minimumIntervalSeconds = 0f,
            int maxConcurrentInstances = 0)
        {
            ThrowIfUnavailable();
            ValidateId(id);

            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (!IsFinite(minimumIntervalSeconds) || minimumIntervalSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumIntervalSeconds),
                    "最短播放间隔不能为负数或非有限数值。");
            }

            if (maxConcurrentInstances < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentInstances),
                    "单音效并发上限不能为负数。");
            }

            if (_registrations.ContainsKey(id))
            {
                throw new InvalidOperationException($"音频 ID '{id}' 已经注册，不能重复注册。");
            }

            _registrations.Add(
                id,
                new AudioRegistration(clip, minimumIntervalSeconds, maxConcurrentInstances));
        }

        /// <summary>
        /// 获取指定 ID 是否已经注册 AudioClip。
        /// </summary>
        /// <param name="id">需要查询的 ID。</param>
        /// <returns>ID 已注册时返回 <see langword="true"/>。</returns>
        public bool IsRegistered(MAudioId id)
        {
            ThrowIfUnavailable();
            ValidateId(id);
            return _registrations.ContainsKey(id);
        }

        /// <summary>
        /// 移除 AudioClip 注册，并可选择停止正在使用它的声部。
        /// </summary>
        /// <param name="id">需要移除的已注册 ID。</param>
        /// <param name="stopPlaying">
        /// 是否停止正在使用该 ID 的 BGM 或 SFX 声部。
        /// </param>
        /// <returns>成功移除注册时返回 <see langword="true"/>。</returns>
        public bool Unregister(MAudioId id, bool stopPlaying = true)
        {
            ThrowIfUnavailable();
            ValidateId(id);

            if (!_registrations.Remove(id))
            {
                return false;
            }

            if (stopPlaying)
            {
                StopVoices(id);
            }

            return true;
        }

        /// <summary>
        /// 使用唯一的循环 BGM AudioSource 播放已注册的 AudioClip。
        /// </summary>
        /// <param name="id">已注册 AudioClip 的 ID。</param>
        /// <param name="restart">
        /// 请求的 BGM 与当前 BGM 相同时是否重新开始播放。默认忽略重复请求，
        /// 避免重新进入场景时叠加或重启音乐。
        /// </param>
        /// <returns>
        /// 开始或重新开始播放时返回 <see langword="true"/>；忽略重复请求时返回
        /// <see langword="false"/>。
        /// </returns>
        public bool PlayBgm(MAudioId id, bool restart = false)
        {
            ThrowIfUnavailable();
            AudioRegistration registration = GetRegistration(id);

            if (_bgmRequested && _hasCurrentBgm && _currentBgmId == id && !restart)
            {
                return false;
            }

            _bgmSource.Stop();
            _bgmSource.clip = registration.Clip;
            _bgmSource.loop = true;
            _bgmSource.volume = _musicVolume;
            _bgmSource.mute = _musicMuted;
            _bgmSource.Play();

            _currentBgmId = id;
            _hasCurrentBgm = true;
            _bgmRequested = true;
            _bgmPausedByService = false;

            if (_isPaused)
            {
                _bgmSource.Pause();
                _bgmPausedByService = true;
            }

            return true;
        }

        /// <summary>
        /// 停止当前 BGM，并清除对应 ID；即使宿主、AudioSource 或服务已经被释放，
        /// 也可安全调用。
        /// </summary>
        public void StopBgm()
        {
            if (_bgmSource != null)
            {
                _bgmSource.Stop();
                _bgmSource.clip = null;
            }

            _bgmRequested = false;
            _bgmPausedByService = false;
            _hasCurrentBgm = false;
            _currentBgmId = default;
        }

        /// <summary>
        /// 尝试在 SFX 总线上播放已注册的 AudioClip。
        /// </summary>
        /// <param name="id">已注册 AudioClip 的 ID。</param>
        /// <param name="volumeScale">
        /// 本次播放使用的音量倍率，取值范围为零到一（包含边界）；超出范围的值会被截断。
        /// </param>
        /// <returns>
        /// 成功启动声部时返回 <see langword="true"/>；因全局上限、单 ID 上限或最短播放间隔
        /// 拒绝请求时返回 <see langword="false"/>。
        /// </returns>
        public bool PlaySfx(MAudioId id, float volumeScale = 1f)
        {
            ThrowIfUnavailable();
            ValidateFinite(volumeScale, nameof(volumeScale));
            AudioRegistration registration = GetRegistration(id);
            double now = Time.realtimeSinceStartupAsDouble;

            if (now - registration.LastPlayedAt < registration.MinimumIntervalSeconds)
            {
                return false;
            }

            if (registration.MaxConcurrentInstances > 0 &&
                CountBusyVoices(id, now) >= registration.MaxConcurrentInstances)
            {
                return false;
            }

            SfxVoice voice = FindAvailableVoice(now);
            if (voice == null)
            {
                return false;
            }

            float clampedScale = Mathf.Clamp01(volumeScale);
            voice.Id = id;
            voice.HasId = true;
            voice.VolumeScale = clampedScale;
            voice.BusyUntil = now + Math.Max(0.001d, registration.Clip.length);
            voice.PausedAt = 0d;
            voice.IsPaused = false;
            voice.Source.clip = registration.Clip;
            voice.Source.loop = false;
            voice.Source.volume = _sfxVolume * clampedScale;
            voice.Source.mute = _sfxMuted;
            voice.Source.Play();

            registration.LastPlayedAt = now;

            if (_isPaused)
            {
                voice.Source.Pause();
                voice.PausedAt = now;
                voice.IsPaused = true;
            }

            return true;
        }

        /// <summary>
        /// 停止所有活跃 SFX 声部，但不改变 AudioClip 注册；即使宿主、AudioSource
        /// 或服务已经被释放，也可安全调用。
        /// </summary>
        public void StopAllSfx()
        {
            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                ResetVoice(_sfxVoices[i]);
            }
        }

        /// <summary>
        /// 暂停 BGM 与当前占用中的 SFX 声部；可以安全地重复调用。
        /// </summary>
        public void Pause()
        {
            ThrowIfUnavailable();

            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
            _pauseStartedAt = Time.realtimeSinceStartupAsDouble;

            if (_bgmRequested)
            {
                _bgmSource.Pause();
                _bgmPausedByService = true;
            }

            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                SfxVoice voice = _sfxVoices[i];
                if (IsVoiceBusy(voice, _pauseStartedAt))
                {
                    voice.Source.Pause();
                    voice.PausedAt = _pauseStartedAt;
                    voice.IsPaused = true;
                }
            }
        }

        /// <summary>
        /// 只恢复由 <see cref="Pause"/> 暂停的声部；可以安全地重复调用。
        /// </summary>
        public void Resume()
        {
            ThrowIfUnavailable();

            if (!_isPaused)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            _isPaused = false;

            if (_bgmPausedByService && _bgmRequested)
            {
                _bgmSource.UnPause();
            }

            _bgmPausedByService = false;

            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                SfxVoice voice = _sfxVoices[i];
                if (!voice.IsPaused)
                {
                    continue;
                }

                // 每个声部记录自己的暂停起点，因为暂停期间新播放的 SFX 并未经历整段全局暂停时间。
                voice.BusyUntil += Math.Max(0d, now - voice.PausedAt);
                voice.PausedAt = 0d;
                voice.IsPaused = false;
                voice.Source.UnPause();
            }
        }

        /// <summary>
        /// 不执行额外初始化，因为 AudioSource 已在构造函数中创建。
        /// </summary>
        /// <param name="app">持有此模块的框架应用；本模块不需要读取它。</param>
        public void Initialize(MFrameworkApp app)
        {
        }

        /// <summary>
        /// 关闭音频模块，并释放其创建的 AudioSource。
        /// </summary>
        public void Shutdown()
        {
            Dispose();
        }

        /// <summary>
        /// 停止播放、清空注册，并移除 MAudio 创建的 AudioSource 组件；可以安全地重复调用。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            // 先标记关闭，避免释放过程中发生重入时再次操作半释放的音频图。
            _isDisposed = true;
            StopBgm();
            StopAllSfx();

            _registrations.Clear();
            DestroyObject(_bgmSource);
            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                DestroyObject(_sfxVoices[i].Source);
            }

            _isPaused = false;
        }

        private AudioRegistration GetRegistration(MAudioId id)
        {
            ValidateId(id);

            if (!_registrations.TryGetValue(id, out AudioRegistration registration))
            {
                throw new KeyNotFoundException($"音频 ID '{id}' 尚未注册任何 AudioClip。");
            }

            if (registration.Clip == null)
            {
                throw new InvalidOperationException(
                    $"音频 ID '{id}' 对应的 AudioClip 已被销毁或释放。");
            }

            return registration;
        }

        private SfxVoice FindAvailableVoice(double now)
        {
            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                SfxVoice voice = _sfxVoices[i];
                if (IsVoiceBusy(voice, now))
                {
                    continue;
                }

                ResetVoice(voice);
                return voice;
            }

            return null;
        }

        private int CountBusyVoices(MAudioId id, double now)
        {
            int count = 0;
            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                SfxVoice voice = _sfxVoices[i];
                if (voice.HasId && voice.Id == id && IsVoiceBusy(voice, now))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsVoiceBusy(SfxVoice voice, double now)
        {
            return voice.HasId && (voice.IsPaused || now < voice.BusyUntil);
        }

        private void RefreshSfxVolumes()
        {
            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                SfxVoice voice = _sfxVoices[i];
                voice.Source.volume = _sfxVolume * voice.VolumeScale;
            }
        }

        private void StopVoices(MAudioId id)
        {
            if (_hasCurrentBgm && _currentBgmId == id)
            {
                StopBgm();
            }

            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                SfxVoice voice = _sfxVoices[i];
                if (voice.HasId && voice.Id == id)
                {
                    ResetVoice(voice);
                }
            }
        }

        private static void ResetVoice(SfxVoice voice)
        {
            if (voice.Source != null)
            {
                voice.Source.Stop();
                voice.Source.clip = null;
            }

            voice.HasId = false;
            voice.Id = default;
            voice.VolumeScale = 1f;
            voice.BusyUntil = 0d;
            voice.PausedAt = 0d;
            voice.IsPaused = false;
        }

        private static AudioSource CreateSource(GameObject host, bool loop)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private static void ValidateId(MAudioId id)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("音频 ID 必须包含非空字符串。", nameof(id));
            }
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "音量参数必须是有限数字（不能为 NaN 或无穷大）。");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private void ThrowIfUnavailable()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MAudio));
            }

            if (_host == null)
            {
                throw new InvalidOperationException("承载 MAudio 的宿主 GameObject 已被销毁。");
            }

            if (_bgmSource == null)
            {
                throw new InvalidOperationException("MAudio 持有的 BGM AudioSource 已被销毁。");
            }

            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                if (_sfxVoices[i].Source == null)
                {
                    throw new InvalidOperationException(
                        $"MAudio 持有的第 {i} 个 SFX AudioSource 已被销毁。");
                }
            }
        }

        private sealed class AudioRegistration
        {
            public AudioRegistration(
                AudioClip clip,
                float minimumIntervalSeconds,
                int maxConcurrentInstances)
            {
                Clip = clip;
                MinimumIntervalSeconds = minimumIntervalSeconds;
                MaxConcurrentInstances = maxConcurrentInstances;
                LastPlayedAt = double.NegativeInfinity;
            }

            public AudioClip Clip { get; }

            public float MinimumIntervalSeconds { get; }

            public int MaxConcurrentInstances { get; }

            public double LastPlayedAt { get; set; }
        }

        private sealed class SfxVoice
        {
            public SfxVoice(AudioSource source)
            {
                Source = source;
                VolumeScale = 1f;
            }

            public AudioSource Source { get; }

            public MAudioId Id { get; set; }

            public bool HasId { get; set; }

            public float VolumeScale { get; set; }

            public double BusyUntil { get; set; }

            public double PausedAt { get; set; }

            public bool IsPaused { get; set; }
        }
    }
}
