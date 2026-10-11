using System;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace KMA.Tests.Presentation
{
 public sealed class GameplayDialogueHostTests
 {
  static Type ViewType(){var t=typeof(MinigameGuideHost).Assembly.GetType("KMA.Gameplay.UI.GameplayDialogueView");Assert.That(t,Is.Not.Null);return t;}
  [Test] public void TwoSpeakersShareOneNonBlockingBubble()
  {
   var go=new GameObject("Dialogue test",typeof(RectTransform));
   try {
    var v=go.AddComponent(ViewType());
    ViewType().GetMethod("Configure").Invoke(v,new object[]{JourneyDialogueLibrary.LoadDefault(),go.transform});
    GameplayDialogueLibrary.LoadDefault().TryGet("frog.fall.first",out var e);
    ViewType().GetMethod("Show").Invoke(v,new object[]{e.Lines[0]});
    Assert.That(go.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text=="SV năm 1"),Is.True);
    ViewType().GetMethod("Show").Invoke(v,new object[]{e.Lines[1]});
    Assert.That(go.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text=="Giảng viên thể chất"),Is.True);
    Assert.That(go.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
    Assert.That(go.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="DialogueCard"),Is.EqualTo(1));
   } finally {UnityEngine.Object.DestroyImmediate(go);}
  }
  [Test] public void MissingLibraryReturnsFalseWithoutDisablingGame()
  {
   var type=typeof(MinigameGuideHost).Assembly.GetType("KMA.Gameplay.UI.GameplayDialogueHost");Assert.That(type,Is.Not.Null);
   var go=new GameObject("missing library");
   try {
    var host=go.AddComponent(type);type.GetMethod("Configure").Invoke(host,new object[]{null,null});
    LogAssert.Expect(LogType.Error,new Regex("Gameplay dialogue unavailable"));
    Assert.That((bool)type.GetMethod("Emit").Invoke(host,new object[]{"frog.fall.first",null,float.PositiveInfinity,null}),Is.False);
    Assert.That(((Behaviour)host).enabled,Is.True);
   } finally {UnityEngine.Object.DestroyImmediate(go);}
  }
  [Test] public void DestroyCancelsQueuedDialogue()
  {
   var type=typeof(MinigameGuideHost).Assembly.GetType("KMA.Gameplay.UI.GameplayDialogueHost");Assert.That(type,Is.Not.Null);
   var go=new GameObject("host");var host=go.AddComponent(type);
   var scheduler=type.GetField("scheduler",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(host);
   Assert.That((bool)type.GetMethod("Emit").Invoke(host,new object[]{"frog.fall.first",null,float.PositiveInfinity,null}),Is.True);
   Assert.That((bool)scheduler.GetType().GetProperty("IsIdle").GetValue(scheduler),Is.False);
   UnityEngine.Object.DestroyImmediate(go);
   Assert.That((bool)scheduler.GetType().GetProperty("IsIdle").GetValue(scheduler),Is.True);
  }
 }
}
