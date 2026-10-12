using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.Shell
{
    public sealed class S5ShellSceneController : MonoBehaviour
    {
        [SerializeField] MainMenuScreen mainMenu;
        [SerializeField] MapScreen map;
        [SerializeField] GameOverScreen gameOver;
        [SerializeField] SettingsScreen settings;
        [SerializeField] CalibrateScreen calibrate;
        [SerializeField] Sprite mainMenuBackground;
        GameObject confirmationRoot;

        public Sprite MainMenuBackground => mainMenuBackground;

        void Awake()
        {
            mainMenu = mainMenu ?? GetComponentInChildren<MainMenuScreen>(true);
            map = map ?? GetComponentInChildren<MapScreen>(true);
            gameOver = gameOver ?? GetComponentInChildren<GameOverScreen>(true);
            settings = settings ?? GetComponentInChildren<SettingsScreen>(true);
            calibrate = calibrate ?? GetComponentInChildren<CalibrateScreen>(true);

            if (mainMenu != null)
            {
                // Settings is authored under SafeArea (also MainMenuScreen). Give it a
                // sibling safe-area root so hiding the menu cannot hide settings too.
                if (settings != null && settings.transform.IsChildOf(mainMenu.transform))
                {
                    settings.transform.SetParent(mainMenu.transform.parent, false);
                    settings.gameObject.AddComponent<RectTransform>();
                    settings.gameObject.AddComponent<SafeAreaFitter>();
                }
                if (settings != null)
                    SettingsPresentationBuilder.Build(settings);
                HomePresentationBuilder.Build(mainMenu);
                mainMenu.Show();
                settings?.Hide();
                calibrate?.Hide();
                EnsureNewGameConfirmation();
                mainMenu.Configure(GameManager.Instance != null && GameManager.Instance.HasSavedCampaign);
                mainMenu.ContinueRequested += ContinueCampaign;
                mainMenu.NewGameRequested += StartNewGame;
                mainMenu.NewGameConfirmationRequested += ShowNewGameConfirmation;
                mainMenu.SettingsRequested += OpenSettings;
                mainMenu.QuitRequested += Quit;
                BindMainMenuButtons();
            }
            if (map != null)
            {
                var router = SceneRouter.Instance;
                BuildMapPresentation(router == null ? null : router.Session);
                JourneyDialoguePresenter dialogues = map.GetComponent<JourneyDialoguePresenter>();
                if (dialogues == null) dialogues = map.gameObject.AddComponent<JourneyDialoguePresenter>();
                if (router != null && router.Session != null && GameManager.Instance != null)
                    dialogues.ShowJourney(router.Session, GameManager.Instance, mainMenuBackground);
                if (router != null && !string.IsNullOrEmpty(router.PendingJourneyChallengeId))
                    StartCoroutine(ContinueJourneyAfterDialogue(router, dialogues));
                map.SubjectRequested += SelectMapSubject;
                map.ChallengeRequested += StartChallenge;
                map.CelebrationRequested += ReplayCelebration;
            }
            if (gameOver != null)
            {
                var router = SceneRouter.Instance;
                GameOverPresentationBuilder.Build(gameOver, router == null ? null : router.Session);
                gameOver.RetryRequested += StartNewGame;
                gameOver.NewGameRequested += StartNewGame;
                gameOver.MenuRequested += OpenMenu;
                BindGameOverButtons();
            }
            if (settings != null)
            {
                settings.SettingsChanged += ApplySettings;
                settings.CalibrateRequested += OpenCalibrate;
                settings.BackRequested += OpenMainMenu;
            }
            if (calibrate != null)
            {
                calibrate.OffsetChanged += ApplyOffset;
                calibrate.BackRequested += OpenSettings;
            }
        }

        IEnumerator ContinueJourneyAfterDialogue(SceneRouter router, JourneyDialoguePresenter dialogues)
        {
            // Map Awake runs while its scene load is still in progress. Wait for both
            // the load and all queued dialogue nodes before starting the next challenge.
            while (router != null && (router.IsTransitioning || dialogues.IsShowing))
                yield return null;
            if (router != null && !router.ContinuePendingJourney(out string error) &&
                !string.IsNullOrEmpty(error))
            {
                string id = router.Session.Journey.CheckpointChallengeId;
                if (!string.IsNullOrEmpty(id))
                    map.LessonList?.Open(router.Session.Journey.Catalog.Get(id).Subject);
                map.LessonList?.ShowStartError(error);
            }
        }

        void OnDestroy()
        {
            if (mainMenu != null)
            {
                mainMenu.ContinueRequested -= ContinueCampaign;
                mainMenu.NewGameRequested -= StartNewGame;
                mainMenu.NewGameConfirmationRequested -= ShowNewGameConfirmation;
                mainMenu.SettingsRequested -= OpenSettings;
                mainMenu.QuitRequested -= Quit;
                UnbindMainMenuButtons();
            }
            if (map != null)
            {
                map.SubjectRequested -= SelectMapSubject;
                map.ChallengeRequested -= StartChallenge;
                map.CelebrationRequested -= ReplayCelebration;
            }
            if (gameOver != null)
            {
                gameOver.RetryRequested -= StartNewGame;
                gameOver.NewGameRequested -= StartNewGame;
                gameOver.MenuRequested -= OpenMenu;
                UnbindGameOverButtons();
            }
            if (settings != null)
            {
                settings.SettingsChanged -= ApplySettings;
                settings.CalibrateRequested -= OpenCalibrate;
                settings.BackRequested -= OpenMainMenu;
            }
            if (calibrate != null)
            {
                calibrate.OffsetChanged -= ApplyOffset;
                calibrate.BackRequested -= OpenSettings;
            }
        }

        static void ContinueCampaign()
        {
            var router = SceneRouter.Instance;
            if (router != null)
                router.ResumeCampaign();
        }

        static void StartNewGame() => GameManager.Instance?.StartNewGame();

        static void OpenMenu() => SceneRouter.Instance?.RouteToMenu();

        void BindMainMenuButtons()
        {
            BindButton(mainMenu.transform, "CONTINUEButton", mainMenu.Continue);
            BindButton(mainMenu.transform, "NEW GAMEButton", mainMenu.NewGame);
            BindButton(mainMenu.transform, "SETTINGSButton", mainMenu.OpenSettings);
            BindButton(mainMenu.transform, "QUITButton", mainMenu.Quit);
        }

        void UnbindMainMenuButtons()
        {
            UnbindButton(mainMenu.transform, "CONTINUEButton", mainMenu.Continue);
            UnbindButton(mainMenu.transform, "NEW GAMEButton", mainMenu.NewGame);
            UnbindButton(mainMenu.transform, "SETTINGSButton", mainMenu.OpenSettings);
            UnbindButton(mainMenu.transform, "QUITButton", mainMenu.Quit);
        }

        void BindGameOverButtons()
        {
            BindButton(gameOver.transform, "RETRYButton", gameOver.Retry);
            BindButton(gameOver.transform, "NEW GAMEButton", gameOver.NewGame);
            BindButton(gameOver.transform, "MAIN MENUButton", gameOver.ReturnToMenu);
        }

        void UnbindGameOverButtons()
        {
            UnbindButton(gameOver.transform, "RETRYButton", gameOver.Retry);
            UnbindButton(gameOver.transform, "NEW GAMEButton", gameOver.NewGame);
            UnbindButton(gameOver.transform, "MAIN MENUButton", gameOver.ReturnToMenu);
        }

        static void BindButton(Transform root, string name, UnityEngine.Events.UnityAction action)
        {
            var target = root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == name);
            target?.onClick.AddListener(action);
        }

        static void UnbindButton(Transform root, string name, UnityEngine.Events.UnityAction action)
        {
            var target = root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == name);
            target?.onClick.RemoveListener(action);
        }
        void ShowNewGameConfirmation() => confirmationRoot?.SetActive(true);

        void OpenSettings()
        {
            confirmationRoot?.SetActive(false);
            mainMenu?.CancelNewGame();
            mainMenu?.Hide();
            calibrate?.Hide();
            settings?.Configure(GameManager.Instance?.Settings);
            settings?.Show();
        }

        void OpenCalibrate()
        {
            settings?.Hide();
            calibrate?.Configure(GameManager.Instance?.Settings?.rhythmOffsetMs ?? 0f);
            calibrate?.Show();
        }

        void OpenMainMenu()
        {
            settings?.Hide();
            calibrate?.Hide();
            mainMenu?.Configure(GameManager.Instance != null && GameManager.Instance.HasSavedCampaign);
            mainMenu?.Show();
        }

        void BuildMapPresentation(GameSession session)
            => MapPresentationBuilder.Build(map, session, mainMenuBackground);

        void EnsureNewGameConfirmation()
        {
            if (confirmationRoot != null)
                return;
            confirmationRoot = new GameObject("NewGameConfirmation", typeof(RectTransform), typeof(Image));
            confirmationRoot.transform.SetParent(mainMenu.transform, false);
            RectTransform overlayRect = confirmationRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            confirmationRoot.GetComponent<Image>().color = new Color(0.015f, .06f, .1f, .82f);

            var card = new GameObject("ConfirmationCard", typeof(RectTransform), typeof(Image), typeof(Outline));
            card.transform.SetParent(confirmationRoot.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f);
            cardRect.sizeDelta = new Vector2(620f, 380f);
            card.GetComponent<Image>().color = new Color32(255, 249, 231, 255);
            Outline outline = card.GetComponent<Outline>();
            outline.effectColor = new Color32(3, 18, 33, 255);
            outline.effectDistance = new Vector2(5f, -5f);
            AddLabel(card.transform, "Title", "BẮT ĐẦU HÀNH TRÌNH MỚI?", new Vector2(0f, 98f), 34,
                new Color32(8, 35, 61, 255));
            AddLabel(card.transform, "Message", "Tiến độ hiện tại sẽ được thay thế.\nBạn có chắc muốn tiếp tục?",
                new Vector2(0f, 25f), 22, new Color32(62, 79, 96, 255));
            AddButton(card.transform, "XÁC NHẬN", new Vector2(-145f, -105f), mainMenu.ConfirmNewGame,
                ButtonVariant.Danger);
            AddButton(card.transform, "QUAY LẠI", new Vector2(145f, -105f), () =>
            {
                mainMenu.CancelNewGame();
                confirmationRoot.SetActive(false);
            }, ButtonVariant.Secondary);
            confirmationRoot.SetActive(false);
        }

        static Button AddButton(Transform parent, string label, Vector2 position,
            UnityEngine.Events.UnityAction action, ButtonVariant variant)
        {
            ButtonHandle handle = UiKit.Button(parent, label + "Button", label, variant);
            var rect = (RectTransform)handle.Button.transform;
            rect.sizeDelta = new Vector2(250f, MinigameUiTheme.ButtonHeight);
            rect.anchoredPosition = position;
            handle.Button.onClick.AddListener(action);
            return handle.Button;
        }

        static TMP_Text AddLabel(Transform parent, string name, string value, Vector2 position,
            int fontSize, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(540f, 90f);
            rect.anchoredPosition = position;
            TMP_Text text = root.AddComponent<TextMeshProUGUI>();
            text.text = VietText.Fix(value);
            text.alignment = TextAlignmentOptions.Center;
            UiKit.StyleLabel(text, fontSize, color);
            text.raycastTarget = false;
            UiKit.FitLabel(text, fontSize + 10f);
            return text;
        }

        void SelectMapSubject(SubjectId subject)
        {
            map?.LessonList?.ShowSubject(subject);
        }

        static void ReplayCelebration() => SceneRouter.Instance?.RouteToCelebration();

        static void StartChallenge(string id, ChallengeAttemptMode mode)
        {
            SceneRouter router = SceneRouter.Instance;
            if (router == null || router.Session == null) return;
            ChallengeDefinition definition = router.Session.Journey.Catalog.Get(id);
            router.TryStartChallenge(id, mode, definition.Difficulty);
        }

        static void ApplySettings(Settings value) => GameManager.Instance?.UpdateSettings(value);

        static void ApplyOffset(float value)
        {
            var manager = GameManager.Instance;
            if (manager == null || manager.Settings == null)
                return;
            manager.Settings.rhythmOffsetMs = value;
            manager.UpdateSettings(manager.Settings);
        }

        static void Quit() => Application.Quit();
    }
}
