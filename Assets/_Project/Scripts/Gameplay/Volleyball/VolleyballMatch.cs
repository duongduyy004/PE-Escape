using System;
using UnityEngine;

namespace KMA.Gameplay.Volleyball
{
    public readonly struct OpponentTuning
    {
        public readonly float SpeedFactor;
        public readonly float ReactionDelay;

        public OpponentTuning(float speedFactor, float reactionDelay)
        {
            SpeedFactor = Mathf.Max(0f, speedFactor);
            ReactionDelay = Mathf.Max(0f, reactionDelay);
        }

        // Slow on purpose: the AI reacts late and runs at under half the player's speed, so a ball
        // placed away from it is a point for a casual player.
        public static OpponentTuning Default => new OpponentTuning(.6f, .45f);
    }

    // The whole game as plain state: serve, rally, points, clock and result. Advanced only by
    // Tick, so pausing (dt 0) freezes it and every run with the same inputs is identical.
    public sealed partial class VolleyballMatch
    {
        public const int PointsToWin = 5;
        public const float TimeLimit = 120f;
        public const float PointPause = 1.2f;
        public const float OpponentServeDelay = 1f;
        public const float PlayerServeTimeout = 5f;
        public const float PlayerSpeed = 6.5f;
        // The AI keeps its own base so a faster player doesn't also mean a faster opponent.
        public const float OpponentBaseSpeed = 5f;
        public const float TossStartHeight = 1.2f;
        public const float TossApexHeight = 3.2f;
        public const float SetApexHeight = 4.5f;
        public const float FreeBallApexHeight = 4.5f;
        public const float ReceiveSeconds = .35f;
        public const float SmashSeconds = .45f;
        public const float BlockSeconds = .6f;
        public const float SmashRise = .2f;
        public const float JumpCueLead = .8f;
        public const float SmashAimMargin = .5f;
        const float ServeSeconds = .4f;
        const float DiveSeconds = .8f;
        const float DiveLunge = 1.2f;
        const float DiveApexHeight = 3f;
        // .8 keeps a PERFECT serve to the short line (x 3) clear of the net with margin.
        const float PerfectServeRise = .8f;
        const float NormalServeApexHeight = 5f;
        const float OpponentServeApexHeight = 4.5f;

        public static readonly Vector2 PlayerServeSpot = new Vector2(-8.5f, 0f);
        public static readonly Vector2 OpponentServeSpot = new Vector2(8.5f, 0f);
        public static readonly Vector2 PlayerReadySpot = new Vector2(-5f, 0f);
        public static readonly Vector2 OpponentReadySpot = new Vector2(5f, 0f);
        public static readonly Vector2 PlayerSetSpot = new Vector2(-2f, 0f);
        public static readonly Vector2 OpponentSetSpot = new Vector2(1.5f, 0f);
        static readonly Vector2 NormalServeTarget = new Vector2(5f, 0f);
        static readonly Vector2 FreeBallTarget = new Vector2(5f, 0f);

        readonly OpponentTuning tuning;
        readonly VolleyballMatchOptions options;
        Vector2 move;
        float serveTimer;
        float pauseLeft;
        bool lastHitWasPlayerSmash;

        public VolleyballMatch(OpponentPlan plan = null, OpponentTuning? tuning = null,
            VolleyballMatchOptions options = null)
        {
            this.tuning = tuning ?? OpponentTuning.Default;
            // Matches end on points; TimeLimit is only an opt-in cap for a timed variant.
            this.options = options ?? new VolleyballMatchOptions(PointsToWin, 0f);
            Plan = plan ?? OpponentPlan.Authored();
            Player = new VolleyAthlete(CourtSide.Player, PlayerSpeed);
            Opponent = new VolleyAthlete(CourtSide.Opponent, OpponentBaseSpeed * this.tuning.SpeedFactor);
            Server = this.options.OpponentAlwaysServes ? CourtSide.Opponent : CourtSide.Player;
            BeginPoint();
        }

        public event Action<CourtSide> PointScored;
        public event Action<ActionDecision> PlayerActed;
        public event Action<CourtSide, ActionDecision, int> TouchRegistered;
        public event Action Completed;
        public event Action PlayerBlocked;
        public event Action<CourtSide> ServeStarting;

        public VolleyAthlete Player { get; }
        public VolleyAthlete Opponent { get; }
        public RallyState Rally { get; } = new RallyState();
        public OpponentPlan Plan { get; }
        public BallState BallState { get; private set; }
        public BallFlight Flight { get; private set; }
        public float FlightTime { get; private set; }
        public CourtSide Server { get; private set; }
        public int PlayerPoints { get; private set; }
        public int OpponentPoints { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsOver { get; private set; }
        public int Winners { get; private set; }
        public int TimedPresses { get; private set; }
        public float QualitySum { get; private set; }
        public ActionDecision LastDecision { get; private set; }
        public bool OpponentSmashTell { get; private set; }
        public Vector2 OpponentAim { get; private set; }
        public float TimeRemaining => options.TimeLimit <= 0f ? 0f : Mathf.Max(0f, options.TimeLimit - Elapsed);
        public int WinningPoints => options.PointsToWin;
        public float ClockLimit => options.TimeLimit;

        public Vector2 BallGround => BallState == BallState.Held || Flight == null
            ? ServerAthlete.Position
            : Flight.GroundAt(FlightTime);

        public float BallHeight => BallState == BallState.Held || Flight == null
            ? TossStartHeight
            : Flight.HeightAt(FlightTime);

        VolleyAthlete ServerAthlete => Server == CourtSide.Player ? Player : Opponent;

        public static Vector2 AimAtOpponent(Vector2 stick) =>
            new Vector2(stick.x < -.3f ? 3f : 7f, Mathf.Clamp(stick.y, -1f, 1f) * 3f);

        // Where a jump smash lands: continuous, always inside the opponent's court. Full left is a
        // short drop at x 3 (the shortest smash proven to clear the net), full right the deep line;
        // a diagonal already reaches the sideline, so a short wide smash can swing past a block.
        public static Vector2 SmashAim(Vector2 stick)
        {
            stick = Vector2.ClampMagnitude(stick, 1f);
            float deep = CourtSpace.HalfLength - SmashAimMargin;
            float wide = CourtSpace.HalfWidth - SmashAimMargin;
            return new Vector2(Mathf.Clamp(5.25f + stick.x * 2.25f, 3f, deep),
                Mathf.Clamp(stick.y * 5f, -wide, wide));
        }

        public Vector2 PlayerAim => SmashAim(move);

        public void SetMove(Vector2 stick) => move = Vector2.ClampMagnitude(stick, 1f);

        public ActionDecision PressAction()
        {
            if (IsOver)
                return ActionDecision.None;

            ActionDecision decision = ActionResolver.Resolve(new ActionContext(Player, BallState, Flight, FlightTime,
                Rally, Server, OpponentSmashTell, OpponentAim));
            if (decision.Kind == ActionKind.None)
                return decision;

            // Count the press before applying it: applying can end the match, and the result
            // must already include this press.
            if (decision.IsTimed)
            {
                TimedPresses++;
                QualitySum += decision.Quality;
            }

            LastDecision = decision;
            switch (decision.Kind)
            {
                case ActionKind.ServeToss:
                    StartToss();
                    break;
                case ActionKind.ServeHit:
                    PlayerServe(decision);
                    break;
                case ActionKind.Block:
                    Player.BeginAction(AthleteAction.Block, BlockSeconds);
                    break;
                default:
                    PlayerHit(decision);
                    break;
            }

            PlayerActed?.Invoke(decision);
            return decision;
        }

        // A jump at the net while the opponent telegraphs a smash is a block; anywhere else it is
        // the take-off for a smash. Either way the athlete hangs for JumpSeconds and the stick aims.
        public bool PressJump()
        {
            if (IsOver || BallState != BallState.InPlay)
                return false;

            bool blocking = OpponentSmashTell && Mathf.Abs(Player.Position.x) <= ActionResolver.BlockNetDistance;
            if (!Player.TryJump(blocking ? AthleteAction.Block : AthleteAction.JumpLoad))
                return false;

            if (blocking)
            {
                var decision = new ActionDecision(ActionKind.Block, TimingGrade.Miss, 0f);
                LastDecision = decision;
                PlayerActed?.Invoke(decision);
            }

            return true;
        }

        public void Tick(float deltaTime)
        {
            if (IsOver || deltaTime <= 0f)
                return;

            if (options.TimeLimit > 0f)
                deltaTime = Mathf.Min(deltaTime, Mathf.Max(0f, options.TimeLimit - Elapsed));
            if (deltaTime <= 0f)
            {
                Complete();
                return;
            }

            Elapsed += deltaTime;
            Player.Tick(deltaTime);
            Opponent.Tick(deltaTime);

            switch (BallState)
            {
                case BallState.Held:
                    if (Server == CourtSide.Player)
                    {
                        Player.Move(new Vector2(0f, move.y), deltaTime);
                        // A player who never serves can't stall the match: the ball goes up on its
                        // own, and an unplayed toss is the opponent's point.
                        serveTimer += deltaTime;
                        if (serveTimer >= PlayerServeTimeout)
                            StartToss();
                    }
                    else
                    {
                        Player.Move(move, deltaTime);
                        serveTimer += deltaTime;
                        if (serveTimer >= OpponentServeDelay)
                            StartToss();
                    }
                    break;
                case BallState.Toss:
                    FlightTime += deltaTime;
                    if (Server == CourtSide.Opponent)
                    {
                        Player.Move(move, deltaTime);
                        if (FlightTime >= Flight.ApexTime)
                            OpponentServe();
                    }
                    else if (FlightTime >= Flight.Duration)
                    {
                        AwardPoint(CourtSide.Opponent, false);
                    }
                    break;
                case BallState.InPlay:
                    FlightTime += deltaTime;
                    Player.Move(move, deltaTime);
                    TickOpponent(deltaTime);
                    if (BallState == BallState.InPlay)
                        ResolveBall();
                    break;
                case BallState.Dead:
                    FlightTime += deltaTime;
                    Player.Move(move, deltaTime);
                    pauseLeft -= deltaTime;
                    if (pauseLeft <= 0f && !IsOver)
                        BeginPoint();
                    break;
            }

            if (!IsOver && options.TimeLimit > 0f && Elapsed >= options.TimeLimit)
                Complete();
        }

        public bool TryGetPlayerContactCue(out float secondsToIdeal) =>
            TryGetPlayerContactCue(out secondsToIdeal, out _);

        // Where the player should stand and how long until the ideal press. The contact point is
        // the ball's ground position at that ideal moment, so it stays still while the ball flies.
        public bool TryGetPlayerContactCue(out float secondsToIdeal, out Vector2 contactPoint)
        {
            secondsToIdeal = 0f;
            contactPoint = Vector2.zero;
            if (Flight == null)
                return false;

            float ideal;
            if (BallState == BallState.Toss && Server == CourtSide.Player)
            {
                ideal = Flight.ApexTime;
            }
            else if (BallState == BallState.InPlay && Rally.Possession == CourtSide.Player)
            {
                // Only a jump can smash, so a grounded player is timed for the low receive.
                bool smash = Player.IsAirborne && Rally.Touches >= 1 &&
                             Mathf.Abs(Player.Position.x) <= ActionResolver.SmashNetDistance &&
                             Flight.ApexHeight > ActionResolver.SmashContactHeight;
                ideal = Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
                // Past the smash's late edge the ball can still be played low as a receive.
                if (!smash || FlightTime - ideal > TimingWindows.Late)
                    ideal = Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            }
            else
            {
                return false;
            }

            secondsToIdeal = ideal - FlightTime;
            contactPoint = Flight.GroundAt(ideal);
            return secondsToIdeal >= -TimingWindows.Late;
        }

        // True while a jump now would pay off: a smash coming up near the net, or an opponent
        // smash telegraphed while the player stands at the net.
        public bool TryGetJumpCue(out float secondsToIdeal)
        {
            secondsToIdeal = 0f;
            if (IsOver || BallState != BallState.InPlay || Flight == null || Player.IsAirborne)
                return false;

            if (Rally.Possession != CourtSide.Player)
                return OpponentSmashTell && Mathf.Abs(Player.Position.x) <= ActionResolver.BlockNetDistance;

            if (Rally.Touches < 1 || Rally.Touches > 2 ||
                Mathf.Abs(Player.Position.x) > ActionResolver.SmashNetDistance ||
                Flight.ApexHeight <= ActionResolver.SmashContactHeight)
                return false;

            float smashIdeal = Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
            // Never invite a jump that cannot connect: mid-air the only touch is the smash.
            if (!PlayerInReachOf(Flight.GroundAt(smashIdeal)))
                return false;

            secondsToIdeal = smashIdeal - FlightTime;
            return secondsToIdeal <= JumpCueLead && secondsToIdeal >= -TimingWindows.Late;
        }

        public bool PlayerInReachOf(Vector2 contactPoint) =>
            Vector2.Distance(Player.Position, contactPoint) <= ActionResolver.Reach;

        public MinigameResult BuildResult()
        {
            bool pass = IsOver && (options.RequirePointsToWin
                ? PlayerPoints >= options.PointsToWin
                : PlayerPoints > OpponentPoints);
            float accuracy = TimedPresses == 0 ? 0f : 2f * QualitySum / TimedPresses;
            int target = options.PointsToWin > 0 ? options.PointsToWin : PointsToWin;
            float efficiency = 1f - OpponentPoints / (float)target;
            float mastery = Mathf.Min(Winners, target) / (float)target;
            return ScoreUtil.Build(pass, accuracy, efficiency, mastery);
        }

        public void SetScoreForTest(int playerPoints, int opponentPoints)
        {
            PlayerPoints = Mathf.Max(0, playerPoints);
            OpponentPoints = Mathf.Max(0, opponentPoints);
        }

        public void ForceServerForTest(CourtSide server)
        {
            Server = server;
            BeginPoint();
        }

        public void ResetRally(CourtSide server)
        {
            if (IsOver) return;
            Server = server;
            BeginPoint();
        }

        void BeginPoint()
        {
            Rally.BeginServe(Server);
            BallState = BallState.Held;
            Flight = null;
            FlightTime = 0f;
            serveTimer = 0f;
            lastHitWasPlayerSmash = false;
            OpponentSmashTell = false;
            Player.PlaceAt(Server == CourtSide.Player ? PlayerServeSpot : PlayerReadySpot);
            Opponent.PlaceAt(Server == CourtSide.Opponent ? OpponentServeSpot : OpponentReadySpot);
            ResetOpponentState();
            ServeStarting?.Invoke(Server);
        }

        void StartToss()
        {
            VolleyAthlete server = ServerAthlete;
            Flight = new BallFlight(server.Position, TossStartHeight, server.Position, TossApexHeight, Server);
            FlightTime = 0f;
            BallState = BallState.Toss;
            server.BeginAction(AthleteAction.ServeToss, 0f);
        }

        void PlayerServe(ActionDecision decision)
        {
            bool perfect = decision.Grade == TimingGrade.Perfect;
            Vector2 target = perfect ? AimAtOpponent(move) : NormalServeTarget;
            float apex = perfect ? BallHeight + PerfectServeRise : NormalServeApexHeight;
            Rally.RegisterServe(CourtSide.Player);
            Player.BeginAction(AthleteAction.Serve, ServeSeconds);
            Launch(target, apex, CourtSide.Player);
            TouchRegistered?.Invoke(CourtSide.Player, decision, 0);
        }

        void OpponentServe()
        {
            OpponentStep step = Plan.Current;
            Rally.RegisterServe(CourtSide.Opponent);
            Opponent.BeginAction(AthleteAction.Serve, ServeSeconds);
            Launch(step.ServeTarget, OpponentServeApexHeight, CourtSide.Opponent);
            Plan.Advance();
        }

        void PlayerHit(ActionDecision decision)
        {
            int touchesBefore = Rally.Touches;
            float startHeight = BallHeight;
            Vector2 target;
            float apex;
            AthleteAction animation;
            float lockSeconds;

            switch (decision.Kind)
            {
                case ActionKind.Receive:
                    target = SetTarget(decision.Grade, decision.Offset);
                    apex = SetApexHeight;
                    animation = startHeight <= ActionResolver.ReceiveContactHeight + .45f
                        ? AthleteAction.Dig : AthleteAction.Receive;
                    lockSeconds = ReceiveSeconds;
                    break;
                case ActionKind.FreeBall:
                    target = FreeBallTarget;
                    apex = FreeBallApexHeight;
                    animation = AthleteAction.Receive;
                    lockSeconds = ReceiveSeconds;
                    break;
                case ActionKind.Smash:
                    if (decision.Grade == TimingGrade.Late && decision.Offset < 0f)
                    {
                        target = new Vector2(-.3f, BallGround.y);
                        apex = startHeight + .1f;
                    }
                    else if (decision.Grade == TimingGrade.Late)
                    {
                        target = SmashAim(move);
                        apex = SetApexHeight;
                    }
                    else
                    {
                        // The jump reaches the ball at contact height: a late-side PERFECT/GOOD
                        // press would otherwise hit from below the net, the set falls that fast.
                        startHeight = Mathf.Max(startHeight, ActionResolver.SmashContactHeight);
                        target = SmashAim(move);
                        apex = startHeight + SmashRise;
                    }
                    animation = AthleteAction.Smash;
                    lockSeconds = SmashSeconds;
                    break;
                default:
                    Player.Lunge(Flight.Target, DiveLunge);
                    // On the last available touch a dive can't legally set up for another player
                    // hit - SetTarget always stays on the player's own side, which would be a
                    // guaranteed fault. Send it over as a weak free ball instead, mirroring the
                    // FreeBall branch above.
                    if (Rally.Touches == RallyState.MaxTouches - 1)
                    {
                        target = FreeBallTarget;
                        apex = FreeBallApexHeight;
                    }
                    else
                    {
                        target = SetTarget(TimingGrade.Late, decision.Offset);
                        apex = DiveApexHeight;
                    }

                    animation = AthleteAction.Dive;
                    lockSeconds = DiveSeconds;
                    break;
            }

            bool sendsOver = CourtSpace.SideOf(target) == CourtSide.Opponent;
            Player.BeginAction(animation, lockSeconds);
            TouchRegistered?.Invoke(CourtSide.Player, decision, touchesBefore);
            if (Rally.RegisterTouch(CourtSide.Player, sendsOver) == TouchOutcome.FourthTouchFault)
            {
                AwardPoint(CourtSide.Opponent, false);
                return;
            }

            if (decision.Kind == ActionKind.Smash && sendsOver && OpponentBlocks(target))
                return;

            lastHitWasPlayerSmash = decision.Kind == ActionKind.Smash && sendsOver && decision.Grade != TimingGrade.Late;
            Launch(target, apex, CourtSide.Player, startHeight);
        }

        Vector2 SetTarget(TimingGrade grade, float offset)
        {
            float error = grade == TimingGrade.Perfect ? 0f : grade == TimingGrade.Good ? 1f : 2.5f;
            float x = PlayerSetSpot.x + (offset < 0f ? -error : error);
            return new Vector2(Mathf.Clamp(x, -7.5f, -.5f), Mathf.Clamp(Player.Position.y, -3f, 3f));
        }

        void Launch(Vector2 target, float apex, CourtSide hitter, float? fromHeight = null)
        {
            float startHeight = fromHeight ?? BallHeight;
            Vector2 from = BallGround;
            Flight = new BallFlight(from, startHeight, target, Mathf.Max(apex, startHeight + .01f), hitter);
            FlightTime = 0f;
            BallState = BallState.InPlay;
        }

        void ResolveBall()
        {
            if (Flight.CrossesNet && !Flight.ClearsNet && FlightTime >= Flight.NetCrossTime)
            {
                AwardPoint(Rally.WinnerForNetFault(), false);
                return;
            }

            if (FlightTime < Flight.Duration)
                return;

            CourtSide winner = Rally.WinnerForLanding(Flight.Target);
            bool winnerShot = winner == CourtSide.Player && lastHitWasPlayerSmash && CourtSpace.IsIn(Flight.Target);
            AwardPoint(winner, winnerShot);
        }

        void AwardPoint(CourtSide winner, bool winnerShot)
        {
            if (winner == CourtSide.Player)
            {
                PlayerPoints++;
                if (winnerShot)
                    Winners++;
            }
            else
            {
                OpponentPoints++;
            }

            Server = winner;
            BallState = BallState.Dead;
            pauseLeft = PointPause;
            OpponentSmashTell = false;
            lastHitWasPlayerSmash = false;
            PointScored?.Invoke(winner);
            if (options.OpponentAlwaysServes)
                Server = CourtSide.Opponent;
            if (options.PointsToWin > 0 && (PlayerPoints >= options.PointsToWin || OpponentPoints >= options.PointsToWin))
                Complete();
            else if (options.OpponentPointLimit > 0 && OpponentPoints >= options.OpponentPointLimit)
                Complete();
        }

        void Complete()
        {
            if (IsOver)
                return;

            IsOver = true;
            Completed?.Invoke();
        }
    }
}
