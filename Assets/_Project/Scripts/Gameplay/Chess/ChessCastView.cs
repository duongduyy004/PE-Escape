using System;
using System.Collections;
using System.Linq;
using TMPro;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.Chess
{
    public sealed class ChessCastView : MonoBehaviour, IGameplayDialogueView
    {
        [Serializable]
        public struct Pose
        {
            public string name;
            public Sprite sprite;
        }

        [SerializeField] Image student;
        [SerializeField] Image teacher;
        [SerializeField] Pose[] studentPoses;
        [SerializeField] Pose[] teacherPoses;
        [SerializeField] GameObject bubble;
        [SerializeField] TMP_Text bubbleText;
        [SerializeField] float stepSeconds = .3f;
        float bubbleUntil;
        [SerializeField] TMP_Text speakerName;
        [SerializeField] GameplayDialogueHost dialogueHost;
        [SerializeField] TMP_Text studentName,teacherName;
        [SerializeField] RectTransform boardFrame;
        Vector2 lastLayoutSize;
        GameplayDialogueLine spoken;
        float poseTime;
        public DialogueSpeaker BubbleSpeaker { get; private set; }
        public void BindDialogue(GameplayDialogueHost host) => dialogueHost=host;
        void EnsureSpeakerName()
        {
            if(speakerName!=null || bubble==null)return;
            speakerName=UiKit.Label(bubble.transform,"SpeakerName","",18,MinigameUiTheme.TextPrimary,TextAlignmentOptions.MidlineLeft);
            RefreshLayout();
        }
        public void RefreshLayout()
        {
            if(bubble==null || !(transform is RectTransform root))return;
            if(studentName==null)studentName=transform.Find("StudentName")?.GetComponent<TMP_Text>();
            if(teacherName==null)teacherName=transform.Find("TeacherName")?.GetComponent<TMP_Text>();
            if(boardFrame==null)boardFrame=transform.Find("BoardFrame") as RectTransform;
            var style=UITheme.Shared.Dialogue;float available=(root.rect.width-(boardFrame!=null?boardFrame.rect.width:style.chessBoardWidth))*.5f-2*style.chessPadding;
            float width=root.rect.width>0?Mathf.Min(style.chessBubbleSize.x,Mathf.Max(120f,available)):style.chessBubbleSize.x;
            float edge=BubbleSpeaker==DialogueSpeaker.Student?0:1;float offset=edge==0?style.chessPadding:-style.chessPadding;
            UiKit.Place((RectTransform)bubble.transform,new Vector2(edge,style.chessSpeechY),new Vector2(edge,.5f),new Vector2(offset,0),new Vector2(width,style.chessBubbleSize.y));
            if(speakerName!=null)UiKit.Place(speakerName.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(style.chessPadding,-style.chessPadding),new Vector2(width-2*style.chessPadding,style.chessSpeakerHeight));
            UiKit.Stretch(bubbleText.rectTransform);bubbleText.rectTransform.offsetMin=new Vector2(style.chessPadding,style.chessPadding);bubbleText.rectTransform.offsetMax=new Vector2(-style.chessPadding,-style.chessPadding-style.chessSpeakerHeight);
            bubbleText.enableAutoSizing=true;bubbleText.fontSizeMin=MinigameUiTheme.MinimumFontSize;bubbleText.fontSizeMax=Mathf.Max(style.textSize,MinigameUiTheme.MinimumFontSize);
            void Name(TMP_Text label,float side)
            {
                if(label==null)return;UiKit.Place(label.rectTransform,new Vector2(side,1),new Vector2(side,1),new Vector2(side==0?style.chessPadding:-style.chessPadding,-style.chessNameTop),new Vector2(width,style.chessNameHeight));
                label.textWrappingMode=TextWrappingModes.Normal;label.enableAutoSizing=true;label.fontSizeMin=MinigameUiTheme.MinimumFontSize;label.fontSizeMax=MinigameUiTheme.MinimumFontSize;label.fontSize=MinigameUiTheme.MinimumFontSize;
            }
            Name(studentName,0);Name(teacherName,1);
            foreach(var graphic in bubble.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;student.raycastTarget=teacher.raycastTarget=false;
            lastLayoutSize=root.rect.size;
        }
        void Start()=>RefreshLayout();
        public void Show(GameplayDialogueLine line)
        {
            if(line==null){Hide();return;}
            if(teacherSequence!=null)StopCoroutine(teacherSequence);teacherSequence=null;
            EnsureSpeakerName();spoken=line;poseTime=0;BubbleSpeaker=line.Speaker;
            bubble.SetActive(true);bubbleText.text=VietText.Fix(line.Text);
            speakerName.text=VietText.Fix(JourneyDialogueLibrary.LoadDefault().GetCharacter(line.Speaker==DialogueSpeaker.Student?"tan_thu":"co_the_chat").DisplayName);
            RefreshLayout();
            foreach(var graphic in bubble.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            student.raycastTarget=teacher.raycastTarget=false;
            ApplyLinePose();
        }
        public void Hide(){spoken=null;if(bubble!=null)bubble.SetActive(false);}
        void ApplyLinePose()
        {
            if(spoken?.Poses==null || spoken.Poses.Length==0)return;
            var sprite=spoken.Poses[Mathf.Min(spoken.Poses.Length-1,Mathf.FloorToInt(poseTime/Mathf.Max(.01f,spoken.PoseStepSeconds)))];
            var set=spoken.Speaker==DialogueSpeaker.Student?studentPoses:teacherPoses;
            string key=(set ?? Array.Empty<Pose>()).FirstOrDefault(p=>p.sprite==sprite && sprite.name.EndsWith("_"+p.name)).name;
            key ??= (set ?? Array.Empty<Pose>()).FirstOrDefault(p=>p.sprite==sprite).name;
            if(spoken.Speaker==DialogueSpeaker.Student){student.sprite=sprite;StudentPose=key??sprite.name;}
            else{teacher.sprite=sprite;TeacherPose=key??sprite.name;}
        }
        Coroutine teacherSequence;

        public string StudentPose { get; private set; }
        public string TeacherPose { get; private set; }
        public bool TeacherSequenceRunning => teacherSequence != null || (spoken!=null && spoken.Speaker==DialogueSpeaker.Teacher && spoken.Poses.Length>1 && poseTime<(spoken.Poses.Length-1)*spoken.PoseStepSeconds);
        public string BubbleText => bubble != null && bubble.activeSelf ? bubbleText.text : string.Empty;

        public void Configure(Image studentImage, Image teacherImage, Pose[] studentSet, Pose[] teacherSet,
            GameObject speechBubble, TMP_Text speechText)
        {
            student = studentImage;
            teacher = teacherImage;
            studentPoses = studentSet;
            teacherPoses = teacherSet;
            bubble = speechBubble;
            bubbleText = speechText;
            EnsureSpeakerName();RefreshLayout();
        }

        public void SetStudent(string pose)
        {
            StudentPose = pose;
            student.sprite = Find(studentPoses, pose);
        }

        public void SetTeacher(string pose)
        {
            if (teacherSequence != null) StopCoroutine(teacherSequence);
            teacherSequence = null;
            ApplyTeacher(pose);
        }

        /// Plays the poses one step apart; the last one stays.
        public void PlayTeacher(params string[] poses)
        {
            if (teacherSequence != null) StopCoroutine(teacherSequence);
            teacherSequence = StartCoroutine(Sequence(poses));
        }

        public bool BubbleVisible => bubble != null && bubble.activeSelf;

        public void Say(string text, float seconds = 2.5f)
        {
            if(dialogueHost!=null)
            {
                dialogueHost.Clear();
                dialogueHost.EmitLine(new GameplayDialogueLine { Speaker=DialogueSpeaker.Teacher,Text=text,DurationSeconds=seconds,Poses=new[]{teacher.sprite} });
                return;
            }
            bubble.SetActive(true);
            bubbleText.text = VietText.Fix(text);
            bubbleUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if(transform is RectTransform root && lastLayoutSize!=root.rect.size)RefreshLayout();
            if (bubble == null || !bubble.activeSelf) return;
            if(spoken!=null){poseTime+=Time.deltaTime;ApplyLinePose();return;}
            if (Time.timeScale == 0f) bubbleUntil += Time.unscaledDeltaTime;
            else if (Time.unscaledTime > bubbleUntil) bubble.SetActive(false);
        }

        IEnumerator Sequence(string[] poses)
        {
            for (int i = 0; i < poses.Length; i++)
            {
                ApplyTeacher(poses[i]);
                if (i + 1 < poses.Length) yield return new WaitForSeconds(stepSeconds);
            }
            teacherSequence = null;
        }

        void ApplyTeacher(string pose)
        {
            TeacherPose = pose;
            teacher.sprite = Find(teacherPoses, pose);
        }

        static Sprite Find(Pose[] set, string pose) =>
            set.FirstOrDefault(p => p.name == pose).sprite ??
            throw new InvalidOperationException($"[KMA] Pose {pose} is not configured.");
    }
}
