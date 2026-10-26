using System;
using System.Collections.Generic;

namespace MFramework
{
    /// <summary>协调缓存的 UGUI 页面、模态弹窗、通用按钮以及统一返回行为。</summary>
    /// <remarks>
    /// 导航操作不会同步重入。视图生命周期钩子抛出异常时，路由器会先恢复一致的可见状态与栈状态，
    /// 再把原异常交还给调用方；回滚阶段也失败时会抛出 <see cref="AggregateException"/>。
    /// </remarks>
    public sealed class MUIRouter : IMFrameworkModule, IDisposable
    {
        private readonly MUIService _ui;
        private readonly List<MUIPage> _pages = new List<MUIPage>();
        private readonly List<MUIPopup> _popups = new List<MUIPopup>();
        private readonly HashSet<MUIView> _boundViews = new HashSet<MUIView>();

        private bool _isNavigating;
        private bool _isDisposed;

        /// <summary>为一个 UGUI 服务创建路由器。</summary>
        /// <param name="ui">持有已注册视图实例的服务。</param>
        /// <exception cref="ArgumentNullException"><paramref name="ui"/> 为 null。</exception>
        /// <exception cref="ObjectDisposedException"><paramref name="ui"/> 已经释放。</exception>
        public MUIRouter(MUIService ui)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _ui.ViewCreated += BindView;

            try
            {
                // Router 可以晚于 Service 创建，因此还要补绑已经缓存的视图。
                _ui.VisitCreatedViews(BindView);
            }
            catch
            {
                _ui.ViewCreated -= BindView;
                throw;
            }
        }

        /// <summary>返回请求到达根页面时触发。</summary>
        /// <remarks>游戏层可以把此事件映射为暂停、退出确认或平台桥接操作。</remarks>
        public event Action RootBackRequested;

        /// <summary>视图因校验失败而拒绝操作时触发。</summary>
        public event Action<MUIView, string> ActionRejected;

        /// <summary>获取当前可见页面；首次 Push 之前返回 null。</summary>
        public MUIPage CurrentPage => _pages.Count == 0 ? null : _pages[_pages.Count - 1];

        /// <summary>获取最上层模态弹窗；没有打开弹窗时返回 null。</summary>
        public MUIPopup CurrentPopup => _popups.Count == 0 ? null : _popups[_popups.Count - 1];

        /// <summary>获取导航历史中的页面数量。</summary>
        public int PageCount => _pages.Count;

        /// <summary>获取当前打开的弹窗数量。</summary>
        public int PopupCount => _popups.Count;

        /// <summary>获取当前是否存在可由框架关闭的弹窗或上一层页面。</summary>
        public bool CanGoBack => _popups.Count > 0 || _pages.Count > 1;

        /// <summary>压入一个页面；若同类型缓存页已在历史中，则回到该页并移除其后的历史。</summary>
        /// <typeparam name="TPage">已注册的页面类型。</typeparam>
        /// <param name="arguments">可选页面参数。</param>
        /// <returns>当前可见页面。</returns>
        /// <exception cref="InvalidOperationException">另一个导航操作尚未完成。</exception>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public TPage Push<TPage>(object arguments = null)
            where TPage : MUIPage
        {
            return ExecuteNavigation(() => (TPage)PushCore(typeof(TPage), arguments));
        }

        /// <summary>替换当前页面，同时保留更早的导航历史。</summary>
        /// <remarks>目标缓存页已在更早历史中时，会先移除它原来的历史位置，确保同一实例不会重复入栈。</remarks>
        /// <typeparam name="TPage">已注册的页面类型。</typeparam>
        /// <param name="arguments">可选页面参数。</param>
        /// <returns>替换后显示的页面。</returns>
        /// <exception cref="InvalidOperationException">另一个导航操作尚未完成。</exception>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public TPage Replace<TPage>(object arguments = null)
            where TPage : MUIPage
        {
            return ExecuteNavigation(() => (TPage)ReplaceCore(typeof(TPage), arguments));
        }

        /// <summary>在页面栈中返回一级。</summary>
        /// <returns>成功弹出页面时返回 <see langword="true"/>。</returns>
        /// <exception cref="InvalidOperationException">另一个导航操作尚未完成。</exception>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public bool Back()
        {
            return ExecuteNavigation(BackCore);
        }

        /// <summary>关闭中间页面并返回第一个页面。</summary>
        /// <returns>至少移除一个页面时返回 <see langword="true"/>。</returns>
        /// <exception cref="InvalidOperationException">另一个导航操作尚未完成。</exception>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public bool PopToRoot()
        {
            return ExecuteNavigation(PopToRootCore);
        }

        /// <summary>在当前页面上方显示模态弹窗。</summary>
        /// <typeparam name="TPopup">已注册的弹窗类型。</typeparam>
        /// <param name="arguments">可选弹窗参数。</param>
        /// <returns>当前可见弹窗。</returns>
        /// <exception cref="InvalidOperationException">另一个导航操作尚未完成。</exception>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public TPopup ShowPopup<TPopup>(object arguments = null)
            where TPopup : MUIPopup
        {
            return ExecuteNavigation(() =>
            {
                TPopup popup = (TPopup)_ui.Show(typeof(TPopup), arguments);
                int existingIndex = _popups.IndexOf(popup);
                if (existingIndex >= 0)
                {
                    _popups.RemoveAt(existingIndex);
                }

                _popups.Add(popup);
                return popup;
            });
        }

        /// <summary>关闭最上层弹窗，并传递语义化结果。</summary>
        /// <param name="result">弹窗结果；null 表示取消。</param>
        /// <returns>成功关闭弹窗时返回 <see langword="true"/>。</returns>
        /// <exception cref="InvalidOperationException">另一个导航操作尚未完成。</exception>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public bool CloseTopPopup(MUIPopupResult result = null)
        {
            return ExecuteNavigation(() => CloseTopPopupCore(result));
        }

        /// <summary>显示非模态覆盖层，不修改页面或弹窗历史。</summary>
        /// <typeparam name="TOverlay">已注册的覆盖层类型。</typeparam>
        /// <param name="arguments">可选覆盖层参数。</param>
        /// <returns>当前可见覆盖层。</returns>
        /// <exception cref="InvalidOperationException">另一个导航操作尚未完成。</exception>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public TOverlay ShowOverlay<TOverlay>(object arguments = null)
            where TOverlay : MUIOverlay
        {
            return ExecuteNavigation(() => _ui.Show<TOverlay>(arguments));
        }

        /// <summary>隐藏非模态覆盖层。</summary>
        /// <typeparam name="TOverlay">已注册的覆盖层类型。</typeparam>
        /// <returns>覆盖层实例存在时返回 <see langword="true"/>。</returns>
        /// <exception cref="InvalidOperationException">另一个导航操作尚未完成。</exception>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public bool HideOverlay<TOverlay>()
            where TOverlay : MUIOverlay
        {
            return ExecuteNavigation(_ui.Hide<TOverlay>);
        }

        /// <summary>
        /// 使用相同 UI 规则处理返回按钮、键盘 Escape、Android 返回键或小游戏平台返回操作。
        /// </summary>
        /// <returns>当前视图消费了请求或完成了返回时返回 <see langword="true"/>；请求到达根页面时返回 <see langword="false"/>。</returns>
        /// <exception cref="InvalidOperationException">另一个导航操作尚未完成。</exception>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public bool RequestBack()
        {
            return ExecuteNavigation(RequestBackCore);
        }

        /// <summary>确认路由器尚未释放；视图绑定已在构造阶段完成。</summary>
        /// <param name="app">持有此模块的框架应用。</param>
        /// <exception cref="ObjectDisposedException">路由器已经释放。</exception>
        public void Initialize(MFrameworkApp app)
        {
            ThrowIfDisposed();
        }

        /// <summary>解除路由器订阅、隐藏受管视图，并清空导航状态。</summary>
        public void Shutdown()
        {
            Dispose();
        }

        /// <summary>解除路由器订阅、隐藏受管视图，并清空导航状态。</summary>
        /// <exception cref="InvalidOperationException">正在执行导航操作。</exception>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isNavigating)
            {
                throw new InvalidOperationException("MUIRouter 正在执行导航，不能在视图生命周期钩子中释放路由器。");
            }

            _isDisposed = true;
            _isNavigating = true;
            var exceptions = new List<Exception>();

            try
            {
                for (int i = _popups.Count - 1; i >= 0; i--)
                {
                    TryHide(_popups[i], exceptions);
                }

                for (int i = _pages.Count - 1; i >= 0; i--)
                {
                    TryHide(_pages[i], exceptions);
                }
            }
            finally
            {
                _ui.ViewCreated -= BindView;
                foreach (MUIView view in _boundViews)
                {
                    if (view != null)
                    {
                        view.ActionRequested -= HandleActionRequested;
                    }
                }

                _boundViews.Clear();
                _pages.Clear();
                _popups.Clear();
                RootBackRequested = null;
                ActionRejected = null;
                _isNavigating = false;
            }

            MUIExceptionUtility.ThrowIfAny("MUIRouter 释放受管视图时发生了异常。", exceptions);
        }

        private MUIPage PushCore(Type pageType, object arguments)
        {
            MUIPage originalTop = CurrentPage;
            object originalArguments = originalTop?.Arguments;
            int existingIndex = FindPageIndex(pageType);
            var exceptions = new List<Exception>();

            if (existingIndex >= 0)
            {
                for (int i = _pages.Count - 1; i > existingIndex; i--)
                {
                    TryHide(_pages[i], exceptions);
                }

                if (exceptions.Count > 0)
                {
                    RestoreOriginalPage(originalTop, originalArguments, null, exceptions);
                    MUIExceptionUtility.ThrowIfAny("页面压栈失败，原页面已经恢复。", exceptions);
                }

                MUIPage existing = _pages[existingIndex];
                TryShow(existing, arguments, exceptions);
                if (exceptions.Count > 0)
                {
                    RestoreOriginalPage(originalTop, originalArguments, existing, exceptions);
                    MUIExceptionUtility.ThrowIfAny("页面压栈失败，原页面已经恢复。", exceptions);
                }

                int removeCount = _pages.Count - existingIndex - 1;
                if (removeCount > 0)
                {
                    _pages.RemoveRange(existingIndex + 1, removeCount);
                }

                return existing;
            }

            if (originalTop != null)
            {
                TryHide(originalTop, exceptions);
                if (exceptions.Count > 0)
                {
                    RestoreOriginalPage(originalTop, originalArguments, null, exceptions);
                    MUIExceptionUtility.ThrowIfAny("页面压栈失败，原页面已经恢复。", exceptions);
                }
            }

            MUIPage page = null;
            try
            {
                page = (MUIPage)_ui.Show(pageType, arguments);
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }

            if (exceptions.Count > 0)
            {
                RestoreOriginalPage(originalTop, originalArguments, page, exceptions);
                MUIExceptionUtility.ThrowIfAny("页面压栈失败，原页面已经恢复。", exceptions);
            }

            _pages.Add(page);
            return page;
        }

        private MUIPage ReplaceCore(Type pageType, object arguments)
        {
            MUIPage originalTop = CurrentPage;
            object originalArguments = originalTop?.Arguments;
            var exceptions = new List<Exception>();

            if (originalTop != null)
            {
                TryHide(originalTop, exceptions);
                if (exceptions.Count > 0)
                {
                    RestoreOriginalPage(originalTop, originalArguments, null, exceptions);
                    MUIExceptionUtility.ThrowIfAny("页面替换失败，原页面已经恢复。", exceptions);
                }
            }

            MUIPage page = null;
            try
            {
                page = (MUIPage)_ui.Show(pageType, arguments);
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }

            if (exceptions.Count > 0)
            {
                RestoreOriginalPage(originalTop, originalArguments, page, exceptions);
                MUIExceptionUtility.ThrowIfAny("页面替换失败，原页面已经恢复。", exceptions);
            }

            if (_pages.Count > 0)
            {
                _pages.RemoveAt(_pages.Count - 1);
            }

            int existingIndex = _pages.IndexOf(page);
            if (existingIndex >= 0)
            {
                _pages.RemoveAt(existingIndex);
            }

            _pages.Add(page);
            return page;
        }

        private bool BackCore()
        {
            if (_pages.Count <= 1)
            {
                RootBackRequested?.Invoke();
                return false;
            }

            MUIPage current = _pages[_pages.Count - 1];
            object currentArguments = current.Arguments;
            MUIPage previous = _pages[_pages.Count - 2];
            var exceptions = new List<Exception>();

            TryHide(current, exceptions);
            if (exceptions.Count > 0)
            {
                RestoreOriginalPage(current, currentArguments, null, exceptions);
                MUIExceptionUtility.ThrowIfAny("页面返回失败，当前页面已经恢复。", exceptions);
            }

            TryShow(previous, previous.Arguments, exceptions);
            if (exceptions.Count > 0)
            {
                RestoreOriginalPage(current, currentArguments, previous, exceptions);
                MUIExceptionUtility.ThrowIfAny("页面返回失败，当前页面已经恢复。", exceptions);
            }

            _pages.RemoveAt(_pages.Count - 1);
            return true;
        }

        private bool PopToRootCore()
        {
            if (_pages.Count <= 1)
            {
                return false;
            }

            MUIPage originalTop = CurrentPage;
            object originalArguments = originalTop.Arguments;
            MUIPage root = _pages[0];
            var exceptions = new List<Exception>();

            for (int i = _pages.Count - 1; i > 0; i--)
            {
                TryHide(_pages[i], exceptions);
            }

            if (exceptions.Count == 0)
            {
                TryShow(root, root.Arguments, exceptions);
            }

            if (exceptions.Count > 0)
            {
                RestoreOriginalPage(originalTop, originalArguments, root, exceptions);
                MUIExceptionUtility.ThrowIfAny("返回根页面失败，原页面已经恢复。", exceptions);
            }

            _pages.RemoveRange(1, _pages.Count - 1);
            return true;
        }

        private bool CloseTopPopupCore(MUIPopupResult result)
        {
            if (_popups.Count == 0)
            {
                return false;
            }

            MUIPopup popup = _popups[_popups.Count - 1];
            var exceptions = new List<Exception>();

            try
            {
                popup.ClosePopup(result ?? MUIPopupResult.Cancel());
            }
            catch (Exception closeException)
            {
                exceptions.Add(closeException);
            }

            TryHide(popup, exceptions);
            _popups.RemoveAt(_popups.Count - 1);
            MUIExceptionUtility.ThrowIfAny("弹窗关闭钩子发生异常；弹窗已经安全隐藏并移出栈。", exceptions);
            return true;
        }

        private bool RequestBackCore()
        {
            if (CurrentPopup != null)
            {
                return DispatchActionCore(CurrentPopup, MUIAction.Back);
            }

            if (CurrentPage != null)
            {
                return DispatchActionCore(CurrentPage, MUIAction.Back);
            }

            RootBackRequested?.Invoke();
            return false;
        }

        private void BindView(MUIView view)
        {
            if (view != null && _boundViews.Add(view))
            {
                view.ActionRequested += HandleActionRequested;
            }
        }

        private void HandleActionRequested(MUIView view, MUIAction action)
        {
            // 生命周期钩子或连点造成的同步重入直接忽略，外层导航可以保持原子性。
            if (_isDisposed || _isNavigating || !IsActiveActionSource(view))
            {
                return;
            }

            ExecuteNavigation(() => DispatchActionCore(view, action));
        }

        private bool DispatchActionCore(MUIView view, MUIAction action)
        {
            MUIActionResult result = view.EvaluateAction(action);
            if (result.Type == MUIActionResultType.Unhandled)
            {
                return ApplyDefaultCore(view, action);
            }

            switch (result.Type)
            {
                case MUIActionResultType.Handled:
                    return true;
                case MUIActionResultType.Navigate:
                    PushCore(result.DestinationPageType, result.Arguments);
                    return true;
                case MUIActionResultType.Back:
                    return BackCore();
                case MUIActionResultType.ClosePopup:
                    return CloseTopPopupCore(result.PopupResult);
                case MUIActionResultType.Rejected:
                    ActionRejected?.Invoke(view, result.Reason);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool ApplyDefaultCore(MUIView view, MUIAction action)
        {
            if (view is MUIPopup)
            {
                if (action == MUIAction.Confirm)
                {
                    return CloseTopPopupCore(MUIPopupResult.Confirm());
                }

                if (action == MUIAction.Cancel || action == MUIAction.Back || action == MUIAction.Previous)
                {
                    return CloseTopPopupCore(MUIPopupResult.Cancel());
                }

                return false;
            }

            if (view is MUIPage)
            {
                if (action == MUIAction.Back)
                {
                    return BackCore();
                }

                // Previous 仍是独立的“上一步”意图；页面未处理时才按旧约定回退一级页面历史。
                if (action == MUIAction.Previous)
                {
                    return BackCore();
                }
            }

            return false;
        }

        private bool IsActiveActionSource(MUIView view)
        {
            if (view == null || !view.IsVisible)
            {
                return false;
            }

            if (view is MUIPopup popup)
            {
                return ReferenceEquals(CurrentPopup, popup);
            }

            if (view is MUIPage page)
            {
                return CurrentPopup == null && ReferenceEquals(CurrentPage, page);
            }

            return view is MUIOverlay;
        }

        private void RestoreOriginalPage(
            MUIPage originalTop,
            object originalArguments,
            MUIView additionallyShownView,
            List<Exception> exceptions)
        {
            if (additionallyShownView != null &&
                !ReferenceEquals(additionallyShownView, originalTop) &&
                additionallyShownView.IsVisible)
            {
                TryHide(additionallyShownView, exceptions);
            }

            for (int i = 0; i < _pages.Count; i++)
            {
                MUIPage page = _pages[i];
                if (!ReferenceEquals(page, originalTop) && page != null && page.IsVisible)
                {
                    TryHide(page, exceptions);
                }
            }

            if (originalTop != null && !originalTop.IsVisible)
            {
                TryShow(originalTop, originalArguments, exceptions);
            }
        }

        private static void TryShow(MUIView view, object arguments, List<Exception> exceptions)
        {
            try
            {
                view.ShowView(arguments);
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }

        private static void TryHide(MUIView view, List<Exception> exceptions)
        {
            if (view == null)
            {
                return;
            }

            try
            {
                view.HideView();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }

        private int FindPageIndex(Type pageType)
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                if (_pages[i] != null && _pages[i].GetType() == pageType)
                {
                    return i;
                }
            }

            return -1;
        }

        private TResult ExecuteNavigation<TResult>(Func<TResult> operation)
        {
            ThrowIfDisposed();
            if (_isNavigating)
            {
                throw new InvalidOperationException("MUIRouter 正在执行另一个导航操作，请等待当前操作结束。");
            }

            _isNavigating = true;
            try
            {
                return operation();
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MUIRouter));
            }
        }
    }
}
