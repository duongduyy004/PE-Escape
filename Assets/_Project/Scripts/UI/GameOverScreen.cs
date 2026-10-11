using System;
using UnityEngine;

namespace KMA.Gameplay.UI
{
    public sealed class GameOverScreen : ScreenBase
    {
        [SerializeField] Sprite background;
        public Sprite Background => background;
        public void SetBackground(Sprite sprite) => background = sprite;

        [SerializeField] GameplayDialogueHost dialogue;
        [SerializeField] GameplayDialogueView dialogueView;
        bool spoke;
        public void EnsureDialogue()
        {
            if(dialogueView==null)
            {
                dialogueView=GetComponent<GameplayDialogueView>();
                if(dialogueView==null)dialogueView=gameObject.AddComponent<GameplayDialogueView>();dialogueView.SetSlot(DialogueHudSlot.GameOver);
                dialogueView.Configure(JourneyDialogueLibrary.LoadDefault(),transform as RectTransform);
            }
            if(!Application.isPlaying)dialogueView.Configure(JourneyDialogueLibrary.LoadDefault(),transform as RectTransform);
            if(dialogue==null)dialogue=GetComponent<GameplayDialogueHost>();
            if(dialogue==null)dialogue=gameObject.AddComponent<GameplayDialogueHost>();
            dialogue.Configure(GameplayDialogueLibrary.LoadDefault(),dialogueView);
        }
        public override void Show()
        {
            base.Show();EnsureDialogue();
            if(!spoke && Application.isPlaying){spoke=true;dialogue.BeginResult();dialogue.Emit("gameover.comment");}
        }
        void Start(){if(gameObject.scene.name=="GameOver" && gameObject.activeInHierarchy)Show();}

        public event Action RetryRequested;
        public event Action NewGameRequested;
        public event Action MenuRequested;
        public event Action RestartRequested;
        public event Action ExitToMapRequested;

        public void Retry() => RetryRequested?.Invoke();
        public void NewGame() => NewGameRequested?.Invoke();
        public void Restart() => RestartRequested?.Invoke();
        public void ExitToMap() => ExitToMapRequested?.Invoke();
        public void ReturnToMenu() => MenuRequested?.Invoke();
    }
}
