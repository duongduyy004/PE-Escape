using System.Linq;
using KMA.EditorTools;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
namespace KMA.Tests.EditorTools
{
 public sealed class DialoguePresentationAuthoringTests
 {
        [TestCase("MG_Volleyball")] [TestCase("MG_FrogJump")]
        public void SceneConfiguratorsRetainResultBindingAfterHudAssembly(string name)
        {
            if(name=="MG_FrogJump")FrogJumpSceneConfigurator.BuildScene();else VolleyballSceneConfigurator.BuildScene();
            var panel=Object.FindFirstObjectByType<KMA.Gameplay.UI.ResultPanel>(FindObjectsInactive.Include);var host=Object.FindFirstObjectByType<GameplayDialogueHost>();
            Assert.That(new UnityEditor.SerializedObject(panel).FindProperty("dialogueHost").objectReferenceValue,Is.SameAs(host));
        }
        [TestCase("MG_Sprint")] [TestCase("MG_Volleyball")] [TestCase("MG_Football")] [TestCase("MG_ChessFinal")] [TestCase("MG_FrogJump")]
        public void ReopenedResultsUseTheSameSceneDialogueHost(string name)
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+name+".unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try {
                var panels=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<KMA.Gameplay.UI.ResultPanel>(true));
                var host=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameplayDialogueHost>(true)).Single();
                foreach(var panel in panels){var fields=new UnityEditor.SerializedObject(panel);Assert.That(fields.FindProperty("dialogueHost").objectReferenceValue,Is.SameAs(host),name);Assert.That(fields.FindProperty("dialogueView").objectReferenceValue,Is.Not.Null,name);}
            } finally {UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
        }
        [TestCase(1440f)] [TestCase(1920f)] [TestCase(2400f)]
        public void ChessSpeechAndNamesStayInsideSideColumnsAndBelowMistakes(float width)
        {
            var root=new GameObject("Chess layout",typeof(RectTransform));
            try {
                var area=(RectTransform)root.transform;area.sizeDelta=new Vector2(width,1080);
                UnityEngine.UI.Image Img(string name)=>KMA.UI.Kit.UiKit.Rect(root.transform,name).gameObject.AddComponent<UnityEngine.UI.Image>();
                var a=Img("Student");var b=Img("Teacher");var bubble=Img("SpeechBubble");
                KMA.UI.Kit.UiKit.Place(bubble.rectTransform,new Vector2(.9f,.72f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(320,190));
                var text=KMA.UI.Kit.UiKit.Label(bubble.transform,"Text","",28,Color.white);KMA.UI.Kit.UiKit.Stretch(text.rectTransform);text.rectTransform.offsetMin=new Vector2(18,18);text.rectTransform.offsetMax=new Vector2(-18,-18);
                var left=KMA.UI.Kit.UiKit.Label(root.transform,"StudentName","SV năm 1",40,Color.white);var right=KMA.UI.Kit.UiKit.Label(root.transform,"TeacherName","Giảng viên thể chất",40,Color.white);
                KMA.UI.Kit.UiKit.Place(left.rectTransform,new Vector2(.1f,.86f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(320,60));KMA.UI.Kit.UiKit.Place(right.rectTransform,new Vector2(.9f,.86f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(320,60));
                var cast=root.AddComponent<KMA.Gameplay.Chess.ChessCastView>();cast.Configure(a,b,new KMA.Gameplay.Chess.ChessCastView.Pose[0],new KMA.Gameplay.Chess.ChessCastView.Pose[0],bubble.gameObject,text);
                foreach(var line in GameplayDialogueLibrary.LoadDefault().Events.Where(e=>e.Id.StartsWith("chess.") || e.Id.StartsWith("ch.extra.")).SelectMany(e=>e.Lines))
                {
                    cast.Show(line);var corners=new Vector3[4];bubble.rectTransform.GetWorldCorners(corners);
                    Assert.That(corners[0].x,Is.GreaterThanOrEqualTo(area.rect.xMin+8));Assert.That(corners[2].x,Is.LessThanOrEqualTo(area.rect.xMax-8));
                    Assert.That(corners[2].y,Is.LessThanOrEqualTo((.78f-.5f)*1080-30-8),"must clear mistake counter");
                    if(line.Speaker==DialogueSpeaker.Student)Assert.That(corners[2].x,Is.LessThanOrEqualTo(-402-8));else Assert.That(corners[0].x,Is.GreaterThanOrEqualTo(402+8));
                    foreach(var label in new[]{text,right,left}){label.ForceMeshUpdate(true);Assert.That(label.GetPreferredValues(label.text,label.rectTransform.rect.width,0).y,Is.LessThanOrEqualTo(label.rectTransform.rect.height+1),label.name);}
                }
            } finally {Object.DestroyImmediate(root);}
        }
        [TestCase(1440f)] [TestCase(1920f)] [TestCase(2400f)]
        public void GameOverSpeechFitsAtSharedFontFloor(float width)
        {
            var root=new UnityEngine.GameObject("dialogue-layout",typeof(UnityEngine.RectTransform));
            try {
                var rect=(UnityEngine.RectTransform)root.transform;rect.sizeDelta=new UnityEngine.Vector2(width,1080);
                var view=root.AddComponent<KMA.Gameplay.UI.GameplayDialogueView>();view.SetSlot(KMA.Gameplay.UI.DialogueHudSlot.GameOver);view.Configure(KMA.Gameplay.JourneyDialogueLibrary.LoadDefault(),rect);
                var lines=KMA.Gameplay.UI.GameplayDialogueLibrary.LoadDefault().Resolve("gameover.comment",null).Lines;
                foreach(var line in lines){view.Show(line);foreach(var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true)){text.ForceMeshUpdate(true);UnityEngine.Vector2 required=text.GetPreferredValues(text.text,text.rectTransform.rect.width,0);NUnit.Framework.Assert.That(required.y,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1),text.name+" width "+width);}}
            } finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        [TestCase(1440f)] [TestCase(1920f)] [TestCase(2400f)]
        public void ResultSpeechClearsModalAndRendersAboveScrim(float width)
        {
            var root=new GameObject("Result",typeof(RectTransform));
            try {
                var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(width,1080);
                var panel=root.AddComponent<KMA.Gameplay.UI.ResultPanel>();panel.BindDialogue(null);
                var view=root.GetComponentInChildren<GameplayDialogueView>(true);
                var canvas=view.GetComponent<Canvas>();Assert.That(canvas,Is.Not.Null,"speech needs its own canvas above result scrim");
                Assert.That(canvas.overrideSorting,Is.True);Assert.That(canvas.sortingOrder,Is.GreaterThan(500));
                foreach(var line in GameplayDialogueLibrary.LoadDefault().Resolve("frog.timeout",null).Lines)
                {
                    view.Show(line);var card=(RectTransform)view.transform.Find("DialogueCard");var corners=new Vector3[4];card.GetWorldCorners(corners);
                    Assert.That(corners[0].x,Is.GreaterThan(450+8),"speech must clear centered 900px result modal");
                    Assert.That(corners[2].x,Is.LessThanOrEqualTo(width/2-12));
                    foreach(var label in view.GetComponentsInChildren<TMPro.TMP_Text>(true)){label.ForceMeshUpdate(true);Assert.That(label.GetPreferredValues(label.text,label.rectTransform.rect.width,0).y,Is.LessThanOrEqualTo(label.rectTransform.rect.height+1),label.name);}
                }
            } finally {Object.DestroyImmediate(root);}
        }
        [TestCase(1080f)] [TestCase(1247f)]
        public void FrogSpeechClearsTopProgressCard(float height)
        {
            var root=new GameObject("Frog layout",typeof(RectTransform));
            try {
                var area=(RectTransform)root.transform;area.sizeDelta=new Vector2(1920,height);
                var view=root.AddComponent<GameplayDialogueView>();view.SetSlot(DialogueHudSlot.Frog);view.Configure(KMA.Gameplay.JourneyDialogueLibrary.LoadDefault(),area);
                view.Show(GameplayDialogueLibrary.LoadDefault().Resolve("frog.fall.first",null).Lines[0]);
                var corners=new Vector3[4];((RectTransform)root.transform.Find("DialogueCard")).GetWorldCorners(corners);
                Assert.That(area.rect.yMax-corners[2].y,Is.GreaterThanOrEqualTo(190),"clear progress card and its shadow");
            } finally {Object.DestroyImmediate(root);}
        }
  [Test] public void DialogueFontNeverShrinksBelowSharedFloor()
  {
   var root=new GameObject("font floor");
   try {
    DialoguePresentationAuthoring.Configure(root.transform,DialogueHudSlot.Sprint);
    foreach(var label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
     Assert.That(label.enableAutoSizing?label.fontSizeMin:label.fontSize,Is.GreaterThanOrEqualTo(KMA.UI.Kit.MinigameUiTheme.MinimumFontSize),label.name);
   } finally {Object.DestroyImmediate(root);}
  }
  [Test] public void ReauthorKeepsOnePersistentView()
  {
   var root=new GameObject("authoring");
   try {
    var first=DialoguePresentationAuthoring.Configure(root.transform,DialogueHudSlot.Sprint);
    var second=DialoguePresentationAuthoring.Configure(root.transform,DialogueHudSlot.Sprint);
    Assert.That(second,Is.SameAs(first));Assert.That(root.GetComponentsInChildren<GameplayDialogueHost>(true).Length,Is.EqualTo(1));
   } finally {Object.DestroyImmediate(root);}
  }
 }
}
