using System;
using UnityEngine;
using KMA.UI.Kit;

namespace KMA.Gameplay
{
    public sealed class FootballPresentation : MonoBehaviour
    {
        public const float PixelToWorld = .016f;
        public const float KeeperDisplayWidth = 108f, KeeperDisplayHeight = 144f;
        /// <summary>Screen pixels from the keeper's hip (the dive's rotation centre) down to the sprite's feet pivot.</summary>
        public const float KeeperFeetDrop = 21f;
        [SerializeField] SpriteRenderer field, goal, ball, ballShadow, player, goalkeeper, crosshair;
        [SerializeField] SpriteRenderer goalNet;
        [SerializeField] FootballPoseSprites poses;
        [SerializeField, Min(0f)] float keeperDiveThresholdMetres = 1f;
        [SerializeField, Min(0f)] float keeperCentreDeadZoneMetres = .08f;
        [SerializeField, Min(0f)] float keeperLandingHoldSeconds = .3f;
        [SerializeField, Min(0f)] float keeperCheerSeconds = .45f;
        [SerializeField] Transform leftGoalPoint, rightGoalPoint;
        [SerializeField] SpriteRenderer[] trajectoryDots = Array.Empty<SpriteRenderer>();
        readonly Vector3[] previewPoints = new Vector3[140];
        LineRenderer aimArrow;
        Vector3 ballScale, shadowScale, playerRest, netRest;
        bool configured;

        public void Configure(SpriteRenderer fieldView, SpriteRenderer goalView, SpriteRenderer ballView,
            SpriteRenderer shadowView, SpriteRenderer playerView, SpriteRenderer keeperView,
            SpriteRenderer crosshairView, Transform leftPoint, Transform rightPoint, SpriteRenderer[] dots = null, SpriteRenderer net = null)
        {
            field = fieldView; goal = goalView; ball = ballView; ballShadow = shadowView;
            player = playerView; goalkeeper = keeperView; crosshair = crosshairView;
            leftGoalPoint = leftPoint; rightGoalPoint = rightPoint;
            goalNet = net;
            trajectoryDots = dots ?? Array.Empty<SpriteRenderer>();
            CaptureRestPositions();
            HidePreview();
        }
        public bool ValidateReferences() => field && goal && ball && ballShadow && player && goalkeeper && crosshair &&
            leftGoalPoint && rightGoalPoint && Vector3.Distance(leftGoalPoint.position, rightGoalPoint.position) > .01f;
        public FootballPoseSprites Poses => poses;
        public void ConfigurePoses(FootballPoseSprites sprites) => poses = sprites;

        /// <summary>Where the keeper's feet pivot goes so that the dive still rotates about his hip.</summary>
        public static Vector3 KeeperWorldPosition(float keeperX, float angleDegrees)
        {
            Vector3 hip = ScreenToWorld(600f + keeperX * 300f / FootballShotSolver.GoalHalfWidth, FootballFlightSimulation.KeeperHipY);
            return hip + Quaternion.Euler(0f, 0f, -angleDegrees) * new Vector3(0f, -KeeperFeetDrop * PixelToWorld, 0f);
        }
        public static Vector3 ScreenToWorld(float x, float y) => new Vector3((x - 600f) * PixelToWorld, (337.5f - y) * PixelToWorld, 0f);
        public static Vector3 BallToWorld(Vector3 position)
        {
            Vector3 projected = FootballShotSolver.Project(position);
            return ScreenToWorld(projected.x, projected.y);
        }
        public void Render(FootballRules rules)
        {
            if (rules == null || !ValidateReferences()) return;
            if (!configured) CaptureRestPositions();
            if (rules.PreviewVisible)
            {
                int count = rules.GetPreview(previewPoints);
                for (int i = 0; i < trajectoryDots.Length; i++)
                {
                    trajectoryDots[i].enabled = i < count;
                    if (i < count) trajectoryDots[i].transform.position = BallToWorld(previewPoints[i]);
                }
                if (count > 0)
                {
                    crosshair.enabled = true;
                    crosshair.transform.position = BallToWorld(previewPoints[count - 1]);
                }
            }
            else HidePreview();
            var flight = rules.Flight;
            if (goalNet)
            {
                float elapsed = flight?.OutcomeElapsed ?? 0f;
                float ripple = flight?.Outcome == FootballOutcome.Goal ? Mathf.Sin(elapsed * 35f) * Mathf.Exp(-elapsed * 4f) * 4f * PixelToWorld : 0f;
                goalNet.transform.position = netRest + Vector3.up * ripple;
            }
            bool flying = flight != null && rules.State != FootballState.Kicking;
            Vector3 position = flying ? flight.Position : new Vector3(0f, FootballShotSolver.BallRadius, 0f);
            Vector3 point = FootballShotSolver.Project(position);
            ball.transform.position = ScreenToWorld(point.x, point.y);
            ball.transform.localScale = ballScale * point.z;
            ball.transform.rotation = Quaternion.Euler(0f, 0f, flying ? -flight.Time * 550f : 0f);
            Vector3 ground = FootballShotSolver.Project(new Vector3(position.x, 0f, position.z));
            ballShadow.transform.position = ScreenToWorld(ground.x, ground.y);
            ballShadow.transform.localScale = shadowScale * point.z;
            float keeperX = flying ? flight.KeeperX : 0f;
            goalkeeper.enabled = rules.KeeperEnabled;
            // Keep the simulated horizontal travel and tilt through the saved-ball landing hold so
            // the keeper visibly reaches, leans, and falls away from the centre of the goal.
            float keeperAngle = flying ? flight.KeeperAngle : 0f;
            goalkeeper.transform.position = KeeperWorldPosition(keeperX, keeperAngle);
            goalkeeper.transform.rotation = Quaternion.Euler(0f, 0f, -keeperAngle);
            float swing = rules.State == FootballState.Kicking ? Mathf.Sin(Mathf.Clamp01(rules.StateElapsed / .18f) * Mathf.PI) : 0f;
            player.transform.position = playerRest + Vector3.right * (swing * .32f);
            player.transform.rotation = Quaternion.Euler(0f, 0f, swing * 7f);
            if (poses != null)
            {
                FootballOutcome? outcome = flight?.Outcome;
                Sprite kicker = poses.For(FootballPoses.Kicker(rules.State, rules.StateElapsed, outcome));
                if (kicker) player.sprite = kicker;
                Sprite keeperSprite = poses.For(FootballPoses.Keeper(outcome, keeperX,
                    flight?.OutcomeElapsed ?? 0f, keeperDiveThresholdMetres, keeperCentreDeadZoneMetres,
                    keeperLandingHoldSeconds, keeperCheerSeconds));
                if (keeperSprite) goalkeeper.sprite = keeperSprite;
            }
            RenderAimArrow(rules);
        }
        void RenderAimArrow(FootballRules rules)
        {
            bool visible = rules.State == FootballState.Aiming || rules.State == FootballState.Charging;
            if (!aimArrow && visible)
            {
                var arrowObject = new GameObject("AimArrow");
                arrowObject.transform.SetParent(transform, false);
                aimArrow = arrowObject.AddComponent<LineRenderer>();
                aimArrow.sharedMaterial = ball.sharedMaterial;
                aimArrow.useWorldSpace = true;
                aimArrow.sortingLayerID = ball.sortingLayerID;
                aimArrow.sortingOrder = ball.sortingOrder + 3;
                aimArrow.startColor = aimArrow.endColor = MinigameUiTheme.Accent;
                aimArrow.startWidth = aimArrow.endWidth = .10f;
                aimArrow.numCapVertices = 4;
                aimArrow.numCornerVertices = 4;
                aimArrow.positionCount = 5;
            }
            if (!aimArrow) return;
            aimArrow.enabled = visible;
            if (!visible) return;

            // Project the launch tangent using the same shot model as the actual ball.
            Vector3 origin = new Vector3(0f, FootballShotSolver.BallRadius, 0f);
            Vector3 velocity = FootballShotSolver.Create(rules.AimX, 0f).Velocity;
            Vector3 direction = (BallToWorld(origin + velocity * .01f) - BallToWorld(origin)).normalized;
            Vector3 side = new Vector3(-direction.y, direction.x, 0f);
            Vector3 tail = ball.transform.position + direction * .4f;
            Vector3 tip = tail + direction * 1.2f;
            aimArrow.SetPosition(0, tail);
            aimArrow.SetPosition(1, tip);
            aimArrow.SetPosition(2, tip - direction * .32f + side * .25f);
            aimArrow.SetPosition(3, tip);
            aimArrow.SetPosition(4, tip - direction * .32f - side * .25f);
        }
        public void HidePreview()
        {
            if (aimArrow) aimArrow.enabled = false;
            if (crosshair) crosshair.enabled = false;
            foreach (var dot in trajectoryDots) if (dot) dot.enabled = false;
        }
        void Awake() { CaptureRestPositions(); HidePreview(); }
        void OnDisable() => HidePreview();
        void CaptureRestPositions()
        {
            if (!ball || !ballShadow || !player) return;
            ballScale = ball.transform.localScale;
            shadowScale = ballShadow.transform.localScale;
            playerRest = player.transform.position;
            if (goalNet) netRest = goalNet.transform.position;
            configured = true;
        }
    }
}
