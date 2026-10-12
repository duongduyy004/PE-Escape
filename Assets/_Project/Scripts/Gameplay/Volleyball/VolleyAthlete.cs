using UnityEngine;

namespace KMA.Gameplay.Volleyball
{
    public enum AthleteAction
    {
        Idle,
        Run,
        Serve,
        ServeToss,
        Receive,
        Dig,
        Smash,
        JumpLoad,
        Block,
        Dive
    }

    public sealed class VolleyAthlete
    {
        public const float NetGap = .3f;
        public const float BackMargin = 1.5f;
        public const float SideMargin = 1f;
        public const float JumpSeconds = 1f;
        public const float JumpPeak = .8f;

        public VolleyAthlete(CourtSide side, float speed)
        {
            Side = side;
            Speed = Mathf.Max(0f, speed);
        }

        public CourtSide Side { get; }
        public float Speed { get; }
        public Vector2 Position { get; private set; }
        public AthleteAction Action { get; private set; }
        public float LockTimeLeft { get; private set; }
        public bool IsLocked => LockTimeLeft > 0f;
        public float AirTimeLeft { get; private set; }
        public bool IsAirborne => AirTimeLeft > 0f;
        public float ActionElapsed { get; private set; }
        public int ActionSequence { get; private set; }

        // Visual only: the rules never read how high the athlete is.
        public float JumpHeight => IsAirborne
            ? JumpPeak * Mathf.Sin(Mathf.PI * (1f - AirTimeLeft / JumpSeconds))
            : 0f;

        public void PlaceAt(Vector2 position)
        {
            Position = Clamp(position);
            Action = AthleteAction.Idle;
            LockTimeLeft = 0f;
            AirTimeLeft = 0f;
            ActionElapsed = 0f;
            ActionSequence++;
        }

        // Mid-air the athlete holds its spot and pose; the stick aims instead of steering.
        public bool TryJump(AthleteAction pose)
        {
            if (IsAirborne || IsLocked)
                return false;

            AirTimeLeft = JumpSeconds;
            BeginAction(pose, 0f);
            return true;
        }

        public void Move(Vector2 input, float deltaTime)
        {
            if (IsLocked || IsAirborne)
                return;

            Vector2 direction = Vector2.ClampMagnitude(input, 1f);
            if (direction.sqrMagnitude < .0001f)
            {
                if (Action == AthleteAction.Run)
                {
                    Action = AthleteAction.Idle;
                    ActionElapsed = 0f;
                    ActionSequence++;
                }
                return;
            }

            Position = Clamp(Position + direction * Speed * deltaTime);
            if (Action != AthleteAction.Run)
            {
                Action = AthleteAction.Run;
                ActionElapsed = 0f;
                ActionSequence++;
            }
        }

        public void MoveToward(Vector2 target, float deltaTime)
        {
            Vector2 delta = target - Position;
            float distance = delta.magnitude;
            float step = Speed * deltaTime;
            if (distance < .01f || step <= 0f)
            {
                Move(Vector2.zero, deltaTime);
                return;
            }

            Move(delta / distance * Mathf.Min(1f, distance / step), deltaTime);
        }

        public void BeginAction(AthleteAction action, float seconds)
        {
            Action = action;
            LockTimeLeft = Mathf.Max(0f, seconds);
            ActionElapsed = 0f;
            ActionSequence++;
        }

        public void Lunge(Vector2 toward, float distance)
        {
            Position = Clamp(Position + Vector2.ClampMagnitude(toward - Position, distance));
        }

        public void Tick(float deltaTime)
        {
            if (Action != AthleteAction.Idle)
                ActionElapsed += deltaTime;

            if (AirTimeLeft > 0f)
            {
                AirTimeLeft = Mathf.Max(0f, AirTimeLeft - deltaTime);
                if (AirTimeLeft <= 0f && LockTimeLeft <= 0f)
                {
                    Action = AthleteAction.Idle;
                    ActionElapsed = 0f;
                    ActionSequence++;
                }
            }

            if (LockTimeLeft <= 0f)
                return;

            LockTimeLeft = Mathf.Max(0f, LockTimeLeft - deltaTime);
            if (LockTimeLeft <= 0f && !IsAirborne)
            {
                Action = AthleteAction.Idle;
                ActionElapsed = 0f;
                ActionSequence++;
            }
        }

        Vector2 Clamp(Vector2 position)
        {
            float far = CourtSpace.HalfLength + BackMargin;
            float wide = CourtSpace.HalfWidth + SideMargin;
            float y = Mathf.Clamp(position.y, -wide, wide);
            return Side == CourtSide.Player
                ? new Vector2(Mathf.Clamp(position.x, -far, -NetGap), y)
                : new Vector2(Mathf.Clamp(position.x, NetGap, far), y);
        }
    }
}
