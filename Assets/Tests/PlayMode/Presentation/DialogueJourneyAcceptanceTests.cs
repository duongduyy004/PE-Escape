using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using KMA.Gameplay.Volleyball;
using KMA.Gameplay.FrogJump;
using KMA.Gameplay.Chess;
using KMA.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace KMA.Tests.Presentation
{
 public sealed class DialogueJourneyAcceptanceTests
 {
  static readonly string[] Scenes={"MG_Sprint","MG_Volleyball","MG_Football","MG_ChessFinal","MG_FrogJump"};
  [SetUp] public void Setup(){Time.timeScale=1;foreach(var r in Object.FindObjectsByType<SceneRouter>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(r.gameObject);foreach(var r in Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(r.gameObject);}
  [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;yield return SceneManager.LoadSceneAsync("Map");Setup();}
  static IEnumerator Load(string scene){yield return SceneManager.LoadSceneAsync(scene);yield return null;var guide=MinigameGuideHost.Current?.Panel;while(guide!=null && guide.IsOpen)guide.PressPrimary();yield return null;}
  static GameplayDialogueHost Host()=>Object.FindObjectsByType<GameplayDialogueHost>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
  static GameplayDialogueScheduler Scheduler(GameplayDialogueHost h)=>(GameplayDialogueScheduler)typeof(GameplayDialogueHost).GetField("scheduler",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(h);
  [UnityTest] public IEnumerator DirectAndJourneyEntryKeepOneDialogueHost()
  {
   foreach(string scene in Scenes){yield return Load(scene);Assert.That(Host(),Is.Not.Null,scene);}
   foreach(var definition in ChallengeCatalog.LoadDefault().Ordered)
   {
    Setup();var router=SceneRouter.EnsurePersistentInstance();
    foreach(var prior in router.Session.Journey.Catalog.Ordered)
    {
     if(prior.Id==definition.Id)break;
     Assert.That(router.Session.TryStartChallenge(prior.Id,ChallengeAttemptMode.Journey,prior.Difficulty,out var context),Is.True);
     router.Session.SubmitChallengeResult(new ChallengeAttemptResult(context,true,new ChallengeMetrics(completedTargets:prior.TargetCount),ChallengeDefinition.IsScored(prior.Kind)?new MinigameResult(true,6,Rank.C):null));
    }
    router.ConfigureJourneyPersistence((out string error)=>{error=null;return true;});Assert.That(router.TryStartChallenge(definition.Id,difficulty:definition.Difficulty),Is.True);
    float end=Time.realtimeSinceStartup+15;while(router.IsTransitioning && Time.realtimeSinceStartup<end)yield return null;yield return null;
    Assert.That(Host(),Is.Not.Null,definition.Id);
   }
  }
  [UnityTest] public IEnumerator RetryAndSceneChangeDiscardOldLines()
  {
   yield return Load("MG_FrogJump");var old=Host();var scheduler=Scheduler(old);old.Emit("frog.fall.first");old.ResetAttempt();Assert.That(old.IsIdle,Is.True);
   old.Emit("frog.fall.first");yield return Load("MG_Sprint");Assert.That(scheduler.IsIdle,Is.True);Assert.That(Host(),Is.Not.SameAs(old));Assert.That(Host().IsIdle,Is.True);
  }
  static PointerEventData Pointer(RectTransform rect)
  {
   Canvas.ForceUpdateCanvases();var canvas=rect.GetComponentInParent<Canvas>();return new PointerEventData(EventSystem.current){pointerId=42,button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
  }
  static void Down(Component control)
  {
   var p=Pointer((RectTransform)control.transform);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Assert.That(hits,Is.Not.Empty,control.name);
   var target=ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject);Assert.That(target,Is.EqualTo(control.gameObject),control.name+" blocked by "+hits[0].gameObject.name);ExecuteEvents.Execute(target,p,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(target,p,ExecuteEvents.pointerUpHandler);
  }
  [UnityTest] public IEnumerator RealPointerInputWorksBehindDialogue()
  {
   foreach(string scene in Scenes)
   {
    yield return Load(scene);var game=Object.FindFirstObjectByType<MinigameBase>();var football=game as FootballController;if(football!=null)football.BeginMatch(FootballDifficulty.Normal);
    float end=Time.realtimeSinceStartup+12;while(game.PresentationPhase!=MinigamePhase.Play && Time.realtimeSinceStartup<end){if(game is ChessFinalController ch)ch.BeginAttempt();yield return null;}
    Host().Clear();Host().Emit("frog.fall.first");Canvas.ForceUpdateCanvases();
    foreach(var graphic in Object.FindObjectsByType<GameplayDialogueView>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(v=>v.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)))Assert.That(graphic.raycastTarget,Is.False);
    if(game is FrogJumpController frog){Down(Object.FindFirstObjectByType<FrogJumpTapArea>());Assert.That(frog.Rules.State,Is.Not.EqualTo(FrogJumpState.Aiming));}
    else if(game is VolleyballController volley){Down(Object.FindFirstObjectByType<ActionButton>());Assert.That(volley.Input.ConsumePresses(),Is.EqualTo(1));}
    else if(football!=null){Down(Object.FindFirstObjectByType<FootballHud>().ShootButton);Assert.That(football.Rules.State,Is.EqualTo(FootballState.Kicking));}
    else if(game is SprintController sprint){int taps=0;sprint.CadenceTapped+=_=>taps++;Down(Object.FindFirstObjectByType<ScreenTapArea>());yield return null;Assert.That(taps,Is.EqualTo(1));}
    else if(game is ChessFinalController chess)
    {
     var board=Object.FindFirstObjectByType<ChessBoardView>();ChessMove.TryParseUci(chess.Machine.Puzzle.nodes[0].moves[0].uci,out var move);
     var cell=(RectTransform)board.transform.Find(Square.Name(move.From));var pointer=Pointer(cell);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
     var target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);Assert.That(target,Is.EqualTo(cell.gameObject));ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler);Assert.That(board.SelectedSquare,Is.EqualTo(move.From));
    }
   }
  }
 }
}
