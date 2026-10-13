using System;
using System.Collections;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.Gameplay.Chess;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace KMA.Tests.Gameplay.Chess
{
 public sealed class ChessDialogueTests
 {
  [Test] public void StudentSpeechUsesStudentAnchorAndTeacherSpeechUsesTeacherAnchor()
  {
   var root=new GameObject("cast",typeof(RectTransform));
   try {
    Image Image(string name){var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(root.transform);return g.GetComponent<Image>();}
    var student=Image("student");var teacher=Image("teacher");var bubble=Image("bubble");
    var text=new GameObject("text",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();text.transform.SetParent(bubble.transform);
    var cast=root.AddComponent<ChessCastView>();cast.Configure(student,teacher,new ChessCastView.Pose[0],new ChessCastView.Pose[0],bubble.gameObject,text);
    var view=(object)cast as IGameplayDialogueView;Assert.That(view,Is.Not.Null);
    GameplayDialogueLibrary.LoadDefault().TryGet("ch.extra.begin",out var s);view.Show(s.Lines[0]);Assert.That(bubble.rectTransform.anchorMin.x,Is.EqualTo(0f));
    GameplayDialogueLibrary.LoadDefault().TryGet("chess.hint1",out var t);view.Show(t.Lines[0]);Assert.That(bubble.rectTransform.anchorMin.x,Is.EqualTo(1f));
    Assert.That(bubble.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),Is.True);
   } finally {UnityEngine.Object.DestroyImmediate(root);}
  }
  [Test] public void ChessLinesNoLongerPromiseAFixedTimeOrMistakeAllowance()
  {
   var library=GameplayDialogueLibrary.LoadDefault();
   foreach(string id in new[]{"chess.start","chess.failed","ch.extra.begin","ch.extra.firstcorrect"})
   {
    Assert.That(library.TryGet(id,out var e),Is.True,id);
    foreach(var line in e.Lines){Assert.That(line.Text,Does.Not.Contain("90"),id);Assert.That(line.Text,Does.Not.Contain("sửa"),id);Assert.That(line.Text,Does.Not.Contain("tượng"),id);}
   }
  }
  [UnityTest] public IEnumerator AllCalledPoseKeysExist()
  {
   yield return SceneManager.LoadSceneAsync("MG_ChessFinal");yield return null;
   var cast=UnityEngine.Object.FindFirstObjectByType<ChessCastView>();
   foreach(var key in new[]{"fear","think","happy","disappointed"})Assert.DoesNotThrow(()=>cast.SetStudent(key),key);
   foreach(var key in new[]{"command","angry","scoreWrite","handsOnHips"})Assert.DoesNotThrow(()=>cast.SetTeacher(key),key);
  }
  static IEnumerator WaitFor(Func<bool> condition,string what,float seconds=10f)
  {
   float until=Time.realtimeSinceStartup+seconds;
   while(!condition() && Time.realtimeSinceStartup<until)yield return null;
   Assert.That(condition(),Is.True,what);
  }
  IEnumerator LoadReady()
  {
   Time.timeScale=1;yield return SceneManager.LoadSceneAsync("MG_ChessFinal");yield return null;
   var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();
   yield return WaitFor(()=>game.PuzzleReady,"the puzzle was not generated",20f);
   var opening=UnityEngine.Object.FindFirstObjectByType<GameplayOpeningFlow>();
   yield return WaitFor(()=>opening.IsComplete,"the opening did not finish",8f);
  }
  static ChessMove Optimal(ChessAttemptMachine m)=>ChessHintBuilder.Build(new ChessSearchEngine(SearchBudget.Default()),
   new HintRequest{Id=0,Fen=m.Position.ToFen(),MovesLeft=m.MaxMoves-m.MovesUsed,Level=3}).Move;
  static ChessMove Suboptimal(ChessAttemptMachine m)
  {
   var engine=new ChessSearchEngine(SearchBudget.Default());int movesLeft=m.MaxMoves-m.MovesUsed;ChessMove fallback=default;
   foreach(var move in MoveGenerator.LegalMoves(m.Position).Where(x=>x.Promotion==0))
   {
    var after=m.Position.Apply(move);if(MoveGenerator.IsCheckmate(after) || MoveGenerator.LegalMoves(after).Count==0)continue;
    fallback=move;if(ChessAttemptGrader.Evaluate(engine,m.Position,move,movesLeft).Q<.999)return move;
   }
   return fallback;
  }
  [UnityTest] public IEnumerator OpeningOccursOnceBeforeTheBeginLine()
  {
   yield return SceneManager.LoadSceneAsync("MG_ChessFinal");yield return null;
   var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();var cast=UnityEngine.Object.FindFirstObjectByType<ChessCastView>();
   yield return WaitFor(()=>game.PuzzleReady,"the puzzle was not generated",20f);
   var opening=UnityEngine.Object.FindFirstObjectByType<GameplayOpeningFlow>();
   if(!opening.IsComplete){game.BeginAttempt();Assert.That(game.Machine.Phase,Is.EqualTo(ChessAttemptPhase.Intro),"Begin waits for the opening");}
   Assert.That(game.Machine.Clock.Elapsed,Is.Zero);
   yield return WaitFor(()=>opening.IsComplete,"the opening did not finish",8f);
   game.BeginAttempt();Assert.That(game.Machine.Phase,Is.EqualTo(ChessAttemptPhase.PlayerTurn));
   Assert.That(cast.BubbleSpeaker,Is.EqualTo(DialogueSpeaker.Student));Assert.That(cast.BubbleText,Does.Contain("đi thế nào"));
  }
  [UnityTest] public IEnumerator IllegalMoveKeepsPosesAndDoesNotWhistle()
  {
   yield return LoadReady();var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();var cast=UnityEngine.Object.FindFirstObjectByType<ChessCastView>();game.BeginAttempt();
   var host=UnityEngine.Object.FindFirstObjectByType<GameplayDialogueHost>();host.Clear();cast.SetStudent("disappointed");cast.SetTeacher("command");
   int whistles=0;Action<GameSound> sound=x=>{if(x==GameSound.Whistle)whistles++;};GameAudio.Requested+=sound;
   try {
    game.Machine.Submit(new ChessMove(0,0));
    Assert.That(cast.StudentPose,Is.EqualTo("disappointed"));Assert.That(cast.TeacherPose,Is.EqualTo("command"));Assert.That(whistles,Is.Zero);Assert.That(game.Machine.MovesUsed,Is.Zero);
   } finally {GameAudio.Requested-=sound;}
  }
  [UnityTest] public IEnumerator SuboptimalMoveStaysPlayableWithoutAMistakeSpeechOrWhistle()
  {
   yield return LoadReady();var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();var cast=UnityEngine.Object.FindFirstObjectByType<ChessCastView>();game.BeginAttempt();
   yield return null;yield return null; // the shared start whistle plays when the Play phase begins
   int whistles=0;Action<GameSound> sound=x=>{if(x==GameSound.Whistle)whistles++;};GameAudio.Requested+=sound;
   try {
    var wrong=Suboptimal(game.Machine);game.RequestMove(wrong.From,wrong.To);
    yield return WaitFor(()=>game.Machine.Phase==ChessAttemptPhase.PlayerTurn || game.Machine.Phase==ChessAttemptPhase.Failed,"the turn came back",8f);
    Assert.That(game.Machine.MovesUsed,Is.EqualTo(1));Assert.That(whistles,Is.Zero);
    Assert.That(cast.TeacherPose,Is.Not.EqualTo("whistle0"));Assert.That(cast.TeacherPose,Is.Not.EqualTo("whistle1"));
   } finally {GameAudio.Requested-=sound;}
  }
  [UnityTest] public IEnumerator SolvingKeepsPointNineSecondsAndPlaysTheSolvedResult()
  {
   yield return LoadReady();var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();game.BeginAttempt();
   int completed=0;game.Completed+=r=>completed++;
   for(int i=0;i<game.Machine.MaxMoves && game.Machine.Phase!=ChessAttemptPhase.Completed;i++)
   {
    yield return WaitFor(()=>game.Machine.Phase==ChessAttemptPhase.PlayerTurn || game.Machine.Phase==ChessAttemptPhase.Completed,"player turn",8f);
    if(game.Machine.Phase==ChessAttemptPhase.Completed)break;
    var move=Optimal(game.Machine);game.RequestMove(move.From,move.To);
    yield return null;
   }
   yield return WaitFor(()=>game.Machine.Phase==ChessAttemptPhase.Completed,"mate was not reached",8f);
   yield return new WaitForSeconds(.5f);Assert.That(completed,Is.Zero);
   yield return new WaitForSeconds(.7f);Assert.That(completed,Is.EqualTo(1));
   var host=UnityEngine.Object.FindFirstObjectByType<GameplayDialogueHost>();Assert.That(host.PreferredResultEventId,Is.EqualTo("chess.solved"));
  }
  [UnityTest] public IEnumerator RunningOutOfTimePlaysTheTimeoutResult()
  {
   yield return LoadReady();var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();game.BeginAttempt();
   game.Machine.Tick(game.Machine.Clock.Limit);
   yield return WaitFor(()=>game.Machine.Phase==ChessAttemptPhase.Failed,"the clock did not fail the attempt");
   var host=UnityEngine.Object.FindFirstObjectByType<GameplayDialogueHost>();Assert.That(host.PreferredResultEventId,Is.EqualTo("chess.timeout"));
  }
  [UnityTest] public IEnumerator RunningOutOfMovesPlaysTheFailedResult()
  {
   yield return LoadReady();var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();game.BeginAttempt();
   for(int i=0;i<game.Machine.MaxMoves && game.Machine.Phase!=ChessAttemptPhase.Failed;i++)
   {
    yield return WaitFor(()=>game.Machine.Phase==ChessAttemptPhase.PlayerTurn || game.Machine.Phase==ChessAttemptPhase.Failed || game.Machine.Phase==ChessAttemptPhase.Completed,"player turn",8f);
    if(game.Machine.Phase!=ChessAttemptPhase.PlayerTurn)break;
    var move=Suboptimal(game.Machine);game.RequestMove(move.From,move.To);yield return null;
   }
   yield return WaitFor(()=>game.Machine.Phase==ChessAttemptPhase.Failed,"the attempt did not run out of moves",10f);
   var host=UnityEngine.Object.FindFirstObjectByType<GameplayDialogueHost>();
   Assert.That(game.Machine.EndReason,Is.Not.EqualTo(ChessEndReason.TimeUp));Assert.That(host.PreferredResultEventId,Is.EqualTo("chess.failed"));
  }
  [UnityTest] public IEnumerator LatestHintPreemptsEarlierSpeechNearTimeout()
  {
   yield return LoadReady();var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();
   var cast=UnityEngine.Object.FindFirstObjectByType<ChessCastView>();game.BeginAttempt();game.Machine.Tick(game.Machine.Clock.Limit-20f);
   for(int level=1;level<=3;level++)
   {
    game.RevealHint();
    yield return WaitFor(()=>game.Machine.Phase==ChessAttemptPhase.PlayerTurn && game.Machine.HintLevel==level,"hint "+level,8f);
    Assert.That(cast.BubbleText,Is.EqualTo(game.Machine.HintText));
   }
   Assert.That(game.Machine.CanMove,Is.True);
  }
  [UnityTest] public IEnumerator AHintIsNotReplayedAfterTheBoardChanges()
  {
   yield return LoadReady();var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();
   var cast=UnityEngine.Object.FindFirstObjectByType<ChessCastView>();var host=UnityEngine.Object.FindFirstObjectByType<GameplayDialogueHost>();game.BeginAttempt();
   game.RevealHint();game.RevealHint();
   yield return WaitFor(()=>game.Machine.Phase==ChessAttemptPhase.PlayerTurn && game.Machine.HintLevel==1,"hint shown",8f);
   string oldHint=game.Machine.HintText;Assert.That(oldHint,Is.Not.Null.And.Not.Empty);
   var move=Optimal(game.Machine);game.RequestMove(move.From,move.To);
   yield return WaitFor(()=>game.Machine.Phase!=ChessAttemptPhase.PlayerTurn,"move accepted");
   yield return new WaitForSeconds(.3f);
   Assert.That(cast.BubbleText,Is.Not.EqualTo(oldHint),"the old hint must not be replayed after the board changed");
   Assert.That(game.Machine.HintLevel,Is.Zero);
  }
  [UnityTest] public IEnumerator MissingHostKeepsBeginAndHintsPlayable()
  {
   yield return SceneManager.LoadSceneAsync("MG_ChessFinal");yield return null;
   var game=UnityEngine.Object.FindFirstObjectByType<ChessFinalController>();
   yield return WaitFor(()=>game.PuzzleReady,"the puzzle was not generated",20f);
   var opening=UnityEngine.Object.FindFirstObjectByType<GameplayOpeningFlow>();opening.ResetAttempt();
   var adapter=game.GetComponent<ChessDialogueAdapter>();
   adapter.Configure(game,null,UnityEngine.Object.FindFirstObjectByType<ChessCastView>());
   Assert.That(adapter.OpeningReady,Is.True);Assert.DoesNotThrow(()=>game.BeginAttempt());
   Assert.That(game.Machine.CanMove,Is.True);Assert.DoesNotThrow(()=>game.RevealHint());
  }
 }
}
