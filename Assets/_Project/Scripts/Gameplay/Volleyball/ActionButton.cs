using System;
using UnityEngine;
using UnityEngine.EventSystems;
using KMA.UI.Kit;

namespace KMA.Gameplay.Volleyball
{
    /// The ĐÁNH hit area. Its look and press response come from the kit's KitPressFeedback.
    public sealed class ActionButton : MonoBehaviour, IPointerDownHandler
    {
        public event Action Pressed;
        bool interactable = true;

        // Fires on touch-down, not release: timing windows are only a few frames wide.
        public void OnPointerDown(PointerEventData eventData) => Press();

        public void Press()
        {
            if (interactable) Pressed?.Invoke();
        }

        public void SetInteractable(bool value)
        {
            if (interactable == value) return;
            interactable = value;
            GetComponent<KitPressFeedback>()?.SetInteractable(value);
        }
    }
}
