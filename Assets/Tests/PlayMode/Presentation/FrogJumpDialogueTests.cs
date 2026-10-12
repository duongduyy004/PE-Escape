using System.Collections.Generic;
using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using KMA.Gameplay.FrogJump;
using NUnit.Framework;
using UnityEngine;
namespace KMA.Tests.Presentation
{
 public sealed class FrogSpeechSpy : MonoBehaviour,IGameplayDialogueView
 {
  public readonly List<GameplayDialogueLine> Seen=new List<GameplayDialogueLine>();
  public void Show(GameplayDialogueLine line)=>Seen.Add(line);public void Hide() { }
 }
 public sealed class FrogJumpDialogueTests
 {
  GameObject root,managerRoot;FrogJumpController game;GameplayDialogueHost host;FrogSpeechSpy spy;FrogJumpView view;SpriteRenderer body;GameplayOpeningFlow opening;
  GameManager oldManager;GameplayDialogueScheduler scheduler;
  const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
  void Setup(bool? saves=null,bool adapterNeeded=true,bool enterPlay=true)
  {
   managerRoot=new GameObject("in-memory context");managerRoot.SetActive(false);var manager=managerRoot.AddComponent<GameManager>();
   var singleton=typeof(GameManager).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);oldManager=(GameManager)singleton.GetValue(null);singleton.SetValue(null,manager);
   var session=new GameSession();if(saves.HasValue)typeof(JourneyProgress).GetField("pendingFrogJump",F).SetValue(session.Journey,new FrogJumpPending("pending","failed","sprint_exam",saves.Value));
   typeof(GameManager).GetField("session",F).SetValue(manager,session);typeof(GameManager).GetField("initialized",F).SetValue(manager,true);
   root=new GameObject("frog speech");body=root.AddComponent<SpriteRenderer>();view=root.AddComponent<FrogJumpView>();
   var library=GameplayDialogueLibrary.LoadDefault();library.TryGet("frog.fall.first",out var fall);
   var poses=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/MaleAdventurer/MaleAdventurer_frogReadyRight.png");
   var air=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/MaleAdventurer/MaleAdventurer_frogAirRight.png");
   var sit=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/MaleAdventurer/MaleAdventurer_fallSitRight.png");view.Configure(body,poses,air,sit,-7,7,-2);
   game=root.AddComponent<FrogJumpController>();game.Configure(null,view,null,null,null);
   spy=root.AddComponent<FrogSpeechSpy>();host=root.AddComponent<GameplayDialogueHost>();host.Configure(library,spy);
   scheduler=(GameplayDialogueScheduler)typeof(GameplayDialogueHost).GetField("scheduler",F).GetValue(host);
   if(adapterNeeded){var type=typeof(FrogJumpController).Assembly.GetType("KMA.Gameplay.FrogJump.FrogJumpDialogueAdapter");Assert.That(type,Is.Not.Null);var adapter=root.AddComponent(type);type.GetMethod("Configure").Invoke(adapter,new object[]{game,host,null});}
   if(enterPlay){game.SetTutorialGate(false);((MinigameLifecycle)typeof(MinigameBase).GetField("lifecycle",F).GetValue(game)).Tick(4);}
  }
  void Step(float dt){typeof(FrogJumpController).GetMethod("TickPlay",F).Invoke(game,new object[]{dt});scheduler.Tick(dt,false);}
  [TearDown] public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);typeof(GameManager).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,oldManager);if(managerRoot!=null)Object.DestroyImmediate(managerRoot);}
  [Test] public void FirstFallShowsStudentForPointSevenThenTeacherForOnePointEight()
  {
   Setup();game.Rules.Stop();Assert.That(body.sprite.name,Is.EqualTo("MaleAdventurer_fallSitRight"));Assert.That(spy.Seen[0].Speaker,Is.EqualTo(DialogueSpeaker.Student));Assert.That(spy.Seen[0].Text,Is.EqualTo("Ui da!"));
   Step(.7f);Assert.That(spy.Seen[1].Speaker,Is.EqualTo(DialogueSpeaker.Teacher));Assert.That(spy.Seen[1].Icon,Is.Not.Null);Assert.That(spy.Seen[1].Poses[0].name,Is.EqualTo("BossPE_command"));
   Step(1.79f);Assert.That(game.Rules.State,Is.EqualTo(FrogJumpState.Fallen));Step(.01f);Assert.That(game.Rules.State,Is.EqualTo(FrogJumpState.Aiming));Assert.That(host.IsIdle,Is.True);Assert.That(game.Rules.Elapsed,Is.EqualTo(2.5f).Within(.001f));
  }
  [TestCase(false)] [TestCase(true)] public void DisabledOrReboundAdapterInvalidatesOldFall(bool rebind)
  {
   Setup();game.Rules.Stop();var adapter=root.GetComponent<FrogJumpDialogueAdapter>();
   if(rebind)adapter.Configure(game,host,null);else adapter.enabled=false;
   Assert.DoesNotThrow(()=>scheduler.Tick(.1f,false));Assert.That(host.IsIdle,Is.True);
   Assert.That(spy.Seen.Count,Is.EqualTo(1));
  }
  [Test] public void RepeatFallsShareCooldownAndCap()
  {
   Setup();game.Rules.Stop();Step(.7f);Step(1.8f);game.Rules.Stop();Assert.That(spy.Seen.Count,Is.EqualTo(2));Step(2.5f);Step(5);
   game.Rules.Stop();Assert.That(spy.Seen.Count,Is.EqualTo(3));Assert.That(spy.Seen[2].Speaker,Is.EqualTo(DialogueSpeaker.Teacher));Step(2.5f);Step(7.5f);
   game.Rules.Stop();Assert.That(spy.Seen.Count,Is.EqualTo(3));
  }
  [Test] public void FinishOnFinalLandingDoesNotQueueGoodJump()
  {
   Setup();typeof(FrogJumpRules).GetField("<Distance>k__BackingField",F).SetValue(game.Rules,58.5f);Step(.35f);game.Rules.Stop();Step(.6f);
   Assert.That(game.Rules.ReachedFinish,Is.True);Assert.That(host.PreferredResultEventId,Is.EqualTo("frog.finish"));Assert.That(spy.Seen.Count,Is.EqualTo(1));Assert.That(spy.Seen[0].Text,Does.Contain("Cuối cùng"));
  }
  [Test] public void TenSecondWarningDropsWhileFallen()
  {
   Setup();game.Rules.Tick(48.999f);game.Rules.Stop();Assert.That(game.Rules.State,Is.EqualTo(FrogJumpState.Fallen));host.Clear();spy.Seen.Clear();Step(1.01f);Step(1.49f);Assert.That(spy.Seen,Is.Empty);
  }
  void StartOpening(bool saves)
  {
   Setup(saves,true,false);opening=root.AddComponent<GameplayOpeningFlow>();
   root.GetComponent<FrogJumpDialogueAdapter>().Configure(game,host,opening);opening.GuideClosed();
  }
  [Test] public void OpeningReadsSixtyMetres()
  {
   StartOpening(true);Assert.That(game.PresentationPhase,Is.EqualTo(MinigamePhase.Tutorial));scheduler.Tick(2.5f,false);
   Assert.That(spy.Seen[1].Text,Does.Contain("60 mét").And.Contain("60 giây"));Assert.That(game.Rules.TimeRemaining,Is.EqualTo(60));
  }
  [Test] public void ChargedBranchNeverClaimsLifeSaved()
  {
   StartOpening(false);scheduler.Tick(1.2f,false);Assert.That(spy.Seen.Count,Is.EqualTo(2));
   foreach(var line in spy.Seen)Assert.That(line.Text,Does.Not.Contain("giữ"));Assert.That(game.BuildGuide()[0].Instruction,Does.Contain("không trừ thêm"));
  }
  [Test] public void DirectGuideDoesNotInventChargedAttempt()
  {
   Setup(null,false);Assert.That(game.BuildGuide()[0].Instruction,Does.Not.Contain("đã bị trừ"));
  }
 }
}
