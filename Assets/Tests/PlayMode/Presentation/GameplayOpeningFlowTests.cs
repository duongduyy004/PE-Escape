using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
namespace KMA.Tests.Presentation
{
 public sealed class OpeningTestGame : MinigameBase
 {
  public void Step(float dt) => Lifecycle.Tick(dt);
  protected override void TickPlay(float dt) { }
 }
 public sealed class GameplayOpeningFlowTests
 {
  GameObject root; OpeningTestGame game; GameplayDialogueHost host; GameplayOpeningFlow opening;
  [SetUp] public void SetUp() {root=new GameObject("opening");game=root.AddComponent<OpeningTestGame>();host=root.AddComponent<GameplayDialogueHost>();opening=root.AddComponent<GameplayOpeningFlow>();}
  [TearDown] public void TearDown() {Object.DestroyImmediate(root);}
  [Test] public void NoGuideStillPlaysOpeningOnce()
  {
   opening.Configure(game,host,"sp.learn.start");game.RequestStart();opening.GuideClosed();
   game.Step(100);Assert.That(game.PresentationPhase,Is.EqualTo(MinigamePhase.Tutorial));Assert.That(host.IsIdle,Is.False);
   opening.GuideClosed();Assert.That(game.StartPreparationReady,Is.False);
   var scheduler=(GameplayDialogueScheduler)typeof(GameplayDialogueHost).GetField("scheduler",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(host);
   scheduler.Tick(20,false);opening.GuideClosed();Assert.That(game.PresentationPhase,Is.EqualTo(MinigamePhase.Countdown));
   opening.GuideClosed();Assert.That(host.IsIdle,Is.True);
  }
  [Test] public void MissingOpeningReleasesHold()
  {
   opening.Configure(game,host,"missing");LogAssert.Expect(LogType.Error,new Regex("Gameplay dialogue unavailable"));opening.GuideClosed();
   Assert.That(game.StartPreparationReady,Is.True);
  }
  [Test] public void UnloadDoesNotOpenNextScene()
  {
   opening.Configure(game,host,"sp.learn.start");opening.GuideClosed();Object.DestroyImmediate(root);root=new GameObject("next");
   var next=root.AddComponent<OpeningTestGame>();using(var hold=next.AcquireStartHold("next")) {next.RequestStart();Assert.That(next.StartPreparationReady,Is.False);}
   Assert.That(next.StartPreparationReady,Is.True);
  }
 }
}
