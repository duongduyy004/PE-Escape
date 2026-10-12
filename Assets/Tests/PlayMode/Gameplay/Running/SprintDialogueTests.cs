using System;
using System.Collections.Generic;
using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
namespace KMA.Tests.Gameplay.Running
{
 public sealed class SprintSpeechSpy : MonoBehaviour, IGameplayDialogueView
 {
  public readonly List<GameplayDialogueLine> Seen=new List<GameplayDialogueLine>();
  public void Show(GameplayDialogueLine line)=>Seen.Add(line);
  public void Hide() { }
 }
 public sealed class SprintDialogueTests
 {
  GameObject root;SprintController game;GameplayDialogueHost host;SprintSpeechSpy view;Component adapter;
  const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
  void Setup(string id)
  {
   root=new GameObject("sprint speech");game=root.AddComponent<SprintController>();
   var def=ChallengeCatalog.LoadDefault().Get(id);game.ConfigureChallenge(def,new ChallengeAttemptContext("speech",id,ChallengeAttemptMode.Journey,def.Difficulty));
   view=root.AddComponent<SprintSpeechSpy>();host=root.AddComponent<GameplayDialogueHost>();host.Configure(GameplayDialogueLibrary.LoadDefault(),view);
   var type=typeof(SprintController).Assembly.GetType("KMA.Gameplay.SprintDialogueAdapter");Assert.That(type,Is.Not.Null);
   adapter=root.AddComponent(type);type.GetMethod("Configure").Invoke(adapter,new object[]{game,host,null});
   game.SetTutorialGate(false);game.Simulate(3.1f);
  }
  [TearDown] public void Cleanup(){if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
  [Test] public void LearnWrongResetsStreakWithoutFallDialogue()
  {
   Setup("sprint_learn");game.OnLeftTap();game.OnLeftTap();Assert.That(game.CorrectStreak,Is.Zero);
   Assert.That(view.Seen.Count,Is.EqualTo(1));Assert.That(view.Seen[0].Poses[0].name,Is.EqualTo("MaleAdventurer_disappointed"));
  }
  [Test] public void ExamStumbleRequiresPresentedReaction()
  {
   Setup("sprint_exam");game.OnRightTap();adapter.GetType().GetMethod("Update",F).Invoke(adapter,null);
   Assert.That(view.Seen,Is.Empty);
  }
  [Test] public void FiveSecondWarningFiresOnce()
  {
   Setup("sprint_exam");game.Simulate(10.1f);adapter.GetType().GetMethod("Update",F).Invoke(adapter,null);
   Assert.That(view.Seen.Count,Is.EqualTo(1));host.Clear();adapter.GetType().GetMethod("Update",F).Invoke(adapter,null);Assert.That(host.IsIdle,Is.True);
  }
  [Test] public void LearnDoneRequiresFinalPass()
  {
   Setup("sprint_learn");for(int i=0;i<50;i++){if(i%2==0)game.OnLeftTap();else game.OnRightTap();}
   Assert.That(host.PreferredResultEventId,Is.EqualTo("sp.learn.done"));Assert.That(view.Seen.Count,Is.EqualTo(1));
  }
 }
}
