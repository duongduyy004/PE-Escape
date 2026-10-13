using System;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    public sealed class MainMenuScreen : ScreenBase
    {
        const string ContinueButtonName = "CONTINUEButton";

        [SerializeField] Button continueButton;

        public event Action NewGameRequested;
        public event Action NewGameConfirmationRequested;
        public event Action ContinueRequested;
        public event Action SettingsRequested;
        public event Action QuitRequested;
        public bool IsConfirmingNewGame { get; private set; }
        public bool CanContinue { get; private set; }

        public void Configure(bool canContinue)
        {
            CanContinue = canContinue;
            Button button = ResolveContinueButton();
            if (button != null)
                button.interactable = canContinue;
        }

        public void NewGame()
        {
            if (!CanContinue)
            {
                NewGameRequested?.Invoke();
                return;
            }
            IsConfirmingNewGame = true;
            NewGameConfirmationRequested?.Invoke();
        }
        public void ConfirmNewGame()
        {
            if (!IsConfirmingNewGame)
                return;
            IsConfirmingNewGame = false;
            NewGameRequested?.Invoke();
        }
        public void CancelNewGame() => IsConfirmingNewGame = false;
        public void Continue()
        {
            if (!CanContinue)
                return;
            ContinueRequested?.Invoke();
        }
        public void OpenSettings() => SettingsRequested?.Invoke();
        public void Quit() => QuitRequested?.Invoke();

        Button ResolveContinueButton()
        {
            if (continueButton != null)
                return continueButton;

            foreach (Button candidate in GetComponentsInChildren<Button>(true))
            {
                if (string.Equals(candidate.gameObject.name, ContinueButtonName, StringComparison.Ordinal))
                {
                    continueButton = candidate;
                    break;
                }
            }

            return continueButton;
        }
    }
}
