#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using KMA.Gameplay.UI;
namespace KMA.EditorTools
{
 public static class DialogueIntegrationAuthoring
 {
  public static void RepairSavedBindings()
  {
   foreach(string name in new[]{"MG_Sprint","MG_Volleyball","MG_Football","MG_ChessFinal","MG_FrogJump"})
   {
    var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+name+".unity");
    var source=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<KMA.Gameplay.MinigameBase>(true)).Single();
    var host=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameplayDialogueHost>(true)).Single();DialoguePresentationAuthoring.BindResult(source,host);
    foreach(var cast in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<KMA.Gameplay.Chess.ChessCastView>(true))){cast.RefreshLayout();EditorUtility.SetDirty(cast);}
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
   RefreshSavedViews();
  }
  public static void RefreshSavedViews()
  {
   foreach(string name in new[]{"MG_Sprint","MG_Volleyball","MG_Football","MG_ChessFinal","MG_FrogJump","Celebration","GameOver"})
   {
    var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+name+".unity");
    foreach(var root in scene.GetRootGameObjects())foreach(var view in root.GetComponentsInChildren<GameplayDialogueView>(true))
    {
     var serialized=new SerializedObject(view);var layout=serialized.FindProperty("layoutRoot").objectReferenceValue as RectTransform;
     view.Configure(KMA.Gameplay.JourneyDialogueLibrary.LoadDefault(),layout);PrefabUtility.RecordPrefabInstancePropertyModifications(view);
    }
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
   AssetDatabase.SaveAssets();
  }
  public static void RegenerateAndVerify()
  {
   string before=Semantic();BuildAll();string after=Semantic();
   File.WriteAllText("Builds/TestResults/dialogue-semantic-before.txt",before);File.WriteAllText("Builds/TestResults/dialogue-semantic-after.txt",after);
   if(before!=after)throw new InvalidOperationException("Dialogue semantic references changed during regeneration; compare dialogue-semantic files.");
   Debug.Log("[KMA] Dialogue regeneration preserved semantic references, content, and tuning.");
  }
  static string ObjectKey(UnityEngine.Object value)
  {
   if(value==null)return "null";
   string path=AssetDatabase.GetAssetPath(value);if(!string.IsNullOrEmpty(path))return path+":"+value.name;
   if(value is Component component)return Hierarchy(component.transform)+":"+component.GetType().FullName;
   if(value is GameObject game)return Hierarchy(game.transform);
   return value.GetType().FullName+":"+value.name;
  }
  static string Hierarchy(Transform t)=>t.parent==null?t.name:Hierarchy(t.parent)+"/"+t.name;
  static void Properties(List<string> values,string key,UnityEngine.Object target)
  {
   var iterator=new SerializedObject(target).GetIterator();while(iterator.Next(true))
   {
    if(iterator.propertyPath.EndsWith(".m_FileID") || iterator.propertyPath.EndsWith(".m_PathID"))continue;
    string v=null;
    switch(iterator.propertyType){case SerializedPropertyType.ObjectReference:v=ObjectKey(iterator.objectReferenceValue);break;case SerializedPropertyType.String:v=iterator.stringValue;break;case SerializedPropertyType.Float:v=iterator.floatValue.ToString("R",System.Globalization.CultureInfo.InvariantCulture);break;case SerializedPropertyType.Boolean:v=iterator.boolValue.ToString();break;case SerializedPropertyType.Integer:case SerializedPropertyType.Enum:case SerializedPropertyType.ArraySize:v=iterator.intValue.ToString();break;}
    if(v!=null)values.Add(key+"|"+iterator.propertyPath+"="+v);
   }
  }
  static string Semantic()
  {
   var values=new List<string>();
   foreach(string name in new[]{"MG_Sprint","MG_Volleyball","MG_Football","MG_ChessFinal","MG_FrogJump","Celebration","GameOver"})
   {
    var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+name+".unity");
    foreach(var root in scene.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true))
    {
     if(c==null)throw new InvalidOperationException(name+" has missing script");string type=c.GetType().Name;
     if(type.Contains("Dialogue") || type=="ChessCastView" || type=="FrogJumpView" || type=="CelebrationSceneController" || type=="StartLecturer" || type=="ResultPanel" || c is TMPro.TMP_Text)
      Properties(values,name+"/"+Hierarchy(c.transform)+"/"+type,c);
    }
   }
   foreach(string folder in new[]{"Assets/_Project/Resources/Journey","Assets/_Project/ScriptableObjects"})foreach(string guid in AssetDatabase.FindAssets("t:ScriptableObject",new[]{folder}))
   {
    string path=AssetDatabase.GUIDToAssetPath(guid);if(path.Contains("JourneyEmoji"))continue;
    Properties(values,path,AssetDatabase.LoadAssetAtPath<ScriptableObject>(path));
   }
   values.Sort(StringComparer.Ordinal);return string.Join("\n",values);
  }
  public static void BuildAll()
  {
   StudentJourneyContentBuilder.BuildAll();GameplayDialogueContentBuilder.Build();
   SprintSceneConfigurator.ConfigureDialogue();VolleyballSceneConfigurator.BuildScene();FootballSceneConfigurator.BuildScene();ChessFinalSceneConfigurator.BuildScene();FrogJumpSceneConfigurator.BuildScene();
   CelebrationSceneConfigurator.BuildScene();StartLecturerAuthoring.AddToSportsMinigames();ShellSceneAuthoring.Apply();AssetDatabase.SaveAssets();
  }
 }
}
#endif
