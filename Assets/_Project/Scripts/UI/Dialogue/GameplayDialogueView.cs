using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace KMA.Gameplay.UI
{
 public sealed class GameplayDialogueView : MonoBehaviour, IGameplayDialogueView
 {
  [SerializeField] JourneyDialogueLibrary cast;
  [SerializeField] GameObject card;
  [SerializeField] Image portrait,icon;
  [SerializeField] TMP_Text speaker,text;
  GameplayDialogueLine line;float poseTime;
  public void Configure(JourneyDialogueLibrary library,RectTransform safeArea)
  {
   cast=library;
   if(card!=null)return;
   var style=UITheme.Shared.Dialogue;
   var panel=UiKit.Panel(safeArea,"DialogueCard");card=panel.gameObject;
   UiKit.Place(panel.rectTransform,style.anchor,Vector2.one,style.offset,style.size);
   portrait=UiKit.Rect(panel.transform,"Portrait").gameObject.AddComponent<Image>();portrait.preserveAspect=true;
   UiKit.Place(portrait.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(style.padding,0),new Vector2(style.portraitWidth,style.size.y-2*style.padding));
   speaker=UiKit.Label(panel.transform,"SpeakerName","",style.nameSize,MinigameUiTheme.TextPrimary,TextAlignmentOptions.MidlineLeft);
   UiKit.Place(speaker.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(style.portraitWidth+2*style.padding,-style.padding),new Vector2(style.size.x-style.portraitWidth-4*style.padding-style.iconSize,style.iconSize));
   text=UiKit.Label(panel.transform,"SpeechText","",style.textSize,MinigameUiTheme.TextPrimary,TextAlignmentOptions.TopLeft);
   UiKit.Place(text.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(style.portraitWidth+2*style.padding,-style.padding-style.iconSize),new Vector2(style.size.x-style.portraitWidth-3*style.padding,style.size.y-style.iconSize-2*style.padding));
   text.textWrappingMode=TextWrappingModes.Normal;text.enableAutoSizing=true;text.fontSizeMin=style.textSize*.85f;text.fontSizeMax=style.textSize;
   icon=UiKit.Rect(panel.transform,"PointingIcon").gameObject.AddComponent<Image>();icon.preserveAspect=true;
   UiKit.Place(icon.rectTransform,Vector2.one,Vector2.one,new Vector2(-style.padding,-style.padding),Vector2.one*style.iconSize);
   foreach(var g in card.GetComponentsInChildren<Graphic>(true))g.raycastTarget=false;
   Hide();
  }
  public void Show(GameplayDialogueLine value)
  {
   if(card==null)return;line=value;poseTime=0;
   if(value==null){Hide();return;}
   card.SetActive(true);
   var character=cast.GetCharacter(value.Speaker==DialogueSpeaker.Student?"tan_thu":"co_the_chat");
   speaker.text=VietText.Fix(character.DisplayName);text.text=VietText.Fix(value.Text);
   portrait.sprite=value.Poses!=null && value.Poses.Length>0?value.Poses[0]:null;
   icon.sprite=value.Icon;icon.gameObject.SetActive(value.Icon!=null);
  }
  void Update()
  {
   if(line==null || line.Poses==null || line.Poses.Length<2 || !card.activeSelf)return;
   poseTime+=Time.deltaTime;int n=Mathf.Min(line.Poses.Length-1,Mathf.FloorToInt(poseTime/Mathf.Max(.01f,line.PoseStepSeconds)));portrait.sprite=line.Poses[n];
  }
  public void Hide(){line=null;if(card!=null)card.SetActive(false);}
 }
}
