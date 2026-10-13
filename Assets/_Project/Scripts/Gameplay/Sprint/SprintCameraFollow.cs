using UnityEngine;

namespace KMA.Gameplay
{
    /// Tracks the player along the straight like a broadcast rail camera: it opens with the start line
    /// just inside the left edge, holds still while the player runs in to the centre, then keeps them
    /// exactly centred. It never looks at the rivals.
    [DefaultExecutionOrder(100)]
    public sealed class SprintCameraFollow : MonoBehaviour
    {
        /// How far inside the left view edge the start line sits. Must stay below 3.2: the left view
        /// edge is TrackStartX - StartMargin and the authored track tile begins at -12.8.
        public const float StartMargin = 2f;

        [SerializeField] Transform target;
        float authoredX;
        float startX;
        bool hasStart;
        Camera view;

        public Transform Target => target;
        public float StartX => startX;
        /// How far the camera has moved from its authored position; the backdrop scrolls by this.
        public float Travel => hasStart ? transform.position.x - authoredX : 0f;

        public static float StartXFor(float halfWidth) => SprintTrackLayout.TrackStartX + halfWidth - StartMargin;

        public void Bind(Transform player)
        {
            target = player;
            if (!hasStart)
            {
                authoredX = transform.position.x;
                hasStart = true;
            }
            // Frame the start straight away: no pan during the countdown.
            startX = CurrentStartX();
            Vector3 position = transform.position;
            position.x = startX;
            transform.position = position;
        }

        public static float TargetX(float startX, float playerX) => Mathf.Max(startX, playerX);

        public static float Step(float currentX, float targetX, float dt)
        {
            if (dt <= 0f) return currentX;
            return targetX;
        }

        float CurrentStartX()
        {
            if (view == null) view = GetComponent<Camera>();
            return view == null ? authoredX : StartXFor(view.orthographicSize * view.aspect);
        }

        void LateUpdate()
        {
            if (target == null) return;
            // Still holding at the start: follow aspect changes (editor resize, the first device frame).
            if (target.position.x <= startX) startX = CurrentStartX();
            Vector3 position = transform.position;
            position.x = Step(position.x, TargetX(startX, target.position.x), Time.deltaTime);
            transform.position = position;
        }
    }
}
