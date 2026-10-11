using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
namespace KMA.Tests.Presentation
{
 public sealed class ResultDialoguePolicyTests
 {
  string Select(bool pass,ChallengeKind kind,string specific,JourneyCommitOutcome? outcome,string error)
  {
   var t=typeof(MinigameGuideHost).Assembly.GetType("KMA.Gameplay.UI.ResultDialoguePolicy");Assert.That(t,Is.Not.Null);
   return (string)t.GetMethod("Select").Invoke(null,new object[]{pass,kind,specific,outcome,error});
  }
  [Test] public void SpecificCompletionSuppressesGenericPass()=>Assert.That(Select(true,ChallengeKind.Practice,"sp.practice.done",null,null),Is.Null);
  [Test] public void FirstAndRepeatFailureFollowCommittedOutcome()
  {
   var first=new JourneyCommitOutcome(true,"sprint_exam",5,true,true,false);var repeat=new JourneyCommitOutcome(true,"sprint_exam",4,true,false,false);
   Assert.That(Select(false,ChallengeKind.Exam,null,first,null),Is.EqualTo("result.fail.first"));Assert.That(Select(false,ChallengeKind.Exam,null,repeat,null),Is.EqualTo("result.fail.repeat"));
   Assert.That(Select(false,ChallengeKind.Exam,null,null,null),Is.Null);
  }
  [Test] public void SaveErrorStaysSystemText()=>Assert.That(Select(true,ChallengeKind.Exam,null,null,"Save unavailable"),Is.Null);
  [Test] public void PracticeAndExamUseTheirOwnConclusion()
  {
   Assert.That(Select(true,ChallengeKind.Practice,null,null,null),Is.EqualTo("result.pass.practice"));Assert.That(Select(true,ChallengeKind.Exam,null,null,null),Is.EqualTo("result.pass.exam"));Assert.That(Select(true,ChallengeKind.Learn,null,null,null),Is.Null);
  }
 }
}
