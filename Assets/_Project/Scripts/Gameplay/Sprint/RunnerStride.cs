using UnityEngine;

namespace KMA.Gameplay
{
    /// Plays the run cycle at the speed a runner really covers ground, so feet never skate over
    /// the track the camera is scrolling past.
    public static class RunnerStride
    {
        /// Speed, in m/s, the authored run clips were drawn for.
        public const float ClipSpeed = 9f;
        public const float MinPlayback = .5f, MaxPlayback = 1.5f;

        public static float Playback(float metersPerSecond) => metersPerSecond <= .05f
            ? 1f : Mathf.Clamp(metersPerSecond / ClipSpeed, MinPlayback, MaxPlayback);
    }
}
