using System.Collections.Generic;
using System.Globalization;
using KMA.Gameplay.UI;
using UnityEngine;
namespace KMA.Gameplay
{
    public sealed class SprintDialogueAdapter : MonoBehaviour
    {
        [SerializeField] SprintController controller;
        [SerializeField] GameplayDialogueHost host;
        [SerializeField] GameplayOpeningFlow opening;
        [SerializeField] Animator playerAnimator;
        bool subscribed, warned, wasStumbling;
        void Start() => Configure(controller,host,opening);
        public void Configure(SprintController source, GameplayDialogueHost dialogue, GameplayOpeningFlow flow)
        {
            Unsubscribe(); controller=source; host=dialogue; opening=flow;
            if (controller == null || host == null) return;
            controller.CadenceTapped += OnTap;
            controller.ResultDecided += OnResult;
            subscribed=true;
            var kind=controller.CurrentChallenge?.Kind;
            string id=kind==ChallengeKind.Learn?"sp.learn.start":kind==ChallengeKind.Practice?"sp.practice.start":kind==ChallengeKind.Exam?"sp.exam.start":null;
            opening?.Configure(controller,host,id,new Dictionary<string,string> {
                {"time",controller.TargetTime.ToString("0.#",CultureInfo.InvariantCulture)} });
        }
        bool Playing => controller != null && controller.Phase == MinigamePhase.Play && !controller.IsWaitingForRivals;
        void OnTap(bool correct)
        {
            if (correct || !Playing) return;
            var kind=controller.CurrentChallenge?.Kind;
            if (kind==ChallengeKind.Learn)
            {
                host.Emit("sp.learn.wrong",()=>Playing,budgetSeconds:3.7f);
                controller.SetLearnDialogueSuspended(!host.IsIdle);
            }
            else if (kind==ChallengeKind.Practice) host.Emit("sp.practice.break",()=>Playing,budgetSeconds:3.7f);
        }
        void Update()
        {
            if (controller == null || host == null) return;
            if (controller.IsLearnChallenge) controller.SetLearnDialogueSuspended(!host.IsIdle);
            if (!Playing || controller.CurrentChallenge?.Kind!=ChallengeKind.Exam) return;
            float remaining=controller.TargetTime-controller.Snapshot.Elapsed;
            if (!warned && remaining>0 && remaining<=5) { warned=true;host.Emit("sp.exam.lowtime",()=>Playing,budgetSeconds:remaining); }
            bool stumble=playerAnimator!=null && playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Stumble");
            if (stumble && !wasStumbling) host.Emit("sp.exam.stumble",()=>Playing,budgetSeconds:3.7f);
            wasStumbling=stumble;
        }
        void OnResult(bool pass)
        {
            var kind=controller.CurrentChallenge?.Kind;
            string id=pass && kind==ChallengeKind.Learn?"sp.learn.done":pass && kind==ChallengeKind.Practice?"sp.practice.done":null;
            host.BeginResult(id);
            if (id!=null) host.Emit(id);
        }
        void Unsubscribe()
        {
            if (subscribed && controller!=null) {controller.CadenceTapped-=OnTap;controller.ResultDecided-=OnResult;}
            controller?.SetLearnDialogueSuspended(false);
            subscribed=false;
        }
        void OnDisable()=>Unsubscribe();
    }
}
