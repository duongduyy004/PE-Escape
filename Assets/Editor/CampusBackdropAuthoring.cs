#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace KMA.EditorTools
{
    /// Imports the generated campus art and drops the shared backdrop into a scene. Every scene
    /// configurator uses these instead of drawing its own sky/campus quads.
    public static class CampusBackdropAuthoring
    {
        public const string ArtDir = "Assets/_Project/Art/Environments/Campus";
        public const string AssetPath = "Assets/_Project/ScriptableObjects/Backdrop/CampusBackdropArt.asset";
        const string RootName = "CampusBackdrop";
        static readonly string[] Tiled = { "CampusSky", "CampusSkyline" };
        static readonly string[] Names = { "CampusSky", "CampusSkyline", "CampusPixel", "SprintTrack", "VolleyNet" };

        [MenuItem("KMA/Campus/Import Backdrop Art")]
        public static CampusBackdropArt EnsureArt()
        {
            foreach (string name in Names)
                Import(name);
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            AssetDatabase.Refresh();
            var art = AssetDatabase.LoadAssetAtPath<CampusBackdropArt>(AssetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<CampusBackdropArt>();
                AssetDatabase.CreateAsset(art, AssetPath);
            }
            art.Configure(Load("CampusSky"), Load("CampusSkyline"), Load("CampusPixel"),
                new Color32(0x2e, 0x9b, 0xe6, 0xff), new Color32(0x62, 0xb5, 0x4a, 0xff));
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
        }

        public static Sprite Load(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
            if (sprite == null)
                throw new InvalidOperationException($"[KMA] {ArtDir}/{name}.png did not import as a sprite; run tools/render-campus-art.js.");
            return sprite;
        }

        public static CampusBackdropWorld AddWorld(Scene scene, Camera camera, float horizonY, float skylineHeight, bool ground,
            Color? groundColor = null)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            var backdrop = UiKit.GetOrAdd<CampusBackdropWorld>(root);
            backdrop.Configure(EnsureArt(), camera, horizonY, skylineHeight, ground, groundColor);
            EditorUtility.SetDirty(backdrop);
            return backdrop;
        }

        public static CampusBackdropUi AddUi(RectTransform canvasRoot, float horizon01, float skylineHeight01, bool ground)
        {
            Transform existing = canvasRoot.Find(RootName);
            RectTransform rect = existing != null ? (RectTransform)existing
                : (RectTransform)new GameObject(RootName, typeof(RectTransform)).transform;
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            var backdrop = UiKit.GetOrAdd<CampusBackdropUi>(rect);
            UiKit.GetOrAdd<StyleExempt>(rect.gameObject);
            backdrop.Configure(EnsureArt(), horizon01, skylineHeight01, ground);
            EditorUtility.SetDirty(backdrop);
            return backdrop;
        }

        static void Import(string name)
        {
            string path = $"{ArtDir}/{name}.png";
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("[KMA] Missing campus art " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.wrapMode = Tiled.Contains(name) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
#endif
