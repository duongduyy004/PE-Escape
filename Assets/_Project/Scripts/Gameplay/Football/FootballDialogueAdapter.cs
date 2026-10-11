using KMA.Gameplay.UI;
using UnityEngine;
namespace KMA.Gameplay
{
    public sealed class FootballDialogueAdapter : MonoBehaviour
    {
        [SerializeField] FootballController controller;
        [SerializeField] GameplayDialogueHost host;
        [SerializeField] GameplayOpeningFlow opening;
        bool subscribed, lastKick;
        int describedKick=-1;
        void Start()=>Configure(controller,host,opening);
        public void Configure(FootballController source,GameplayDialogueHost dialogue,GameplayOpeningFlow flow)
        {
            Unsubscribe();controller=source;host=dialogue;opening=flow;
            if(source==null || host==null)return;
            source.StatePresented+=OnState;source.ResultDecided+=OnResult;subscribed=true;
            var kind=source.CurrentChallenge?.Kind;
            opening?.Configure(source,host,kind==ChallengeKind.Learn?"fb.learn.start":kind==ChallengeKind.Practice?"fb.practice.start":kind==ChallengeKind.Exam?"fb.exam.start":null);
        }
        void OnState(FootballState state)
        {
            var rules=controller.Rules;var kind=controller.CurrentChallenge?.Kind;
            if(state==FootballState.Aiming && kind==ChallengeKind.Exam && rules.Kicks==4 && !lastKick)
            {
                lastKick=true;host.Emit("fb.exam.lastkick",()=>rules.State==FootballState.Aiming,budgetSeconds:3.7f);
            }
            if(state!=FootballState.ShotResult || rules.Kicks==describedKick)return;
            describedKick=rules.Kicks;
            string id=kind==ChallengeKind.Learn && rules.LastOutcome!=FootballOutcome.Goal?"fb.learn.miss":
                kind==ChallengeKind.Practice && rules.LastOutcome==FootballOutcome.Saved?"fb.practice.saved":
                kind==ChallengeKind.Exam && rules.LastOutcome==FootballOutcome.Goal && rules.Kicks<5?"fb.exam.goal":null;
            int kick=rules.Kicks;
            if(id!=null)host.Emit(id,()=>rules.Kicks==kick && (rules.State==FootballState.ShotResult || rules.State==FootballState.Aiming),budgetSeconds:3.7f);
        }
        void OnResult(bool pass)
        {
            var kind=controller.CurrentChallenge?.Kind;
            string id=pass && kind==ChallengeKind.Learn?"fb.learn.done":pass && kind==ChallengeKind.Practice?"fb.practice.done":!pass && kind==ChallengeKind.Exam?"fb.exam.impossible":null;
            host.BeginResult(id);if(id!=null)host.Emit(id);
        }
        void Unsubscribe()
        {
            if(subscribed && controller!=null){controller.StatePresented-=OnState;controller.ResultDecided-=OnResult;}
            subscribed=false;
        }
        void OnDisable()=>Unsubscribe();
    }
}
