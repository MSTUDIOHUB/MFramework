using NUnit.Framework;
using UnityEngine;

namespace MFramework.Tests.EditMode
{
    public sealed class MUITests
    {
        private GameObject _pageLayer;
        private GameObject _popupLayer;
        private GameObject _overlayLayer;
        private MUITestPageA _pageAPrefab;
        private MUITestPageB _pageBPrefab;
        private MUITestActionPage _actionPagePrefab;
        private MUITestThrowOnShowPage _throwOnShowPagePrefab;
        private MUITestThrowOnHidePage _throwOnHidePagePrefab;
        private MUITestThrowOnCreatePage _throwOnCreatePagePrefab;
        private MUITestReentrantShowPage _reentrantShowPagePrefab;
        private MUITestPopup _popupPrefab;
        private MUITestPopupB _popupBPrefab;
        private MUITestThrowOnClosePopup _throwOnClosePopupPrefab;
        private MUIService _ui;
        private MUIRouter _router;

        [SetUp]
        public void SetUp()
        {
            _pageLayer = new GameObject("Pages", typeof(RectTransform));
            _popupLayer = new GameObject("Popups", typeof(RectTransform));
            _overlayLayer = new GameObject("Overlays", typeof(RectTransform));

            _pageAPrefab = CreateView<MUITestPageA>("Page A");
            _pageBPrefab = CreateView<MUITestPageB>("Page B");
            _actionPagePrefab = CreateView<MUITestActionPage>("Action Page");
            _throwOnShowPagePrefab = CreateView<MUITestThrowOnShowPage>("Throw On Show Page");
            _throwOnHidePagePrefab = CreateView<MUITestThrowOnHidePage>("Throw On Hide Page");
            _throwOnCreatePagePrefab = CreateView<MUITestThrowOnCreatePage>("Throw On Create Page");
            _reentrantShowPagePrefab = CreateView<MUITestReentrantShowPage>("Reentrant Show Page");
            _popupPrefab = CreateView<MUITestPopup>("Popup");
            _popupBPrefab = CreateView<MUITestPopupB>("Popup B");
            _throwOnClosePopupPrefab = CreateView<MUITestThrowOnClosePopup>("Throw On Close Popup");

            _ui = new MUIService(_pageLayer.transform, _popupLayer.transform, _overlayLayer.transform)
                .Register(_pageAPrefab)
                .Register(_pageBPrefab)
                .Register(_actionPagePrefab)
                .Register(_throwOnShowPagePrefab)
                .Register(_throwOnHidePagePrefab)
                .Register(_throwOnCreatePagePrefab)
                .Register(_reentrantShowPagePrefab)
                .Register(_popupPrefab)
                .Register(_popupBPrefab)
                .Register(_throwOnClosePopupPrefab);
            _router = new MUIRouter(_ui);
        }

        [TearDown]
        public void TearDown()
        {
            _router?.Dispose();
            _ui?.Dispose();
            DestroyView(_pageAPrefab);
            DestroyView(_pageBPrefab);
            DestroyView(_actionPagePrefab);
            DestroyView(_throwOnShowPagePrefab);
            DestroyView(_throwOnHidePagePrefab);
            DestroyView(_throwOnCreatePagePrefab);
            DestroyView(_reentrantShowPagePrefab);
            DestroyView(_popupPrefab);
            DestroyView(_popupBPrefab);
            DestroyView(_throwOnClosePopupPrefab);
            Object.DestroyImmediate(_pageLayer);
            Object.DestroyImmediate(_popupLayer);
            Object.DestroyImmediate(_overlayLayer);
        }

        [Test]
        public void PreviousActionReturnsToTheEarlierPage()
        {
            MUITestPageA first = _router.Push<MUITestPageA>("first");
            MUITestPageB second = _router.Push<MUITestPageB>("second");

            second.RequestAction(MUIAction.Previous);

            Assert.AreSame(first, _router.CurrentPage);
            Assert.AreEqual(1, _router.PageCount);
            Assert.IsTrue(first.IsVisible);
            Assert.IsFalse(second.IsVisible);
        }

        [Test]
        public void NextActionCanNavigateThroughAViewResult()
        {
            MUITestPageA first = _router.Push<MUITestPageA>();

            first.RequestAction(MUIAction.Next);

            Assert.IsInstanceOf<MUITestPageB>(_router.CurrentPage);
            Assert.AreEqual(2, _router.PageCount);
        }

        [Test]
        public void ConfirmClosesPopupWithConfirmedResultByDefault()
        {
            _router.Push<MUITestPageA>();
            MUITestPopup popup = _router.ShowPopup<MUITestPopup>();

            popup.RequestAction(MUIAction.Confirm);

            Assert.AreEqual(0, _router.PopupCount);
            Assert.IsFalse(popup.IsVisible);
            Assert.NotNull(popup.LastResult);
            Assert.IsTrue(popup.LastResult.Confirmed);
        }

        [Test]
        public void RootBackRaisesAnEventInsteadOfGuessingPlatformExit()
        {
            _router.Push<MUITestPageA>();
            int requests = 0;
            _router.RootBackRequested += () => requests++;

            bool handled = _router.RequestBack();

            Assert.AreEqual(1, requests);
            Assert.AreEqual(1, _router.PageCount);
            Assert.IsFalse(handled);
        }

        [Test]
        public void CachedPageRunsCreateOnceAndShowForEachVisit()
        {
            MUITestPageA first = _router.Push<MUITestPageA>();
            _router.Push<MUITestPageB>();
            _router.Back();

            Assert.AreEqual(1, first.CreateCount);
            Assert.AreEqual(2, first.ShowCount);
        }

        [Test]
        public void PushingAlreadyVisiblePageDoesNotRunShowTwice()
        {
            MUITestPageA first = _router.Push<MUITestPageA>();

            MUITestPageA shownAgain = _router.Push<MUITestPageA>();

            Assert.AreSame(first, shownAgain);
            Assert.AreEqual(1, first.ShowCount);
            Assert.AreEqual(1, _router.PageCount);
        }

        [Test]
        public void RequestBackSendsBackInsteadOfPrevious()
        {
            _router.Push<MUITestPageA>();
            MUITestActionPage page = _router.Push<MUITestActionPage>();

            bool handled = _router.RequestBack();

            Assert.IsTrue(handled);
            Assert.AreEqual(MUIAction.Back, page.LastAction);
            Assert.AreSame(page, _router.CurrentPage);
            Assert.AreEqual(2, _router.PageCount);
        }

        [Test]
        public void CancelOnPageDoesNotReturnByDefault()
        {
            _router.Push<MUITestPageA>();
            MUITestPageB page = _router.Push<MUITestPageB>();

            page.RequestAction(MUIAction.Cancel);

            Assert.AreSame(page, _router.CurrentPage);
            Assert.AreEqual(2, _router.PageCount);
        }

        [Test]
        public void BackOnPopupClosesItAsCancelled()
        {
            _router.Push<MUITestPageA>();
            MUITestPopup popup = _router.ShowPopup<MUITestPopup>();

            Assert.IsTrue(_router.CanGoBack);
            Assert.IsTrue(_router.RequestBack());

            Assert.AreEqual(0, _router.PopupCount);
            Assert.IsFalse(popup.IsVisible);
            Assert.NotNull(popup.LastResult);
            Assert.IsFalse(popup.LastResult.Confirmed);
        }

        [Test]
        public void ShowingCachedPopupAgainMovesItToVisualAndLogicalTop()
        {
            MUITestPopup first = _router.ShowPopup<MUITestPopup>();
            MUITestPopupB second = _router.ShowPopup<MUITestPopupB>();

            MUITestPopup shownAgain = _router.ShowPopup<MUITestPopup>();

            Assert.AreSame(first, shownAgain);
            Assert.AreSame(first, _router.CurrentPopup);
            Assert.Greater(first.transform.GetSiblingIndex(), second.transform.GetSiblingIndex());
            Assert.AreEqual(2, _router.PopupCount);
        }

        [Test]
        public void LowerPopupActionCannotCloseTopPopup()
        {
            MUITestPopup lower = _router.ShowPopup<MUITestPopup>();
            MUITestPopupB top = _router.ShowPopup<MUITestPopupB>();

            lower.RequestAction(MUIAction.Cancel);

            Assert.AreSame(top, _router.CurrentPopup);
            Assert.AreEqual(2, _router.PopupCount);
            Assert.IsTrue(lower.IsVisible);
            Assert.IsTrue(top.IsVisible);
        }

        [Test]
        public void ThrowingPopupCloseHookStillHidesAndRemovesPopup()
        {
            MUITestThrowOnClosePopup popup = _router.ShowPopup<MUITestThrowOnClosePopup>();

            Assert.Throws<System.InvalidOperationException>(() => _router.CloseTopPopup());

            Assert.AreEqual(0, _router.PopupCount);
            Assert.IsFalse(popup.IsVisible);
            Assert.IsFalse(popup.gameObject.activeSelf);
        }

        [Test]
        public void ThrowingPageShowRestoresPreviousPageAndStack()
        {
            MUITestPageA original = _router.Push<MUITestPageA>();

            Assert.Throws<System.InvalidOperationException>(() => _router.Push<MUITestThrowOnShowPage>());

            Assert.AreSame(original, _router.CurrentPage);
            Assert.AreEqual(1, _router.PageCount);
            Assert.IsTrue(original.IsVisible);
            Assert.IsTrue(_ui.TryGet<MUITestThrowOnShowPage>(out MUITestThrowOnShowPage failed));
            Assert.IsFalse(failed.IsVisible);
        }

        [Test]
        public void ThrowingPageCreateDoesNotLeaveCachedOrVisibleOrphan()
        {
            MUITestPageA original = _router.Push<MUITestPageA>();

            Assert.Throws<System.InvalidOperationException>(() => _router.Push<MUITestThrowOnCreatePage>());

            Assert.AreSame(original, _router.CurrentPage);
            Assert.AreEqual(1, _router.PageCount);
            Assert.IsTrue(original.IsVisible);
            Assert.IsFalse(_ui.TryGet<MUITestThrowOnCreatePage>(out _));
        }

        [Test]
        public void ThrowingPageHideMakesBackAtomic()
        {
            _router.Push<MUITestPageA>();
            MUITestThrowOnHidePage current = _router.Push<MUITestThrowOnHidePage>();

            Assert.Throws<System.InvalidOperationException>(() => _router.Back());

            Assert.AreSame(current, _router.CurrentPage);
            Assert.AreEqual(2, _router.PageCount);
            Assert.IsTrue(current.IsVisible);
        }

        [Test]
        public void ThrowingTargetShowMakesReplaceAtomic()
        {
            MUITestPageA original = _router.Push<MUITestPageA>();

            Assert.Throws<System.InvalidOperationException>(() => _router.Replace<MUITestThrowOnShowPage>());

            Assert.AreSame(original, _router.CurrentPage);
            Assert.AreEqual(1, _router.PageCount);
            Assert.IsTrue(original.IsVisible);
        }

        [Test]
        public void ThrowingPageHideMakesPopToRootAtomic()
        {
            _router.Push<MUITestPageA>();
            MUITestThrowOnHidePage current = _router.Push<MUITestThrowOnHidePage>();

            Assert.Throws<System.InvalidOperationException>(() => _router.PopToRoot());

            Assert.AreSame(current, _router.CurrentPage);
            Assert.AreEqual(2, _router.PageCount);
            Assert.IsTrue(current.IsVisible);
        }

        [Test]
        public void ActionRequestedDuringOnShowDoesNotCorruptStack()
        {
            _router.Push<MUITestPageA>();

            MUITestReentrantShowPage current = _router.Push<MUITestReentrantShowPage>();

            Assert.AreSame(current, _router.CurrentPage);
            Assert.AreEqual(2, _router.PageCount);
            Assert.IsTrue(current.IsVisible);
        }

        [Test]
        public void RecreatedRouterBindsViewsThatServiceAlreadyCached()
        {
            MUITestPageA page = _router.Push<MUITestPageA>();
            _router.Dispose();
            _router = new MUIRouter(_ui);
            _router.Push<MUITestPageA>();

            page.RequestAction(MUIAction.Next);

            Assert.IsInstanceOf<MUITestPageB>(_router.CurrentPage);
            Assert.AreEqual(2, _router.PageCount);
        }

        private static TView CreateView<TView>(string name)
            where TView : MUIView
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(TView));
            gameObject.SetActive(false);
            return gameObject.GetComponent<TView>();
        }

        private static void DestroyView(MUIView view)
        {
            if (view != null)
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }
    }

    public sealed class MUITestPageA : MUIPage
    {
        public int CreateCount { get; private set; }
        public int ShowCount { get; private set; }

        protected override void OnCreate()
        {
            CreateCount++;
        }

        protected override void OnShow(object arguments)
        {
            ShowCount++;
        }

        protected override MUIActionResult OnAction(MUIAction action)
        {
            return action == MUIAction.Next
                ? MUIActionResult.NavigateTo<MUITestPageB>()
                : MUIActionResult.Unhandled();
        }
    }

    public sealed class MUITestPageB : MUIPage
    {
    }

    public sealed class MUITestActionPage : MUIPage
    {
        public MUIAction LastAction { get; private set; }

        protected override MUIActionResult OnAction(MUIAction action)
        {
            LastAction = action;
            return action == MUIAction.Back
                ? MUIActionResult.Handled()
                : MUIActionResult.Unhandled();
        }
    }

    public sealed class MUITestThrowOnShowPage : MUIPage
    {
        protected override void OnShow(object arguments)
        {
            throw new System.InvalidOperationException("测试用 OnShow 异常。");
        }
    }

    public sealed class MUITestThrowOnHidePage : MUIPage
    {
        private bool _throwNextHide = true;

        protected override void OnHide()
        {
            if (!_throwNextHide)
            {
                return;
            }

            _throwNextHide = false;
            throw new System.InvalidOperationException("测试用 OnHide 异常。");
        }
    }

    public sealed class MUITestThrowOnCreatePage : MUIPage
    {
        protected override void OnCreate()
        {
            throw new System.InvalidOperationException("测试用 OnCreate 异常。");
        }
    }

    public sealed class MUITestReentrantShowPage : MUIPage
    {
        protected override void OnShow(object arguments)
        {
            RequestAction(MUIAction.Back);
        }
    }

    public sealed class MUITestPopup : MUIPopup
    {
        public MUIPopupResult LastResult { get; private set; }

        protected override void OnClosed(MUIPopupResult result)
        {
            LastResult = result;
        }
    }

    public sealed class MUITestPopupB : MUIPopup
    {
    }

    public sealed class MUITestThrowOnClosePopup : MUIPopup
    {
        protected override void OnClosed(MUIPopupResult result)
        {
            throw new System.InvalidOperationException("测试用 OnClosed 异常。");
        }
    }
}
