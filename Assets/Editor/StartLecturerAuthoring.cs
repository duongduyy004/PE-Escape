#if UNITY_EDITOR
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.Volleyball;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMA.EditorTools
{
    /// <summary>
    /// Stands the red-shirted lecturer (BossPE) in each sports minigame so she starts it with her
    /// whistle and gestures through the rest of it. Every placement says which way she faces: the art looks right, so a lecturer to the
    /// right of the athletes is flipped to look left at them.
    /// </summary>
    public static class StartLecturerAuthoring
    {
        public const string ObjectName = "StartLecturer";

        public readonly struct Placement
        {
            public readonly Vector3 Feet;
            public readonly float Scale;
            public readonly bool FaceLeft;
            public readonly int SortingOrder;
            public readonly float LeaveAfterSeconds;

            public Placement(Vector3 feet, float scale, bool faceLeft, int sortingOrder, float leaveAfterSeconds = 0f)
            {
                Feet = feet;
                Scale = scale;
                FaceLeft = faceLeft;
                SortingOrder = sortingOrder;
                LeaveAfterSeconds = leaveAfterSeconds;
            }
        }

        [MenuItem("KMA/Start Lecturer/Add To Sports Minigames")]
        public static void AddToSportsMinigames()
        {
            CharacterArt.ImportAll();
            Apply(SprintSceneConfigurator.ScenePath, AddToSprint);
            Apply(VolleyballSceneConfigurator.ScenePath, AddToVolleyball);
            Apply(FootballSceneConfigurator.ScenePath, AddToFootball);
            Apply(FrogJumpSceneConfigurator.ScenePath, AddToFrogJump);
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] Start lecturer added to Sprint, Volleyball, Football and Frog Jump.");
        }

        static void Apply(string path, System.Action<Scene> add)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            add(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void AddToSprint(Scene scene) => Add(scene, PlaceInSprint(scene));
        public static void AddToVolleyball(Scene scene) => Add(scene, VolleyballPlacement());
        public static void AddToFootball(Scene scene) => Add(scene, FootballPlacement());
        public static void AddToFrogJump(Scene scene) => Add(scene, FrogJumpPlacement());

        /// <summary>Adds the lecturer, replacing any earlier one so the menu item is repeatable.</summary>
        public static StartLecturer Add(Scene scene, Placement placement)
        {
            foreach (GameObject root in scene.GetRootGameObjects().Where(r => r.name == ObjectName).ToArray())
                Object.DestroyImmediate(root);

            var go = new GameObject(ObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = placement.Feet;
            go.transform.localScale = Vector3.one * placement.Scale;
            var body = go.AddComponent<SpriteRenderer>();
            body.sortingOrder = placement.SortingOrder;
            var lecturer = go.AddComponent<StartLecturer>();
            lecturer.Configure(body,
                CharacterArt.Load(CharacterArt.Boss, "strictLook"),
                CharacterArt.Load(CharacterArt.Boss, "whistle0"),
                CharacterArt.Load(CharacterArt.Boss, "whistle1"),
                placement.FaceLeft, placement.LeaveAfterSeconds);
            lecturer.ConfigureGestures(
                CharacterArt.Load(CharacterArt.Boss, "count"),
                CharacterArt.Load(CharacterArt.Boss, "command"),
                CharacterArt.Load(CharacterArt.Boss, "strictLook"),
                CharacterArt.Load(CharacterArt.Boss, "taunt"),
                CharacterArt.Load(CharacterArt.Boss, "scoreWrite"),
                CharacterArt.Load(CharacterArt.Boss, "scoreWrite"),
                CharacterArt.Load(CharacterArt.Boss, "penalty0"),
                CharacterArt.Load(CharacterArt.Boss, "penalty1"));
            body.flipX = placement.FaceLeft;
            EditorUtility.SetDirty(go);
            return lecturer;
        }

        /// Below the bottom lane line, just right of the TRÁI button, looking right down the track
        /// at the finish. She leaves after the whistle so she never stands in a rival's way. The
        /// runners are placed by the Sprint presentation at run time, so the spot is measured on
        /// screen: the start line sits near world x -10 on the 21:9 camera.
        static Placement PlaceInSprint(Scene scene)
        {
            float lowestLaneLineY = SprintTrackLayout.WorldYForRow(SprintTrackLayout.LaneLineRows[SprintTrackLayout.LaneCount]);
            return new Placement(new Vector3(-5.8f, lowestLaneLineY - .55f, 0f), 1.3f, false, 50, 1.6f);
        }

        /// Beside the left baseline, level with the far half of the court, looking right at the player.
        static Placement VolleyballPlacement()
        {
            var ground = new Vector2(-CourtSpace.HalfLength - 2.2f, 1.4f);
            return new Placement(CourtSpace.ToWorld(ground, 0f), VolleyballSceneConfigurator.AthleteScale, false,
                VolleyAthleteView.SortingOrderFor(ground.y));
        }

        /// To the right of the kicker, a little further back, looking left at the kicker.
        static Placement FootballPlacement()
        {
            // The Football scene stretches every sprite to a display size; match the kicker's height.
            float kickerScale = FootballSceneConfigurator.KickerDisplayHeight * FootballPresentation.PixelToWorld /
                                CharacterArt.Load(CharacterArt.Boss, "strictLook").bounds.size.y;
            return new Placement(FootballPresentation.ScreenToWorld(720f, 560f), .9f * kickerScale, true, 22);
        }

        /// In front of the track at the finish line, looking left back down the track at the student.
        static Placement FrogJumpPlacement() =>
            new Placement(new Vector3(FrogJumpSceneConfigurator.FinishX - 1f, FrogJumpSceneConfigurator.GroundY - .6f, 0f),
                FrogJumpSceneConfigurator.HeroScale, true, 12);
    }
}
#endif
