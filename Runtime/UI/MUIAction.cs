using System;

namespace MFramework
{
    /// <summary>定义菜单与对话框共用的五种语义操作。</summary>
    public enum MUIAction
    {
        /// <summary>确认当前选择。</summary>
        Confirm = 0,

        /// <summary>取消当前临时选择。</summary>
        /// <remarks>弹窗未处理时默认取消关闭；页面未处理时不会自动返回页面栈。</remarks>
        Cancel = 1,

        /// <summary>前往下一页或下一步骤。</summary>
        Next = 2,

        /// <summary>返回向导或教学流程的上一步骤。</summary>
        /// <remarks>页面可以优先处理步骤数据；未处理时路由器按兼容约定返回一级页面历史。</remarks>
        Previous = 3,

        /// <summary>离开当前弹窗或返回页面栈的上一页。</summary>
        /// <remarks>
        /// 弹窗未处理时默认以取消结果关闭；页面未处理时默认返回一级页面历史。
        /// 此枚举值固定为 4，以免改变 Prefab 中已经序列化的其他操作数值。
        /// </remarks>
        Back = 4
    }

    /// <summary>描述视图处理 <see cref="MUIAction"/> 后，路由器应如何继续。</summary>
    public enum MUIActionResultType
    {
        /// <summary>视图未处理操作，因此路由器可以应用默认行为。</summary>
        Unhandled,

        /// <summary>视图已经处理操作，导航应到此结束。</summary>
        Handled,

        /// <summary>路由器应打开另一个页面。</summary>
        Navigate,

        /// <summary>路由器应返回上一页面。</summary>
        Back,

        /// <summary>路由器应关闭最上层弹窗。</summary>
        ClosePopup,

        /// <summary>操作被拒绝，通常表示数据校验未通过。</summary>
        Rejected
    }

    /// <summary>表示 UGUI 视图收到通用操作请求后返回的处理结果。</summary>
    public readonly struct MUIActionResult
    {
        private MUIActionResult(
            MUIActionResultType type,
            Type destinationPageType,
            object arguments,
            MUIPopupResult popupResult,
            string reason)
        {
            Type = type;
            DestinationPageType = destinationPageType;
            Arguments = arguments;
            PopupResult = popupResult;
            Reason = reason;
        }

        /// <summary>获取请求路由器执行的行为。</summary>
        public MUIActionResultType Type { get; }

        /// <summary>获取 <see cref="MUIActionResultType.Navigate"/> 使用的目标页面类型。</summary>
        public Type DestinationPageType { get; }

        /// <summary>获取传给目标页面的可选参数。</summary>
        public object Arguments { get; }

        /// <summary>获取关闭弹窗时传递的结果。</summary>
        public MUIPopupResult PopupResult { get; }

        /// <summary>获取 <see cref="MUIActionResultType.Rejected"/> 的校验失败原因。</summary>
        public string Reason { get; }

        /// <summary>创建一个允许路由器应用默认行为的结果。</summary>
        public static MUIActionResult Unhandled()
        {
            return new MUIActionResult(MUIActionResultType.Unhandled, null, null, null, null);
        }

        /// <summary>创建一个消费操作但不继续导航的结果。</summary>
        public static MUIActionResult Handled()
        {
            return new MUIActionResult(MUIActionResultType.Handled, null, null, null, null);
        }

        /// <summary>创建一个将目标页面压入页面栈的结果。</summary>
        /// <typeparam name="TPage">已注册的页面类型。</typeparam>
        /// <param name="arguments">传给页面的可选参数。</param>
        public static MUIActionResult NavigateTo<TPage>(object arguments = null)
            where TPage : MUIPage
        {
            return new MUIActionResult(MUIActionResultType.Navigate, typeof(TPage), arguments, null, null);
        }

        /// <summary>创建一个返回上一页面的结果。</summary>
        public static MUIActionResult Back()
        {
            return new MUIActionResult(MUIActionResultType.Back, null, null, null, null);
        }

        /// <summary>创建一个关闭最上层弹窗的结果。</summary>
        /// <param name="result">传给弹窗关闭回调的可选结果。</param>
        public static MUIActionResult ClosePopup(MUIPopupResult result = null)
        {
            return new MUIActionResult(MUIActionResultType.ClosePopup, null, null, result, null);
        }

        /// <summary>创建一个校验拒绝结果，并保持当前视图打开。</summary>
        /// <param name="reason">适合 Toast 或行内校验提示显示的可读原因。</param>
        public static MUIActionResult Rejected(string reason)
        {
            return new MUIActionResult(MUIActionResultType.Rejected, null, null, null, reason);
        }
    }

    /// <summary>保存弹窗关闭时返回的语义化结果。</summary>
    public sealed class MUIPopupResult
    {
        /// <summary>创建一个弹窗结果。</summary>
        /// <param name="confirmed">用户是否执行确认，而不是取消。</param>
        /// <param name="value">弹窗收集的可选业务值。</param>
        public MUIPopupResult(bool confirmed, object value = null)
        {
            Confirmed = confirmed;
            Value = value;
        }

        /// <summary>获取用户是否确认了弹窗。</summary>
        public bool Confirmed { get; }

        /// <summary>获取可选业务值。</summary>
        public object Value { get; }

        /// <summary>创建一个已确认的弹窗结果。</summary>
        public static MUIPopupResult Confirm(object value = null)
        {
            return new MUIPopupResult(true, value);
        }

        /// <summary>创建一个已取消的弹窗结果。</summary>
        public static MUIPopupResult Cancel(object value = null)
        {
            return new MUIPopupResult(false, value);
        }
    }
}
