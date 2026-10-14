using KMA.Gameplay.UI;
using UnityEngine;
namespace KMA.Gameplay.Volleyball
{
    public sealed class VolleyballDialogueAdapter : MonoBehaviour
    {
        [SerializeField] VolleyballController controller;
        [SerializeField] GameplayDialogueHost host;
        [SerializeField] GameplayOpeningFlow opening;
        VolleyballMatch match;
        VolleyAthleteView view;
        bool diving, pendingTiming, warned;
        void Start()=>Configure(controller,host,opening);
        public void Configure(VolleyballController source,GameplayDialogueHost dialogue,GameplayOpeningFlow flow)
        {
            Unsubscribe();controller=source;host=dialogue;opening=flow;
            if (source==null || host==null) return;
            match=source.Match;view=source.PlayerView;
            match.PlayerActed+=OnActed;match.PointScored+=OnPoint;source.ResultDecided+=OnResult;
            if(view!=null)view.ActionPresented+=OnAction;
            var kind=source.CurrentChallenge?.Kind;
            opening?.Configure(source,host,kind==ChallengeKind.Learn?"vb.learn.start":kind==ChallengeKind.Practice?"vb.practice.start":kind==ChallengeKind.Exam?"vb.exam.start":null);
        }
        bool Playing=>controller!=null && controller.PresentationPhase==MinigamePhase.Play && match!=null && !match.IsOver;
        void OnActed(ActionDecision decision)
        {
            if(controller.CurrentChallenge?.Kind==ChallengeKind.Practice && decision.IsTimed &&
                decision.Grade==TimingGrade.Late) pendingTiming=true;
        }
        void EmitGap(string id)
        {
            int points=match.PlayerPoints+match.OpponentPoints;
            host.Emit(id,()=>Playing && match.PlayerPoints+match.OpponentPoints==points && match.BallState==BallState.Dead,
                budgetSeconds:VolleyballMatch.PointPause);
        }
        void OnPoint(CourtSide winner)
        {
            if(!Playing)return;
            var kind=controller.CurrentChallenge?.Kind;
            // The late-timing reaction is a miss-only line ("Tớ đỡ lệch rồi").
            // A late input can still be resolved as a player point, so do not
            // enqueue the loss dialogue on a successful rally.
            if(kind==ChallengeKind.Practice && pendingTiming)
            {
                pendingTiming=false;
                if(winner==CourtSide.Opponent)EmitGap("vb.practice.badtime");
            }
            if(winner==CourtSide.Opponent && kind==ChallengeKind.Learn)EmitGap("vb.learn.miss");
            else if(winner==CourtSide.Opponent && kind==ChallengeKind.Exam)EmitGap("vb.exam.lostpoint");
            if(kind==ChallengeKind.Exam && match.PlayerPoints==9 && !warned){warned=true;host.Emit("vb.exam.matchpoint",()=>Playing,budgetSeconds:3.7f);}
        }
        void OnAction(AthleteAction action)
        {
            if(action==AthleteAction.Dive){diving=true;return;}
            if(!diving)return;
            diving=false;
            if(Playing && controller.CurrentChallenge?.Kind==ChallengeKind.Exam)
                host.Emit("vb.exam.dive",()=>Playing && match.Player.Action!=AthleteAction.Dive,budgetSeconds:3.7f);
        }
        void Update()
        {
            if(match!=null && (match.BallState==BallState.Held || match.BallState==BallState.Toss))pendingTiming=false;
        }
        void OnResult(bool pass)
        {
            var kind=controller.CurrentChallenge?.Kind;
            string id=pass && kind==ChallengeKind.Learn?"vb.learn.done":pass && kind==ChallengeKind.Practice?"vb.practice.done":null;
            host.BeginResult(id);if(id!=null)host.Emit(id);
        }
        void Unsubscribe()
        {
            if(match!=null){match.PlayerActed-=OnActed;match.PointScored-=OnPoint;}
            if(controller!=null)controller.ResultDecided-=OnResult;
            if(view!=null)view.ActionPresented-=OnAction;
            match=null;view=null;
        }
        void OnDisable()=>Unsubscribe();
    }
}
