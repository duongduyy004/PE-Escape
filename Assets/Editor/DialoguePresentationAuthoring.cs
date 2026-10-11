using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using UnityEngine;
using UnityEngine.UI;
namespace KMA.EditorTools
{
 public enum DialogueHudSlot { Sprint,Volleyball,Football,Frog,Result,GameOver }
 public static class DialoguePresentationAuthoring
 {
  public static GameplayDialogueHost Configure(Transform root,DialogueHudSlot slot)
  {
   var existing=root.GetComponentInChildren<GameplayDialogueHost>(true);
   if(existing!=null)return existing;
   var parent=UiKit.Rect(root,"GameplayDialogue");
   var canvas=parent.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=600;
   var scale=parent.gameObject.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
   var safe=UiKit.Rect(parent,"SafeArea");UiKit.Stretch(safe);safe.gameObject.AddComponent<SafeAreaFitter>();
   var view=parent.gameObject.AddComponent<GameplayDialogueView>();view.Configure(JourneyDialogueLibrary.LoadDefault(),safe);
   var host=parent.gameObject.AddComponent<GameplayDialogueHost>();host.Configure(GameplayDialogueLibrary.LoadDefault(),view);
   UnityEditor.EditorUtility.SetDirty(UITheme.Shared);
   return host;
  }
 }
}
