using KMA.EditorTools;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
namespace KMA.Tests.EditorTools
{
 public sealed class DialoguePresentationAuthoringTests
 {
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
