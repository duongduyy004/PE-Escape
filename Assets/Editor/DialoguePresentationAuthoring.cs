using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using UnityEngine;
using UnityEngine.UI;
namespace KMA.EditorTools
{
 public static class DialoguePresentationAuthoring
 {
  public static void WireOpening(MinigameBase source,GameplayDialogueHost host,Component adapter)
  {
   var flow=source.GetComponent<GameplayOpeningFlow>() ?? source.gameObject.AddComponent<GameplayOpeningFlow>();
   var o=new UnityEditor.SerializedObject(flow);o.FindProperty("game").objectReferenceValue=source;o.FindProperty("host").objectReferenceValue=host;o.ApplyModifiedPropertiesWithoutUndo();
   BindResult(source,host);
   var a=new UnityEditor.SerializedObject(adapter);a.FindProperty("controller").objectReferenceValue=source;a.FindProperty("host").objectReferenceValue=host;a.FindProperty("opening").objectReferenceValue=flow;a.ApplyModifiedPropertiesWithoutUndo();
  }
  public static void BindResult(MinigameBase source,GameplayDialogueHost host)
  {
   foreach(var root in source.gameObject.scene.GetRootGameObjects())
    foreach(var panel in root.GetComponentsInChildren<ResultPanel>(true))
    {
     panel.BindDialogue(host);
     var p=new UnityEditor.SerializedObject(panel);p.FindProperty("dialogueHost").objectReferenceValue=host;
     p.FindProperty("frogBalance").objectReferenceValue=UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/ScriptableObjects/FrogJump/FrogJumpBalance.asset");
     p.ApplyModifiedPropertiesWithoutUndo();
     UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
    }
  }
  public static GameplayDialogueHost Configure(Transform root,DialogueHudSlot slot)
  {
   var existing=root.GetComponentInChildren<GameplayDialogueHost>(true);
   if(existing!=null)
   {
    var oldView=existing.GetComponent<GameplayDialogueView>();var oldSafe=existing.transform.Find("SafeArea") as RectTransform;
    if(oldView!=null && oldSafe!=null){oldView.SetSlot(slot);oldView.Configure(JourneyDialogueLibrary.LoadDefault(),oldSafe);}
    return existing;
   }
   var parent=UiKit.Rect(root,"GameplayDialogue");
   var canvas=parent.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=600;
   var scale=parent.gameObject.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
   var safe=UiKit.Rect(parent,"SafeArea");UiKit.Stretch(safe);safe.gameObject.AddComponent<SafeAreaFitter>();
   var view=parent.gameObject.AddComponent<GameplayDialogueView>();view.SetSlot(slot);view.Configure(JourneyDialogueLibrary.LoadDefault(),safe);
   var host=parent.gameObject.AddComponent<GameplayDialogueHost>();host.Configure(GameplayDialogueLibrary.LoadDefault(),view);
   UnityEditor.EditorUtility.SetDirty(UITheme.Shared);
   return host;
  }
 }
}
