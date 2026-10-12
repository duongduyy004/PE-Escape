using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KMA.Gameplay.Volleyball
{
    /// The NHẢY hit area. KitPressFeedback fades and scales the icon on press; the cue pulse scales
    /// this hit area itself, so the two never fight over one transform.
    public sealed class JumpButton : MonoBehaviour, IPointerDownHandler
    {
        public const float PulseAmount = .12f;
        const float PulseRate = 10f;

        public event Action Pressed;
        public bool Cued { get; private set; }

        // Fires on touch-down, not release, like ĐÁNH: the jump window is only a few frames wide.
        public void OnPointerDown(PointerEventData eventData) => Press();

        public void Press() => Pressed?.Invoke();

        public void SetCue(bool cued)
        {
            Cued = cued;
            float pulse = cued ? PulseAmount * Mathf.Abs(Mathf.Sin(Time.unscaledTime * PulseRate)) : 0f;
            transform.localScale = Vector3.one * (1f + pulse);
        }
    }
}
