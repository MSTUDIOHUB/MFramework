using System;
using System.Collections.Generic;
using UnityEngine;

namespace MFramework
{
    /// <summary>
    /// 为玩法代码提供一个稳定入口，并把调用转发给选定的 <see cref="IMPlatformBridge"/>。
    /// </summary>
    /// <remarks>
    /// 平台桥接没有统一的取消协议。服务关闭时会清空仍在等待的初始化回调；
    /// 如果桥接之后才返回结果，该迟到回调会被忽略。
    /// </remarks>
    public sealed class MPlatformService : IMFrameworkModule, IDisposable
    {
        private readonly IMPlatformBridge _bridge;
        private readonly List<Action<MPlatformInitializationResult>> _initializationCallbacks =
            new List<Action<MPlatformInitializationResult>>();

        private bool _isInitializing;
        private bool _isDisposed;
        private int _initializationGeneration;

        /// <summary>
        /// 使用显式指定的平台实现创建服务。
        /// </summary>
        /// <param name="bridge">接收全部平台操作的桥接。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="bridge"/> 为 null 时抛出。</exception>
        public MPlatformService(IMPlatformBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <summary>
        /// 获取当前平台桥接。
        /// </summary>
        /// <exception cref="ObjectDisposedException">服务已经关闭。</exception>
        public IMPlatformBridge Bridge
        {
            get
            {
                ThrowIfDisposed();
                return _bridge;
            }
        }

        /// <summary>
        /// 获取当前桥接报告的诊断名称。
        /// </summary>
        /// <exception cref="ObjectDisposedException">服务已经关闭。</exception>
        public string Name
        {
            get
            {
                ThrowIfDisposed();
                try
                {
                    return string.IsNullOrWhiteSpace(_bridge.Name)
                        ? _bridge.GetType().Name
                        : _bridge.Name;
                }
                catch (Exception exception)
                {
                    LogBridgeFailure("读取平台名称", exception);
                    return _bridge.GetType().Name;
                }
            }
        }

        /// <summary>
        /// 获取当前桥接是否已经可以接收平台调用。
        /// </summary>
        /// <exception cref="ObjectDisposedException">服务已经关闭。</exception>
        public bool IsReady
        {
            get
            {
                ThrowIfDisposed();
                return GetReadySafely();
            }
        }

        /// <summary>
        /// 初始化当前桥接，并合并同时发起的初始化请求。
        /// </summary>
        /// <param name="completed">
        /// 只调用一次的完成回调。回调异常会写入日志，确保其他等待者仍能收到结果。
        /// </param>
        /// <remarks>
        /// 桥接已经就绪时会立即执行回调。初始化失败后可以再次调用本方法进行重试。
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="completed"/> 为 null。</exception>
        /// <exception cref="ObjectDisposedException">服务已经关闭。</exception>
        public void Initialize(Action<MPlatformInitializationResult> completed)
        {
            ThrowIfDisposed();

            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            if (GetReadySafely() && !_isInitializing)
            {
                InvokeCallbackSafely(completed, MPlatformInitializationResult.Success());
                return;
            }

            _initializationCallbacks.Add(completed);
            if (_isInitializing)
            {
                return;
            }

            _isInitializing = true;
            int generation = ++_initializationGeneration;

            try
            {
                _bridge.Initialize(result => CompleteInitialization(generation, result));
            }
            catch (Exception exception)
            {
                string message = string.IsNullOrWhiteSpace(exception.Message)
                    ? exception.GetType().Name
                    : exception.Message;
                CompleteInitialization(
                    generation,
                    MPlatformInitializationResult.Failure(message));
            }
        }

        /// <summary>
        /// 通过当前桥接请求一次短震动；桥接未就绪或调用失败时安全跳过。
        /// </summary>
        /// <exception cref="ObjectDisposedException">服务已经关闭。</exception>
        public void VibrateShort()
        {
            ThrowIfDisposed();
            if (!GetReadySafely())
            {
                return;
            }

            try
            {
                _bridge.VibrateShort();
            }
            catch (Exception exception)
            {
                LogBridgeFailure("短震动", exception);
            }
        }

        /// <summary>
        /// 通过当前平台桥接读取字符串。
        /// </summary>
        /// <param name="key">非空的存储 Key。</param>
        /// <param name="fallback">尚未保存该 Key 时返回的默认值。</param>
        /// <returns>已保存的值；不存在时返回 <paramref name="fallback"/>。</returns>
        /// <remarks>桥接未就绪或读取失败时同样返回 <paramref name="fallback"/>。</remarks>
        /// <exception cref="ObjectDisposedException">服务已经关闭。</exception>
        public string LoadString(string key, string fallback)
        {
            ThrowIfDisposed();
            if (!GetReadySafely())
            {
                return fallback;
            }

            try
            {
                return _bridge.LoadString(key, fallback);
            }
            catch (Exception exception)
            {
                LogBridgeFailure("读取平台存储", exception);
                return fallback;
            }
        }

        /// <summary>
        /// 通过当前平台桥接保存字符串。
        /// </summary>
        /// <param name="key">非空的存储 Key。</param>
        /// <param name="value">需要保存的非 null 字符串。</param>
        /// <remarks>桥接未就绪或写入失败时安全跳过，并把失败写入警告日志。</remarks>
        /// <exception cref="ObjectDisposedException">服务已经关闭。</exception>
        public void SaveString(string key, string value)
        {
            ThrowIfDisposed();
            if (!GetReadySafely())
            {
                return;
            }

            try
            {
                _bridge.SaveString(key, value);
            }
            catch (Exception exception)
            {
                LogBridgeFailure("写入平台存储", exception);
            }
        }

        /// <summary>
        /// 不主动开始异步平台初始化；调用方仍需使用带完成回调的
        /// <see cref="Initialize(Action{MPlatformInitializationResult})"/>。
        /// </summary>
        /// <param name="app">持有此模块的框架应用；本服务不需要读取它。</param>
        /// <exception cref="ObjectDisposedException">服务已经关闭。</exception>
        void IMFrameworkModule.Initialize(MFrameworkApp app)
        {
            ThrowIfDisposed();
        }

        /// <summary>
        /// 关闭平台服务、移除等待回调，并忽略桥接随后到达的初始化结果。
        /// </summary>
        public void Shutdown()
        {
            Dispose();
        }

        /// <summary>
        /// 关闭平台服务并清空仍在等待的初始化回调；可以安全地重复调用。
        /// </summary>
        /// <remarks>
        /// <see cref="IMPlatformBridge"/> 没有取消初始化的统一协议，因此本方法不会尝试中断桥接；
        /// 桥接在关闭后触发的迟到回调会被安全忽略。
        /// </remarks>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _isInitializing = false;
            _initializationGeneration++;
            _initializationCallbacks.Clear();
        }

        private void CompleteInitialization(
            int generation,
            MPlatformInitializationResult result)
        {
            if (_isDisposed || !_isInitializing || generation != _initializationGeneration)
            {
                return;
            }

            _isInitializing = false;
            result = NormalizeResult(result);
            Action<MPlatformInitializationResult>[] callbacks = _initializationCallbacks.ToArray();
            _initializationCallbacks.Clear();

            for (int i = 0; i < callbacks.Length; i++)
            {
                if (_isDisposed)
                {
                    return;
                }

                InvokeCallbackSafely(callbacks[i], result);
            }
        }

        private static MPlatformInitializationResult NormalizeResult(
            MPlatformInitializationResult result)
        {
            if (result.Succeeded || !string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return result;
            }

            // 结构体的 default 值无法遵守“失败必须带原因”的构造约束，因此在 SDK 边界统一修正。
            return MPlatformInitializationResult.Failure("平台初始化失败，但桥接没有提供错误说明。");
        }

        private static void InvokeCallbackSafely(
            Action<MPlatformInitializationResult> callback,
            MPlatformInitializationResult result)
        {
            try
            {
                callback(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private bool GetReadySafely()
        {
            try
            {
                return _bridge.IsReady;
            }
            catch (Exception exception)
            {
                LogBridgeFailure("读取平台就绪状态", exception);
                return false;
            }
        }

        private static void LogBridgeFailure(string operation, Exception exception)
        {
            Debug.LogWarning($"MPlatform {operation}失败，已使用安全降级。{exception.Message}");
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MPlatformService));
            }
        }
    }
}
