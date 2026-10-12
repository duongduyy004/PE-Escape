using System;
using UnityEngine;

namespace KMA.Gameplay
{
    public enum KickerPose { Ready, RunUp, Strike, Celebrate }

    public enum KeeperPose { Ready, ReachLeft, ReachRight, DiveLeft, DiveRight, Cheer }

    /// <summary>Which pose the kicker and the keeper show at each moment of a penalty.</summary>
    public static class FootballPoses
    {
        public static KickerPose Kicker(FootballState state, float stateElapsed, FootballOutcome? outcome)
        {
            if (outcome.HasValue)
                return outcome.Value == FootballOutcome.Goal ? KickerPose.Celebrate : KickerPose.Ready;
            if (state == FootballState.Kicking)
                return stateElapsed < FootballRules.KickAnimationSeconds * .5f ? KickerPose.RunUp : KickerPose.Strike;
            return state == FootballState.Flying ? KickerPose.Strike : KickerPose.Ready;
        }

        public static KeeperPose KeeperLanding(float keeperX, float diveThresholdMetres, float centreDeadZoneMetres)
        {
            float distance = Mathf.Abs(keeperX);
            if (distance <= Mathf.Max(0f, centreDeadZoneMetres)) return KeeperPose.Ready;
            bool left = keeperX < 0f;
            bool diving = distance >= Mathf.Max(0f, diveThresholdMetres);
            if (diving) return left ? KeeperPose.DiveLeft : KeeperPose.DiveRight;
            return left ? KeeperPose.ReachLeft : KeeperPose.ReachRight;
        }

        /// <summary>Post-contact pose timeline. The flight simulation still owns the result and direction.</summary>
        public static KeeperPose Keeper(FootballOutcome? outcome, float keeperX, float outcomeElapsed,
            float diveThresholdMetres, float centreDeadZoneMetres, float landingHoldSeconds, float cheerSeconds)
        {
            if (!outcome.HasValue) return KeeperPose.Ready;
            if (outcomeElapsed < Mathf.Max(0f, landingHoldSeconds))
                return KeeperLanding(keeperX, diveThresholdMetres, centreDeadZoneMetres);
            if (outcome == FootballOutcome.Saved && outcomeElapsed < landingHoldSeconds + cheerSeconds)
                return KeeperPose.Cheer;
            return KeeperPose.Ready;
        }
    }

    /// <summary>The sprite for every pose. All poses share one size, so swapping keeps the renderer's scale.</summary>
    [Serializable]
    public sealed class FootballPoseSprites
    {
        public Sprite kickerReady, kickerRunUp, kickerStrike, kickerCelebrate;
        public Sprite keeperReady, keeperReachLeft, keeperReachRight, keeperDiveLeft, keeperDiveRight, keeperCheer;

        public Sprite For(KickerPose pose) => pose switch
        {
            KickerPose.RunUp => kickerRunUp,
            KickerPose.Strike => kickerStrike,
            KickerPose.Celebrate => kickerCelebrate,
            _ => kickerReady
        };

        public Sprite For(KeeperPose pose) => pose switch
        {
            KeeperPose.ReachLeft => keeperReachLeft,
            KeeperPose.ReachRight => keeperReachRight,
            KeeperPose.DiveLeft => keeperDiveLeft,
            KeeperPose.DiveRight => keeperDiveRight,
            KeeperPose.Cheer => keeperCheer,
            _ => keeperReady
        };
    }
}
