namespace KMA.Gameplay.UI
{
    public static class ResultDialoguePolicy
    {
        public static string Select(bool pass,ChallengeKind kind,string specificEventId,JourneyCommitOutcome? outcome,string saveError)
        {
            if(!string.IsNullOrEmpty(saveError) || !string.IsNullOrEmpty(specificEventId))return null;
            if(pass)return kind==ChallengeKind.Practice?"result.pass.practice":kind==ChallengeKind.Exam?"result.pass.exam":null;
            if(outcome.HasValue && outcome.Value.Accepted && outcome.Value.FrogJumpRequired)
                return outcome.Value.FrogJumpSavesLife?"result.fail.first":"result.fail.repeat";
            return null;
        }
    }
}
