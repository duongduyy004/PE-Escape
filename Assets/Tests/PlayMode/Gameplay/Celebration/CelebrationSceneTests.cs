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
