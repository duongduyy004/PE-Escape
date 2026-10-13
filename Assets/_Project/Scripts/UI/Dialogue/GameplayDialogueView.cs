using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace KMA.Gameplay.UI
{
    public sealed class GameplayDialogueView : MonoBehaviour,IGameplayDialogueView
    {
        const float FadeSeconds=.22f;
        [SerializeField] JourneyDialogueLibrary cast;
        [SerializeField] GameObject card;
        [SerializeField] Image portrait,icon;
        [SerializeField] TMP_Text speaker,text;
        [SerializeField] RectTransform layoutRoot;
        [SerializeField] DialogueHudSlot slot;
        GameplayDialogueLine line;
        CanvasGroup fadeGroup;
        float fadeAge,fadeStartAlpha;
        bool fading;
        float poseTime;
        Vector2 lastSize;
        public bool IsArtwork(Image image)=>image==portrait || image==icon;
        void Awake()=>EnsureFadeGroup();
        void EnsureFadeGroup(){if(card!=null && fadeGroup==null){fadeGroup=card.GetComponent<CanvasGroup>();if(fadeGroup==null)fadeGroup=card.AddComponent<CanvasGroup>();}}
        public void SetSlot(DialogueHudSlot value){slot=value;RefreshLayout();}
        public void Configure(JourneyDialogueLibrary library,RectTransform safeArea)
        {
            cast=library;layoutRoot=safeArea;
            if(card==null)
            {
                card=UiKit.Panel(safeArea,"DialogueCard").gameObject;
                portrait=UiKit.Rect(card.transform,"Portrait").gameObject.AddComponent<Image>();portrait.preserveAspect=true;
                speaker=UiKit.Label(card.transform,"SpeakerName","",MinigameUiTheme.Caption,MinigameUiTheme.TextPrimary,TextAlignmentOptions.MidlineLeft);
                text=UiKit.Label(card.transform,"SpeechText","",MinigameUiTheme.Caption,MinigameUiTheme.TextPrimary,TextAlignmentOptions.TopLeft);
                icon=UiKit.Rect(card.transform,"PointingIcon").gameObject.AddComponent<Image>();icon.preserveAspect=true;
            }
            EnsureFadeGroup();
            RefreshLayout();Hide();
        }
        void Start()
        {
            if(layoutRoot==null && card!=null)layoutRoot=card.transform.parent as RectTransform;
            RefreshLayout();
        }
        void RefreshLayout()
        {
            if(card==null || layoutRoot==null)return;
            var style=UITheme.Shared.Dialogue;
            var size=style.size;var anchor=slot==DialogueHudSlot.Volleyball?style.volleyballAnchor:style.anchor;
            if(slot==DialogueHudSlot.Frog)anchor=style.frogAnchor;
            var offset=style.offset;var pivot=Vector2.one;float header=style.iconSize;
            if(slot==DialogueHudSlot.Result || slot==DialogueHudSlot.GameOver)
            {
                anchor=slot==DialogueHudSlot.Result?style.resultAnchor:style.gameOverAnchor;offset=slot==DialogueHudSlot.Result?style.resultOffset:style.gameOverOffset;pivot=new Vector2(1f,.5f);size=style.gameOverSize;header=style.gameOverNameHeight;
                if(layoutRoot.rect.width>style.gameOverContentWidth)
                    size.x=Mathf.Min(size.x,(layoutRoot.rect.width-style.gameOverContentWidth)*.5f-Mathf.Abs(offset.x)-style.padding);
            }
            UiKit.Place((RectTransform)card.transform,anchor,pivot,offset,size);
            UiKit.Place(portrait.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(style.padding,0),new Vector2(style.portraitWidth,size.y-2*style.padding));
            UiKit.Place(speaker.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(style.portraitWidth+2*style.padding,-style.padding),new Vector2(size.x-style.portraitWidth-3*style.padding,header));
            speaker.fontSize=Mathf.Max(style.nameSize,MinigameUiTheme.MinimumFontSize);speaker.textWrappingMode=TextWrappingModes.Normal;
            UiKit.FitLabel(speaker, speaker.fontSize);
            UiKit.Place(text.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(style.portraitWidth+2*style.padding,-style.padding-header),new Vector2(size.x-style.portraitWidth-3*style.padding,size.y-header-2*style.padding));
            text.textWrappingMode=TextWrappingModes.Normal;text.enableAutoSizing=true;text.fontSizeMin=MinigameUiTheme.MinimumFontSize;text.fontSizeMax=Mathf.Max(style.textSize,MinigameUiTheme.MinimumFontSize);
            if(slot==DialogueHudSlot.Result || slot==DialogueHudSlot.GameOver)
            {
                UiKit.Place(portrait.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(style.padding,-style.padding),new Vector2(style.portraitWidth,header));
                UiKit.Place(text.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(style.padding,-style.padding-header),new Vector2(size.x-2*style.padding,size.y-header-2*style.padding));
            }
            UiKit.Place(icon.rectTransform,Vector2.one,Vector2.one,new Vector2(-style.padding,-style.padding),Vector2.one*style.iconSize);
            foreach(var graphic in card.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            lastSize=layoutRoot.rect.size;
        }
        public void Show(GameplayDialogueLine value)
        {
            if(card==null)return;line=value;poseTime=0;
            if(value==null){Hide();return;}
            EnsureFadeGroup();fading=false;fadeAge=0;fadeGroup.alpha=1;fadeGroup.interactable=false;fadeGroup.blocksRaycasts=false;
            RefreshLayout();card.SetActive(true);
            var character=cast.GetCharacter(value.Speaker==DialogueSpeaker.Student?"tan_thu":"co_the_chat");
            speaker.text=VietText.Fix(character.DisplayName);text.text=VietText.Fix(value.Text);
            portrait.sprite=value.Poses!=null && value.Poses.Length>0?value.Poses[0]:null;
            icon.sprite=value.Icon;icon.gameObject.SetActive(value.Icon!=null);
        }
        void Update()
        {
            if(layoutRoot!=null && lastSize!=layoutRoot.rect.size)RefreshLayout();
            if(fading)
            {
                fadeAge+=Time.unscaledDeltaTime;
                fadeGroup.alpha=fadeStartAlpha*(1f-Mathf.Clamp01(fadeAge/FadeSeconds));
                if(fadeAge>=FadeSeconds)Hide();
            }
            if(line==null || line.Poses==null || line.Poses.Length<2 || !card.activeSelf)return;
            poseTime+=Time.deltaTime;
            int n=Mathf.Min(line.Poses.Length-1,Mathf.FloorToInt(poseTime/Mathf.Max(.01f,line.PoseStepSeconds)));portrait.sprite=line.Poses[n];
        }
        public void FadeOut(){line=null;if(card==null)return;EnsureFadeGroup();if(!card.activeSelf){Hide();return;}fading=true;fadeAge=0;fadeStartAlpha=fadeGroup.alpha;fadeGroup.interactable=false;fadeGroup.blocksRaycasts=false;}
        public void Hide(){line=null;fading=false;fadeAge=0;if(card!=null){EnsureFadeGroup();fadeGroup.alpha=0;fadeGroup.interactable=false;fadeGroup.blocksRaycasts=false;card.SetActive(false);}}
    }
}
