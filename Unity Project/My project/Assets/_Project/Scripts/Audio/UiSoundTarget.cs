using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LanternKeeper
{
    /// <summary>Added by <see cref="UiSounds"/> to each button and slider; forwards its events.</summary>
    public class UiSoundTarget : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
    {
        public UiSounds Owner { get; set; }

        public void OnPointerEnter(PointerEventData eventData) => Hover();

        // Only keyboard navigation: a menu selecting its first button when it opens is silent.
        public void OnSelect(BaseEventData eventData)
        {
            if (eventData is AxisEventData) Hover();
        }

        public void OnPointerClick(PointerEventData eventData) => Click();
        public void OnSubmit(BaseEventData eventData) => Click();

        private void Hover()
        {
            if (Owner != null) Owner.PlayHover();
        }

        private void Click()
        {
            if (Owner != null && GetComponent<Button>() != null) Owner.PlayClick();
        }
    }
}
