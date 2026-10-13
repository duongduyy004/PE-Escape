namespace KMA.Gameplay
{
    /// How one rival reacts to the gun, accelerates, finishes and pulls up. Rolled per race;
    /// <see cref="Instant"/> moves at pace at once, so rules tests can reason in distance = speed × time.
    public readonly struct RivalMotionTraits
    {
        public const float MinReaction = .12f, MaxReaction = .25f;
        public const float MinTau = .9f, MaxTau = 1.4f;
        public const float MinFinishStyle = -.06f, MaxFinishStyle = .08f;
        public const float MinStopDistance = 3f, MaxStopDistance = 5f;
        public const float MinTetherSlot = 1.5f, MaxTetherSlot = 3.5f;

        public float ReactionSeconds { get; }
        /// Seconds the gap to the target pace takes to close by ~63 %; 0 jumps straight to pace.
        public float AccelerationTau { get; }
        /// Fraction added to (closer) or taken off (fader) the pace over the last fifth of the race.
        public float FinishStyle { get; }
        /// Metres past the line the rival runs before standing still.
        public float StopDistance { get; }
        /// Learn only: where the rival settles relative to the player, in metres (negative = behind).
        public float TetherSlot { get; }

        public RivalMotionTraits(float reactionSeconds, float accelerationTau, float finishStyle,
            float stopDistance, float tetherSlot)
        {
            ReactionSeconds = reactionSeconds;
            AccelerationTau = accelerationTau;
            FinishStyle = finishStyle;
            StopDistance = stopDistance;
            TetherSlot = tetherSlot;
        }

        public static RivalMotionTraits Instant => new RivalMotionTraits(0f, 0f, 0f, 4f, 0f);

        public static RivalMotionTraits Roll(System.Random random)
        {
            float reaction = Range(random, MinReaction, MaxReaction);
            float tau = Range(random, MinTau, MaxTau);
            float style = Range(random, MinFinishStyle, MaxFinishStyle);
            float stop = Range(random, MinStopDistance, MaxStopDistance);
            float slot = Range(random, MinTetherSlot, MaxTetherSlot) * (random.NextDouble() < .5 ? -1f : 1f);
            return new RivalMotionTraits(reaction, tau, style, stop, slot);
        }

        static float Range(System.Random random, float min, float max) =>
            min + (max - min) * (float)random.NextDouble();
    }
}
