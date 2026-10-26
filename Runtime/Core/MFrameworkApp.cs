using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>
    /// 持有框架服务，并驱动它们的显式生命周期。
    /// </summary>
    /// <remarks>
    /// 服务按照注册顺序初始化，并按照相反顺序关闭。
    /// 所有注册均为显式操作；该类型不会扫描程序集，也不会通过反射创建服务。
    /// </remarks>
    public sealed class MFrameworkApp : IDisposable
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly List<object> _orderedServices = new List<object>();
        private readonly List<IMFrameworkModule> _initializedModules = new List<IMFrameworkModule>();

        /// <summary>获取框架应用当前的生命周期状态。</summary>
        public MFrameworkAppState State { get; private set; } = MFrameworkAppState.Created;

        /// <summary>
        /// 使用接口或具体服务类型作为键注册一个服务。
        /// </summary>
        /// <typeparam name="TService">调用方获取服务时使用的类型键。</typeparam>
        /// <param name="service">要注册的服务实例，不能为 null。</param>
        /// <returns>当前框架应用，便于在启动代码中链式注册。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="service"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">注册阶段已经结束，或该类型键已经注册。</exception>
        public MFrameworkApp Register<TService>(TService service)
            where TService : class
        {
            EnsureCanRegister();

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Type serviceType = typeof(TService);
            if (_services.ContainsKey(serviceType))
            {
                throw new InvalidOperationException($"服务类型 {serviceType.FullName} 已经注册，不能重复注册。");
            }

            _services.Add(serviceType, service);
            if (!ContainsReference(_orderedServices, service))
            {
                _orderedServices.Add(service);
            }

            return this;
        }

        /// <summary>
        /// 检查指定类型键的服务是否已经注册。
        /// </summary>
        /// <typeparam name="TService">注册服务时使用的类型键。</typeparam>
        /// <returns>已注册时返回 <see langword="true"/>。</returns>
        public bool Has<TService>()
            where TService : class
        {
            return _services.ContainsKey(typeof(TService));
        }

        /// <summary>
        /// 获取一个必须存在的已注册服务。
        /// </summary>
        /// <typeparam name="TService">注册服务时使用的类型键。</typeparam>
        /// <returns>已经注册的服务。</returns>
        /// <exception cref="InvalidOperationException">请求的类型键没有对应的已注册服务。</exception>
        public TService Get<TService>()
            where TService : class
        {
            object service;
            if (!_services.TryGetValue(typeof(TService), out service))
            {
                throw new InvalidOperationException($"未找到已注册的服务类型：{typeof(TService).FullName}。");
            }

            return (TService)service;
        }

        /// <summary>
        /// 尝试获取一个已注册服务。
        /// </summary>
        /// <typeparam name="TService">注册服务时使用的类型键。</typeparam>
        /// <param name="service">找到服务时，接收对应的服务实例。</param>
        /// <returns>找到服务时返回 <see langword="true"/>。</returns>
        public bool TryGet<TService>(out TService service)
            where TService : class
        {
            object value;
            if (_services.TryGetValue(typeof(TService), out value))
            {
                service = (TService)value;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// 初始化所有已经注册的 <see cref="IMFrameworkModule"/>。
        /// </summary>
        /// <exception cref="InvalidOperationException">框架应用当前不在 Created 状态。</exception>
        /// <exception cref="MFrameworkInitializationException">某个模块初始化失败。</exception>
        public void Initialize()
        {
            if (State != MFrameworkAppState.Created)
            {
                throw new InvalidOperationException("MFrameworkApp 只能初始化一次。");
            }

            State = MFrameworkAppState.Initializing;
            for (int i = 0; i < _orderedServices.Count; i++)
            {
                IMFrameworkModule module = _orderedServices[i] as IMFrameworkModule;
                if (module == null)
                {
                    continue;
                }

                try
                {
                    module.Initialize(this);
                    _initializedModules.Add(module);
                }
                catch (Exception exception)
                {
                    List<Exception> rollbackExceptions = new List<Exception>();

                    // 初始化可能在分配资源后才失败，因此失败模块自身也必须获得一次清理机会。
                    try
                    {
                        module.Shutdown();
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackExceptions.Add(rollbackException);
                    }

                    rollbackExceptions.AddRange(RollbackInitializedModules());
                    State = MFrameworkAppState.Faulted;
                    throw new MFrameworkInitializationException(module.GetType(), exception, rollbackExceptions);
                }
            }

            State = MFrameworkAppState.Running;
        }

        /// <summary>
        /// 按照注册顺序 Tick 所有帧更新服务。
        /// </summary>
        /// <param name="time">缩放与非缩放的帧时间。</param>
        public void Tick(MFrameTime time)
        {
            if (State != MFrameworkAppState.Running)
            {
                return;
            }

            for (int i = 0; i < _orderedServices.Count; i++)
            {
                IMFrameworkTickable tickable = _orderedServices[i] as IMFrameworkTickable;
                tickable?.Tick(time);
            }
        }

        /// <summary>
        /// 按照注册顺序 Tick 所有固定时间步长服务。
        /// </summary>
        /// <param name="fixedDeltaTime">固定时间步长，单位为秒。</param>
        public void FixedTick(float fixedDeltaTime)
        {
            if (State != MFrameworkAppState.Running)
            {
                return;
            }

            for (int i = 0; i < _orderedServices.Count; i++)
            {
                IMFrameworkFixedTickable tickable = _orderedServices[i] as IMFrameworkFixedTickable;
                tickable?.FixedTick(fixedDeltaTime);
            }
        }

        /// <summary>
        /// 按照注册顺序 Tick 所有帧末更新服务。
        /// </summary>
        /// <param name="time">缩放与非缩放的帧时间。</param>
        public void LateTick(MFrameTime time)
        {
            if (State != MFrameworkAppState.Running)
            {
                return;
            }

            for (int i = 0; i < _orderedServices.Count; i++)
            {
                IMFrameworkLateTickable tickable = _orderedServices[i] as IMFrameworkLateTickable;
                tickable?.LateTick(time);
            }
        }

        /// <summary>
        /// 按照初始化顺序的反向顺序，关闭所有已经初始化的模块。
        /// </summary>
        /// <exception cref="MFrameworkShutdownException">一个或多个模块关闭失败。</exception>
        public void Shutdown()
        {
            if (State == MFrameworkAppState.Shutdown || State == MFrameworkAppState.ShuttingDown)
            {
                return;
            }

            if (State == MFrameworkAppState.Created)
            {
                State = MFrameworkAppState.ShuttingDown;
                List<Exception> uninitializedExceptions = ShutdownRegisteredModules();
                State = MFrameworkAppState.Shutdown;

                if (uninitializedExceptions.Count > 0)
                {
                    throw new MFrameworkShutdownException(uninitializedExceptions);
                }

                return;
            }

            State = MFrameworkAppState.ShuttingDown;
            List<Exception> exceptions = RollbackInitializedModules();
            State = MFrameworkAppState.Shutdown;

            if (exceptions.Count > 0)
            {
                throw new MFrameworkShutdownException(exceptions);
            }
        }

        /// <summary>关闭框架应用。</summary>
        public void Dispose()
        {
            Shutdown();
        }

        private static bool ContainsReference(List<object> values, object candidate)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (ReferenceEquals(values[i], candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureCanRegister()
        {
            if (State != MFrameworkAppState.Created)
            {
                throw new InvalidOperationException("只能在框架初始化前（Created 状态）注册服务。");
            }
        }

        private List<Exception> RollbackInitializedModules()
        {
            List<Exception> exceptions = new List<Exception>();
            for (int i = _initializedModules.Count - 1; i >= 0; i--)
            {
                try
                {
                    _initializedModules[i].Shutdown();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            _initializedModules.Clear();
            return exceptions;
        }

        private List<Exception> ShutdownRegisteredModules()
        {
            List<Exception> exceptions = new List<Exception>();
            for (int i = _orderedServices.Count - 1; i >= 0; i--)
            {
                IMFrameworkModule module = _orderedServices[i] as IMFrameworkModule;
                if (module == null)
                {
                    continue;
                }

                try
                {
                    module.Shutdown();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            return exceptions;
        }
    }
}
