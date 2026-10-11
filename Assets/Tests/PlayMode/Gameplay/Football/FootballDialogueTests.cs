using System.Collections.Generic;
using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
namespace KMA.Tests.Gameplay.Football
{
 public sealed class FootballSpeechSpy : MonoBehaviour,IGameplayDialogueView
 {
  public readonly List<GameplayDialogueLine> Seen=new List<GameplayDialogueLine>();
  public void Show(GameplayDialogueLine line)=>Seen.Add(line);public void Hide() { }
 }
 public sealed class FootballDialogueTests
 {
  FootballControllerTests.Fixture fixture;GameplayDialogueHost host;FootballSpeechSpy view;Component adapter;
  const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
  FootballController Game=>fixture.controller;
  void Setup(string id)
  {
   FootballController.ResetSessionPreferenceForTests();fixture=new FootballControllerTests.Fixture();
   var def=ChallengeCatalog.LoadDefault().Get(id);Game.ConfigureChallenge(def,new ChallengeAttemptContext("speech",id,ChallengeAttemptMode.Journey,def.Difficulty));
   view=fixture.root.AddComponent<FootballSpeechSpy>();host=fixture.root.AddComponent<GameplayDialogueHost>();host.Configure(GameplayDialogueLibrary.LoadDefault(),view);
   var t=typeof(FootballController).Assembly.GetType("KMA.Gameplay.FootballDialogueAdapter");Assert.That(t,Is.Not.Null);
   adapter=fixture.root.AddComponent(t);t.GetMethod("Configure").Invoke(adapter,new object[]{Game,host,null});
   Game.BeginMatch(FootballDifficulty.Normal);
   ((MinigameLifecycle)typeof(MinigameBase).GetField("lifecycle",F).GetValue(Game)).Tick(4f);Tick(.01f);
  }
  void Tick(float dt)=>typeof(FootballController).GetMethod("TickPlay",F).Invoke(Game,new object[]{dt});
  void Shoot(float aim,float charge)
  {
   Game.Rules.SetAim(aim);Game.Rules.BeginCharge();Game.Rules.Tick(charge);Game.Rules.ReleaseShot();
   for(int i=0;i<2000 && Game.Rules.State!=FootballState.ShotResult && Game.Rules.State!=FootballState.MatchResult;i++)Tick(.01f);
  }
  [TearDown] public void Cleanup(){fixture?.Dispose();}
  [Test] public void SavedLineRequiresSavedOutcome()
  {
   Setup("soccer_practice");Shoot(0,.9f);Assert.That(Game.Rules.LastOutcome,Is.EqualTo(FootballOutcome.Saved));Assert.That(view.Seen.Count,Is.EqualTo(1));
   Assert.That(view.Seen[0].Text,Does.Contain("bắt"));Tick(1);
   var scheduler=(GameplayDialogueScheduler)typeof(GameplayDialogueHost).GetField("scheduler",F).GetValue(host);scheduler.Tick(1.3f,false);
   Assert.That(view.Seen.Count,Is.EqualTo(2),"teacher can finish during idle aiming, before the next shot");
  }
  [Test] public void MissNeverInventsSavedOrFallSpeech()
  {
   Setup("soccer_practice");Shoot(1,FootballTuning.For(FootballDifficulty.Normal).PowerRiseSeconds/2);
   Assert.That(Game.Rules.LastOutcome,Is.EqualTo(FootballOutcome.Wide));Assert.That(view.Seen,Is.Empty);
  }
  [Test] public void FifthKickLineRunsOnlyWhileAiming()
  {
   Setup("soccer_exam");for(int i=0;i<4;i++){Shoot(0,0);Tick(1);host.Clear();}
   Assert.That(Game.Rules.Kicks,Is.EqualTo(4));Assert.That(Game.Rules.State,Is.EqualTo(FootballState.Aiming));
   Assert.That(view.Seen.Count,Is.EqualTo(1));Game.Rules.BeginCharge();
   var scheduler=(GameplayDialogueScheduler)typeof(GameplayDialogueHost).GetField("scheduler",F).GetValue(host);scheduler.Tick(.01f,false);Assert.That(host.IsIdle,Is.True);
  }
  [Test] public void GoalsDoNotAnnounceExamPassBeforeFiveKicks()
  {
   Setup("soccer_exam");for(int i=0;i<3;i++){Shoot(.55f,FootballTuning.For(FootballDifficulty.Normal).PowerRiseSeconds/2);Tick(1);host.Clear();}
   Assert.That(Game.Rules.Goals,Is.EqualTo(3));Assert.That(host.PreferredResultEventId,Is.Null);Assert.That(Game.Rules.State,Is.EqualTo(FootballState.Aiming));
  }
  [Test] public void PracticeDoneRequiresRulesMatchResult()
  {
   Setup("soccer_practice");Shoot(.55f,FootballTuning.For(FootballDifficulty.Normal).PowerRiseSeconds/2);Tick(1);
   Assert.That(host.PreferredResultEventId,Is.Null);Shoot(.55f,FootballTuning.For(FootballDifficulty.Normal).PowerRiseSeconds/2);
   Assert.That(Game.Rules.State,Is.EqualTo(FootballState.MatchResult));Assert.That(host.PreferredResultEventId,Is.EqualTo("fb.practice.done"));
  }
  [Test] public void FailedFinalUsesImpossibleWithoutDuplicateFailureSpeech()
  {
   Setup("soccer_exam");for(int i=0;i<5;i++){Shoot(0,0);Tick(1);}
   Assert.That(Game.Rules.State,Is.EqualTo(FootballState.MatchResult));Assert.That(host.PreferredResultEventId,Is.EqualTo("fb.exam.impossible"));
  }
 }
}
