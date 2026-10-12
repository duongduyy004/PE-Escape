using System.Collections;
using KMA.Gameplay.Celebration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KMA.Tests.Gameplay.Celebration
{
    public sealed class CelebrationSceneTests
    {
        [UnityTest]
        public IEnumerator TeacherTimelineStrictHoldWriteWithThreeFrames()
        {
            yield return SceneManager.LoadSceneAsync("Celebration", LoadSceneMode.Single);yield return null;
            var controller=Object.FindFirstObjectByType<CelebrationSceneController>();
            var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var frames=(Sprite[])typeof(CelebrationSceneController).GetField("teacherFrames",f).GetValue(controller);
            Assert.That(frames.Length,Is.EqualTo(3));Assert.That(frames[0].name,Is.EqualTo("BossPE_strictLook"));
            var teacher=(UnityEngine.UI.Image)typeof(CelebrationSceneController).GetField("teacher",f).GetValue(controller);
            controller.Timeline.Tick(5-controller.Timeline.Time);yield return null;
            Assert.That(teacher.sprite.name,Is.EqualTo("BossPE_scoreHold"));
            var words=(TMPro.TMP_Text)typeof(CelebrationSceneController).GetField("bubbleText",f).GetValue(controller);
            Assert.That(words.text,Is.EqualTo("Tốt, cậu này được. Qua rồi thì về, đừng đứng chắn sân."));Assert.That(controller.SummaryVisible,Is.False);
            controller.Timeline.Tick(1);yield return null;Assert.That(teacher.sprite.name,Is.EqualTo("BossPE_scoreWrite"));
            controller.Timeline.Tick(8-controller.Timeline.Time);yield return new WaitForSeconds(.4f);Assert.That(controller.SummaryVisible,Is.True);
        }

        [UnityTest]
        public IEnumerator SummaryTitleStaysInsideTheCard()
        {
            yield return SceneManager.LoadSceneAsync("Celebration", LoadSceneMode.Single);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var controller = Object.FindFirstObjectByType<CelebrationSceneController>();
            var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var summary = (CanvasGroup)typeof(CelebrationSceneController).GetField("summaryGroup", fields).GetValue(controller);
            var title = (TMPro.TMP_Text)typeof(CelebrationSceneController).GetField("title", fields).GetValue(controller);
            var rows = (TMPro.TMP_Text[])typeof(CelebrationSceneController).GetField("rows", fields).GetValue(controller);
            RectTransform cardRect = summary.transform as RectTransform;
            foreach (float width in new[] { 1100f, 900f }) // 16:9 reference width and a narrow 20:9 safe-area panel.
            {
                cardRect.sizeDelta = new Vector2(width, 620f);
                Canvas.ForceUpdateCanvases();
                title.ForceMeshUpdate(true, true);
                foreach (TMPro.TMP_Text row in rows) row.ForceMeshUpdate(true, true);
                Rect cardBounds = Bounds(cardRect);
                Rect titleBounds = Bounds(title.rectTransform);
                Assert.That(titleBounds.xMin, Is.GreaterThanOrEqualTo(cardBounds.xMin - 1f));
                Assert.That(titleBounds.xMax, Is.LessThanOrEqualTo(cardBounds.xMax + 1f));
                Assert.That(title.enableAutoSizing, Is.True);
                Assert.That(title.fontSizeMin, Is.GreaterThanOrEqualTo(32f), "The title must remain readable on phone ratios.");
                Assert.That(title.enableWordWrapping, Is.False);
                Assert.That(title.textBounds.size.x, Is.LessThanOrEqualTo(title.rectTransform.rect.width + .5f));
                Assert.That(title.isTextTruncated, Is.False, "The full title must remain visible.");
                Assert.That(rows[3].text, Does.Contain("Không dùng gợi ý"));
                foreach (TMPro.TMP_Text row in rows)
                {
                    Assert.That(row.enableAutoSizing, Is.True);
                    Assert.That(row.textBounds.size.x, Is.LessThanOrEqualTo(row.rectTransform.rect.width + .5f), row.name);
                    Assert.That(row.textBounds.size.y, Is.LessThanOrEqualTo(row.rectTransform.rect.height + .5f), row.name);
                    Assert.That(row.isTextTruncated, Is.False, row.name + " must preserve the full recap text.");
                }
            }
        }

        static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0], corners[2] - corners[0]);
        }

        [UnityTest]
        public IEnumerator OpenedWithoutASessionShowsSampleDataAndSkipReachesTheSummary()
        {
            yield return SceneManager.LoadSceneAsync("Celebration", LoadSceneMode.Single);
            yield return null;
            var controller = Object.FindFirstObjectByType<CelebrationSceneController>();
            Assert.That(controller.Summary.IsSample, Is.True);
            Assert.That(controller.SummaryVisible, Is.False);
            UnityEngine.UI.Image fade = controller.transform.Find("SafeAreaRoot/FadeIn").GetComponent<UnityEngine.UI.Image>();
            Assert.That(fade.raycastTarget, Is.False);
            Assert.That(fade.color.a, Is.GreaterThan(.2f));
            controller.Skip();
            yield return new WaitForSeconds(.6f);
            Assert.That(controller.SummaryVisible, Is.True);
            Assert.That(fade.gameObject.activeSelf, Is.False);
            Assert.That(controller.RowTexts.Length, Is.EqualTo(4));
            Assert.That(controller.RowTexts[3], Does.Contain("Bài kiểm tra cuối"));
        }
    }
}
