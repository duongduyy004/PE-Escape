using System;

namespace KMA.Gameplay.FrogJump
{
    [Serializable]
    public sealed class FrogJumpTuning
    {
        public float trackMetres = 60f;
        public float sweepSeconds = 1f;
        public float maxJumpMetres = 1.5f;
        public float minJumpMetres = .25f;
        // Normalised distance from the centre (0 centre, 1 edge) beyond which the jump is a fall.
        public float safeZone = .2f;
        public float jumpSeconds = .6f;
        public float recoverSeconds = 2.5f;
        public float timeLimitSeconds = 40f;
    }
}
