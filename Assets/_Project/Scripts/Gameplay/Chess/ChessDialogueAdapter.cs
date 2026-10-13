using System.Collections.Generic;
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
        ChessAttemptMachine machine;
        bool warned, firstCorrect, relief;
        // Read by the opening line when it plays, so it carries the limits of the puzzle that is on the board.
        readonly Dictionary<string,string> openingTokens=new Dictionary<string,string>();
        public bool OpeningReady=>host==null || view==null || opening==null || opening.IsComplete;
        public void Configure(ChessFinalController source,GameplayDialogueHost dialogue,ChessCastView cast)
        {
            Unsubscribe();controller=source;host=dialogue;view=cast;
            if(source==null || host==null || view==null)return;
            view.BindDialogue(host);host.SetView(view);
            view.SetStudent("fear");
            opening?.Configure(source,host,"chess.start",openingTokens,()=>machine!=null);
            source.PuzzleReadyEvent+=Bind;
            if(source.Machine!=null)Bind(source.Machine);
        }
        void Bind(ChessAttemptMachine next)
        {
            if(machine!=null){machine.MoveCommitted-=OnMove;machine.PhaseChanged-=OnPhase;}
            machine=next;warned=firstCorrect=relief=false;
            if(machine==null)return;
            openingTokens["maxMoves"]=machine.MaxMoves.ToString(System.Globalization.CultureInfo.InvariantCulture);
            openingTokens["timeSeconds"]=Mathf.RoundToInt(machine.Clock.Limit).ToString(System.Globalization.CultureInfo.InvariantCulture);
            machine.MoveCommitted+=OnMove;machine.PhaseChanged+=OnPhase;
        }
        public void Began()
        {
            if(machine==null || host==null || view==null)return;
            host.Clear();view.SetTeacher("strictLook");view.SetStudent("think");
            if(HasSecondAttacker(machine.Position))host.Emit("ch.extra.begin",()=>machine.CanMove && machine.MovesUsed==0,budgetSeconds:2.5f);
        }
        public void Hint(string text)
        {
            if(machine==null || host==null || view==null)return;
            view.SetStudent("think");int revision=machine.Revision, level=machine.HintLevel;
            // An explicit hint replaces earlier speech, including another hint level.
            host.Clear();
            host.EmitText("chess.hint"+level,text,8f,()=>machine!=null && machine.Revision==revision && machine.HintLevel==level && machine.CanMove);
        }
        public void FinishDialogue(bool solved)
        {
            if(machine==null || host==null || view==null)return;
            string id=solved?"chess.solved":machine.EndReason==ChessEndReason.TimeUp?"chess.timeout":"chess.failed";
            host.BeginResult(id);view.SetStudent(solved?"happy":"disappointed");host.Emit(id);
        }
        /// Two or more white pieces besides the king and pawns, so "both must work together" is true.
        static bool HasSecondAttacker(ChessPosition position)
        {
            int attackers=0;
            for(int square=0;square<64;square++)
            {
                sbyte piece=position[square];
                if(piece==0 || Piece.ColorOf(piece)!=PieceColor.White)continue;
                int type=Piece.TypeOf(piece);
                if(type!=Piece.King && type!=Piece.Pawn)attackers++;
            }
            return attackers>=2;
        }
        void OnMove(ChessMove move,bool byPlayer){host.Clear();}
        void OnPhase(ChessAttemptPhase phase){if(phase==ChessAttemptPhase.Intro)warned=firstCorrect=relief=false;}
        void Update()
        {
            if(machine==null)return;
            if(machine.Phase==ChessAttemptPhase.Intro)
            {
                var hud=controller.GetComponentInChildren<ChessFinalHud>();
                if(hud==null)hud=FindFirstObjectByType<ChessFinalHud>();
                if(hud!=null)hud.StartButton.interactable=OpeningReady;
            }
            if(machine.CanMove)
            {
                if(!firstCorrect && machine.MovesUsed==1 && machine.Moves.Count==1 && machine.Moves[0].Q>=0.999)
                {firstCorrect=true;host.Emit("ch.extra.firstcorrect",()=>machine.CanMove && machine.MovesUsed==1,budgetSeconds:3.7f);}
                if(!warned && machine.Clock.Remaining<=15 && machine.Clock.Remaining>0)
                {
                    warned=true;
                    var remaining=new Dictionary<string,string>{{"secondsRemaining",Mathf.CeilToInt(machine.Clock.Remaining).ToString(System.Globalization.CultureInfo.InvariantCulture)}};
                    host.Emit("ch.extra.lowtime",()=>machine.CanMove,budgetSeconds:machine.Clock.Remaining,tokens:remaining);
                }
            }
            if(machine.Phase==ChessAttemptPhase.Completed && host.IsIdle && !relief)
            {
                relief=true;var panel=FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
                if(panel==null || !panel.IsVisible)host.Emit("ch.extra.relief",()=>panel==null || !panel.IsVisible,budgetSeconds:2.5f);
            }
        }
        void Unsubscribe()
        {
            if(controller!=null)controller.PuzzleReadyEvent-=Bind;
            if(machine!=null){machine.MoveCommitted-=OnMove;machine.PhaseChanged-=OnPhase;}
            machine=null;
        }
        void OnDisable()=>Unsubscribe();
    }
}
