using UnityEngine;

namespace KMA.Gameplay.Volleyball
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] SpriteRenderer target;
        [SerializeField] Sprite[] initialFrames;
        [SerializeField] bool initialLoop = true;
        [SerializeField, Min(1f)] float framesPerSecond = 12f;

        Sprite[] frames;
        bool loop;
        float time;

        public Sprite[] Frames => frames;
        public bool Loop => loop;
        public int FrameIndex { get; private set; }

        public void Configure(SpriteRenderer renderer, Sprite[] startFrames, bool startLoop, float fps)
        {
            target = renderer;
            initialFrames = startFrames;
            initialLoop = startLoop;
            framesPerSecond = Mathf.Max(1f, fps);
            frames = null;
            Play(startFrames, startLoop);
        }

        void Awake()
        {
            if (frames == null)
                Play(initialFrames, initialLoop);
        }

        void Update() => Advance(Time.deltaTime);

        public void Play(Sprite[] newFrames, bool shouldLoop, bool forceRestart = false)
        {
            if (newFrames == null || newFrames.Length == 0)
                return;
            if (!forceRestart && ReferenceEquals(newFrames, frames) && shouldLoop == loop)
                return;

            frames = newFrames;
            loop = shouldLoop;
            time = 0f;
            Apply(0);
        }

        public void Advance(float deltaTime)
        {
            if (frames == null || frames.Length == 0)
                return;

            time += Mathf.Max(0f, deltaTime);
            int index = Mathf.FloorToInt(time * framesPerSecond);
            Apply(loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1));
        }

        void Apply(int index)
        {
            FrameIndex = index;
            if (!target)
                target = GetComponent<SpriteRenderer>();
            if (target)
                target.sprite = frames[index];
        }
    }
}
