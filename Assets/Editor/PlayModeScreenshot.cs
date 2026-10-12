using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KMA.EditorTools
{
    /// <summary>
    /// QA helper: keeps a single Unity Editor instance running and services screenshot
    /// requests dropped as a JSON file, so external tooling never has to relaunch the Editor.
    ///
    /// One-time launch (GUI, not -batchmode - Play Mode needs a real render surface):
    ///   Unity.exe -projectPath . -logFile Builds/Screenshots/editor.log
    /// [InitializeOnLoad] means the poll loop starts as soon as the Editor is open, and keeps
    /// running across script-recompile domain reloads.
    ///
    /// Each iteration, write Builds/Screenshots/request.json (atomically - write to a .tmp file
    /// then rename, so the Editor never reads a half-written file):
    ///   {"id":"<unique>","scene":"Assets/_Project/Scenes/Map.unity","output":"Builds/Screenshots/map.png","waitSeconds":3}
    /// "scene" may be empty/omitted to reuse whatever scene is already open.
    /// "gameViewSize" ("2400x1080") selects a fixed Game view resolution before Play Mode starts;
    /// empty keeps the current one, so pass "1920x1080" to return to 16:9 after a wide capture.
    /// Poll for Builds/Screenshots/done.json; it is rewritten once per request with
    ///   {"id":"<same id>","status":"ok"|"error","message":"..."}
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeScreenshot
    {
        const string RequestPath = "Builds/Screenshots/request.json";
        const string DonePath = "Builds/Screenshots/done.json";

        const string KeyLastRequestId = "KMA_PMS_LastRequestId";
        const string KeyActive = "KMA_PMS_Active";
        const string KeyId = "KMA_PMS_Id";
        const string KeyOutput = "KMA_PMS_Output";
        const string KeyCaptureAt = "KMA_PMS_CaptureAt";
        const string KeyPhase = "KMA_PMS_Phase";
        const string KeyFrames = "KMA_PMS_Frames";
        const string KeyHoldSprintTutorial = "KMA_PMS_HoldSprintTutorial";
        const string KeyForceSprintDistance = "KMA_PMS_ForceSprintDistance";
        const string KeyForceSprintResult = "KMA_PMS_ForceSprintResult";
        const string KeyOpenPause = "KMA_PMS_OpenPause";
        const string KeyOpenSettings = "KMA_PMS_OpenSettings";
        const string KeyOpenDialogue = "KMA_PMS_OpenDialogue";
        const string KeyDialogueTaps = "KMA_PMS_DialogueTaps";
        const string KeyQaState = "KMA_PMS_QaState";

        [Serializable]
        class Request
        {
            public string id;
            public string scene;
            public string output;
            public float waitSeconds = 3f;
            public bool holdSprintTutorial;
            // QA-only: drive SprintController/ResultPanel through their existing public test
            // seams so a screenshot can show a late-race or result state without simulating
            // real taps. Never touches production state flow otherwise.
            public float forceSprintDistance = -1f;
            public string forceSprintResult = "";
            public bool openPause;
            // QA-only: open Settings from the main menu shortly before the capture.
            public bool openSettings;
            // QA-only: open a journey dialogue node on the loaded scene and tap it N times.
            public string openDialogue = "";
            public int dialogueTaps;
            // QA-only: named frog-jump-penalty state, see PlayModeScreenshotQaStates.
            public string qaState = "";
            // QA-only: "<width>x<height>" fixed Game view resolution, e.g. "2400x1080" for 20:9.
            public string gameViewSize = "";
        }

        static PlayModeScreenshot()
        {
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // Optional: -executeMethod entry point for a one-shot legacy invocation.
        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var req = new Request
            {
                id = Guid.NewGuid().ToString(),
                scene = GetArg(args, "-kmaScene"),
                output = GetArg(args, "-kmaOutput") ?? "Builds/Screenshots/screenshot.png",
            };
            var waitArg = GetArg(args, "-kmaWaitSeconds");
            if (waitArg != null) float.TryParse(waitArg, out req.waitSeconds);
            StartCapture(req);
        }

        static void OnUpdate()
        {
            if (Application.isBatchMode)
                return;

            if (!SessionState.GetBool(KeyActive, false))
            {
                PollForNewRequest();
                return;
            }
            RunActiveCapture();
        }

        static void PollForNewRequest()
        {
            string fullPath = Path.GetFullPath(RequestPath);
            if (!File.Exists(fullPath)) return;

            Request req;
            try
            {
                req = JsonUtility.FromJson<Request>(File.ReadAllText(fullPath));
            }
            catch
            {
                return; // likely mid-write; try again next tick
            }

            if (req == null || string.IsNullOrEmpty(req.id) || req.id == SessionState.GetString(KeyLastRequestId, ""))
                return;

            SessionState.SetString(KeyLastRequestId, req.id);
            StartCapture(req);
        }

        static void StartCapture(Request req)
        {
            try
            {
                if (!string.IsNullOrEmpty(req.scene))
                    EditorSceneManager.OpenScene(req.scene);
            }
            catch (Exception e)
            {
                WriteDone(req.id, "error", "OpenScene failed: " + e.Message);
                return;
            }

            if (!string.IsNullOrWhiteSpace(req.gameViewSize))
            {
                if (!TryParseGameViewSize(req.gameViewSize, out int width, out int height))
                {
                    WriteDone(req.id, "error", "gameViewSize must look like 2400x1080: " + req.gameViewSize);
                    return;
                }
                try
                {
                    SetGameViewSize(width, height);
                }
                catch (Exception e)
                {
                    WriteDone(req.id, "error", "Setting the Game view size failed: " + e.Message);
                    return;
                }
            }

            string output = Path.GetFullPath(string.IsNullOrEmpty(req.output) ? "Builds/Screenshots/screenshot.png" : req.output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            SessionState.SetString(KeyId, req.id);
            SessionState.SetString(KeyOutput, output);
            SessionState.SetFloat(KeyCaptureAt, (float)(EditorApplication.timeSinceStartup + req.waitSeconds));
            SessionState.SetInt(KeyPhase, 0);
            SessionState.SetInt(KeyFrames, 0);
            SessionState.SetBool(KeyHoldSprintTutorial, req.holdSprintTutorial);
            SessionState.SetFloat(KeyForceSprintDistance, req.forceSprintDistance);
            SessionState.SetString(KeyForceSprintResult, req.forceSprintResult ?? "");
            SessionState.SetBool(KeyOpenPause, req.openPause);
            SessionState.SetBool(KeyOpenSettings, req.openSettings);
            SessionState.SetString(KeyOpenDialogue, req.openDialogue ?? "");
            SessionState.SetInt(KeyDialogueTaps, req.dialogueTaps);
            SessionState.SetString(KeyQaState, req.qaState ?? "");
            SessionState.SetBool(KeyActive, true);

            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            // Keep the Game view in front so the background Editor keeps rendering it.
            if (SessionState.GetBool(KeyActive, false))
                EditorApplication.ExecuteMenuItem("Window/General/Game");

            if (SessionState.GetBool(KeyHoldSprintTutorial, false))
            {
                var presenter = UnityEngine.Object.FindFirstObjectByType<KMA.Gameplay.SprintStartPresentation>();
                if (presenter != null)
                    presenter.enabled = false;
            }

            float forceDistance = SessionState.GetFloat(KeyForceSprintDistance, -1f);
            if (forceDistance >= 0f)
            {
                var controller = UnityEngine.Object.FindFirstObjectByType<KMA.Gameplay.SprintController>();
                if (controller != null)
                    controller.AdvanceToDistance(forceDistance);
            }

            string forceResult = SessionState.GetString(KeyForceSprintResult, "");
            if (!string.IsNullOrEmpty(forceResult))
            {
                var panel = UnityEngine.Object.FindFirstObjectByType<KMA.Gameplay.UI.ResultPanel>(FindObjectsInactive.Include);
                if (panel != null)
                {
                    bool pass = forceResult == "pass";
                    var result = new KMA.Gameplay.MinigameResult(pass, pass ? 8.4f : 0f,
                        pass ? KMA.Gameplay.Rank.A : KMA.Gameplay.Rank.F);
                    panel.Show(result, "Map");
                }
            }

            if (SessionState.GetBool(KeyOpenPause, false))
            {
                var pausePanel = UnityEngine.Object.FindFirstObjectByType<KMA.Gameplay.UI.PausePanel>();
                if (pausePanel != null)
                    pausePanel.Open();
            }

            if (TryConsumeDialogueRequest(out string dialogueNode, out int dialogueTaps))
            {
                Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    var presenter = UnityEngine.Object.FindFirstObjectByType<KMA.Gameplay.UI.JourneyDialoguePresenter>();
                    if (presenter == null)
                        presenter = canvas.rootCanvas.gameObject.AddComponent<KMA.Gameplay.UI.JourneyDialoguePresenter>();
                    var shell = UnityEngine.Object.FindFirstObjectByType<KMA.Gameplay.Shell.S5ShellSceneController>();
                    presenter.Configure(KMA.Gameplay.JourneyDialogueLibrary.LoadDefault(), _ => true,
                        shell != null ? shell.MainMenuBackground : null);
                    presenter.Show(dialogueNode, null);
                    for (int i = 0; i < dialogueTaps; i++) presenter.Advance();
                }
            }
        }

        /// Returns the dialogue node a capture asked to open, once. A request is honoured only while a
        /// capture is active and is cleared either way, so it cannot revive on a later manual Play.
        public static bool TryConsumeDialogueRequest(out string node, out int taps)
        {
            node = SessionState.GetString(KeyOpenDialogue, "");
            taps = SessionState.GetInt(KeyDialogueTaps, 0);
            bool active = SessionState.GetBool(KeyActive, false);
            SessionState.SetString(KeyOpenDialogue, "");
            SessionState.SetInt(KeyDialogueTaps, 0);
            if (active && !string.IsNullOrEmpty(node)) return true;
            node = null;
            taps = 0;
            return false;
        }

        static void RunActiveCapture()
        {
            if (!EditorApplication.isPlaying) return;

            // A background Editor does not always repaint the Game view; capture would grab a stale frame.
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            int phase = SessionState.GetInt(KeyPhase, 0);
            if (phase == 0)
            {
                // The shell builds Settings in Start, so open it late rather than on EnteredPlayMode.
                if (SessionState.GetBool(KeyOpenSettings, false) &&
                    EditorApplication.timeSinceStartup >= SessionState.GetFloat(KeyCaptureAt, 0) - 1.5f)
                {
                    SessionState.SetBool(KeyOpenSettings, false);
                    var menu = UnityEngine.Object.FindFirstObjectByType<KMA.Gameplay.UI.MainMenuScreen>();
                    if (menu != null) menu.OpenSettings();
                }
                float untilCapture = SessionState.GetFloat(KeyCaptureAt, 0) - (float)EditorApplication.timeSinceStartup;
                string qaState = SessionState.GetString(KeyQaState, "");
                if (qaState != "" && PlayModeScreenshotQaStates.Apply(qaState, untilCapture))
                {
                    SessionState.SetString(KeyQaState, "");
                    qaState = "";
                    // Game time can lag wall time in a background Editor, so capture shortly after the state lands.
                    if (PlayModeScreenshotQaStates.CaptureSoonAfterApply)
                    {
                        untilCapture=PlayModeScreenshotQaStates.CaptureDelaySeconds;
                        SessionState.SetFloat(KeyCaptureAt, (float)EditorApplication.timeSinceStartup + untilCapture);
                    }
                }
                // A pending QA state holds the capture (up to a minute past the requested wait).
                if (qaState != "" ? untilCapture > -60f : untilCapture > 0f) return;
                string output = SessionState.GetString(KeyOutput, null);
                ScreenCapture.CaptureScreenshot(output);
                Debug.Log("[KMA] PlayModeScreenshot captured: " + output);
                SessionState.SetInt(KeyPhase, 1);
                return;
            }

            // Give CaptureScreenshot a few frames to flush the file before tearing down.
            int frames = SessionState.GetInt(KeyFrames, 0) + 1;
            SessionState.SetInt(KeyFrames, frames);
            if (frames < 5) return;

            SessionState.SetBool(KeyActive, false);
            EditorApplication.isPlaying = false;
            WriteDone(SessionState.GetString(KeyId, ""), "ok", null);
        }

        static void WriteDone(string id, string status, string message)
        {
            string fullPath = Path.GetFullPath(DonePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string json = JsonUtility.ToJson(new DoneResult { id = id, status = status, message = message });
            File.WriteAllText(fullPath + ".tmp", json);
            File.Copy(fullPath + ".tmp", fullPath, true);
            File.Delete(fullPath + ".tmp");
        }

        [Serializable]
        class DoneResult
        {
            public string id;
            public string status;
            public string message;
        }

        /// Parses "<width>x<height>" (case-insensitive x, surrounding spaces allowed), both positive.
        public static bool TryParseGameViewSize(string value, out int width, out int height)
        {
            width = height = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string[] parts = value.Trim().ToLowerInvariant().Split('x');
            return parts.Length == 2 &&
                int.TryParse(parts[0], out width) && int.TryParse(parts[1], out height) &&
                width > 0 && height > 0;
        }

        /// Selects (adding it once if needed) a fixed-resolution entry in the Game view's current size
        /// group. Unity has no public API for this, so it goes through UnityEditor.GameViewSizes.
        static void SetGameViewSize(int width, int height)
        {
            Assembly editor = typeof(Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes", true);
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType)
                .GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(sizes, new[] { groupType });
            Type groupClass = group.GetType();
            int count = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null);
            MethodInfo getSize = groupClass.GetMethod("GetGameViewSize");
            Type sizeClass = editor.GetType("UnityEditor.GameViewSize", true);
            Type sizeKind = editor.GetType("UnityEditor.GameViewSizeType", true);
            object fixedKind = Enum.Parse(sizeKind, "FixedResolution");

            int index = -1;
            for (int i = 0; i < count && index < 0; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                if ((int)sizeClass.GetProperty("width").GetValue(size) == width &&
                    (int)sizeClass.GetProperty("height").GetValue(size) == height &&
                    Equals(sizeClass.GetProperty("sizeType").GetValue(size), fixedKind))
                    index = i;
            }
            if (index < 0)
            {
                object custom = sizeClass.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) })
                    .Invoke(new[] { fixedKind, width, height, $"KMA QA {width}x{height}" });
                groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { custom });
                index = count;
            }

            Type gameViewType = editor.GetType("UnityEditor.GameView", true);
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (selected != null && selected.CanWrite)
                selected.SetValue(gameView, index);
            else
                gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(gameView, new object[] { index, null });
            gameView.Repaint();
        }

        static string GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
