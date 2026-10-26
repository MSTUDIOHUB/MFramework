using UnityEngine;
using UnityEngine.UI;

namespace MFramework
{
    /// <summary>把 UGUI <see cref="Button"/> 点击转换为语义化的 <see cref="MUIAction"/>。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MUIActionButton : MonoBehaviour
    {
        [SerializeField]
        private MUIAction action = MUIAction.Confirm;

        private Button _button;
        private MUIView _view;

        /// <summary>获取或设置此按钮发送的语义化操作。</summary>
        public MUIAction Action
        {
            get => action;
            set => action = value;
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            _view = GetComponentInParent<MUIView>(true);
        }

        private void OnEnable()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            _button.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            _button?.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick()
        {
            if (_view == null)
            {
                _view = GetComponentInParent<MUIView>(true);
            }

            if (_view == null)
            {
                Debug.LogWarning("MUIActionButton 必须放置在某个 MUIView 组件的子级节点下。", this);
                return;
            }

            _view.RequestAction(action);
        }
    }
}
