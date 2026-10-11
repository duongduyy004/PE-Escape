using KMA.EditorTools;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
namespace KMA.Tests.EditorTools
{
 public sealed class DialoguePresentationAuthoringTests
 {
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
