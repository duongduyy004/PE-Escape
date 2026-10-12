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
   } finally {Object.DestroyImmediate(root);}
  }
  [UnityTest] public IEnumerator AllCalledPoseKeysExist()
  {
   yield return SceneManager.LoadSceneAsync("MG_ChessFinal");yield return null;
   var cast=Object.FindFirstObjectByType<ChessCastView>();
   foreach(var key in new[]{"fear","think","happy","disappointed"})Assert.DoesNotThrow(()=>cast.SetStudent(key),key);
   foreach(var key in new[]{"command","angry","scoreWrite","handsOnHips"})Assert.DoesNotThrow(()=>cast.SetTeacher(key),key);
  }
  IEnumerator LoadReady()
  {
   Time.timeScale=1;yield return SceneManager.LoadSceneAsync("MG_ChessFinal");yield return null;
   var opening=Object.FindFirstObjectByType<GameplayOpeningFlow>();float until=Time.realtimeSinceStartup+6;
   yield return new WaitUntil(()=>opening.IsComplete || Time.realtimeSinceStartup>until);Assert.That(opening.IsComplete,Is.True);
  }
  [UnityTest] public IEnumerator OpeningOccursOnceBeforeBeginExtraOnlyForM2001()
  {
   yield return SceneManager.LoadSceneAsync("MG_ChessFinal");yield return null;
   var game=Object.FindFirstObjectByType<ChessFinalController>();var cast=Object.FindFirstObjectByType<ChessCastView>();
   Assert.That(game.Machine.Clock.Elapsed,Is.Zero);game.BeginAttempt();Assert.That(game.Machine.Phase,Is.EqualTo(ChessFinalPhase.Intro));
   var opening=Object.FindFirstObjectByType<GameplayOpeningFlow>();yield return new WaitUntil(()=>opening.IsComplete);
   game.BeginAttempt();Assert.That(game.Machine.Puzzle.id,Is.EqualTo("m2_001"));Assert.That(cast.BubbleSpeaker,Is.EqualTo(DialogueSpeaker.Student));
   Assert.That(cast.BubbleText,Does.Contain("tượng"));
  }
  [UnityTest] public IEnumerator IllegalAndDataErrorKeepPosesAndDoNotWhistle()
  {
   yield return LoadReady();var game=Object.FindFirstObjectByType<ChessFinalController>();var cast=Object.FindFirstObjectByType<ChessCastView>();game.BeginAttempt();
   var host=Object.FindFirstObjectByType<GameplayDialogueHost>();host.Clear();cast.SetStudent("disappointed");cast.SetTeacher("command");
   int whistles=0;System.Action<GameSound> sound=x=>{if(x==GameSound.Whistle)whistles++;};GameAudio.Requested+=sound;
   var reply=game.Machine.Puzzle.nodes[0].moves[0].replies[0];string original=reply.uci;
   try {
    game.Machine.Submit(new ChessMove(0,0));Assert.That(cast.StudentPose,Is.EqualTo("disappointed"));Assert.That(cast.TeacherPose,Is.EqualTo("command"));
    reply.uci="bad";LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("Chess puzzle data error"));ChessMove.TryParseUci("e6h6",out var first);game.Machine.Submit(first);
    Assert.That(game.Machine.Mistakes,Is.Zero);Assert.That(cast.StudentPose,Is.EqualTo("disappointed"));Assert.That(cast.TeacherPose,Is.EqualTo("command"));Assert.That(whistles,Is.Zero);
   } finally {reply.uci=original;GameAudio.Requested-=sound;}
  }
  [UnityTest] public IEnumerator SecondMistakeAllowsCorrection()
  {
   yield return LoadReady();var game=Object.FindFirstObjectByType<ChessFinalController>();var cast=Object.FindFirstObjectByType<ChessCastView>();game.BeginAttempt();
   var wrong=MoveGenerator.LegalMoves(game.Machine.Position).First(m=>m.ToUci()!="e6h6" && !MoveGenerator.IsCheckmate(game.Machine.Position.Apply(m)));
   game.Machine.Submit(wrong);game.Machine.Submit(wrong);yield return new WaitForSeconds(.7f);
   Assert.That(game.Machine.CanMove,Is.True);Assert.That(cast.TeacherPose,Is.EqualTo("angry"));
   ChessMove.TryParseUci("e6h6",out var first);game.RequestMove(first.From,first.To);
   float until=Time.realtimeSinceStartup+3;yield return new WaitUntil(()=>game.Machine.CanMove || Time.realtimeSinceStartup>until);
   Assert.That(game.Machine.PlayerMovesMade,Is.EqualTo(1));Assert.That(game.Machine.Mistakes,Is.EqualTo(2));Assert.That(game.Machine.CanMove,Is.True);
  }
  [UnityTest] public IEnumerator ResolutionKeepsPointNineSecondsAndDropsReliefAfterResultOpens()
  {
   yield return LoadReady();var game=Object.FindFirstObjectByType<ChessFinalController>();game.BeginAttempt();
   ChessMove.TryParseUci("e6h6",out var first);game.RequestMove(first.From,first.To);
   yield return new WaitUntil(()=>game.Machine.CanMove);int completed=0;game.Completed+=r=>completed++;
   ChessMove.TryParseUci("h6g7",out var last);game.RequestMove(last.From,last.To);
   yield return new WaitForSeconds(.7f);Assert.That(completed,Is.Zero);
   yield return new WaitForSeconds(.35f);Assert.That(completed,Is.EqualTo(1));
   var host=Object.FindFirstObjectByType<GameplayDialogueHost>();Assert.That(host.PreferredResultEventId,Is.EqualTo("chess.solved"));
  }
  [Test] public void HintsFollowQueenE6H6ThenH6G7()
  {
   var p=ChessPuzzleLibrary.ForDifficulty(ChallengeDifficulty.Normal);var position=ChessPosition.FromFen(p.startFen);
   Assert.That(PuzzleHints.For(p,0,position,2),Is.EqualTo("Xem lại quân Hậu ở e6."));
   Assert.That(PuzzleHints.For(p,0,position,3),Is.EqualTo("Đi Hậu từ e6 đến h6. Tôi đã chỉ rõ rồi."));
   ChessMove.TryParseUci("e6h6",out var first);ChessMove.TryParseUci("h8g8",out var reply);position=position.Apply(first).Apply(reply);
   Assert.That(PuzzleHints.For(p,1,position,2),Does.Contain("Hậu ở h6"));Assert.That(PuzzleHints.For(p,1,position,3),Does.Contain("h6 đến g7"));
  }
 }
}
