using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEngine;
namespace KMA.Tests.Presentation
{
 public sealed class ResultDialogueTests
 {
  GameObject root;ResultPanel panel;GameplayDialogueHost host;GameplayDialogueScheduler scheduler;TMP_Text detail;
  const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
  [SetUp] public void Setup()
  {
   root=new GameObject("Result",typeof(RectTransform));var content=UiKit.Rect(root.transform,"Content").gameObject;
   TMP_Text Text(string name)=>UiKit.Label(content.transform,name,"",28,Color.white);
   panel=root.AddComponent<ResultPanel>();detail=Text("Detail");
   panel.Configure(content,Text("Status"),detail,Text("Score"),Text("Rank"),Text("Lives"),UiKit.Button(content.transform,"Continue","",ButtonVariant.Primary).Button,UiKit.Button(content.transform,"Retry","",ButtonVariant.Primary).Button,Text("Error"));
   host=root.AddComponent<GameplayDialogueHost>();host.Configure(GameplayDialogueLibrary.LoadDefault(),root.AddComponent<FrogSpeechSpy>());panel.BindDialogue(host);
   scheduler=(GameplayDialogueScheduler)typeof(GameplayDialogueHost).GetField("scheduler",F).GetValue(host);
  }
  [TearDown] public void Cleanup()=>Object.DestroyImmediate(root);
  ChallengeAttemptContext Context()=>new ChallengeAttemptContext("same-attempt","sprint_exam",ChallengeAttemptMode.Journey,ChallengeDifficulty.Normal);
  [UnityEngine.TestTools.UnityTest] public System.Collections.IEnumerator InactiveAuthoredSpeechCanvasRaisesOnReveal()
  {
   yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MG_Sprint");yield return null;
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
   var result=System.Linq.Enumerable.First(System.Linq.Enumerable.SelectMany(scene.GetRootGameObjects(),g=>g.GetComponentsInChildren<ResultPanel>(true)));
   var canvas=result.GetComponentInChildren<GameplayDialogueView>(true).GetComponent<Canvas>();
   result.Show(new MinigameResult(false,0,Rank.F),"Map");
   Assert.That(canvas.overrideSorting,Is.True);Assert.That(canvas.sortingOrder,Is.GreaterThan(result.GetComponent<Canvas>().sortingOrder));
   yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Map");
  }
  [Test] public void SpecificCompletionSuppressesGenericPass()
  {
   host.BeginResult("sp.practice.done");host.Emit("sp.practice.done");var chosen=scheduler.Current;
   panel.Show(new MinigameResult(true,6,Rank.C),"Map");Assert.That(scheduler.Current,Is.SameAs(chosen));scheduler.Tick(100,false);Assert.That(host.IsIdle,Is.True);
  }
  [Test] public void RetrySaveDoesNotReplaySpeechOrChangeAttemptId()
  {
   var context=Context();var result=new ChallengeAttemptResult(context,false,new ChallengeMetrics(),new MinigameResult(false,0,Rank.F));
   panel.ShowChallenge(context,result,null,"Cannot save");Assert.That(host.IsIdle,Is.True);Assert.That(panel.ContinueLabel,Is.EqualTo("LƯU LẠI"));
   var committed=new JourneyCommitOutcome(true,"sprint_exam",5,true,true,false);
   panel.ShowChallenge(context,result,committed,null);Assert.That(scheduler.Current,Is.Not.Null);Assert.That(scheduler.Current.Poses[0].name,Is.EqualTo("MaleAdventurer_disappointed"));
   scheduler.Tick(100,false);panel.ShowChallenge(context,result,committed,null);Assert.That(host.IsIdle,Is.True);
   Assert.That(((ChallengeAttemptContext)typeof(ResultPanel).GetField("challengeContext",F).GetValue(panel)).AttemptId,Is.EqualTo(context.AttemptId));
  }
  [Test] public void ZeroAttemptsKeepsMapActionAndChargedPenaltyDoesNotChargeAgain()
  {
   panel.ShowFrogJump(new FrogJumpResultView(false,false,0,null));Assert.That(panel.ContinueLabel,Is.EqualTo("VỀ BẢN ĐỒ"));Assert.That(detail.text,Does.Contain("không trừ thêm"));Assert.That(panel.RetryAvailable,Is.False);
  }
  [Test] public void GameOverSpeaksOnce()
  {
   var screen=root.AddComponent<GameOverScreen>();screen.Show();var speech=root.GetComponents<GameplayDialogueHost>();
   // Reuse the existing scene host rather than creating a second scheduler.
   Assert.That(speech.Length,Is.EqualTo(1));var own=speech[0];var s=(GameplayDialogueScheduler)typeof(GameplayDialogueHost).GetField("scheduler",F).GetValue(own);
   Assert.That(s.Current,Is.Not.Null);s.Tick(100,false);screen.Show();Assert.That(own.IsIdle,Is.True);Assert.That(root.GetComponents<GameplayDialogueHost>().Length,Is.EqualTo(1));
   int actions=0;screen.RetryRequested+=()=>actions++;screen.NewGameRequested+=()=>actions++;screen.MenuRequested+=()=>actions++;screen.Retry();screen.NewGame();screen.ReturnToMenu();Assert.That(actions,Is.EqualTo(3));
  }
 }
}
