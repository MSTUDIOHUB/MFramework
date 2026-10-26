using System;
using System.Collections.Generic;
using System.Threading;

namespace MFramework
{
    /// <summary>
    /// 实现一个同步、类型安全、零装箱，并支持释放订阅的事件总线。
    /// </summary>
    /// <remarks>
    /// 该实现只能在创建它的线程中使用，通常是 Unity 主线程。
    /// 发布事件时会遍历泛型通道的稳定快照，因此既消除了值类型事件装箱，也可以安全地在发布期间增删订阅。
    /// </remarks>
    public sealed class MEventBus : IMEventBus, IMFrameworkModule
    {
        private readonly Dictionary<Type, IEventChannel> _channels =
            new Dictionary<Type, IEventChannel>();

        private readonly int _ownerThreadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// 为指定事件类型订阅一个处理器。
        /// </summary>
        /// <typeparam name="TEvent">事件载荷类型。</typeparam>
        /// <param name="handler">发布事件时同步调用的回调。</param>
        /// <param name="owner">可选的 Unity 所有者。所有者销毁后，该订阅会失效。</param>
        /// <returns>订阅句柄；释放该句柄时会移除订阅。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">该方法由非创建线程调用。</exception>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler, UnityEngine.Object owner = null)
        {
            EnsureOwnerThread();
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type eventType = typeof(TEvent);
            IEventChannel rawChannel;
            EventChannel<TEvent> channel;
            if (!_channels.TryGetValue(eventType, out rawChannel))
            {
                channel = new EventChannel<TEvent>(this, eventType);
                _channels.Add(eventType, channel);
            }
            else
            {
                channel = (EventChannel<TEvent>)rawChannel;
            }

            return channel.Subscribe(handler, owner);
        }

        /// <summary>
        /// 向当前订阅者快照同步发布一个事件。
        /// </summary>
        /// <typeparam name="TEvent">事件载荷类型。</typeparam>
        /// <param name="message">要发布的事实事件。</param>
        /// <exception cref="InvalidOperationException">该方法由非创建线程调用。</exception>
        /// <exception cref="MEventDispatchException">一个或多个订阅者执行失败。</exception>
        public void Publish<TEvent>(TEvent message)
        {
            EnsureOwnerThread();

            IEventChannel rawChannel;
            if (!_channels.TryGetValue(typeof(TEvent), out rawChannel))
            {
                return;
            }

            ((EventChannel<TEvent>)rawChannel).Publish(message);
        }

        /// <summary>移除全部订阅。</summary>
        public void Clear()
        {
            EnsureOwnerThread();

            foreach (KeyValuePair<Type, IEventChannel> pair in _channels)
            {
                pair.Value.Clear();
            }

            _channels.Clear();
        }

        /// <summary>该模块无需启动订阅，因此初始化时不执行任何操作。</summary>
        /// <param name="app">持有该模块的框架应用。</param>
        public void Initialize(MFrameworkApp app)
        {
        }

        /// <summary>清除全部订阅。</summary>
        public void Shutdown()
        {
            Clear();
        }

        private void RemoveChannelIfEmpty(Type eventType, IEventChannel channel)
        {
            if (channel.Count == 0)
            {
                _channels.Remove(eventType);
            }
        }

        private void EnsureOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
            {
                throw new InvalidOperationException("MEventBus 只能在其创建线程（通常是 Unity 主线程）中使用。");
            }
        }

        private interface IEventChannel
        {
            int Count { get; }
            void Clear();
        }

        private sealed class EventChannel<TEvent> : IEventChannel
        {
            private readonly MEventBus _ownerBus;
            private readonly Type _eventType;
            private readonly List<EventSubscription<TEvent>> _subscriptions =
                new List<EventSubscription<TEvent>>();
            private readonly List<EventSubscription<TEvent>> _dispatchBuffer =
                new List<EventSubscription<TEvent>>();
            private bool _isPublishing;

            public EventChannel(MEventBus ownerBus, Type eventType)
            {
                _ownerBus = ownerBus;
                _eventType = eventType;
            }

            public int Count => _subscriptions.Count;

            public IDisposable Subscribe(Action<TEvent> handler, UnityEngine.Object owner)
            {
                EventSubscription<TEvent> subscription = new EventSubscription<TEvent>(handler, owner);
                subscription.SetDisposeCallback(() => Remove(subscription));
                _subscriptions.Add(subscription);
                return subscription;
            }

            public void Publish(TEvent message)
            {
                if (_subscriptions.Count == 0)
                {
                    return;
                }

                // 非重入发布直接复用通道内的 _dispatchBuffer，实现零 GC 快照遍历；
                // 若在回调中嵌套发布同类型事件，则降级分配临时列表保证快照隔离。
                bool useSharedBuffer = !_isPublishing;
                List<EventSubscription<TEvent>> snapshot;
                if (useSharedBuffer)
                {
                    _isPublishing = true;
                    _dispatchBuffer.Clear();
                    _dispatchBuffer.AddRange(_subscriptions);
                    snapshot = _dispatchBuffer;
                }
                else
                {
                    snapshot = new List<EventSubscription<TEvent>>(_subscriptions);
                }

                List<Exception> exceptions = null;
                try
                {
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        EventSubscription<TEvent> subscription = snapshot[i];
                        if (!subscription.IsAlive)
                        {
                            subscription.Dispose();
                            continue;
                        }

                        try
                        {
                            subscription.Invoke(message);
                        }
                        catch (Exception exception)
                        {
                            if (exceptions == null)
                            {
                                exceptions = new List<Exception>();
                            }

                            exceptions.Add(exception);
                        }
                    }
                }
                finally
                {
                    if (useSharedBuffer)
                    {
                        _dispatchBuffer.Clear();
                        _isPublishing = false;
                    }
                }

                if (exceptions != null)
                {
                    throw new MEventDispatchException(_eventType, exceptions);
                }
            }

            public void Clear()
            {
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    _subscriptions[i].DisposeWithoutCallback();
                }

                _subscriptions.Clear();
            }

            private void Remove(EventSubscription<TEvent> subscription)
            {
                if (_subscriptions.Remove(subscription))
                {
                    _ownerBus.RemoveChannelIfEmpty(_eventType, this);
                }
            }
        }

        private sealed class EventSubscription<TEvent> : IDisposable
        {
            private Action<TEvent> _handler;
            private readonly UnityEngine.Object _owner;
            private readonly bool _hasOwner;
            private Action _disposeCallback;
            private bool _isDisposed;

            public EventSubscription(Action<TEvent> handler, UnityEngine.Object owner)
            {
                _handler = handler;
                _owner = owner;
                // ReferenceEquals 区分“没有传 owner”和“传入了已被 Unity 销毁的伪 null 对象”。
                _hasOwner = !ReferenceEquals(owner, null);
            }

            public bool IsAlive => !_isDisposed && (!_hasOwner || _owner != null);

            public void SetDisposeCallback(Action disposeCallback)
            {
                _disposeCallback = disposeCallback;
            }

            public void Invoke(TEvent message)
            {
                if (IsAlive)
                {
                    _handler(message);
                }
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _handler = null;
                Action callback = _disposeCallback;
                _disposeCallback = null;
                callback?.Invoke();
            }

            public void DisposeWithoutCallback()
            {
                _isDisposed = true;
                _handler = null;
                _disposeCallback = null;
            }
        }
    }
}
