#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KMA.EditorTools;
using KMA.Gameplay;
using KMA.Gameplay.Celebration;
using KMA.Gameplay.Chess;
using KMA.Gameplay.FrogJump;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KMA.Tests.EditorTools
{
    /// <summary>Which sports-supplement pose each saved scene and asset draws.</summary>
    public sealed class SupplementPoseWiringTests
    {
        [TearDown]
        public void ReleaseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void FootballKicksFromBehindAgainstTheStudentKeeper()
        {
            EditorSceneManager.OpenScene(FootballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            FootballPoseSprites poses = Object.FindFirstObjectByType<FootballPresentation>().Poses;
            Assert.That(PoseNames(new[] { poses.kickerReady, poses.kickerRunUp, poses.kickerStrike, poses.kickerCelebrate }),
                Is.EqualTo(new[]
                {
                    "MaleAdventurer_footballBackIdle", "MaleAdventurer_footballBackApproach",
                    "MaleAdventurer_footballBackKick", "MaleAdventurer_footballBackCelebrate"
                }));
            string[] keeper = PoseNames(new[] { poses.keeperReady, poses.keeperSave, poses.keeperBeaten });
            Assert.That(keeper, Is.EqualTo(new[] { "StudentKeeper_ready", "StudentKeeper_cheer", "StudentKeeper_recover" }));
            // The live ball stays visible on a save, so a keeper drawn holding one would show two balls.
            Assert.That(keeper, Has.None.EndWith("Ball"));
            Assert.That(PoseName(GameObject.Find("FootballWorld/Goalkeeper").GetComponent<SpriteRenderer>().sprite),
                Is.EqualTo("StudentKeeper_ready"));
        }

        [Test]
        public void VolleyballPlayerUsesVolleyballPosesAndTheOpponentKeepsHers()
        {
            EditorSceneManager.OpenScene(VolleyballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<VolleyballController>();
            string[] Player(AthleteAction action) => PoseNames(controller.PlayerView.FramesFor(action));
            string[] Opponent(AthleteAction action) => PoseNames(controller.OpponentView.FramesFor(action));

            Assert.That(Player(AthleteAction.Idle), Is.EqualTo(new[] { "MaleAdventurer_volleyReady" }));
            Assert.That(Player(AthleteAction.Run),
                Is.EqualTo(new[] { "MaleAdventurer_volleyShuffleRight", "MaleAdventurer_volleyShuffleLeft" }));
            Assert.That(Player(AthleteAction.Receive),
                Is.EqualTo(new[] { "MaleAdventurer_volleyDig", "MaleAdventurer_volleyRecover" }));
            Assert.That(Player(AthleteAction.Smash),
                Is.EqualTo(new[] { "MaleAdventurer_volleySpikeWindup", "MaleAdventurer_volleySpikeContact" }));
            Assert.That(Player(AthleteAction.Block),
                Is.EqualTo(new[] { "MaleAdventurer_volleyJumpLoad", "MaleAdventurer_volleySet" }));
            Assert.That(Player(AthleteAction.Dive),
                Is.EqualTo(new[] { "MaleAdventurer_fallForwardRight", "MaleAdventurer_fallSitRight" }));

            Assert.That(Opponent(AthleteAction.Idle), Is.EqualTo(new[] { "FemaleAdventurer_idle" }));
            Assert.That(Opponent(AthleteAction.Run), Is.EqualTo(new[]
                { "FemaleAdventurer_run0", "FemaleAdventurer_run1", "FemaleAdventurer_run2", "FemaleAdventurer_run1" }));
            Assert.That(Opponent(AthleteAction.Smash), Is.EqualTo(new[] { "FemaleAdventurer_jump", "FemaleAdventurer_attack1" }));
        }

        [Test]
        public void FrogJumpHopsAndFallsWithTheSupplementPoses()
        {
            EditorSceneManager.OpenScene(FrogJumpSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var view = new SerializedObject(Object.FindFirstObjectByType<FrogJumpView>());
            Assert.That(Wired(view, "squatPose"), Is.EqualTo("MaleAdventurer_frogReadyRight"));
            Assert.That(Wired(view, "jumpPose"), Is.EqualTo("MaleAdventurer_frogAirRight"));
            Assert.That(Wired(view, "fallPose"), Is.EqualTo("MaleAdventurer_fallSitRight"));
        }

        [Test]
        public void SprintHeroFallsWithTheSupplementPosesAndRivalsKeepTheirs()
        {
            Assert.That(ClipPoses("MaleAdventurer_Stumble"), Is.Not.Empty.And.All.EqualTo("MaleAdventurer_fallForwardRight"));
            Assert.That(ClipPoses("MaleAdventurer_Fail"), Is.Not.Empty.And.All.EqualTo("MaleAdventurer_fallSitRight"));
            // Idle also plays mid-race while the player stops tapping, so it keeps the standing pose.
            Assert.That(ClipPoses("MaleAdventurer_Idle"), Is.Not.Empty.And.All.EqualTo("MaleAdventurer_idle"));
            Assert.That(ClipPoses("FemalePerson_Fail"), Is.Not.Empty.And.All.EqualTo("FemalePerson_fallDown"));
            Assert.That(ClipPoses("FemalePerson_Stumble"), Is.Not.Empty.And.All.EqualTo("FemalePerson_hurt"));
        }

        static string Wired(SerializedObject component, string field) =>
            PoseName((Sprite)component.FindProperty(field).objectReferenceValue);

        static string[] ClipPoses(string clipName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/" + clipName + ".anim");
            Assert.That(clip, Is.Not.Null, clipName);
            return AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .SelectMany(binding => AnimationUtility.GetObjectReferenceCurve(clip, binding))
                .Select(key => PoseName(key.value as Sprite)).ToArray();
        }

        [Test]
        public void ChessCastShowsTheSupplementExpressions()
        {
            EditorSceneManager.OpenScene(ChessFinalSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var cast = new SerializedObject(Object.FindFirstObjectByType<ChessCastView>());
            Assert.That(CastPose(cast, "studentPoses", "idle"), Is.EqualTo("MaleAdventurer_think"));
            Assert.That(CastPose(cast, "studentPoses", "hurt"), Is.EqualTo("MaleAdventurer_disappointed"));
            Assert.That(CastPose(cast, "studentPoses", "cheer0"), Is.EqualTo("MaleAdventurer_happy"));
            Assert.That(CastPose(cast, "studentPoses", "cheer1"), Is.EqualTo("MaleAdventurer_happy"));
            Assert.That(CastPose(cast, "teacherPoses", "idleBoss"), Is.EqualTo("BossPE_handsOnHips"));
            Assert.That(CastPose(cast, "teacherPoses", "taunt"), Is.EqualTo("BossPE_taunt"));
            Assert.That(CastPose(cast, "teacherPoses", "chessThink"), Is.EqualTo("BossPE_think"));
            Assert.That(CastPose(cast, "teacherPoses", "cheer0"), Is.EqualTo("BossPE_scoreWrite"));
            Assert.That(CastPose(cast, "teacherPoses", "chessMove"), Is.EqualTo("BossPE_chessMove"));
            Assert.That(CastPose(cast, "teacherPoses", "strictLook"), Is.EqualTo("BossPE_strictLook"));
        }

        [Test]
        public void CelebrationCastShowsTheSupplementExpressions()
        {
            EditorSceneManager.OpenScene(CelebrationSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var controller = new SerializedObject(Object.FindFirstObjectByType<CelebrationSceneController>());
            // Slots 1 and 2 alternate every 0.35 s, so they stay the designed cheer0/cheer1 pair.
            Assert.That(Frames(controller, "studentFrames"),
                Is.EqualTo(new[] { "MaleAdventurer_idle", "MaleAdventurer_happy", "MaleAdventurer_happy" }));
            Assert.That(Frames(controller, "classmateFrames"),
                Is.EqualTo(new[] { "FemalePerson_idle", "FemalePerson_cheer0", "FemalePerson_cheer1" }));
            Assert.That(Frames(controller, "teacherFrames"),
                Is.EqualTo(new[] { "BossPE_strictLook", "BossPE_scoreHold", "BossPE_scoreWrite" }));
        }

        static string CastPose(SerializedObject cast, string list, string key)
        {
            SerializedProperty poses = cast.FindProperty(list);
            for (int i = 0; i < poses.arraySize; i++)
            {
                SerializedProperty pose = poses.GetArrayElementAtIndex(i);
                if (pose.FindPropertyRelative("name").stringValue == key)
                    return PoseName((Sprite)pose.FindPropertyRelative("sprite").objectReferenceValue);
            }
            return "<no pose " + key + ">";
        }

        static string[] Frames(SerializedObject component, string field)
        {
            SerializedProperty frames = component.FindProperty(field);
            return Enumerable.Range(0, frames.arraySize)
                .Select(i => PoseName((Sprite)frames.GetArrayElementAtIndex(i).objectReferenceValue)).ToArray();
        }

        [TestCase("MG_Sprint")]
        [TestCase("MG_Volleyball")]
        [TestCase("MG_Football")]
        [TestCase("MG_FrogJump")]
        public void StartLecturerReactsWithTheSupplementGestures(string scene)
        {
            EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{scene}.unity", OpenSceneMode.Single);
            var lecturer = new SerializedObject(Object.FindFirstObjectByType<StartLecturer>());
            Assert.That(Wired(lecturer, "taunt"), Is.EqualTo("BossPE_taunt"));
            Assert.That(Wired(lecturer, "cheer"), Is.EqualTo("BossPE_scoreWrite"));
            Assert.That(Wired(lecturer, "cheerBothArms"), Is.EqualTo("BossPE_scoreWrite"));
            Assert.That(Wired(lecturer, "penalty"), Is.EqualTo("BossPE_penalty0"));
        }

        [Test]
        public void JourneyHeroAndTeacherReactWithTheSupplementExpressions()
        {
            var library = AssetDatabase.LoadAssetAtPath<JourneyDialogueLibrary>(
                "Assets/_Project/Resources/Journey/JourneyDialogues.asset");
            JourneyCharacter Cast(string id) => library.Cast.Single(character => character.Id == id);

            Assert.That(PoseName(Cast("anh_khoa_tren").GetPose(DialoguePose.Cheer)), Is.EqualTo("MalePerson_cheer0"));
            Assert.That(PoseName(Cast("anh_khoa_tren").GetPose(DialoguePose.Hurt)), Is.EqualTo("MalePerson_hurt"));
            Assert.That(PoseName(Cast("anh_khoa_tren").GetPose(DialoguePose.Idle)), Is.EqualTo("MalePerson_idle"));
            Assert.That(PoseName(Cast("co_the_chat").GetPose(DialoguePose.Cheer)), Is.EqualTo("BossPE_scoreWrite"));
            Assert.That(PoseName(Cast("co_the_chat").GetPose(DialoguePose.Hurt)), Is.EqualTo("BossPE_angry"));
            Assert.That(PoseName(Cast("tan_thu").GetPose(DialoguePose.Cheer)), Is.EqualTo("MaleAdventurer_happy"));
            Assert.That(PoseName(Cast("mai_toang").GetPose(DialoguePose.Hurt)), Does.EndWith("_hurt"));
        }

        internal static string PoseName(Sprite sprite) =>
            sprite == null ? "<null>" : Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sprite));

        internal static string[] PoseNames(IEnumerable<Sprite> sprites) => sprites.Select(PoseName).ToArray();
    }
}
#endif
