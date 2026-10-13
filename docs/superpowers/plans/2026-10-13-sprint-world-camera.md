# Sprint World Space & Follow Camera Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Sprint race into a real 150 m straight in world space: the camera follows only the player, the finish line has a fixed world X, every rival has its own position/velocity/acceleration, and everyone runs through the line and pulls up within 3–5 m.

**Architecture:** `SprintRules` stays the pure, metre-based logic core; a new `RivalMotion` integrates each rival's `{x, v, a}`. One mapping, `SprintTrackLayout.WorldX(m) = -9.6 + m`, places runners and lines in the world. Presentation components only read state: `SprintCameraFollow` tracks the player, `SprintParallax` scrolls by camera travel, `SprintFinishLinePresenter` pins static lines, `SprintOffscreenRivalChips` labels rivals outside the view.

**Tech Stack:** Unity 6000.3.23f1, C#, Unity Test Framework (NUnit EditMode + PlayMode), TextMeshPro, project UI kit (`KMA.UI.Kit.UiKit`).

**Spec:** `docs/superpowers/specs/2026-10-13-sprint-world-camera-design.md`

## Global Constraints

- World scale: 1 m = 1 world unit. `StartX = -9.6` (existing `SprintTrackLayout.TrackStartX`). Finish at `WorldX(TargetDistance)` = 140.4 for 150 m.
- Rival rolled ranges: reaction 0.12–0.25 s; top speed bands across 8.5–10 m/s (existing `RollField`); acceleration constant τ 0.9–1.4 s with max acceleration 7 m/s²; finish style −6 % … +8 % applied from 80 % of the distance; stop distance 3–5 m past the line.
- Player tap model (impulse, drag, combo boost, fall on wrong tap, idle stop) does not change. Player run-out distance is rolled 3–5 m per attempt.
- Camera: `targetX = max(cameraStartX, playerWorldX)`, smoothing ≈ 0.12 s, no look-ahead, never reads rivals.
- Learn: endless, no finish line, rivals tethered within about 6 m of the player.
- Off-screen rival chip text: `LÀN <lane> +4.2 m` / `LÀN <lane> −3.1 m` (one decimal, signed gap to the player). Vietnamese copy passes through `VietText.Fix`.
- Commits go on the current branch (`codex/sprint-level1`); commit messages carry **no** `Co-Authored-By` trailer.
- Tests run with the Unity Editor **closed**: `tools/run-unity-tests.sh <EditMode|PlayMode> "<filter>" <name>`; the script prints `result=Passed` and exits 0 only on success.

## Review Focus

1. **Pause (`Time.timeScale = 0`) mid-race** — camera, rivals and run-out must hold still; a zero `dt` never moves anything. Pinned in Task 7 (`Step_HoldsWhenPaused`) and Task 4 (existing zero-dt guard, plus `TickAfterRace` ignores zero dt).
2. **Frame hitch across the line** — one long tick (0.5 s) that carries both the player and a rival over the line must still rank them by interpolated crossing time. Pinned in Task 4 (`Rank_UsesInterpolatedCrossingTimes_WhenBothCrossInOneTick`, both orders).
3. **Result opens before the run-out ends** — when the last runner crosses, the result card opens at once, but the player and rivals must keep pulling up rather than freezing on the line. Pinned in Task 5 (`RunnersKeepPullingUpAfterTheResultOpens`).
4. **Learn has no line** — no rival may finish, burst or show a finish line in Learn, and rivals must not run away from the player. Pinned in Task 4 (`Endless_RivalsStayBesideARunningPlayerAndNeverFinish`) and Task 6 (`Rival_NeverBurstsInLearn`).
5. **Time runs out before the player finishes** — the player stops, rivals keep running home during Resolve, nothing divides by a zero run-out speed. Pinned in Task 4 (`TickAfterRace_StopsAnUnfinishedPlayerButRivalsRunOn`).

---

### Task 1: World mapping on the track

**Files:**
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintTrackLayout.cs:32-37`
- Test: `Assets/Tests/EditMode/Presentation/SprintTrackLayoutTests.cs`

**Interfaces:**
- Produces: `SprintTrackLayout.UnitsPerMeter` (const float = 1), `SprintTrackLayout.WorldX(float meters) : float`, `SprintTrackLayout.FinishWorldX(float raceDistance) : float`. `TrackStartX` stays.

- [ ] **Step 1: Write the failing tests** — add inside `SprintTrackLayoutTests`:

```csharp
        [Test]
        public void OneMetreOfRaceIsOneWorldUnitFromTheStart()
        {
            Assert.That(SprintTrackLayout.WorldX(0f), Is.EqualTo(SprintTrackLayout.TrackStartX).Within(.0001f));
            Assert.That(SprintTrackLayout.WorldX(10f) - SprintTrackLayout.WorldX(0f), Is.EqualTo(10f).Within(.0001f));
        }

        [Test]
        public void FinishLineSitsAtTheRaceDistanceInTheWorld() =>
            Assert.That(SprintTrackLayout.FinishWorldX(150f), Is.EqualTo(140.4f).Within(.0001f));
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation.SprintTrackLayoutTests" track-layout`
Expected: no results / compile error `'SprintTrackLayout' does not contain a definition for 'WorldX'`.

- [ ] **Step 3: Implement** — in `SprintTrackLayout`, below `TrackStartX`:

```csharp
        /// The race runs along a real straight: one metre is one world unit from the start.
        public const float UnitsPerMeter = 1f;

        public static float WorldX(float meters) => TrackStartX + meters * UnitsPerMeter;

        /// The finish line stands where a runner who has covered the whole race distance stands.
        public static float FinishWorldX(float raceDistance) => WorldX(raceDistance);
```

- [ ] **Step 4: Run to verify pass** — same command. Expected: `result=Passed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint/SprintTrackLayout.cs Assets/Tests/EditMode/Presentation/SprintTrackLayoutTests.cs
git commit -m "feat(sprint): map race metres onto world X, one metre per unit"
```

---

### Task 2: `RivalMotionTraits` and `RivalMotion`

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Sprint/RivalMotionTraits.cs`
- Create: `Assets/_Project/Scripts/Gameplay/Sprint/RivalMotion.cs`
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/RivalPaceProfile.cs` (add `Traits`)
- Test: `Assets/Tests/EditMode/Gameplay/Running/RivalMotionTests.cs` (create)

**Interfaces:**
- Produces:
  - `readonly struct RivalMotionTraits(float reactionSeconds, float accelerationTau, float finishStyle, float stopDistance, float tetherSlot)` with properties of the same names (PascalCase), constants `MinReaction=.12f, MaxReaction=.25f, MinTau=.9f, MaxTau=1.4f, MinFinishStyle=-.06f, MaxFinishStyle=.08f, MinStopDistance=3f, MaxStopDistance=5f, MinTetherSlot=1.5f, MaxTetherSlot=3.5f`, `static RivalMotionTraits Instant`, `static RivalMotionTraits Roll(System.Random)`.
  - `RivalPaceProfile.Traits : RivalMotionTraits` (defaults to `Instant`); new ctor `RivalPaceProfile(string name, float openingSpeed, float sustainedSpeed, PaceSegment[] segments, RivalMotionTraits traits)`.
  - `sealed class RivalMotion(RivalPaceProfile profile)` with `Position, Velocity, Acceleration, FinishTime (−1 until crossed), HasFinished, Profile`, `StepRace(float clock, float dt, float goal)`, `StepTethered(float clock, float dt, float playerPosition, float playerSpeed)`, `IsSurgingAt(float clock)`, `IsSlowingAt(float clock)`, constants `MaxAcceleration=7f, FinishStretchFrom=.8f, TetherGain=1.5f, TetherPaceDamping=.35f, SurgingRatio=1.06f, SlowingRatio=.9f`.

- [ ] **Step 1: Write the failing tests** — create `RivalMotionTests.cs`:

```csharp
using System;
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class RivalMotionTests
    {
        const float Dt = 1f / 120f;

        static RivalPaceProfile Steady(float speed, RivalMotionTraits traits) =>
            new RivalPaceProfile("Steady", speed, speed, Array.Empty<PaceSegment>(), traits);

        static RivalMotionTraits Traits(float reaction = 0f, float tau = 1f, float style = 0f, float stop = 4f,
            float slot = 0f) => new RivalMotionTraits(reaction, tau, style, stop, slot);

        static float TimeToReach(RivalMotion rival, float speed, float goal = 1000f)
        {
            float clock = 0f;
            while (rival.Velocity < speed && clock < 20f) { rival.StepRace(clock, Dt, goal); clock += Dt; }
            return clock;
        }

        [Test]
        public void StandsStillUntilItsReactionTimeHasPassed()
        {
            var rival = new RivalMotion(Steady(9f, Traits(reaction: .2f)));
            float clock = 0f;
            for (; clock + Dt <= .2f; clock += Dt) rival.StepRace(clock, Dt, 150f);
            Assert.That(rival.Position, Is.Zero);
            Assert.That(rival.Velocity, Is.Zero);
            rival.StepRace(clock, Dt * 2f, 150f);
            Assert.That(rival.Velocity, Is.GreaterThan(0f));
        }

        [TestCase(.9f, 2.5f, 3.5f)]
        [TestCase(1.4f, 4f, 4.7f)]
        public void ReachesNinetyFivePercentOfPaceInARealisticTime(float tau, float earliest, float latest)
        {
            var rival = new RivalMotion(Steady(9f, Traits(tau: tau)));
            Assert.That(TimeToReach(rival, 9f * .95f), Is.InRange(earliest, latest));
        }

        [Test]
        public void NeverAcceleratesHarderThanAHumanCan()
        {
            var rival = new RivalMotion(Steady(10f, Traits(tau: .5f)));
            rival.StepRace(0f, Dt, 150f);
            Assert.That(rival.Acceleration, Is.LessThanOrEqualTo(RivalMotion.MaxAcceleration + 1e-4f));
        }

        [Test]
        public void CrossingTimeIsInterpolatedInsideTheTick()
        {
            var rival = new RivalMotion(Steady(10f, RivalMotionTraits.Instant));
            rival.StepRace(0f, 4.9f, 50f);
            Assert.That(rival.HasFinished, Is.False);
            rival.StepRace(4.9f, .2f, 50f);
            Assert.That(rival.HasFinished, Is.True);
            Assert.That(rival.FinishTime, Is.EqualTo(5f).Within(1e-4f));
        }

        [TestCase(3f)]
        [TestCase(5f)]
        public void RunsThroughTheLineAndPullsUpWithinItsStopDistance(float stop)
        {
            var rival = new RivalMotion(Steady(9f, Traits(tau: 0f, stop: stop)));
            float clock = 0f;
            for (int i = 0; i < 120 * 30; i++, clock += Dt) rival.StepRace(clock, Dt, 50f);
            Assert.That(rival.Velocity, Is.Zero);
            Assert.That(rival.Position - 50f, Is.InRange(stop - .2f, stop + .05f));
        }

        [Test]
        public void FinishStyleOnlyChangesPaceInTheLastFifthOfTheRace()
        {
            var closer = new RivalMotion(Steady(9f, Traits(tau: 0f, style: .08f)));
            var plain = new RivalMotion(Steady(9f, Traits(tau: 0f)));
            closer.StepRace(5f, Dt, 150f);
            plain.StepRace(5f, Dt, 150f);
            Assert.That(closer.Velocity, Is.EqualTo(plain.Velocity).Within(1e-4f), "no kick before 80 %");

            float clock = 5f;
            while (closer.Position < 125f) { closer.StepRace(clock, Dt, 150f); clock += Dt; }
            closer.StepRace(clock, Dt, 150f);
            Assert.That(closer.Velocity, Is.EqualTo(9f * 1.08f).Within(1e-3f));
        }

        [Test]
        public void TetheredRivalSettlesBesideTheRunningPlayer()
        {
            var rival = new RivalMotion(Steady(9f, Traits(slot: 3f)));
            float clock = 0f, player = 0f;
            for (int i = 0; i < 120 * 20; i++, clock += Dt)
            {
                player += 8f * Dt;
                rival.StepTethered(clock, Dt, player, 8f);
            }
            Assert.That(rival.Position - player, Is.EqualTo(3f).Within(.5f));
            Assert.That(rival.HasFinished, Is.False);
        }

        [Test]
        public void SameSeedRollsTheSameTraitsInsideTheirRanges()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                RivalMotionTraits a = RivalMotionTraits.Roll(new Random(seed));
                RivalMotionTraits b = RivalMotionTraits.Roll(new Random(seed));
                Assert.That(a.ReactionSeconds, Is.EqualTo(b.ReactionSeconds));
                Assert.That(a.ReactionSeconds, Is.InRange(RivalMotionTraits.MinReaction, RivalMotionTraits.MaxReaction));
                Assert.That(a.AccelerationTau, Is.InRange(RivalMotionTraits.MinTau, RivalMotionTraits.MaxTau));
                Assert.That(a.FinishStyle, Is.InRange(RivalMotionTraits.MinFinishStyle, RivalMotionTraits.MaxFinishStyle));
                Assert.That(a.StopDistance, Is.InRange(RivalMotionTraits.MinStopDistance, RivalMotionTraits.MaxStopDistance));
                Assert.That(Math.Abs(a.TetherSlot), Is.InRange(RivalMotionTraits.MinTetherSlot, RivalMotionTraits.MaxTetherSlot));
            }
        }

        [Test]
        public void HandBuiltProfilesMoveInstantly() =>
            Assert.That(new RivalPaceProfile("A", 6f, 6f).Traits.AccelerationTau, Is.Zero);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Running.RivalMotionTests" rival-motion`
Expected: compile error, `RivalMotion` / `RivalMotionTraits` not found.

- [ ] **Step 3: Implement `RivalMotionTraits.cs`**

```csharp
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
```

- [ ] **Step 4: Add `Traits` to `RivalPaceProfile`** — in `RivalPaceProfile.cs` add the property after `SurgePhase` and a new constructor after the segments constructor:

```csharp
        /// How this rival reacts, accelerates and pulls up. Hand-built profiles move instantly.
        public RivalMotionTraits Traits { get; } = RivalMotionTraits.Instant;
```

```csharp
        public RivalPaceProfile(string name, float openingSpeed, float sustainedSpeed, PaceSegment[] segments,
            RivalMotionTraits traits)
            : this(name, openingSpeed, sustainedSpeed, segments)
        {
            Traits = traits;
        }
```

- [ ] **Step 5: Implement `RivalMotion.cs`**

```csharp
using System;
using UnityEngine;

namespace KMA.Gameplay
{
    /// One rival's body on the track: position, velocity and acceleration in metres and seconds.
    /// The pace profile says how fast they want to run; this decides how fast they actually go.
    public sealed class RivalMotion
    {
        public const float MaxAcceleration = 7f;
        public const float FinishStretchFrom = .8f;
        // Learn: speed added per metre a rival is away from their slot beside the player.
        public const float TetherGain = 1.5f;
        // Learn: how much of the rival's own surge/fade survives, so they wander without running away.
        public const float TetherPaceDamping = .35f;
        public const float SurgingRatio = 1.06f;
        public const float SlowingRatio = .9f;
        // A runner who barely has room left before their stop point pulls up at once.
        const float MinBrakeRoom = .01f;

        readonly RivalPaceProfile profile;
        readonly RivalMotionTraits traits;
        float brake;

        public RivalMotion(RivalPaceProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            traits = profile.Traits;
            FinishTime = -1f;
        }

        public RivalPaceProfile Profile => profile;
        public float Position { get; private set; }
        public float Velocity { get; private set; }
        public float Acceleration { get; private set; }
        /// Race time at which the rival crossed the line, interpolated inside the tick; −1 before.
        public float FinishTime { get; private set; }
        public bool HasFinished => FinishTime >= 0f;

        public bool IsSurgingAt(float clock) => !HasFinished && clock > RivalPaceProfile.OpeningSeconds &&
            Velocity > profile.SustainedSpeed * SurgingRatio;

        public bool IsSlowingAt(float clock) => !HasFinished && clock > RivalPaceProfile.OpeningSeconds &&
            Velocity < profile.SustainedSpeed * SlowingRatio;

        /// One tick of a race to <paramref name="goal"/>; <paramref name="clock"/> is the race time at its start.
        public void StepRace(float clock, float dt, float goal)
        {
            if (dt <= 0f) return;
            if (HasFinished)
            {
                Acceleration = -brake;
                Velocity = Mathf.Max(0f, Velocity - brake * dt);
                Position += Velocity * dt;
                return;
            }

            float end = clock + dt;
            if (end <= traits.ReactionSeconds) { Acceleration = 0f; return; }
            float target = profile.SpeedAt(end);
            if (Position >= goal * FinishStretchFrom) target *= 1f + traits.FinishStyle;
            Chase(target, dt);

            float before = Position;
            Position += Velocity * dt;
            if (before < goal && Position >= goal)
            {
                FinishTime = clock + dt * ((goal - before) / (Position - before));
                float room = goal + StopDistance - Position;
                brake = room > MinBrakeRoom ? Velocity * Velocity / (2f * room) : float.PositiveInfinity;
                if (float.IsPositiveInfinity(brake)) Velocity = 0f;
            }
        }

        /// Learn has no line: the rival runs beside the player, pulled toward their own slot.
        public void StepTethered(float clock, float dt, float playerPosition, float playerSpeed)
        {
            if (dt <= 0f) return;
            float end = clock + dt;
            if (end <= traits.ReactionSeconds) { Acceleration = 0f; return; }
            float pace = end > RivalPaceProfile.OpeningSeconds && profile.SustainedSpeed > 0f
                ? 1f + (profile.SpeedAt(end) / profile.SustainedSpeed - 1f) * TetherPaceDamping
                : 1f;
            float gap = playerPosition + traits.TetherSlot - Position;
            Chase(Mathf.Max(0f, playerSpeed * pace + TetherGain * gap), dt);
            Position += Velocity * dt;
        }

        float StopDistance => traits.StopDistance > 0f ? traits.StopDistance : 4f;

        void Chase(float target, float dt)
        {
            if (traits.AccelerationTau <= 0f)
            {
                Acceleration = 0f;
                Velocity = Mathf.Max(0f, target);
                return;
            }
            Acceleration = Mathf.Min((target - Velocity) / traits.AccelerationTau, MaxAcceleration);
            float next = Velocity + Acceleration * dt;
            // A long tick must not carry the runner past the pace they were chasing.
            Velocity = Mathf.Max(0f, Acceleration >= 0f ? Mathf.Min(next, target) : Mathf.Max(next, target));
        }
    }
}
```

- [ ] **Step 6: Run to verify pass** — same command as Step 2. Expected: `result=Passed`. If `ReachesNinetyFivePercent…` misses its window, re-check `Chase` (the cap must apply to `Acceleration`, not to `Velocity`), not the test bounds.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint/RivalMotionTraits.cs* Assets/_Project/Scripts/Gameplay/Sprint/RivalMotion.cs* Assets/_Project/Scripts/Gameplay/Sprint/RivalPaceProfile.cs Assets/Tests/EditMode/Gameplay/Running/RivalMotionTests.cs*
git commit -m "feat(sprint): rival motion with reaction, acceleration, finishing style and run-out"
```

---

### Task 3: Roll motion traits with the field

**Files:**
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/RivalPaceRandomizer.cs:24-32`
- Test: `Assets/Tests/EditMode/Gameplay/Running/RivalPaceProfileTests.cs`

**Interfaces:**
- Consumes: `RivalMotionTraits.Roll(Random)`, `RivalPaceProfile(name, opening, sustained, segments, traits)` (Task 2).
- Produces: every profile returned by `RivalPaceRandomizer.Roll` / `RollField` carries rolled `Traits`.

- [ ] **Step 1: Write the failing test** — add to `RivalPaceProfileTests`:

```csharp
        [Test]
        public void RolledRivalsCarryTheirOwnMotionTraits()
        {
            RivalPaceProfile[] field = RivalPaceRandomizer.RollField(LoadAuthoredProfiles(), MinSpeed, MaxSpeed,
                new Random(7));
            foreach (RivalPaceProfile rolled in field)
                Assert.That(rolled.Traits.AccelerationTau, Is.InRange(RivalMotionTraits.MinTau, RivalMotionTraits.MaxTau));
            Assert.That(field.Select(rolled => rolled.Traits.ReactionSeconds).Distinct().Count(), Is.EqualTo(field.Length),
                "each rival reacts to the gun on their own");
        }
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Running.RivalPaceProfileTests" rival-pace`
Expected: FAIL in `RolledRivalsCarryTheirOwnMotionTraits` (τ is 0, the `Instant` default).

- [ ] **Step 3: Implement** — replace the `return` line of `Roll`:

```csharp
            return new RivalPaceProfile(authored.Name, sustained * openingRatio, sustained, RollSchedule(random),
                RivalMotionTraits.Roll(random));
```

- [ ] **Step 4: Run to verify pass** — same command. Expected: `result=Passed` (the finish-window tests still use the old rules here and still pass).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint/RivalPaceRandomizer.cs Assets/Tests/EditMode/Gameplay/Running/RivalPaceProfileTests.cs
git commit -m "feat(sprint): roll each rival's reaction, acceleration and finishing style"
```

---

### Task 4: `SprintRules` races real bodies, runs through the line, ranks by crossing time

**Files:**
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintRules.cs` (rival storage, Tick, finish, run-out, rank, endless)
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintChallengeRules.cs:11-20` (Learn is endless; run-out pass-through)
- Test: `Assets/Tests/EditMode/Gameplay/Running/SprintRulesTests.cs`, `Assets/Tests/EditMode/Gameplay/Running/RivalPaceProfileTests.cs`

**Interfaces:**
- Consumes: `RivalMotion` (Task 2).
- Produces on `SprintRules`:
  - ctor gains trailing `float runOutDistance = DefaultRunOutDistance`; `const float DefaultRunOutDistance = 4f`.
  - `ForTest(float distance, float elapsed, int rank, RivalPaceProfile[] rivalProfiles = null, Side[] authoredSequence = null, float speed = 0f, float goalDistance = RaceDistance)`.
  - `bool IsEndless`, `float GoalDistance`, `float Position` (distance + run-out), `float MetersPerSecond`, `float FinishTime` (−1 until crossed).
  - `float GetRivalPosition(int)`, `float GetRivalSpeed(int)`, `bool HasRivalFinished(int)`, `float GetRivalFinishTime(int)`; `GetRivalDistance(int)` now returns `min(position, goal)`.
  - `void TickAfterRace(float dt)`.
  - `Tick` now finishes the player itself on crossing (`IsFinished` true, `Elapsed` = interpolated crossing time, `Distance` = goal).
- Produces on `SprintChallengeRules`: ctor `(ChallengeDefinition, SprintBalanceParameters, RivalPaceProfile[], float runOutDistance = SprintRules.DefaultRunOutDistance)`; Learn races use `goalDistance = float.PositiveInfinity`.

- [ ] **Step 1: Replace the two obsolete rival tests and add the new ones** — in `SprintRulesTests`, delete `Rival_StopsAtTheFinishLineInsteadOfRunningOn` and `Rival_StaysAtTheLineWhileThePlayerRunsIn`, then add:

```csharp
        const float Dt = 1f / 120f;
        // Raw tap speed is metres per second divided by DistanceScale.
        static float Raw(float metersPerSecond) => metersPerSecond / SprintBalanceParameters.Default.DistanceScale;

        [Test]
        public void Rival_RunsThroughTheLineAndPullsUpWithinItsStopDistance()
        {
            var rules = new SprintRules(rivalProfiles: new[] { new RivalPaceProfile("Steady", 9f, 9f) },
                goalDistance: 50f);
            for (int i = 0; i < 120 * 10; i++) rules.Tick(Dt);

            Assert.That(rules.HasRivalFinished(0), Is.True);
            Assert.That(rules.GetRivalDistance(0), Is.EqualTo(50f), "race distance stops on the line");
            Assert.That(rules.GetRivalPosition(0) - 50f, Is.InRange(3.8f, 4.05f), "but the body runs on ~4 m");
            Assert.That(rules.GetRivalSpeed(0), Is.Zero);
            Assert.That(rules.AllRivalsReached(50f), Is.True);
        }

        [Test]
        public void Player_CrossingFinishesTheRaceAtTheInterpolatedTimeAndRunsOn()
        {
            var rules = SprintRules.ForTest(distance: 49f, elapsed: 10f, rank: 1, speed: Raw(10f), goalDistance: 50f);
            rules.Tick(.2f);

            Assert.That(rules.IsFinished, Is.True);
            Assert.That(rules.Distance, Is.EqualTo(50f));
            Assert.That(rules.Elapsed, Is.EqualTo(10.1f).Within(.005f));
            Assert.That(rules.BuildResult(50f, 60f).Pass, Is.True);

            for (int i = 0; i < 120 * 3; i++) rules.Tick(Dt);
            Assert.That(rules.Position - 50f, Is.InRange(3.8f, 4.05f));
            Assert.That(rules.MetersPerSecond, Is.Zero);
            Assert.That(rules.Elapsed, Is.EqualTo(10.1f).Within(.005f), "the clock stopped on the line");
        }

        [TestCase(60f, 210f, 1)]   // player crosses at ~0.40 s, rival at ~0.48 s
        [TestCase(60f, 400f, 2)]   // player crosses at ~0.40 s, rival at 0.25 s
        public void Rank_UsesInterpolatedCrossingTimes_WhenBothCrossInOneTick(float playerStart, float rivalSpeed,
            int expectedRank)
        {
            // The rival starts from 0 and covers 100 m at rivalSpeed; the player runs 100 m/s from playerStart.
            var rules = SprintRules.ForTest(distance: playerStart, elapsed: 0f, rank: 1,
                rivalProfiles: new[] { new RivalPaceProfile("R", rivalSpeed, rivalSpeed) },
                speed: Raw(100f), goalDistance: 100f);
            rules.Tick(.5f);

            Assert.That(rules.IsFinished, Is.True);
            Assert.That(rules.HasRivalFinished(0), Is.True);
            Assert.That(rules.Rank, Is.EqualTo(expectedRank));
        }

        [Test]
        public void Endless_RivalsStayBesideARunningPlayerAndNeverFinish()
        {
            var authored = new[]
            {
                new RivalPaceProfile("A", 8.5f, 5.7f), new RivalPaceProfile("B", 5.8f, 5.7f),
                new RivalPaceProfile("C", 6f, 7.1f)
            };
            var rules = new SprintRules(60f, RivalPaceRandomizer.RollField(authored, 8.5f, 10f, new System.Random(3)),
                null, SprintBalanceParameters.Default, float.PositiveInfinity);
            Side next = Side.Left;
            for (int i = 0; i < 60 * 50; i++)
            {
                if (i % 10 == 0) { rules.Tap(next); next = next == Side.Left ? Side.Right : Side.Left; }
                rules.Tick(1f / 60f);
                if (i < 60 * 5) continue;
                for (int r = 0; r < rules.RivalCount; r++)
                    Assert.That(Mathf.Abs(rules.GetRivalPosition(r) - rules.Position), Is.LessThan(6f),
                        $"rival {r} at {i / 60f:0.0}s");
            }
            Assert.That(rules.IsEndless, Is.True);
            Assert.That(rules.IsFinished, Is.False);
            for (int r = 0; r < rules.RivalCount; r++) Assert.That(rules.HasRivalFinished(r), Is.False);
        }

        [Test]
        public void TickAfterRace_StopsAnUnfinishedPlayerButRivalsRunOn()
        {
            var rules = SprintRules.ForTest(distance: 20f, elapsed: 22f, rank: 1,
                rivalProfiles: new[] { new RivalPaceProfile("R", 9f, 9f) }, speed: Raw(8f));
            float before = rules.GetRivalPosition(0);

            rules.TickAfterRace(1f);
            rules.TickAfterRace(0f);

            Assert.That(rules.MetersPerSecond, Is.Zero);
            Assert.That(rules.Position, Is.EqualTo(20f));
            Assert.That(rules.GetRivalPosition(0), Is.EqualTo(before + 9f).Within(.001f));
        }
```

Also add `using UnityEngine;` at the top of the file (for `Mathf`).

- [ ] **Step 2: Tighten the rival finish window** — in `RivalPaceProfileTests`, replace `FinishTime` and the window asserts, since acceleration now costs every rival about a second off the line:

```csharp
        static float FinishTime(RivalPaceProfile profile)
        {
            var rules = new SprintRules(60f, new[] { profile });
            while (!rules.HasRivalFinished(0) && rules.Elapsed < 60f)
                rules.Tick(1f / 120f);
            return rules.GetRivalFinishTime(0);
        }
```

and in `ExtremeRollsHitTheFinishWindowEdges` replace the comment and both asserts:

```csharp
            // With a real start the field crosses 150 m in about 15–17 s (fastest) to 18–20 s (slowest).
            Assert.That(fastest, Is.InRange(13.5f, 17f));
            Assert.That(slowest, Is.InRange(17f, SlowestFinish));
```

- [ ] **Step 3: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Running" running-rules`
Expected: compile errors (`HasRivalFinished`, `GetRivalPosition`, `ForTest(... speed:, goalDistance:)` missing).

- [ ] **Step 4: Implement `SprintRules`** — apply these edits to `SprintRules.cs`:

Fields: replace `readonly float[] rivalDistances;` and `float rivalClock;` with:

```csharp
        readonly RivalMotion[] rivals;
        readonly float runOutDistance;
        // Race time for the whole field; it keeps running after the player's own clock stops on the line.
        float raceClock;
        float finishTime = -1f;
        // The player's few metres past the line, and how fast they are still covering them.
        float runOut;
        float runOutSpeed;
        float runOutBrake;
```

Add the constant under `FallCooldownSeconds`:

```csharp
        // How far past the line the player runs before standing still, unless the controller rolls one.
        public const float DefaultRunOutDistance = 4f;
```

Constructor: add the trailing parameter `float runOutDistance = DefaultRunOutDistance`, store it, and replace `rivalDistances = new float[this.rivalProfiles.Length];` with:

```csharp
            this.runOutDistance = runOutDistance;
            rivals = new RivalMotion[this.rivalProfiles.Length];
            for (int i = 0; i < rivals.Length; i++)
                rivals[i] = this.rivalProfiles[i] == null ? null : new RivalMotion(this.rivalProfiles[i]);
```

`ForTest`:

```csharp
        public static SprintRules ForTest(float distance, float elapsed, int rank,
            RivalPaceProfile[] rivalProfiles = null, Side[] authoredSequence = null, float speed = 0f,
            float goalDistance = RaceDistance)
        {
            _ = rank;
            var value = new SprintRules(DefaultTimeLimit, rivalProfiles, authoredSequence, goalDistance: goalDistance);
            value.distance = distance;
            value.elapsed = elapsed;
            value.raceClock = elapsed;
            value.speed = speed;
            value.UpdateRank();
            return value;
        }
```

Replace the rival/finish accessors (`RivalTime`, `RivalDistances`, `RivalCount`, `GetRivalDistance`, `IsRivalSurging`, `IsRivalSlowing`) with:

```csharp
        /// Learn has no finish line: rivals run beside the player instead of racing to one.
        public bool IsEndless => float.IsPositiveInfinity(goalDistance);
        public float GoalDistance => goalDistance;
        /// Where the player actually is, including the metres they run on past the line.
        public float Position => distance + runOut;
        public float MetersPerSecond => finished ? runOutSpeed : speed * Tuning.DistanceScale;
        /// Race time at which the player crossed the line; −1 until they do.
        public float FinishTime => finishTime;
        public float[] RivalDistances
        {
            get
            {
                var values = new float[rivals.Length];
                for (int i = 0; i < values.Length; i++) values[i] = GetRivalDistance(i);
                return values;
            }
        }
        public int RivalCount => rivals.Length;
        RivalMotion RivalAt(int index) => index < 0 || index >= rivals.Length ? null : rivals[index];
        /// Race distance covered, stopping on the line; see <see cref="GetRivalPosition"/> for the body.
        public float GetRivalDistance(int index) => RivalAt(index) == null ? 0f : Mathf.Min(RivalAt(index).Position, goalDistance);
        public float GetRivalPosition(int index) => RivalAt(index)?.Position ?? 0f;
        public float GetRivalSpeed(int index) => RivalAt(index)?.Velocity ?? 0f;
        public bool HasRivalFinished(int index) => RivalAt(index) != null && RivalAt(index).HasFinished;
        public float GetRivalFinishTime(int index) => RivalAt(index)?.FinishTime ?? -1f;
        public bool IsRivalSurging(int index) => !IsEndless && RivalAt(index) != null && RivalAt(index).IsSurgingAt(raceClock);
        public bool IsRivalSlowing(int index) => !IsEndless && RivalAt(index) != null && RivalAt(index).IsSlowingAt(raceClock);
```

`FinishRace` (still used when a test teleports the player onto the line):

```csharp
        public void FinishRace()
        {
            if (finished) return;
            finished = true;
            finishTime = elapsed;
            StartRunOut(speed * Tuning.DistanceScale);
        }
```

`AllRivalsReached`:

```csharp
        public bool AllRivalsReached(float goalDistance)
        {
            for (int i = 0; i < rivals.Length; i++)
                if (rivals[i] != null && rivals[i].Position < goalDistance) return false;
            return true;
        }
```

`Tick`, plus the new helpers:

```csharp
        public void Tick(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float clock = raceClock;
            raceClock += dt;
            if (finished)
            {
                AdvanceRunOut(dt);
                AdvanceRivals(clock, dt);
                return;
            }

            float startElapsed = elapsed;
            elapsed += dt;
            SprintBalanceParameters tuning = Tuning;
            if (IsFalling || IsIdle) speed = 0f;
            else speed = Mathf.Max(0f, speed - tuning.DragPerSecond * dt);
            float before = distance;
            float step = speed * dt * tuning.DistanceScale;
            distance += step;
            AdvanceRivals(clock, dt);

            if (!IsEndless && before < goalDistance && distance >= goalDistance)
            {
                finishTime = startElapsed + dt * (step > 0f ? (goalDistance - before) / step : 1f);
                elapsed = finishTime;
                runOut = distance - goalDistance;
                distance = goalDistance;
                finished = true;
                // Rivals who crossed earlier in this same tick still beat the player.
                currentRank = 1;
                for (int i = 0; i < rivals.Length; i++)
                    if (rivals[i] != null && rivals[i].HasFinished && rivals[i].FinishTime < finishTime) currentRank++;
                StartRunOut(speed * tuning.DistanceScale);
                return;
            }

            UpdateRank();
        }

        /// After the result is decided the runners still pull up and the field still runs in;
        /// the race numbers do not change.
        public void TickAfterRace(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float clock = raceClock;
            raceClock += dt;
            if (finished) AdvanceRunOut(dt);
            else speed = 0f;
            AdvanceRivals(clock, dt);
        }

        void AdvanceRivals(float clock, float dt)
        {
            for (int i = 0; i < rivals.Length; i++)
            {
                if (rivals[i] == null) continue;
                if (IsEndless) rivals[i].StepTethered(clock, dt, Position, MetersPerSecond);
                else rivals[i].StepRace(clock, dt, goalDistance);
            }
        }

        void StartRunOut(float metersPerSecond)
        {
            speed = 0f;
            runOutSpeed = metersPerSecond;
            float room = runOutDistance - runOut;
            runOutBrake = room > .01f ? metersPerSecond * metersPerSecond / (2f * room) : 0f;
            if (room <= .01f) runOutSpeed = 0f;
        }

        void AdvanceRunOut(float dt)
        {
            if (runOutSpeed <= 0f) return;
            runOutSpeed = Mathf.Max(0f, runOutSpeed - runOutBrake * dt);
            runOut += runOutSpeed * dt;
        }
```

Delete the old `AdvanceRival` method. Replace `UpdateRank`:

```csharp
        void UpdateRank()
        {
            currentRank = 1;
            for (int i = 0; i < rivals.Length; i++)
                if (rivals[i] != null && (rivals[i].HasFinished || rivals[i].Position > distance)) currentRank++;
        }
```

In `BuildResult(float, float)` replace `int rivals = rivalDistances.Length;` with `int rivalTotal = rivals.Length;` and use `rivalTotal` in the placement line.

- [ ] **Step 5: Implement `SprintChallengeRules`** — replace the constructor:

```csharp
        public SprintChallengeRules(ChallengeDefinition definition, SprintBalanceParameters balance,
            RivalPaceProfile[] rivals, float runOutDistance = SprintRules.DefaultRunOutDistance)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.Subject != SubjectId.Sprint)
                throw new ArgumentException("Sprint challenge rules require a Sprint definition.", nameof(definition));
            // Learn is a cadence drill on an endless straight; races run to the authored distance.
            float goal = definition.Kind == ChallengeKind.Learn ? float.PositiveInfinity
                : definition.Distance > 0f ? definition.Distance : SprintRules.RaceDistance;
            Race = new SprintRules(definition.TimeLimit, rivals, new[] { Side.Left, Side.Right }, balance, goal,
                runOutDistance);
        }
```

- [ ] **Step 6: Run to verify pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Running" running-rules`
Expected: `result=Passed`. `SprintChallengeTests.FinishedRacerFreezesWhileRivalsKeepRunning` must still pass unchanged (`Speed` stays 0 after `FinishRace`, `Distance` stays 150).

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint/SprintRules.cs Assets/_Project/Scripts/Gameplay/Sprint/SprintChallengeRules.cs Assets/Tests/EditMode/Gameplay/Running/SprintRulesTests.cs Assets/Tests/EditMode/Gameplay/Running/RivalPaceProfileTests.cs
git commit -m "feat(sprint): race real rival bodies, run through the line, rank by crossing time"
```

---

### Task 5: `SprintController` exposes positions and keeps the run-out alive in Resolve

**Files:**
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintController.cs`
- Test: `Assets/Tests/PlayMode/Gameplay/Running/SprintControllerTests.cs`

**Interfaces:**
- Consumes: Task 4 `SprintRules` API.
- Produces on `SprintController`: `float PlayerPosition`, `float PlayerMetersPerSecond`, `bool IsPlayerFinished`, `float RaceGoal` (∞ in Learn), `float GetRivalPosition(int)`, `float GetRivalSpeed(int)`, `bool HasRivalFinished(int)`. `LeadDistance` stays until Task 9.

- [ ] **Step 1: Write the failing test** — add to `SprintControllerTests`:

```csharp
        [Test]
        public void RunnersKeepPullingUpAfterTheResultOpens()
        {
            var controller = CreateSprintController();
            try
            {
                controller.AdvanceToDistance(SprintRules.RaceDistance - 1f, new[] { new RivalPaceProfile("Fast", 500f, 500f) });
                for (int i = 0; i < 4; i++) { controller.OnLeftTap(); controller.OnRightTap(); }
                for (int i = 0; i < 300 && controller.Phase != MinigamePhase.Resolve; i++) controller.Simulate(.01f);
                Assert.That(controller.Phase, Is.EqualTo(MinigamePhase.Resolve));

                float player = controller.PlayerPosition;
                float rival = controller.GetRivalPosition(0);
                Assert.That(player, Is.LessThan(SprintRules.RaceDistance + 4.05f));
                for (int i = 0; i < 300; i++) controller.Simulate(.01f);

                Assert.That(controller.PlayerPosition, Is.GreaterThan(player), "the player pulls up, not freezes");
                Assert.That(controller.PlayerPosition - SprintRules.RaceDistance, Is.InRange(3.8f, 4.05f));
                Assert.That(controller.GetRivalPosition(0), Is.GreaterThanOrEqualTo(rival));
                Assert.That(controller.PlayerMetersPerSecond, Is.Zero);
                Assert.That(controller.IsPlayerFinished, Is.True);
            }
            finally
            {
                DestroyController(controller);
            }
        }
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Gameplay.Running.SprintControllerTests" sprint-controller`
Expected: compile error (`PlayerPosition` missing).

- [ ] **Step 3: Implement** — in `SprintController.cs`:

Add the properties next to `GetRivalDistance`:

```csharp
        /// Where the player's body is on the straight, including the run-out past the line.
        public float PlayerPosition => rules == null ? 0f : rules.Position;
        public float PlayerMetersPerSecond => rules == null ? 0f : rules.MetersPerSecond;
        public bool IsPlayerFinished => rules != null && rules.IsFinished;
        /// The line everyone races to; Learn has none.
        public float RaceGoal => IsLearnChallenge ? float.PositiveInfinity : TargetDistance;
        public float GetRivalPosition(int index) => rules == null ? 0f : rules.GetRivalPosition(index);
        public float GetRivalSpeed(int index) => rules == null ? 0f : rules.GetRivalSpeed(index);
        public bool HasRivalFinished(int index) => rules != null && rules.HasRivalFinished(index);
```

In `ConfigureChallenge`, roll the player's run-out:

```csharp
            challengeRules = new SprintChallengeRules(definition, SprintBalanceConfig.LoadDefault().ToRuntime(),
                CreateRuntimeProfiles(),
                UnityEngine.Random.Range(RivalMotionTraits.MinStopDistance, RivalMotionTraits.MaxStopDistance));
```

Keep the runners moving once the result is decided — add after `Awake`:

```csharp
        protected override void Update()
        {
            base.Update();
            if (Lifecycle.Phase == MinigamePhase.Resolve) rules?.TickAfterRace(Time.deltaTime);
        }
```

and in `Simulate`:

```csharp
        public void Simulate(float dt)
        {
            Lifecycle.Tick(dt);
            if (Lifecycle.Phase == MinigamePhase.Play)
                TickPlay(dt);
            else if (Lifecycle.Phase == MinigamePhase.Resolve)
                rules.TickAfterRace(dt);
        }
```

Replace the body of `EvaluateTerminalOutcome` after the `rules.IsFinished` block (the rules now finish the player themselves; `FinishRace` remains only for a player placed on the line by a test):

```csharp
            bool learn = IsLearnChallenge;
            bool finished = learn ? challengeRules.IsComplete : rules.Snapshot.Distance >= TargetDistance;
            bool timedOut = !learn && rules.Snapshot.Elapsed >= TargetTime;
            if (!finished && !timedOut)
                return;

            if (!learn && finished)
            {
                rules.FinishRace();
                rivalWaitElapsed = 0f;
                if (rules.RivalCount > 0 && !rules.AllRivalsReached(TargetDistance)) return;
            }

            Resolve();
```

- [ ] **Step 4: Run to verify pass, then the whole Sprint PlayMode suite**

Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Gameplay.Running" sprint-running`
Expected: `result=Passed` (includes `SprintChallengeRuntimeTests`, `SprintRuntimeInputTests`, `SprintDialogueTests`). If `SprintChallengeRuntimeTests` line ~148 (`GetRivalDistance(0) > 0`) fails, it ticks shorter than the rolled reaction time (≤ 0.25 s): make that test simulate at least 0.5 s before asserting.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint/SprintController.cs Assets/Tests/PlayMode/Gameplay/Running/SprintControllerTests.cs
git commit -m "feat(sprint): expose runner positions and keep runners pulling up after the result"
```

---

### Task 6: Runners stand at their world position with matching strides

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Sprint/RunnerStride.cs`
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/RunnerVisualPresenter.cs`
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/RivalRunnerAI.cs`
- Test: `Assets/Tests/EditMode/Gameplay/Running/RunnerStrideTests.cs` (create), `Assets/Tests/PlayMode/Gameplay/Running/SprintControllerTests.cs`, `Assets/Tests/PlayMode/Presentation/RunnerVisualTests.cs`

**Interfaces:**
- Consumes: Task 1 `SprintTrackLayout.WorldX`; Task 5 controller API.
- Produces: `RunnerStride.Playback(float metersPerSecond) : float`, `RunnerStride.ClipSpeed = 9f`; `RivalRunnerAI.RefreshForTest(float position, float speed, bool finished, float goal, MinigamePhase phase, MinigameResult result, bool surging = false, bool slowing = false)`. `RivalRunnerAI.VisualProgress01` is removed.

- [ ] **Step 1: Write the failing tests**

Create `RunnerStrideTests.cs`:

```csharp
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class RunnerStrideTests
    {
        [TestCase(9f, 1f)]
        [TestCase(4.5f, .5f)]
        [TestCase(1f, .5f)]
        [TestCase(20f, 1.5f)]
        [TestCase(0f, 1f)]
        public void RunCyclePlaysAtTheSpeedTheRunnerCoversGround(float metersPerSecond, float expected) =>
            Assert.That(RunnerStride.Playback(metersPerSecond), Is.EqualTo(expected).Within(1e-4f));
    }
}
```

In `SprintControllerTests`, replace `RivalVisualBurstNearTheFinish_IsCosmeticAndDoesNotChangeRulesDistances`, `Rival_KeepsRunningToTheLineWhateverThePlayerDoes` and `Rival_StopsRunningOnceAcrossTheLine` with:

```csharp
        [Test]
        public void RivalVisualBurstNearTheFinish_IsCosmeticAndDoesNotChangeRulesDistances()
        {
            var profile = ScriptableObject.CreateInstance<RivalPaceProfileAsset>();
            profile.profileName = "Test";
            profile.openingSpeed = 8f;
            profile.sustainedSpeed = 7f;
            var runner = new GameObject("Rival").AddComponent<RivalRunnerAI>();
            runner.Configure(profile, 1, 0, null);

            var rules = new SprintRules(rivalProfiles: new[] { profile.ToRuntime() });
            rules.Tick(1f);
            var before = rules.RivalDistances;
            var rankBefore = rules.Rank;
            runner.RefreshForTest(before[0], 7f, false, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Run));
            runner.RefreshForTest(SprintRules.RaceDistance * (RivalRunnerAI.RivalBurstFromProgress + .02f), 7f, false,
                SprintRules.RaceDistance, MinigamePhase.Play, null);

            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Burst));
            Assert.That(rules.RivalDistances, Is.EqualTo(before));
            Assert.That(rules.Rank, Is.EqualTo(rankBefore));
            Object.DestroyImmediate(runner.gameObject);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Rival_StandsAtItsOwnWorldPosition()
        {
            var runner = new GameObject("Rival").AddComponent<RivalRunnerAI>();
            runner.RefreshForTest(70f, 9f, false, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.transform.position.x, Is.EqualTo(SprintTrackLayout.WorldX(70f)).Within(.001f));
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Run));
            Object.DestroyImmediate(runner.gameObject);
        }

        [Test]
        public void Rival_WaitsForTheGunThenPullsUpAndStandsPastTheLine()
        {
            var runner = new GameObject("Rival").AddComponent<RivalRunnerAI>();
            runner.RefreshForTest(0f, 0f, false, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Idle), "still reacting to the gun");
            runner.RefreshForTest(SprintRules.RaceDistance + 1f, 6f, true, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Run), "pulling up past the line");
            runner.RefreshForTest(SprintRules.RaceDistance + 4f, 0f, true, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Idle));
            Object.DestroyImmediate(runner.gameObject);
        }

        [Test]
        public void Rival_NeverBurstsInLearn()
        {
            var runner = new GameObject("Rival").AddComponent<RivalRunnerAI>();
            runner.RefreshForTest(400f, 9f, false, float.PositiveInfinity, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Run));
            Object.DestroyImmediate(runner.gameObject);
        }
```

In `RunnerVisualTests`, replace `PlayerMapsItsOwnRaceDistanceAcrossTheTrack`:

```csharp
        [UnityTest]
        public IEnumerator PlayerStandsAtItsWorldPositionOnTheStraight()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var controller = Object.FindFirstObjectByType<SprintController>();
            controller.ConfigureForTest();
            controller.enabled = false;
            var player = GameObject.Find("Player");

            controller.AdvanceToDistance(SprintRules.RaceDistance / 2f);
            yield return null;

            Assert.That(player.transform.position.x,
                Is.EqualTo(SprintTrackLayout.WorldX(SprintRules.RaceDistance / 2f)).Within(.001f),
                "One race metre is one world unit from the start.");
            Assert.That(player.transform.position.y,
                Is.EqualTo(SprintTrackLayout.LaneCenterYForAuthoredLane(2)).Within(.001f),
                "Race progress must not move the player out of lane 2.");
        }
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Running.RunnerStrideTests" stride`
Expected: compile error, `RunnerStride` not found.

- [ ] **Step 3: Implement `RunnerStride.cs`**

```csharp
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
```

- [ ] **Step 4: Rewrite `RunnerVisualPresenter`** — remove the `trackStartX` field and the `RestBelowSpeed` / `RunAboveSpeed` constants, then:

```csharp
        // Hysteresis, in m/s, so the runner does not flicker between running and resting around one speed.
        const float RestBelowSpeed = .8f;
        const float RunAboveSpeed = 2f;
```

```csharp
        void Update()
        {
            if (controller == null || animator == null) return;
            RefreshPosition(controller.PlayerPosition);
            UpdateResting();
            bool falling = controller.Phase == MinigamePhase.Play && controller.IsFalling;
            string state = controller.Phase == MinigamePhase.Resolve
                ? (controller.LastResult != null && controller.LastResult.Pass ? "Celebrate" : "Fail")
                : falling ? "Stumble"
                : controller.Phase != MinigamePhase.Play || resting ? "Idle"
                : !controller.IsLearnChallenge && !controller.IsPlayerFinished &&
                  controller.Snapshot.Distance >= controller.TargetDistance * .7f
                    ? "Burst" : "Run";
            animator.speed = state == "Run" || state == "Burst"
                ? RunnerStride.Playback(controller.PlayerMetersPerSecond) : 1f;
            if (state == lastState) return;
            animator.Play(state, 0, 0f);
            lastState = state;
        }
```

In `UpdateResting` replace `float speed = controller.Snapshot.Speed;` with `float speed = controller.PlayerMetersPerSecond;`.

```csharp
        void RefreshPosition(float position)
        {
            if (raceRoot == null) return;
            Vector3 world = raceRoot.position;
            world.x = SprintTrackLayout.WorldX(position);
            raceRoot.position = world;
        }
```

- [ ] **Step 5: Rewrite `RivalRunnerAI`** — remove the `trackStartX` field and `VisualProgress01`, then replace `Update`, `RefreshForTest` and `Refresh`:

```csharp
        void Update()
        {
            if (controller == null || rivalIndex < 0 || rivalIndex >= controller.RivalCount)
                return;
            Refresh(controller.GetRivalPosition(rivalIndex), controller.GetRivalSpeed(rivalIndex),
                controller.HasRivalFinished(rivalIndex), controller.RaceGoal, controller.Phase,
                controller.LastResult, controller.IsRivalSurging(rivalIndex), controller.IsRivalSlowing(rivalIndex));
        }

        public void RefreshForTest(float position, float speed, bool finished, float goal, MinigamePhase phase,
            MinigameResult result, bool surging = false, bool slowing = false) =>
            Refresh(position, speed, finished, goal, phase, result, surging, slowing);

        void Refresh(float position, float speed, bool finished, float goal, MinigamePhase phase,
            MinigameResult result, bool surging, bool slowing)
        {
            // Place the visual in world space so the scaled lane root never stretches the distance.
            if (visual != null)
            {
                Vector3 world = visual.position;
                world.x = SprintTrackLayout.WorldX(position);
                visual.position = world;
            }

            bool standing = speed <= .05f;
            if (phase == MinigamePhase.Resolve)
                State = result != null && result.Pass ? RivalRunnerState.Celebrate : RivalRunnerState.Fail;
            else if (phase != MinigamePhase.Play || standing)
                State = RivalRunnerState.Idle;
            else if (!finished && (surging || position >= goal * RivalBurstFromProgress))
                State = RivalRunnerState.Burst;
            else
                State = RivalRunnerState.Run;

            // Waiting on the grid, flagging mid-race or stood past the line: catch a breath.
            if (breathing != null)
                breathing.SetResting(phase == MinigamePhase.Tutorial || phase == MinigamePhase.Countdown ||
                    (phase == MinigamePhase.Play && (slowing || (finished && standing))));

            if (animator != null)
            {
                animator.speed = State == RivalRunnerState.Run || State == RivalRunnerState.Burst
                    ? RunnerStride.Playback(speed) : 1f;
                if (!hasPlayedState || lastPlayedState != State)
                {
                    animator.Play(StateHash(State), 0, 0f);
                    lastPlayedState = State;
                    hasPlayedState = true;
                }
            }
        }
```

Remove `trackStartX: …` lines from `Assets/_Project/Prefabs/Gameplay/RivalRunner.prefab` and `Assets/_Project/Prefabs/Gameplay/PlayerRunnerVisual.prefab` (stale serialized fields).

- [ ] **Step 6: Run to verify pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Running" running-edit` then `tools/run-unity-tests.sh PlayMode "KMA.Tests.Gameplay.Running.SprintControllerTests|KMA.Tests.Presentation.RunnerVisualTests" runner-visuals`
Expected: both `result=Passed`. `PlayerMarkerStaysOnTheTrackAtBothEndsOfTheRace` may fail until Task 7 adds camera follow (the player at 150 m is off the static view); if so, leave it failing and note it in the commit message — Task 7 rewrites it.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint/RunnerStride.cs* Assets/_Project/Scripts/Gameplay/Sprint/RunnerVisualPresenter.cs Assets/_Project/Scripts/Gameplay/Sprint/RivalRunnerAI.cs Assets/_Project/Prefabs/Gameplay/RivalRunner.prefab Assets/_Project/Prefabs/Gameplay/PlayerRunnerVisual.prefab Assets/Tests/EditMode/Gameplay/Running/RunnerStrideTests.cs* Assets/Tests/PlayMode/Gameplay/Running/SprintControllerTests.cs Assets/Tests/PlayMode/Presentation/RunnerVisualTests.cs
git commit -m "feat(sprint): runners stand at their world position with strides matched to speed"
```

---

### Task 7: Camera follows only the player

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Sprint/SprintCameraFollow.cs`
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintFestivalPresentation.cs` (`Build`, `SprintPlayerMarkerPlacement.Refresh`)
- Test: `Assets/Tests/EditMode/Presentation/SprintCameraFollowTests.cs` (create), `Assets/Tests/PlayMode/Presentation/RunnerVisualTests.cs`

**Interfaces:**
- Consumes: player root transform `GameObject.Find("Player")`.
- Produces: `SprintCameraFollow` with `Bind(Transform player)`, `float StartX`, `float Travel`, `Transform Target`, `static float TargetX(float startX, float playerX)`, `static float Step(float currentX, ref float velocity, float targetX, float dt)`, consts `SmoothTime = .12f`, `SnapDistance = 8f`; `[DefaultExecutionOrder(100)]`.

- [ ] **Step 1: Write the failing tests** — create `SprintCameraFollowTests.cs`:

```csharp
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Presentation
{
    public sealed class SprintCameraFollowTests
    {
        [Test]
        public void HoldsStillUntilThePlayerReachesTheMiddle()
        {
            Assert.That(SprintCameraFollow.TargetX(0f, -9.6f), Is.EqualTo(0f));
            Assert.That(SprintCameraFollow.TargetX(0f, -.1f), Is.EqualTo(0f));
            Assert.That(SprintCameraFollow.TargetX(0f, 12f), Is.EqualTo(12f));
        }

        [Test]
        public void Step_ClosesOnThePlayerWithoutOvershooting()
        {
            float x = 0f, velocity = 0f, previous = 0f;
            for (int i = 0; i < 120; i++)
            {
                x = SprintCameraFollow.Step(x, ref velocity, 5f, 1f / 60f);
                Assert.That(x, Is.GreaterThanOrEqualTo(previous));
                Assert.That(x, Is.LessThanOrEqualTo(5f + 1e-4f));
                previous = x;
            }
            Assert.That(x, Is.EqualTo(5f).Within(.01f));
        }

        [Test]
        public void Step_CutsStraightToATeleportedPlayer()
        {
            float velocity = 3f;
            Assert.That(SprintCameraFollow.Step(0f, ref velocity, 60f, 1f / 60f), Is.EqualTo(60f));
            Assert.That(velocity, Is.Zero);
        }

        [Test]
        public void Step_HoldsWhenPaused()
        {
            float velocity = 2f;
            Assert.That(SprintCameraFollow.Step(1f, ref velocity, 3f, 0f), Is.EqualTo(1f));
        }
    }
}
```

In `RunnerVisualTests`, replace `PlayerMarkerStaysOnTheTrackAtBothEndsOfTheRace` and add the camera test:

```csharp
        [UnityTest]
        public IEnumerator PlayerMarkerStaysInViewAtBothEndsOfTheRace()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var controller = Object.FindFirstObjectByType<SprintController>();
            controller.ConfigureForTest();
            controller.enabled = false;
            yield return null;

            var marker = GameObject.Find("Player").GetComponentInChildren<TMPro.TextMeshPro>(true).transform.parent;
            Assert.That(marker.name, Is.EqualTo("PlayerMarker"));
            const float halfView = 9.6f;

            foreach (float distance in new[] { 0f, SprintRules.RaceDistance })
            {
                controller.AdvanceToDistance(distance);
                yield return null;
                yield return null;
                float fromCentre = marker.position.x - Camera.main.transform.position.x;
                Assert.That(fromCentre, Is.InRange(-halfView, halfView), $"marker off screen at {distance} m");
            }
        }

        [UnityTest]
        public IEnumerator CameraHoldsAtTheStartThenFollowsOnlyThePlayer()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var controller = Object.FindFirstObjectByType<SprintController>();
            controller.ConfigureForTest();
            controller.enabled = false;
            var camera = Camera.main;
            Assert.That(camera.GetComponent<SprintCameraFollow>(), Is.Not.Null);
            float startX = camera.GetComponent<SprintCameraFollow>().StartX;

            controller.AdvanceToDistance(5f, new[] { new RivalPaceProfile("Far", 9f, 9f) });
            yield return null;
            yield return null;
            Assert.That(camera.transform.position.x, Is.EqualTo(startX).Within(.001f),
                "the player is still left of centre, so the camera waits");

            controller.AdvanceToDistance(60f, new[] { new RivalPaceProfile("Far", 9f, 9f) });
            yield return null;
            yield return null;
            Assert.That(camera.transform.position.x, Is.EqualTo(SprintTrackLayout.WorldX(60f)).Within(.01f),
                "the camera keeps the player centred, whatever the rivals do");
        }
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation.SprintCameraFollowTests" camera-follow`
Expected: compile error, `SprintCameraFollow` not found.

- [ ] **Step 3: Implement `SprintCameraFollow.cs`**

```csharp
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
```

- [ ] **Step 4: Wire it up** — in `SprintFestivalPresentation.Build`, after `EnsurePlayerIdentity(root);` add `EnsureCameraFollow();` and the method:

```csharp
        /// The race camera rides with the player only; rivals come and go from the frame.
        static void EnsureCameraFollow()
        {
            Camera camera = Camera.main;
            Transform player = GameObject.Find("Player")?.transform;
            if (camera == null || player == null)
                return;
            var follow = camera.GetComponent<SprintCameraFollow>() ?? camera.gameObject.AddComponent<SprintCameraFollow>();
            follow.Bind(player);
        }
```

In `SprintPlayerMarkerPlacement`, the camera now keeps the player centred, so judge the side from the view centre. Add the constant and replace the offset line in `Refresh`:

```csharp
        // The marker swaps to the runner's left only once they sit well right of the view centre.
        public const float FlipBeyond = 2f;
```

```csharp
            float runnerX = marker.parent == null ? 0f : marker.parent.position.x;
            float viewX = Camera.main == null ? 0f : Camera.main.transform.position.x;
            float offset = runnerX - viewX > FlipBeyond ? -SideOffset : SideOffset;
```

Update the class doc comment's last paragraph to: "The camera follows the player, so the marker normally rides ahead of them; it only swaps sides if the runner is well right of the view centre."

- [ ] **Step 5: Run to verify pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation.SprintCameraFollowTests" camera-follow` then `tools/run-unity-tests.sh PlayMode "KMA.Tests.Presentation.RunnerVisualTests" runner-visuals`
Expected: both `result=Passed`.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint/SprintCameraFollow.cs* Assets/_Project/Scripts/Gameplay/Sprint/SprintFestivalPresentation.cs Assets/Tests/EditMode/Presentation/SprintCameraFollowTests.cs* Assets/Tests/PlayMode/Presentation/RunnerVisualTests.cs
git commit -m "feat(sprint): camera holds at the start then follows only the player"
```

---

### Task 8: Backdrop scrolls with the camera; the track is nailed to the world

**Files:**
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintParallax.cs`
- Modify: `Assets/_Project/Scenes/MG_Sprint.unity:589-592` (track layer factor)
- Test: `Assets/Tests/PlayMode/Presentation/RunnerVisualTests.cs`

**Interfaces:**
- Consumes: `SprintCameraFollow.Travel` (Task 7).
- Produces: `SprintParallax.RefreshForTest(float cameraTravel)` — moves the root by `cameraTravel` and slides each layer back by `cameraTravel × factor`. `[DefaultExecutionOrder(110)]`. The `controller` field is removed.

- [ ] **Step 1: Write the failing test** — in `RunnerVisualTests`, replace `TrackLayerStaysFixedAsRunnersAdvance`:

```csharp
        [UnityTest]
        public IEnumerator TrackLayerIsNailedToTheWorldWhileTheSkyDrifts()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var parallax = Object.FindFirstObjectByType<SprintParallax>();
            Assert.That(parallax.TryGetLayerTilePositions(2, out var trackBefore, out _), Is.True);
            Assert.That(parallax.TryGetLayerTilePositions(0, out var skyBefore, out _), Is.True);
            float rootBefore = parallax.transform.position.x;

            parallax.RefreshForTest(25f);

            Assert.That(parallax.transform.position.x, Is.EqualTo(rootBefore + 25f).Within(.001f));
            Assert.That(parallax.TryGetLayerTilePositions(2, out var trackAfter, out _), Is.True);
            Assert.That(rootBefore + 25f + trackAfter.x, Is.EqualTo(rootBefore + trackBefore.x).Within(.001f),
                "the painted track must not move in the world, so runners and lines stay on it");
            Assert.That(parallax.TryGetLayerTilePositions(0, out var skyAfter, out _), Is.True);
            Assert.That(skyAfter.x - skyBefore.x, Is.EqualTo(-25f * .15f).Within(.001f),
                "the sky drifts past at its own slower rate");
        }
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Presentation.RunnerVisualTests" runner-visuals`
Expected: FAIL in `TrackLayerIsNailedToTheWorldWhileTheSkyDrifts` (root does not move).

- [ ] **Step 3: Implement** — in `SprintParallax.cs`:
  - Add `[DefaultExecutionOrder(110)]` above the class.
  - On `scrollMultiplier` add `[Tooltip("World-follow factor: 1 = fixed to the track, 0 = fixed to the camera.")]`.
  - Remove `[SerializeField] SprintController controller;` and replace `Awake`, `Update`, `RefreshForTest`:

```csharp
        SprintCameraFollow cameraFollow;
        Vector3 rootOrigin;

        void Awake()
        {
            rootOrigin = transform.position;
            if (layers == null) return;
            for (var i = 0; i < layers.Length; i++) layers[i]?.CacheOrigins();
        }

        // After the camera has moved this frame (it runs at execution order 100).
        void LateUpdate()
        {
            if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<SprintCameraFollow>();
            if (cameraFollow != null) RefreshForTest(cameraFollow.Travel);
        }

        /// The root rides with the camera; each layer then slides back by its world-follow factor, so a
        /// factor of 1 (the track) stays nailed to the world and lower factors drift past more slowly.
        public void RefreshForTest(float cameraTravel)
        {
            transform.position = new Vector3(rootOrigin.x + cameraTravel, rootOrigin.y, rootOrigin.z);
            if (layers == null) return;
            for (var i = 0; i < layers.Length; i++) layers[i]?.Scroll(cameraTravel);
        }
```

  - In `MG_Sprint.unity`, in the `SprintParallax` component (`&710010`), change the third layer's `scrollMultiplier: 0` to `scrollMultiplier: 1` and delete the `controller: {fileID: 200002}` line.

- [ ] **Step 4: Run to verify pass**

Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Presentation.RunnerVisualTests|KMA.Tests.Gameplay.Running.SprintControllerTests" parallax`
Expected: `result=Passed` (the existing `SprintParallax_*` tile-recycling tests still check local tile positions and must pass unchanged).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint/SprintParallax.cs Assets/_Project/Scenes/MG_Sprint.unity Assets/Tests/PlayMode/Presentation/RunnerVisualTests.cs
git commit -m "feat(sprint): backdrop scrolls with the camera and the track stays fixed in the world"
```

---

### Task 9: Static finish and start lines; remove the slide-in reveal

**Files:**
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintFinishLinePresenter.cs` (rewrite)
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintFestivalPresentation.cs` (`EnsureFinishLine` → `EnsureTrackLines`, `ConfigureChallenge`)
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintTrackLayout.cs` (remove `FinishX`, `TrackLength`, `FinishLineX`)
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintUiLayout.cs` (remove `FinishRevealDistance`, `FinishDistance`, `FinishVisible`, `FinishReveal01`)
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintController.cs` (remove `LeadDistance`)
- Test: `Assets/Tests/PlayMode/Presentation/SprintPresentationGateTests.cs`, `Assets/Tests/EditMode/Presentation/SprintUiLayoutTests.cs`, `Assets/Tests/PlayMode/Gameplay/Running/SprintControllerTests.cs`

**Interfaces:**
- Consumes: `SprintTrackLayout.FinishWorldX`, `SprintController.TargetDistance`, `SprintController.IsLearnChallenge`.
- Produces: `SprintFinishLinePresenter.Configure(SprintController, GameObject finishLineRoot, GameObject startLineRoot)`, `FinishRoot`, `StartRoot`, `IsVisible` (finish line shown; false in Learn), `RefreshForTest()`.

- [ ] **Step 1: Write the failing test** — in `SprintPresentationGateTests`, replace `SprintFinishLineRevealsExactlyAtSeventyMetersWithoutBlockingInput`:

```csharp
        [UnityTest]
        public IEnumerator SprintFinishLineStandsAtItsWorldPositionWithoutBlockingInput()
        {
            yield return LoadSprint();

            var scene = SceneManager.GetActiveScene();
            var controller = SceneObjects<SprintController>(scene)[0];
            var finish = SceneObjects<SprintFinishLinePresenter>(scene)[0];
            Transform finishLine = finish.FinishRoot != null ? finish.FinishRoot.transform : null;
            Assert.That(finishLine, Is.Not.Null);
            Assert.That(finish.StartRoot, Is.Not.Null);

            // World-space sprite on the track: never a UI graphic, so it cannot block taps.
            Assert.That(finishLine.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(finishLine.GetComponentsInChildren<Graphic>(true), Is.Empty);
            Assert.That(finishLine.position.y, Is.EqualTo(SprintTrackLayout.FinishBottomY).Within(.0001f));

            controller.ConfigureForTest();
            foreach (float distance in new[] { 0f, 75f, SprintRules.RaceDistance })
            {
                controller.AdvanceToDistance(distance);
                finish.RefreshForTest();
                Assert.That(finish.IsVisible, Is.True);
                Assert.That(finishLine.gameObject.activeSelf, Is.True);
                Assert.That(finishLine.position.x,
                    Is.EqualTo(SprintTrackLayout.FinishWorldX(controller.TargetDistance)).Within(.0001f),
                    $"the line never moves (player at {distance} m)");
            }
            Assert.That(finish.StartRoot.transform.position.x,
                Is.EqualTo(SprintTrackLayout.TrackStartX).Within(.0001f));
        }
```

In `SprintUiLayoutTests` delete `FinishVisible_UsesApprovedThreshold`, `FinishReveal01_RunsFromTheRevealDistanceToTheLine`, `FinishLineStartsAtTheEntryPointSoItNeverPopsIn`, `FinishLineSlidesInMonotonically` and `FinishLineComesToRestWhereTheRunnerFinishes`. Add to `SprintTrackLayoutTests`:

```csharp
        [Test]
        public void FinishLineSpansThePaintedLanes() =>
            Assert.That(SprintTrackLayout.FinishTopY, Is.GreaterThan(SprintTrackLayout.FinishBottomY));
```

In `SprintControllerTests` delete `LeadDistance_FollowsARivalWhoRunsAheadOfThePlayer`.

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Presentation.SprintPresentationGateTests" presentation-gate`
Expected: compile error, `StartRoot` not found.

- [ ] **Step 3: Rewrite `SprintFinishLinePresenter.cs`**

```csharp
using UnityEngine;

namespace KMA.Gameplay
{
    /// Holds the start and finish lines on the track at their world positions. They never move; the
    /// camera brings them into view as the player runs. Learn has no finish line.
    public sealed class SprintFinishLinePresenter : MonoBehaviour
    {
        SprintController controller;
        GameObject finishRoot;
        GameObject startRoot;

        public bool IsVisible { get; private set; }
        public GameObject FinishRoot => finishRoot;
        public GameObject StartRoot => startRoot;

        public void Configure(SprintController sprintController, GameObject finishLineRoot, GameObject startLineRoot)
        {
            controller = sprintController;
            finishRoot = finishLineRoot;
            startRoot = startLineRoot;
            RefreshForTest();
        }

        public void RefreshForTest()
        {
            IsVisible = controller != null && !controller.IsLearnChallenge;
            if (finishRoot != null)
            {
                finishRoot.SetActive(IsVisible);
                Place(finishRoot, SprintTrackLayout.FinishWorldX(controller == null
                    ? SprintRules.RaceDistance : controller.TargetDistance));
            }
            if (startRoot != null) Place(startRoot, SprintTrackLayout.TrackStartX);
        }

        static void Place(GameObject line, float x)
        {
            Vector3 position = line.transform.position;
            position.x = x;
            line.transform.position = position;
        }

        void Update() => RefreshForTest();
    }
}
```

- [ ] **Step 4: Build both lines** — in `SprintFestivalPresentation`, replace `EnsureFinishLine` (and its call in `Build`) with:

```csharp
        /// The start and finish lines are painted on the track in world space beside the runners;
        /// the camera finds them as the race moves along the straight.
        static void EnsureTrackLines(RectTransform root)
        {
            GameObject finish = EnsureLine("SprintFinishLine", 10, SprintTrackLayout.FinishWidth, checker: true);
            GameObject start = EnsureLine("SprintStartLine", 1, .15f, checker: false);
            var presenter = root.GetComponent<SprintFinishLinePresenter>()
                ?? root.gameObject.AddComponent<SprintFinishLinePresenter>();
            presenter.Configure(Object.FindFirstObjectByType<SprintController>(), finish, start);
        }

        static GameObject EnsureLine(string name, int squareCount, float width, bool checker)
        {
            const int sortingOrder = 5;   // over the track backdrop, under the runners (11+)
            GameObject line = GameObject.Find(name);
            if (line == null)
            {
                line = new GameObject(name);
                var texture = new Texture2D(1, squareCount) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int i = 0; i < squareCount; i++)
                    texture.SetPixel(0, i, checker && i % 2 == 0 ? Color.black : Color.white);
                texture.Apply();

                // One pixel per unit: the sprite is 1 x squareCount units, then scaled onto the lanes.
                var renderer = line.AddComponent<SpriteRenderer>();
                renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, squareCount), new Vector2(.5f, 0f), 1f);
                renderer.sortingOrder = sortingOrder;
            }

            float height = SprintTrackLayout.FinishTopY - SprintTrackLayout.FinishBottomY;
            line.transform.position = new Vector3(line.transform.position.x, SprintTrackLayout.FinishBottomY, 0f);
            line.transform.localScale = new Vector3(width, height / squareCount, 1f);
            return line;
        }
```

In `ConfigureChallenge`, replace the `finishLine` block with:

```csharp
            chrome.GetComponent<SprintFinishLinePresenter>()?.RefreshForTest();
```

- [ ] **Step 5: Remove the reveal code** — delete from `SprintTrackLayout`: `FinishX`, `TrackLength`, `FinishLineX` (keep `FinishWidth`, and update the `TrackStartX` comment to "Runners line up here; see WorldX."). Delete from `SprintUiLayout`: `FinishRevealDistance`, `FinishDistance`, `FinishVisible`, `FinishReveal01`. Delete `LeadDistance` from `SprintController`. Then confirm nothing still references them:

Run: `rg -n "FinishReveal|FinishVisible|FinishRevealDistance|FinishLineX|TrackLength|SprintTrackLayout\.FinishX|LeadDistance|VisualProgress01" Assets`
Expected: no output.

- [ ] **Step 6: Run to verify pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation" presentation-edit` then `tools/run-unity-tests.sh PlayMode "KMA.Tests.Presentation|KMA.Tests.Gameplay.Running" sprint-playmode`
Expected: both `result=Passed`.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint Assets/Tests
git commit -m "feat(sprint): fixed start and finish lines in the world, drop the slide-in reveal"
```

---

### Task 10: Edge chips for rivals out of view

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Sprint/SprintOffscreenRivalChips.cs`
- Modify: `Assets/_Project/Scripts/Gameplay/Sprint/SprintFestivalPresentation.cs` (`Build`)
- Test: `Assets/Tests/EditMode/Presentation/SprintOffscreenRivalChipsTests.cs` (create), `Assets/Tests/PlayMode/Presentation/SprintPresentationGateTests.cs`

**Interfaces:**
- Consumes: `SprintController.GetRivalPosition`, `PlayerPosition`, `Phase`, `RivalCount`; `RivalRunnerAI.Lane`, `RivalIndex`; `UiKit.Chip`.
- Produces: `SprintOffscreenRivalChips` with `enum Edge { None, Left, Right }`, `static Edge EdgeFor(float rivalX, float cameraX, float halfWidth)`, `static string GapText(float rivalPosition, float playerPosition)`, `const float OffscreenMargin = .6f`, `Configure(SprintController, RectTransform chromeRoot, RivalRunnerAI[] runners)`, `int ChipCount`, `bool IsChipVisible(int)`, `string ChipText(int)`, `RefreshForTest()`; `[DefaultExecutionOrder(120)]`.

- [ ] **Step 1: Write the failing tests** — create `SprintOffscreenRivalChipsTests.cs`:

```csharp
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Presentation
{
    public sealed class SprintOffscreenRivalChipsTests
    {
        [TestCase(5f, SprintOffscreenRivalChips.Edge.None)]
        [TestCase(10.1f, SprintOffscreenRivalChips.Edge.None)]
        [TestCase(10.7f, SprintOffscreenRivalChips.Edge.Right)]
        [TestCase(-10.7f, SprintOffscreenRivalChips.Edge.Left)]
        public void ChipAppearsOnlyOnceTheRivalIsFullyOutOfView(float rivalX, SprintOffscreenRivalChips.Edge expected) =>
            Assert.That(SprintOffscreenRivalChips.EdgeFor(rivalX, 0f, 10f), Is.EqualTo(expected));

        [Test]
        public void GapIsSignedWithOneDecimal()
        {
            Assert.That(SprintOffscreenRivalChips.GapText(54.21f, 50f), Is.EqualTo("+4.2 m"));
            Assert.That(SprintOffscreenRivalChips.GapText(46.9f, 50f), Is.EqualTo("−3.1 m"));
        }
    }
}
```

Add to `SprintPresentationGateTests`:

```csharp
        [UnityTest]
        public IEnumerator RivalFarAheadShowsAnEdgeChipWithTheGap()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();
            var controller = SceneObjects<SprintController>(scene)[0];
            var chips = SceneObjects<SprintOffscreenRivalChips>(scene)[0];
            controller.ConfigureForTest();
            controller.enabled = false;
            controller.AdvanceToDistance(0f, new[]
            {
                new RivalPaceProfile("A", 100f, 100f), new RivalPaceProfile("B", 19.2f, 19.2f),
                new RivalPaceProfile("C", 19.2f, 19.2f)
            });
            controller.Simulate(.5f);
            chips.RefreshForTest();

            Assert.That(chips.ChipCount, Is.EqualTo(3));
            int lane1 = System.Array.FindIndex(SceneObjects<RivalRunnerAI>(scene)
                .OrderBy(rival => rival.Lane).ToArray(), rival => rival.RivalIndex == 0);
            Assert.That(chips.IsChipVisible(lane1), Is.True);
            Assert.That(chips.ChipText(lane1), Does.Contain("+50.0 m"));
            Assert.That(Enumerable.Range(0, 3).Count(chips.IsChipVisible), Is.EqualTo(1),
                "B and C stand at the view centre, so they get no chip");
        }
```

(Add `using System.Linq;` to the file if absent.)

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation.SprintOffscreenRivalChipsTests" chips`
Expected: compile error, `SprintOffscreenRivalChips` not found.

- [ ] **Step 3: Implement `SprintOffscreenRivalChips.cs`**

```csharp
using System.Globalization;
using KMA.UI.Kit;
using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.Gameplay
{
    /// The camera follows only the player, so rivals run out of frame. For each one out of view, a
    /// chip at that screen edge, level with their lane, says how far ahead or behind they are.
    [DefaultExecutionOrder(120)]
    public sealed class SprintOffscreenRivalChips : MonoBehaviour
    {
        public enum Edge { None, Left, Right }

        /// A runner sprite is about a unit wide: only call a rival gone once they are fully past the edge.
        public const float OffscreenMargin = .6f;
        const float ChipWidth = 220f, ChipHeight = 56f, EdgeInset = 16f;
        // Chips sit just above the lane's feet line, level with the runner's body.
        const float LaneLift = .6f;

        SprintController controller;
        RectTransform root;
        RivalRunnerAI[] rivals = System.Array.Empty<RivalRunnerAI>();
        ChipHandle[] chips = System.Array.Empty<ChipHandle>();

        public int ChipCount => chips.Length;
        public bool IsChipVisible(int index) => chips[index].Background.gameObject.activeSelf;
        public string ChipText(int index) => chips[index].Label.text;

        public static Edge EdgeFor(float rivalX, float cameraX, float halfWidth)
        {
            if (rivalX > cameraX + halfWidth + OffscreenMargin) return Edge.Right;
            if (rivalX < cameraX - halfWidth - OffscreenMargin) return Edge.Left;
            return Edge.None;
        }

        public static string GapText(float rivalPosition, float playerPosition)
        {
            float gap = rivalPosition - playerPosition;
            return (gap >= 0f ? "+" : "−") + Mathf.Abs(gap).ToString("0.0", CultureInfo.InvariantCulture) + " m";
        }

        public void Configure(SprintController owner, RectTransform chromeRoot, RivalRunnerAI[] runners)
        {
            controller = owner;
            root = chromeRoot;
            rivals = runners ?? System.Array.Empty<RivalRunnerAI>();
            chips = new ChipHandle[rivals.Length];
            for (int i = 0; i < rivals.Length; i++)
            {
                chips[i] = UiKit.Chip(chromeRoot, "RivalChip" + i, string.Empty);
                RectTransform rect = chips[i].Background.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(ChipWidth, ChipHeight);
                // Information only: a chip must never swallow a tap meant for the controls.
                chips[i].Background.raycastTarget = false;
                chips[i].Label.raycastTarget = false;
                chips[i].Background.gameObject.SetActive(false);
            }
        }

        void LateUpdate() => RefreshForTest();

        public void RefreshForTest()
        {
            Camera camera = Camera.main;
            bool racing = controller != null && controller.Phase == MinigamePhase.Play && camera != null;
            for (int i = 0; i < chips.Length; i++)
            {
                GameObject chip = chips[i].Background.gameObject;
                RivalRunnerAI rival = rivals[i];
                if (!racing || rival == null || rival.RivalIndex >= controller.RivalCount)
                {
                    chip.SetActive(false);
                    continue;
                }

                float position = controller.GetRivalPosition(rival.RivalIndex);
                Edge edge = EdgeFor(SprintTrackLayout.WorldX(position), camera.transform.position.x,
                    camera.orthographicSize * camera.aspect);
                chip.SetActive(edge != Edge.None);
                if (edge == Edge.None) continue;

                chips[i].Label.text = VietText.Fix($"LÀN {rival.Lane} {GapText(position, controller.PlayerPosition)}");
                Place(chips[i].Background.rectTransform, edge, camera,
                    SprintTrackLayout.LaneCenterYForAuthoredLane(rival.Lane) + LaneLift);
            }
        }

        void Place(RectTransform chip, Edge edge, Camera camera, float worldY)
        {
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(camera.transform.position.x, worldY, 0f));
            Canvas canvas = root.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 local);
            Rect area = root.rect;
            float x = edge == Edge.Right ? area.xMax - ChipWidth * .5f - EdgeInset : area.xMin + ChipWidth * .5f + EdgeInset;
            // root.rect and local are measured from the root's pivot; centre anchors measure from its centre.
            chip.anchoredPosition = new Vector2(x - area.center.x, local.y - area.center.y);
        }
    }
}
```

Wire it in `SprintFestivalPresentation.Build` after `EnsureTrackLines(root);`:

```csharp
            EnsureRivalChips(root);
```

```csharp
        static void EnsureRivalChips(RectTransform root)
        {
            RivalRunnerAI[] runners = Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None);
            System.Array.Sort(runners, (a, b) => a.Lane.CompareTo(b.Lane));
            var chips = root.GetComponent<SprintOffscreenRivalChips>()
                ?? root.gameObject.AddComponent<SprintOffscreenRivalChips>();
            chips.Configure(Object.FindFirstObjectByType<SprintController>(), root, runners);
        }
```

- [ ] **Step 4: Run to verify pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation.SprintOffscreenRivalChipsTests" chips` then `tools/run-unity-tests.sh PlayMode "KMA.Tests.Presentation.SprintPresentationGateTests" presentation-gate`
Expected: both `result=Passed`, including the existing overlap/tap-area gate tests (the chips do not raycast).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Sprint/SprintOffscreenRivalChips.cs* Assets/_Project/Scripts/Gameplay/Sprint/SprintFestivalPresentation.cs Assets/Tests/EditMode/Presentation/SprintOffscreenRivalChipsTests.cs* Assets/Tests/PlayMode/Presentation/SprintPresentationGateTests.cs
git commit -m "feat(sprint): edge chips show how far off-screen rivals are ahead or behind"
```

---

### Task 11: Full verification and visual check

**Files:**
- No production changes unless a check fails.

- [ ] **Step 1: Run every EditMode test**

Run: `tools/run-unity-tests.sh EditMode "" all-edit`
Expected: `result=Passed`, `failed=0`.

- [ ] **Step 2: Run every PlayMode test**

Run: `tools/run-unity-tests.sh PlayMode "" all-play`
Expected: `result=Passed`, `failed=0`. Any failure in a non-Sprint suite that touches `SprintRules`/`SprintController` (for example `JourneyCatalogTests`, `StudentJourneyContentBuilderTests`) must be fixed at the cause, not by loosening a Sprint assertion.

- [ ] **Step 3: Visual check** — invoke the `testing-unity-ui-with-screenshots` skill on `MG_Sprint` and capture four moments: (a) the countdown with all four runners on the start line and the white start line visible; (b) about 40 m in, the player centred, the track and the lane lines visibly scrolled, a rival chip at an edge if a rival is out of view; (c) the player at the finish, the checkerboard line under their feet; (d) about 1 s after crossing, the player stopped 3–5 m past the line. Confirm by eye: no sky gap under the track at any moment, the track tile seam is hidden, and chips do not overlap the scoreboard or the TRÁI/PHẢI buttons.

- [ ] **Step 4: Commit any fixes from Steps 1–3**

```bash
git add -A Assets
git commit -m "fix(sprint): follow-up fixes from full test run and visual check"
```

(Skip this step if nothing changed.)
