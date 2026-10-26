using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace MFramework
{
    /// <summary>MFramework 管理的所有 UGUI 页面、弹窗和覆盖层的基类组件。</summary>
    public abstract class MUIView : MonoBehaviour
    {
        internal event Action<MUIView, MUIAction> ActionRequested;

        /// <summary>获取服务是否已经调用此视图的一次性创建钩子。</summary>
        public bool IsCreated { get; private set; }

        /// <summary>获取视图当前是否可见。</summary>
        public bool IsVisible { get; private set; }

        /// <summary>获取最近一次显示操作传入的参数。</summary>
        public object Arguments { get; private set; }

        /// <summary>向当前 <see cref="MUIRouter"/> 发送通用按钮操作。</summary>
        /// <param name="action">用户请求的语义化操作。</param>
        /// <remarks>隐藏视图发出的操作会被忽略；页面与弹窗也只有位于对应栈顶时才能驱动导航。</remarks>
        public void RequestAction(MUIAction action)
        {
            if (!IsVisible)
            {
                return;
            }

            ActionRequested?.Invoke(this, action);
        }

        /// <summary>此缓存视图实例创建后执行一次。</summary>
        protected virtual void OnCreate()
        {
        }

        /// <summary>视图每次变为可见时执行。</summary>
        /// <param name="arguments">传给路由器或 UI 服务的参数。</param>
        protected virtual void OnShow(object arguments)
        {
        }

        /// <summary>视图每次变为隐藏时执行。</summary>
        protected virtual void OnHide()
        {
        }

        /// <summary>处理 Back、Cancel、Confirm、Previous 或 Next 操作。</summary>
        /// <param name="action">请求的语义化操作。</param>
        /// <returns>路由器指令；默认结果允许路由器应用约定行为。</returns>
        protected virtual MUIActionResult OnAction(MUIAction action)
        {
            return MUIActionResult.Unhandled();
        }

        /// <summary>UI 服务销毁此缓存实例前执行一次。</summary>
        protected virtual void OnDispose()
        {
        }

        internal void CreateView()
        {
            if (IsCreated)
            {
                return;
            }

            IsCreated = true;
            try
            {
                OnCreate();
            }
            catch (Exception createException)
            {
                var exceptions = new List<Exception> { createException };
                try
                {
                    OnDispose();
                }
                catch (Exception disposeException)
                {
                    exceptions.Add(disposeException);
                }

                IsCreated = false;
                IsVisible = false;
                Arguments = null;
                ActionRequested = null;
                MUIExceptionUtility.ThrowIfAny("UGUI 视图创建失败，回滚清理时也发生了异常。", exceptions);
            }
        }

        internal void ShowView(object arguments)
        {
            if (!IsCreated)
            {
                throw new InvalidOperationException("UGUI 视图必须先完成创建，之后才能显示。");
            }

            transform.SetAsLastSibling();
            if (IsVisible)
            {
                return;
            }

            object previousArguments = Arguments;
            Arguments = arguments;
            gameObject.SetActive(true);
            IsVisible = true;

            try
            {
                OnShow(arguments);
            }
            catch (Exception showException)
            {
                var exceptions = new List<Exception> { showException };
                IsVisible = false;

                // OnShow 可能已经完成了部分订阅，因此失败时仍尝试调用 OnHide 做成对清理。
                try
                {
                    OnHide();
                }
                catch (Exception hideException)
                {
                    exceptions.Add(hideException);
                }

                if (this != null && gameObject != null)
                {
                    gameObject.SetActive(false);
                }

                Arguments = previousArguments;
                MUIExceptionUtility.ThrowIfAny("UGUI 视图显示失败，回滚隐藏时也发生了异常。", exceptions);
            }
        }

        internal void HideView()
        {
            if (!IsVisible)
            {
                return;
            }

            IsVisible = false;
            try
            {
                OnHide();
            }
            finally
            {
                // 即使业务钩子抛出异常，也不能留下仍可见但已被路由栈移除的孤儿视图。
                if (this != null && gameObject != null)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        internal MUIActionResult EvaluateAction(MUIAction action)
        {
            return OnAction(action);
        }

        internal void DisposeView()
        {
            if (!IsCreated)
            {
                ActionRequested = null;
                return;
            }

            var exceptions = new List<Exception>();

            // 先断开动作事件，避免 OnHide 或 OnDispose 在销毁阶段再次发起导航。
            ActionRequested = null;
            try
            {
                HideView();
            }
            catch (Exception hideException)
            {
                exceptions.Add(hideException);
            }

            try
            {
                OnDispose();
            }
            catch (Exception disposeException)
            {
                exceptions.Add(disposeException);
            }
            finally
            {
                IsCreated = false;
                IsVisible = false;
                Arguments = null;
                if (this != null && gameObject != null)
                {
                    gameObject.SetActive(false);
                }
            }

            MUIExceptionUtility.ThrowIfAny("UGUI 视图释放时发生了异常。", exceptions);
        }
    }

    /// <summary>可加入路由器页面栈的完整菜单页面。</summary>
    public abstract class MUIPage : MUIView
    {
    }

    /// <summary>显示在页面上方的模态 UGUI 弹窗。</summary>
    public abstract class MUIPopup : MUIView
    {
        /// <summary>收到关闭请求后，在弹窗隐藏前立即执行。</summary>
        /// <param name="result">用户是否确认，以及可选的业务值。</param>
        protected virtual void OnClosed(MUIPopupResult result)
        {
        }

        internal void ClosePopup(MUIPopupResult result)
        {
            OnClosed(result ?? MUIPopupResult.Cancel());
        }
    }

    /// <summary>非模态 UGUI 覆盖层，例如 HUD、加载遮罩或 Toast 层。</summary>
    public abstract class MUIOverlay : MUIView
    {
    }

    /// <summary>在保留单个异常原始堆栈的同时，统一报告多个生命周期异常。</summary>
    internal static class MUIExceptionUtility
    {
        public static void ThrowIfAny(string message, List<Exception> exceptions)
        {
            if (exceptions == null || exceptions.Count == 0)
            {
                return;
            }

            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
                return;
            }

            throw new AggregateException(message, exceptions);
        }
    }
}
