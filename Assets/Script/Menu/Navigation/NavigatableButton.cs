using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YARG.Menu.Navigation
{
    public sealed class NavigatableButton : NavigatableBehaviour
#if UNITY_IOS
        , IPointerClickHandler
#endif
    {
        [SerializeField]
        private Button.ButtonClickedEvent _onClick = new();

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
#if !UNITY_IOS
            Confirm();
#endif
        }

#if UNITY_IOS
        // iOS port: a touch confirms when the finger lifts, and not at all if it turned into a
        // drag, so scrolling a menu by dragging across buttons never presses them.
        public void OnPointerClick(PointerEventData eventData)
        {
            Confirm();
        }
#endif

        public override void Confirm()
        {
            _onClick.Invoke();
        }

        public void RemoveOnClickListeners()
        {
            _onClick.RemoveAllListeners();
        }

        public void SetOnClickEvent(UnityAction a)
        {
            _onClick.RemoveAllListeners();
            _onClick.AddListener(a);
        }
    }
}