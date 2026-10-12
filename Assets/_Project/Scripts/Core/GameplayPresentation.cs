using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

namespace KMA.Gameplay.Core
{
    public sealed class GameplayPresentation : MonoBehaviour
    {
        string statusText;
        string titleText;
        string controlsText;
        TMP_Text statusLabel;
        Canvas fallbackCanvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallSceneBootstrap()
        {
            SceneManager.sceneLoaded -= EnsureScenePresentation;
            SceneManager.sceneLoaded += EnsureScenePresentation;
            EnsureScenePresentation(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void EnsureScenePresentation(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "" || FindFirstObjectByType<GameplayPresentation>() != null)
                return;

            new GameObject("GameplayPresentation").AddComponent<GameplayPresentation>();
        }

        void Awake()
        {
            gameObject.name = "GameplayPresentation";
            EnsureCamera();
            titleText = SceneTitle(SceneManager.GetActiveScene().name);
            controlsText = Controls(SceneManager.GetActiveScene().name);
        }

        void Update()
        {
            var router = FindFirstObjectByType<SceneRouter>();
            var minigame = FindFirstObjectByType<MinigameBase>();
            var phase = minigame == null ? "Route" : minigame.PresentationPhase.ToString();
            var session = router == null
                ? "Session: waiting for route"
                : $"Lives: {router.Session.Lives}";
            statusText = $"Phase: {phase}\n{session}";
            if (statusLabel != null) statusLabel.text = VietText.Fix(statusText);
        }

        void LateUpdate()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas != fallbackCanvas && canvas.isActiveAndEnabled)
                {
                    if (fallbackCanvas != null) fallbackCanvas.gameObject.SetActive(false);
                    return;
                }
            if (fallbackCanvas != null)
            {
                fallbackCanvas.gameObject.SetActive(true);
                return;
            }

            var root = new GameObject("FallbackPresentation", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            fallbackCanvas = root.GetComponent<Canvas>();
            fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("Background", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(.025f, .04f, .08f, .96f);
            Label(root.transform, "Title", titleText, .76f, .92f, 46);
            statusLabel = Label(root.transform, "Status", statusText ?? "Phase: Tutorial", .38f, .62f, 28);
            Label(root.transform, "Controls", controlsText, .08f, .24f, 24);
        }

        static Camera EnsureCamera()
        {
            var camera = Camera.main ?? FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                camera.tag = "MainCamera";
                return camera;
            }

            // Only a scene with no camera at all gets the fallback; authored cameras keep their sky.
            camera = new GameObject("GameplayCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .04f, .08f, 1f);
            return camera;
        }

        static TMP_Text Label(Transform parent, string name, string value, float bottom, float top, float fontSize)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            var text = root.GetComponent<TextMeshProUGUI>();
            text.rectTransform.anchorMin = new Vector2(0, bottom);
            text.rectTransform.anchorMax = new Vector2(1, top);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSharedMaterial = text.font.material;
            text.fontSize = fontSize; text.text = VietText.Fix(value);
            text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
            text.extraPadding = true; text.lineSpacing = 15;
            text.enableAutoSizing = true;
            text.fontSizeMin = fontSize * .6f;
            text.fontSizeMax = fontSize;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        static string SceneTitle(string sceneName) => sceneName switch
        {
            "MG_Sprint" => "KMA — Sprint",
            "Map" => "KMA — Map",
            "GameOver" => "KMA — Game Over",
            _ => "KMA Gameplay"
        };

        static string Controls(string sceneName) => sceneName switch
        {
            "MG_Sprint" => "Sprint: Left / Right arrows",
            "Map" => "Progression route",
            "GameOver" => "Run complete",
            _ => "KMA Gameplay Prototype"
        };
    }
}
