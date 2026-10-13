using System.Collections;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class JourneyNarrativeTests
    {
        GameObject root;
        JourneyDialoguePresenter presenter;
        JourneyDialogueLibrary library;
        bool allowSave;
        string savedKey;
        int closed;

        void Open(string node,JourneyDialogueLibrary custom=null)
        {
            root = new GameObject("JourneyNarrativeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            presenter = root.AddComponent<JourneyDialoguePresenter>();
            library = custom ?? JourneyDialogueLibrary.LoadDefault();
            allowSave = true;
            savedKey = null;
            closed = 0;
            presenter.Configure(library, key => { savedKey = key; return allowSave; });
            presenter.Show(node, () => closed++);
        }

        T Find<T>(string name) where T : Component =>
            root.GetComponentsInChildren<T>(true).First(component => component.name == name);

        void Tap() => Find<Button>("TapArea").onClick.Invoke();

        void TapToLine(int index)
        {
            while (presenter.CurrentLineIndex < index) Tap();
        }

        [UnityTest]
        public IEnumerator SkipCanRetryAfterSaveFailureAndPersistsBeforeClosing()
        {
            Open("opening");
            allowSave = false;
            yield return null;
            Button skip = Find<Button>("BỎ QUA");
            Assert.That(skip.GetComponent<KMA.UI.Kit.ShellPressFeedback>(), Is.Not.Null);
            Assert.That(skip.transform.Find(KMA.UI.Kit.ShellKit.BodyName + "/" + KMA.UI.Kit.ShellKit.FaceName), Is.Not.Null);
            skip.onClick.Invoke();
            Assert.That(presenter.IsShowing, Is.True);
            Assert.That(savedKey, Is.EqualTo("opening"));
            Assert.That(closed, Is.Zero);
            allowSave = true;
            skip.onClick.Invoke();
            Assert.That(presenter.IsShowing, Is.False);
            Assert.That(closed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator JourneyDialogueFollowsTheShellStyle()
        {
            Open("opening");
            yield return null;
            Transform overlay = Find<RectTransform>("JourneyDialogueOverlay");
            Transform box = overlay.Find("DialogueBox");
            Assert.That(box.Find(KMA.UI.Kit.ShellKit.FaceName).GetComponent<Image>().color, Is.EqualTo(KMA.UI.Kit.ShellTheme.Cream));
            Assert.That(box.Find("Dialogue").GetComponent<TMP_Text>().color, Is.EqualTo(KMA.UI.Kit.ShellTheme.Ink));
            var problems = KMA.UI.Kit.ShellStyleAudit.Audit(overlay.gameObject);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [UnityTest]
        public IEnumerator JourneyQueueCallsCompletionOnceWhenEveryNodeWasAlreadySeen()
        {
            root = new GameObject("JourneyNarrativeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            presenter = root.AddComponent<JourneyDialoguePresenter>();
            library = JourneyDialogueLibrary.LoadDefault();
            var session = new GameSession();
            session.Journey.MarkDialogueSeen("opening");
            session.Journey.MarkDialogueSeen("sprint_intro");
            int completed = 0;
            presenter.ShowJourney(session, library, _ => true, null, () => completed++);
            Assert.That(presenter.IsShowing, Is.False);
            Assert.That(completed, Is.EqualTo(1));
            presenter.Advance();
            Assert.That(completed, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator JourneyQueueCallsCompletionOnlyAfterAllSeenStateSaves()
        {
            root = new GameObject("JourneyNarrativeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            presenter = root.AddComponent<JourneyDialoguePresenter>();
            library = JourneyDialogueLibrary.LoadDefault();
            var session = new GameSession();
            var saved = new System.Collections.Generic.List<string>();
            int completed = 0;
            presenter.ShowJourney(session, library, key => { saved.Add(key); return true; }, null,
                () => completed++);
            Button skip = Find<Button>("BỎ QUA");
            skip.onClick.Invoke();
            Assert.That(completed, Is.Zero);
            skip.onClick.Invoke();
            Assert.That(saved, Is.EqualTo(new[] { "opening", "sprint_intro" }));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(presenter.IsShowing, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstTapRevealsWholeLineSecondTapAdvances()
        {
            Open("opening");
            yield return null;
            TMP_Text body = Find<TMP_Text>("Dialogue");
            Assert.That(presenter.IsLineFullyShown, Is.False);
            Tap();
            Assert.That(presenter.IsLineFullyShown, Is.True);
            Assert.That(presenter.CurrentLineIndex, Is.Zero);
            Assert.That(body.maxVisibleCharacters, Is.GreaterThanOrEqualTo(body.textInfo.characterCount));
            Tap();
            Assert.That(presenter.CurrentLineIndex, Is.EqualTo(1));
            Assert.That(presenter.IsLineFullyShown, Is.False);
        }

        [UnityTest]
        public IEnumerator LastLineFirstTapRevealsSecondTapClosesAndSaves()
        {
            Open("sprint_exam");
            yield return null;
            TapToLine(1);
            Assert.That(presenter.IsLineFullyShown, Is.False);
            Tap();
            Assert.That(presenter.IsShowing, Is.True);
            Assert.That(savedKey, Is.Null);
            Assert.That(Find<TMP_Text>("Hint").text, Does.Contain("ĐÓNG"));
            Tap();
            Assert.That(presenter.IsShowing, Is.False);
            Assert.That(savedKey, Is.EqualTo("sprint_exam"));
            Assert.That(closed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SaveFailureKeepsOverlayAndRetryClosesExactlyOnce()
        {
            Open("sprint_exam");
            allowSave = false;
            yield return null;
            TapToLine(1);
            Tap();
            Tap();
            Assert.That(presenter.IsShowing, Is.True);
            Assert.That(Find<TMP_Text>("SaveError").text, Is.Not.Empty);
            Assert.That(closed, Is.Zero);
            allowSave = true;
            Tap();
            Assert.That(presenter.IsShowing, Is.False);
            presenter.Advance();
            Assert.That(closed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PlayerStaysRightAsListenerWhenSilent()
        {
            Open("sprint_pass");
            yield return new WaitForSecondsRealtime(.4f);
            Image right = Find<Image>("RightActor");
            Image left = Find<Image>("LeftActor");
            JourneyCharacter co = library.GetCharacter("co_the_chat");
            Assert.That(right.enabled, Is.True);
            Assert.That(right.sprite, Is.SameAs(library.Player.GetPose(DialoguePose.Idle)));
            Assert.That(right.color, Is.EqualTo(JourneyDialoguePresenter.ListenerTint));
            Assert.That(left.sprite, Is.SameAs(co.GetPose(DialoguePose.Cheer)));
            Assert.That(left.color, Is.EqualTo(Color.white));
            Assert.That(Find<TMP_Text>("Name").text, Is.EqualTo("Giảng viên thể chất"));
        }

        [UnityTest]
        public IEnumerator LeftSlotSwapsToEachNewSpeaker()
        {
            Open("soccer_intro");
            yield return null;
            TapToLine(1);
            yield return new WaitForSecondsRealtime(.5f);
            Image left = Find<Image>("LeftActor");
            Assert.That(left.sprite, Is.SameAs(library.GetCharacter("anh_khoa_tren").GetPose(DialoguePose.Idle)));
            TapToLine(2);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(left.sprite, Is.SameAs(library.GetCharacter("co_the_chat").GetPose(DialoguePose.Idle)));
            Assert.That(left.rectTransform.anchoredPosition.x, Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator EmojiRenderThroughTheSpriteAssetAndStickersFollowTheLine()
        {
            var custom=ScriptableObject.CreateInstance<JourneyDialogueLibrary>();
            custom.SetContent(JourneyDialogueLibrary.LoadDefault().Cast,new[]{new JourneyDialogueNode("emoji-fixture",new System.Collections.Generic.List<JourneyDialogueLine>{
                new JourneyDialogueLine("anh_khoa_tren",DialoguePose.Idle,"Nhìn :eyes:."),
                new JourneyDialogueLine("mai_toang",DialoguePose.Idle,"Chờ nhé.","ÉT O ÉT!"),
                new JourneyDialogueLine("tan_thu",DialoguePose.Idle,"Vâng.")})});
            Open("emoji-fixture",custom);
            yield return null;
            TMP_Text body = Find<TMP_Text>("Dialogue");
            Assert.That(body.text, Does.Contain("<sprite name=\"eyes\">"));
            Assert.That(body.spriteAsset, Is.Not.Null);
            Assert.That(body.spriteAsset.name, Is.EqualTo("JourneyEmoji"));
            RectTransform sticker = Find<RectTransform>("Sticker");
            Assert.That(sticker.gameObject.activeSelf, Is.False);
            TapToLine(1);
            Assert.That(sticker.gameObject.activeSelf, Is.True);
            Assert.That(sticker.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("ÉT O ÉT!"));
            TapToLine(2);
            Assert.That(sticker.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator SaveErrorStaysClearOfTheNameTagWhenThePlayerSpeaksLast()
        {
            Open("sprint_exam");
            allowSave = false;
            yield return null;
            TapToLine(1);
            Tap();
            Tap();
            yield return null;
            RectTransform error = Find<TMP_Text>("SaveError").rectTransform;
            RectTransform tag = Find<RectTransform>("NameTag");
            Assert.That(Find<TMP_Text>("SaveError").text, Is.Not.Empty);
            Assert.That(Find<TMP_Text>("Name").text, Is.EqualTo("SV năm 1"));
            Assert.That(Overlaps(error, tag), Is.False, "The retry hint must stay readable.");
        }

        static bool Overlaps(RectTransform a, RectTransform b)
        {
            var ca = new Vector3[4];
            var cb = new Vector3[4];
            a.GetWorldCorners(ca);
            b.GetWorldCorners(cb);
            return ca[0].x < cb[2].x && cb[0].x < ca[2].x && ca[0].y < cb[2].y && cb[0].y < ca[2].y;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root) Object.Destroy(root);
            if(library!=null && !UnityEditor.AssetDatabase.Contains(library))Object.Destroy(library);
            yield return null;
        }
    }
}
