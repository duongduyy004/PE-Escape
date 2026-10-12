using TMPro;
using UnityEngine;

namespace KMA.Gameplay.Volleyball
{
    public sealed class VolleyballHud : MonoBehaviour
    {
        public const float FeedbackSeconds = .8f;
        public const string HintText = "Joystick: di chuyển  ·  NHẢY rồi kéo joystick để nhắm  ·  ĐÁNH để đập";
        public const string PracticeHintFormat = "ĐẠT {1} ĐIỂM TRƯỚC ĐỐI THỦ · {0}/{1}";
        public const string BlockText = "CHẮN!";
        public const float HintSeconds = 3.5f;

        [SerializeField] TMP_Text scoreLabel;
        [SerializeField] TMP_Text feedbackLabel;
        [SerializeField] TMP_Text hintLabel;
        [SerializeField] GameObject hintBackdrop;
        [SerializeField] TMP_Text timerLabel;
        [SerializeField] GameObject timerBackdrop;

        float feedbackLeft;
        float hintLeft = HintSeconds;
        MinigamePhase previousPhase = MinigamePhase.Tutorial;
        ChallengeDefinition challenge;
        VolleyballChallengeRules challengeRules;

        public void Configure(TMP_Text score, TMP_Text feedback, TMP_Text hint)
        {
            scoreLabel = score;
            feedbackLabel = feedback;
            hintLabel = hint;
            if (hintLabel)
                hintLabel.text = VietText.Fix(HintText);
            if (feedbackLabel)
                feedbackLabel.enabled = false;
        }

        public static string ScoreText(int playerPoints, int opponentPoints) =>
            $"{playerPoints}  :  {opponentPoints}";

        public void ConfigureHintBackdrop(GameObject backdrop) => hintBackdrop = backdrop;

        public void ConfigureTimer(TMP_Text timer, GameObject backdrop = null)
        {
            timerLabel = timer;
            timerBackdrop = backdrop;
        }

        /// m:ss for the time left on the match clock.
        public static string TimerText(float secondsRemaining)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        public void ConfigureChallenge(ChallengeDefinition definition, VolleyballChallengeRules rules)
        {
            challenge = definition;
            challengeRules = rules;
        }

        public static string FeedbackText(TimingGrade grade, float offset) => grade switch
        {
            TimingGrade.Perfect => "HOÀN HẢO",
            TimingGrade.Good => "TỐT",
            TimingGrade.Late => offset < 0f ? "SỚM" : "MUỘN",
            _ => string.Empty
        };

        public void ShowFeedback(ActionDecision decision)
        {
            if (!decision.IsTimed || !feedbackLabel)
                return;

            feedbackLabel.text = VietText.Fix(FeedbackText(decision.Grade, decision.Offset));
            feedbackLabel.enabled = true;
            feedbackLeft = FeedbackSeconds;
        }

        public void ShowPoint(CourtSide winner)
        {
            if (!feedbackLabel)
                return;
            feedbackLabel.text = VietText.Fix(winner == CourtSide.Player ? "GHI ĐIỂM!" : "ĐỐI THỦ GHI ĐIỂM");
            feedbackLabel.enabled = true;
            feedbackLeft = 1.15f;
        }

        public void ShowBlock()
        {
            if (!feedbackLabel)
                return;
            feedbackLabel.text = VietText.Fix(BlockText);
            feedbackLabel.enabled = true;
            feedbackLeft = 1.15f;
        }

        public void Render(VolleyballMatch match, MinigamePhase phase, float deltaTime)
        {
            if (match != null && scoreLabel)
                scoreLabel.text = VietText.Fix(ScoreText(match.PlayerPoints, match.OpponentPoints));
            if (timerLabel)
            {
                bool timed = match != null && match.ClockLimit > 0f;
                if (timerBackdrop)
                    timerBackdrop.SetActive(timed);
                timerLabel.gameObject.SetActive(timed);
                if (timed)
                    timerLabel.text = VietText.Fix(TimerText(match.TimeRemaining));
            }
            if (phase != previousPhase)
            {
                if (phase == MinigamePhase.Play)
                    hintLeft = HintSeconds;
                previousPhase = phase;
            }
            if (hintLabel)
            {
                string hint = challenge == null ? HintText
                    : challenge.Kind == ChallengeKind.Learn ? challengeRules.LearnProgressText
                    : challenge.Kind == ChallengeKind.Practice ? string.Format(PracticeHintFormat, challengeRules.CompletedTargets, challenge.TargetCount)
                    : HintText;
                hintLabel.text = VietText.Fix(hint);
                hintLabel.enabled = phase == MinigamePhase.Play && hintLeft > 0f;
                if (hintBackdrop)
                    hintBackdrop.SetActive(hintLabel.enabled);
            }

            if (phase == MinigamePhase.Play)
                hintLeft -= Mathf.Max(0f, deltaTime);

            if (!feedbackLabel || !feedbackLabel.enabled)
                return;

            feedbackLeft -= Mathf.Max(0f, deltaTime);
            if (feedbackLeft <= 0f)
                feedbackLabel.enabled = false;
        }
    }
}
