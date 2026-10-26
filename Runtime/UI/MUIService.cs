using System;
using System.Collections.Generic;
using UnityEngine;

namespace MFramework
{
    /// <summary>为每个显式注册的视图类型创建并缓存一个 UGUI 实例。</summary>
    /// <remarks>
    /// MUIService 管理视图生命周期与层级；页面和弹窗导航请使用 <see cref="MUIRouter"/>。
    /// 所有注册均为显式注册，不使用程序集扫描或基于反射的构造方式。
    /// </remarks>
    public sealed class MUIService : IMFrameworkModule, IDisposable
    {
        private readonly Transform _pageLayer;
        private readonly Transform _popupLayer;
        private readonly Transform _overlayLayer;
        private readonly Dictionary<Type, MUIView> _prefabs = new Dictionary<Type, MUIView>();
        private readonly Dictionary<Type, MUIView> _instances = new Dictionary<Type, MUIView>();
        private bool _isDisposed;

        /// <summary>使用三个明确的 Canvas 层级创建 UGUI 服务。</summary>
        /// <param name="pageLayer">完整菜单页面的父级。</param>
        /// <param name="popupLayer">模态弹窗的父级。</param>
        /// <param name="overlayLayer">HUD、加载界面与 Toast 覆盖层的父级。</param>
        /// <exception cref="ArgumentNullException">任意层级为 null。</exception>
        public MUIService(Transform pageLayer, Transform popupLayer, Transform overlayLayer)
        {
            _pageLayer = pageLayer != null ? pageLayer : throw new ArgumentNullException(nameof(pageLayer));
            _popupLayer = popupLayer != null ? popupLayer : throw new ArgumentNullException(nameof(popupLayer));
            _overlayLayer = overlayLayer != null ? overlayLayer : throw new ArgumentNullException(nameof(overlayLayer));

            if (_pageLayer == _popupLayer || _pageLayer == _overlayLayer || _popupLayer == _overlayLayer)
            {
                throw new ArgumentException("Page、Popup 与 Overlay 必须使用三个不同的 UGUI 层级。");
            }
        }

        /// <summary>已注册视图预制体完成实例化和初始化后触发。</summary>
        public event Action<MUIView> ViewCreated;

        /// <summary>按照具体视图类型注册视图预制体。</summary>
        /// <typeparam name="TView">页面、弹窗或覆盖层类型。</typeparam>
        /// <param name="prefab">包含对应类型组件的预制体。</param>
        /// <returns>当前服务，便于编写简洁的启动注册代码。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="prefab"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">同一视图类型已经注册。</exception>
        /// <exception cref="ObjectDisposedException">服务已经释放。</exception>
        public MUIService Register<TView>(TView prefab)
            where TView : MUIView
        {
            ThrowIfDisposed();

            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            Type type = typeof(TView);
            if (_prefabs.ContainsKey(type))
            {
                throw new InvalidOperationException($"视图类型 {type.FullName} 已经注册了 UGUI 预制体，不能重复注册。");
            }

            _prefabs.Add(type, prefab);
            return this;
        }

        /// <summary>获取指定视图类型是否已经注册预制体。</summary>
        /// <typeparam name="TView">视图类型。</typeparam>
        /// <returns>已注册时返回 <see langword="true"/>。</returns>
        public bool IsRegistered<TView>()
            where TView : MUIView
        {
            ThrowIfDisposed();
            return _prefabs.ContainsKey(typeof(TView));
        }

        /// <summary>显示已注册视图，但不修改导航栈。</summary>
        /// <typeparam name="TView">已注册的视图类型。</typeparam>
        /// <param name="arguments">传给视图的可选参数。</param>
        /// <returns>缓存的视图实例。</returns>
        /// <exception cref="InvalidOperationException">视图尚未注册。</exception>
        /// <exception cref="Exception">视图生命周期钩子执行失败。</exception>
        /// <exception cref="ObjectDisposedException">服务已经释放。</exception>
        public TView Show<TView>(object arguments = null)
            where TView : MUIView
        {
            return (TView)Show(typeof(TView), arguments);
        }

        /// <summary>隐藏缓存视图，但不销毁实例。</summary>
        /// <typeparam name="TView">视图类型。</typeparam>
        /// <returns>实例存在时返回 <see langword="true"/>。</returns>
        /// <exception cref="ObjectDisposedException">服务已经释放。</exception>
        public bool Hide<TView>()
            where TView : MUIView
        {
            return Hide(typeof(TView));
        }

        /// <summary>尝试获取缓存的视图实例。</summary>
        /// <typeparam name="TView">视图类型。</typeparam>
        /// <param name="view">实例存在时接收缓存实例。</param>
        /// <returns>视图已经创建时返回 <see langword="true"/>。</returns>
        /// <exception cref="ObjectDisposedException">服务已经释放。</exception>
        public bool TryGet<TView>(out TView view)
            where TView : MUIView
        {
            ThrowIfDisposed();

            MUIView instance;
            if (_instances.TryGetValue(typeof(TView), out instance) && instance != null)
            {
                view = (TView)instance;
                return true;
            }

            view = null;
            return false;
        }

        /// <summary>不执行额外工作，因为视图预制体会在启动阶段显式注册。</summary>
        /// <param name="app">持有此模块的框架应用。</param>
        /// <exception cref="ObjectDisposedException">服务已经释放。</exception>
        public void Initialize(MFrameworkApp app)
        {
            ThrowIfDisposed();
        }

        /// <summary>销毁全部缓存视图实例。</summary>
        /// <exception cref="Exception">一个或多个视图生命周期钩子释放失败。</exception>
        public void Shutdown()
        {
            Dispose();
        }

        /// <summary>销毁全部缓存视图实例，并清空所有预制体注册。</summary>
        /// <exception cref="Exception">一个或多个视图生命周期钩子释放失败。</exception>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            ViewCreated = null;
            var exceptions = new List<Exception>();

            try
            {
                foreach (KeyValuePair<Type, MUIView> pair in _instances)
                {
                    MUIView view = pair.Value;
                    if (view == null)
                    {
                        continue;
                    }

                    try
                    {
                        view.DisposeView();
                    }
                    catch (Exception disposeException)
                    {
                        exceptions.Add(disposeException);
                    }

                    try
                    {
                        DestroyView(view);
                    }
                    catch (Exception destroyException)
                    {
                        exceptions.Add(destroyException);
                    }
                }
            }
            finally
            {
                _instances.Clear();
                _prefabs.Clear();
            }

            MUIExceptionUtility.ThrowIfAny("一个或多个 UGUI 视图释放失败。", exceptions);
        }

        internal MUIView Show(Type viewType, object arguments)
        {
            ThrowIfDisposed();
            MUIView view = GetOrCreate(viewType);
            ThrowIfDisposed();
            view.ShowView(arguments);
            return view;
        }

        internal bool Hide(Type viewType)
        {
            ThrowIfDisposed();

            MUIView view;
            if (!_instances.TryGetValue(viewType, out view) || view == null)
            {
                return false;
            }

            view.HideView();
            return true;
        }

        internal void VisitCreatedViews(Action<MUIView> visitor)
        {
            ThrowIfDisposed();
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor));
            }

            foreach (KeyValuePair<Type, MUIView> pair in _instances)
            {
                if (pair.Value != null)
                {
                    visitor(pair.Value);
                }
            }
        }

        private MUIView GetOrCreate(Type viewType)
        {
            if (viewType == null || !typeof(MUIView).IsAssignableFrom(viewType))
            {
                throw new ArgumentException("请求的视图类型必须继承自 MUIView。", nameof(viewType));
            }

            MUIView instance;
            if (_instances.TryGetValue(viewType, out instance) && instance != null)
            {
                return instance;
            }

            MUIView prefab;
            if (!_prefabs.TryGetValue(viewType, out prefab) || prefab == null)
            {
                throw new InvalidOperationException($"未找到视图类型 {viewType.FullName} 已注册的 UGUI 预制体。");
            }

            Transform parent = GetLayer(viewType);
            instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            try
            {
                instance.gameObject.name = prefab.gameObject.name;
                instance.gameObject.SetActive(false);
                instance.CreateView();
            }
            catch
            {
                DestroyView(instance);
                throw;
            }

            _instances[viewType] = instance;
            NotifyViewCreated(instance);
            return instance;
        }

        private void NotifyViewCreated(MUIView instance)
        {
            Action<MUIView> handlers = ViewCreated;
            if (handlers == null)
            {
                return;
            }

            var exceptions = new List<Exception>();
            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<MUIView>)invocationList[i])(instance);
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            MUIExceptionUtility.ThrowIfAny("UGUI 视图创建通知处理失败。", exceptions);
        }

        private Transform GetLayer(Type viewType)
        {
            if (typeof(MUIPage).IsAssignableFrom(viewType))
            {
                return _pageLayer;
            }

            if (typeof(MUIPopup).IsAssignableFrom(viewType))
            {
                return _popupLayer;
            }

            if (typeof(MUIOverlay).IsAssignableFrom(viewType))
            {
                return _overlayLayer;
            }

            throw new InvalidOperationException(
                $"{viewType.FullName} 必须继承自 MUIPage、MUIPopup 或 MUIOverlay。");
        }

        private static void DestroyView(MUIView view)
        {
            if (view == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(view.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MUIService));
            }
        }
    }
}
