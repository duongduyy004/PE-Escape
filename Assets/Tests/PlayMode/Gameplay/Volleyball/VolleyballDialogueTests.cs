using System.Collections.Generic;
using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;
using UnityEngine;
namespace KMA.Tests.Gameplay.Volleyball
{
 public sealed class VolleySpeechSpy : MonoBehaviour,IGameplayDialogueView
 {
  public readonly List<GameplayDialogueLine> Seen=new List<GameplayDialogueLine>();
  public void Show(GameplayDialogueLine line)=>Seen.Add(line);public void Hide() { }
 }
 public sealed class VolleyballDialogueTests
 {
  GameObject root;VolleyballController game;GameplayDialogueHost host;VolleySpeechSpy view;Component adapter;VolleyAthleteView athlete;
  const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
  void Setup(string id)
  {
   root=new GameObject("volley speech");game=root.AddComponent<VolleyballController>();
   var def=ChallengeCatalog.LoadDefault().Get(id);game.ConfigureChallenge(def,new ChallengeAttemptContext("speech",id,ChallengeAttemptMode.Journey,def.Difficulty));
   var body=root.AddComponent<SpriteRenderer>();athlete=root.AddComponent<VolleyAthleteView>();athlete.Configure(body,null,false,null,null,null,null,null,null);
   game.Configure(athlete,null,null,null,null);
   view=root.AddComponent<VolleySpeechSpy>();host=root.AddComponent<GameplayDialogueHost>();host.Configure(GameplayDialogueLibrary.LoadDefault(),view);
   var t=typeof(VolleyballController).Assembly.GetType("KMA.Gameplay.Volleyball.VolleyballDialogueAdapter");Assert.That(t,Is.Not.Null);
   adapter=root.AddComponent(t);t.GetMethod("Configure").Invoke(adapter,new object[]{game,host,null});game.SkipToPlayForTest();
  }
  void Point(CourtSide winner)=>typeof(VolleyballMatch).GetMethod("AwardPoint",F).Invoke(game.Match,new object[]{winner,false});
  [TearDown] public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);}
  [Test] public void LostPointDoesNotInventDive()
  {
   Setup("volleyball_exam");Point(CourtSide.Opponent);Assert.That(game.Match.Player.Action,Is.Not.EqualTo(AthleteAction.Dive));
   Assert.That(view.Seen.Count,Is.EqualTo(1));Assert.That(view.Seen[0].Poses[0].name,Is.EqualTo("MaleAdventurer_disappointed"));
  }
  [Test] public void ActualDiveAndRecoveryProduceOneStandUpLine()
  {
   Setup("volleyball_exam");game.Match.Player.BeginAction(AthleteAction.Dive,.5f);athlete.Render(game.Match.Player);Assert.That(view.Seen,Is.Empty);
   game.Match.Player.Tick(.5f);athlete.Render(game.Match.Player);Assert.That(view.Seen.Count,Is.EqualTo(1));
   athlete.Render(game.Match.Player);Assert.That(view.Seen.Count,Is.EqualTo(1));
  }
  [TestCase(-.3f)] [TestCase(.3f)] public void EarlyLateAdviceWaitsForAvailableGap(float offset)
  {
   Setup("volleyball_practice");game.Match.ResetRally(CourtSide.Player);game.Match.PressAction();
   game.Match.Tick(game.Match.Flight.ApexTime+offset);
   var decision=game.Match.PressAction();Assert.That(decision.Grade,Is.EqualTo(TimingGrade.Late));Assert.That(view.Seen,Is.Empty);
   Point(CourtSide.Opponent);Assert.That(view.Seen.Count,Is.EqualTo(1));
   game.Match.Tick(VolleyballMatch.PointPause+.01f);
   var scheduler=(GameplayDialogueScheduler)typeof(GameplayDialogueHost).GetField("scheduler",F).GetValue(host);scheduler.Tick(2,false);
   Assert.That(host.IsIdle,Is.True,"old advice must end before the new rally");
  }
  [Test] public void LateTimingAdviceIsNotShownWhenPlayerWinsPoint()
  {
   Setup("volleyball_practice");game.Match.ResetRally(CourtSide.Player);game.Match.PressAction();
   game.Match.Tick(game.Match.Flight.ApexTime+.3f);
   var decision=game.Match.PressAction();Assert.That(decision.Grade,Is.EqualTo(TimingGrade.Late));Assert.That(view.Seen,Is.Empty);
   Point(CourtSide.Player);
   Assert.That(view.Seen,Is.Empty,"the late/miss line must not play after a player point");
  }
  [Test] public void NinePointsTriggersOneMatchpointWhileNotOver()
  {
   Setup("volleyball_exam");typeof(VolleyballMatch).GetField("<PlayerPoints>k__BackingField",F).SetValue(game.Match,8);Point(CourtSide.Player);
   Assert.That(game.Match.IsOver,Is.False);Assert.That(view.Seen.Count,Is.EqualTo(1));
  }
 }
}
