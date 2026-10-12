using KMA.Gameplay.UI;
using UnityEngine;
namespace KMA.Gameplay.Chess
{
    public sealed class ChessDialogueAdapter : MonoBehaviour
    {
        [SerializeField] ChessFinalController controller;
        [SerializeField] GameplayDialogueHost host;
        [SerializeField] GameplayOpeningFlow opening;
        [SerializeField] ChessCastView view;
        ChessFinalStateMachine machine;
        bool warned, firstCorrect, relief;
        public bool OpeningReady=>host==null || view==null || machine==null || opening==null || opening.IsComplete;
        public void Configure(ChessFinalController source,GameplayDialogueHost dialogue,ChessCastView cast)
        {
            Unsubscribe();controller=source;host=dialogue;view=cast;
            if(source==null || host==null || view==null)return;
            machine=source.Machine;machine.MoveCommitted+=OnMove;
            view.BindDialogue(host);host.SetView(view);
            view.SetStudent("fear");
            opening?.Configure(source,host,"chess.start");
        }
        public void Began()
        {
            if(machine==null || host==null || view==null)return;
            host.Clear();view.SetTeacher("strictLook");view.SetStudent("think");
            if(machine.Puzzle.id=="m2_001")host.Emit("ch.extra.begin",()=>machine.CanMove && machine.PlayerMovesMade==0,budgetSeconds:2.5f);
        }
        public void Mistake(int count)
        {
            if(machine==null || host==null || count>machine.MaxMistakes)return;
            host.Clear();host.Emit(count==1?"chess.mistake1":"chess.mistake2");
        }
        public void Hint(string text)
        {
            if(machine==null || host==null || view==null)return;
            view.SetStudent("think");int node=machine.Node, level=machine.HintLevel;
            // An explicit hint replaces earlier speech, including another hint level.
            host.Clear();
            host.EmitText("chess.hint"+level,text,8f,()=>machine!=null && machine.Node==node && machine.HintLevel==level && machine.CanMove);
        }
        public void FinishDialogue(bool solved)
        {
            if(machine==null || host==null || view==null)return;
            string id=solved?"chess.solved":machine.FailReason==ChessFailReason.TimeUp?"chess.timeout":"chess.failed";
            host.BeginResult(id);view.SetStudent(solved?"happy":"disappointed");host.Emit(id);
        }
        void OnMove(ChessMove move,bool byPlayer){host.Clear();}
        void Update()
        {
            if(machine==null)return;
            if(machine.Phase==ChessFinalPhase.Intro)
            {
                var hud=controller.GetComponentInChildren<ChessFinalHud>();
                if(hud==null)hud=FindFirstObjectByType<ChessFinalHud>();
                if(hud!=null)hud.StartButton.interactable=OpeningReady;
            }
            if(machine.CanMove)
            {
                if(!firstCorrect && machine.Puzzle.id=="m2_001" && machine.PlayerMovesMade==1)
                {firstCorrect=true;host.Emit("ch.extra.firstcorrect",()=>machine.CanMove && machine.PlayerMovesMade==1,budgetSeconds:3.7f);}
                if(!warned && machine.Clock.Remaining<=15 && machine.Clock.Remaining>0)
                {warned=true;host.Emit("ch.extra.lowtime",()=>machine.CanMove,budgetSeconds:machine.Clock.Remaining);}
            }
            if(machine.Phase==ChessFinalPhase.Completed && host.IsIdle && !relief)
            {
                relief=true;var panel=FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
                if(panel==null || !panel.IsVisible)host.Emit("ch.extra.relief",()=>panel==null || !panel.IsVisible,budgetSeconds:2.5f);
            }
        }
        void Unsubscribe(){if(machine!=null)machine.MoveCommitted-=OnMove;machine=null;}
        void OnDisable()=>Unsubscribe();
    }
}
