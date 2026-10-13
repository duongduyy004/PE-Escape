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
                {
                    GameSession journeySession = router.Session;
                    dialogues.ShowJourney(journeySession, GameManager.Instance, mainMenuBackground,
                        router.HasPendingJourneyMapContinuation
                            ? () => StartCoroutine(CompleteJourneyMapDialogue(router, journeySession, dialogues))
                            : null);
                    if (router.HasPendingJourneyMapContinuation)
                        router.SceneLoadFailed += OnMapSceneLoadFailed;
                }
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
                var router = SceneRouter.Instance;
                if (router != null) router.SceneLoadFailed -= OnMapSceneLoadFailed;
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
            UiKit.Stretch(confirmationRoot.GetComponent<RectTransform>());
            confirmationRoot.GetComponent<Image>().color = UITheme.Shared.Scrim;

            ShellCardHandle card = ShellKit.Card(confirmationRoot.transform, "ConfirmationCard");
            card.Root.anchorMin = card.Root.anchorMax = new Vector2(.5f, .5f);
            card.Root.sizeDelta = new Vector2(660f, 400f);
            ShellTitleHandle title = ShellKit.Title(card.Root, "Title", "BẮT ĐẦU HÀNH TRÌNH MỚI?", MinigameUiTheme.BodyLarge);
            Place(title.Root, new Vector2(0f, 110f), new Vector2(580f, 70f));
            TMP_Text message = ShellKit.Label(card.Root, "Message",
                "Tiến độ hiện tại sẽ được thay thế.\nBạn có chắc muốn tiếp tục?", MinigameUiTheme.Caption,
                ShellTheme.BodyText, VietFontRole.Body);
            message.textWrappingMode = TextWrappingModes.Normal;
            Place(message.rectTransform, new Vector2(0f, 20f), new Vector2(580f, 100f));
            AddButton(card.Root, "XÁC NHẬN", new Vector2(-145f, -115f), mainMenu.ConfirmNewGame, ShellButtonVariant.Danger);
            AddButton(card.Root, "QUAY LẠI", new Vector2(145f, -115f), () =>
            {
                mainMenu.CancelNewGame();
                confirmationRoot.SetActive(false);
            }, ShellButtonVariant.Secondary);
            confirmationRoot.SetActive(false);
        }

        static Button AddButton(Transform parent, string label, Vector2 position,
            UnityEngine.Events.UnityAction action, ShellButtonVariant variant)
        {
            ShellButtonHandle handle = ShellKit.Button(parent, label + "Button", label, variant);
            Place((RectTransform)handle.Button.transform, position, new Vector2(260f, ShellTheme.ControlHeight));
            handle.Button.onClick.AddListener(action);
            return handle.Button;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
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

        IEnumerator CompleteJourneyMapDialogue(SceneRouter router, GameSession journeySession,
            JourneyDialoguePresenter dialogues)
        {
            while (router != null && router.IsTransitioning)
                yield return null;
            if (router == null || !router.HasPendingJourneyMapContinuation)
                yield break;
            if (!router.CompleteJourneyMapDialogue(journeySession) && router.HasPendingJourneyMapContinuation)
                dialogues?.ShowContinuationError("Không thể mở bài. Chạm để thử lại.",
                    () => StartCoroutine(CompleteJourneyMapDialogue(router, journeySession, dialogues)));
        }

        void OnMapSceneLoadFailed(string message)
        {
            SceneRouter router = SceneRouter.Instance;
            if (router == null || !router.HasPendingJourneyMapContinuation)
                return;
            JourneyDialoguePresenter dialogues = map == null ? null : map.GetComponent<JourneyDialoguePresenter>();
            dialogues?.ShowContinuationError(message, () => StartCoroutine(CompleteJourneyMapDialogue(
                router, router.Session, dialogues)));
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
