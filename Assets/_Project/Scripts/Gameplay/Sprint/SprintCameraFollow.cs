using UnityEngine;

namespace KMA.Gameplay
{
    /// Tracks the player along the straight like a broadcast rail camera: it holds still while the
    /// player runs in from the start, then keeps them centred. It never looks at the rivals.
    [DefaultExecutionOrder(100)]
    public sealed class SprintCameraFollow : MonoBehaviour
    {
        public const float SmoothTime = .12f;
        // A jump this large is a teleport (a scene reload or a test), not running: cut instead of panning.
        public const float SnapDistance = 8f;

        [SerializeField] Transform target;
        float startX;
        float velocity;
        bool hasStart;

        public Transform Target => target;
        public float StartX => startX;
        /// How far the camera has moved from where it started; the backdrop scrolls by this.
        public float Travel => transform.position.x - startX;

        public void Bind(Transform player)
        {
            target = player;
            if (!hasStart)
            {
                startX = transform.position.x;
                hasStart = true;
            }
            velocity = 0f;
        }

        public static float TargetX(float startX, float playerX) => Mathf.Max(startX, playerX);

        public static float Step(float currentX, ref float velocity, float targetX, float dt)
        {
            if (Mathf.Abs(targetX - currentX) > SnapDistance)
            {
                velocity = 0f;
                return targetX;
            }
            if (dt <= 0f) return currentX;
            return Mathf.SmoothDamp(currentX, targetX, ref velocity, SmoothTime, Mathf.Infinity, dt);
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 position = transform.position;
            position.x = Step(position.x, ref velocity, TargetX(startX, target.position.x), Time.deltaTime);
            transform.position = position;
        }
    }
}
