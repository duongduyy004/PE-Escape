using System.Collections.Generic;
using KMA.Gameplay.UI;
using UnityEngine;
namespace KMA.Gameplay.FrogJump
{
    public sealed class FrogJumpDialogueAdapter : MonoBehaviour
    {
        [SerializeField] FrogJumpController controller;
        [SerializeField] GameplayDialogueHost host;
        [SerializeField] GameplayOpeningFlow opening;
        FrogJumpRules rules;
        bool goodCandidate, warned, final;
        void Start()=>Configure(controller,host,opening);
        public void Configure(FrogJumpController source,GameplayDialogueHost dialogue,GameplayOpeningFlow flow)
        {
            Unsubscribe();controller=source;host=dialogue;opening=flow;
            if(source==null || host==null)return;
            rules=source.Rules;rules.Landed+=OnLanded;source.StatePresented+=OnState;
            string id=source.PendingSavesLife==true?"frog.start.save":source.PendingSavesLife==false?"frog.start.charged":null;
            opening?.Configure(source,host,id,new Dictionary<string,string>{
                {"distance",MinigameGuidePages.Number(rules.Tuning.trackMetres)},
                {"time",MinigameGuidePages.Number(rules.Tuning.timeLimitSeconds)} });
        }
        void OnLanded(float metres)
        {
            if(metres>0){goodCandidate=metres>=rules.Tuning.maxJumpMetres*.9f;return;}
            goodCandidate=false;
            host.Emit(rules.Falls==1?"frog.fall.first":"frog.fall.repeat",
                ()=>rules.State==FrogJumpState.Fallen && !rules.IsOver,
                budgetSeconds:Mathf.Max(0,rules.Tuning.recoverSeconds-rules.StateElapsed));
        }
        void OnState(FrogJumpState state)
        {
            if(rules.IsOver)
            {
                goodCandidate=false;
                if(!final){final=true;string id=rules.ReachedFinish?"frog.finish":"frog.timeout";host.BeginResult(id);host.Emit(id);}
                return;
            }
            if(!warned && rules.TimeRemaining<=10 && rules.TimeRemaining>0)
            {
                warned=true;
                if(state!=FrogJumpState.Fallen)host.Emit("frog.lowtime",()=>!rules.IsOver && rules.State!=FrogJumpState.Fallen,budgetSeconds:rules.TimeRemaining);
            }
            if(goodCandidate && state==FrogJumpState.Aiming)
            {
                goodCandidate=false;
                host.Emit("frog.goodjump",()=>!rules.IsOver && rules.State!=FrogJumpState.Fallen,budgetSeconds:3.7f);
            }
        }
        void Unsubscribe()
        {
            if(rules!=null)rules.Landed-=OnLanded;
            if(controller!=null)controller.StatePresented-=OnState;
            rules=null;
        }
        void OnDisable()=>Unsubscribe();
    }
}
