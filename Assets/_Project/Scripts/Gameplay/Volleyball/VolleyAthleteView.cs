using System;
using UnityEngine;

namespace KMA.Gameplay.Volleyball
{
    public sealed class VolleyAthleteView : MonoBehaviour
    {
        public const int NetSortingOrder = 100;

        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteFlipbook flipbook;
        [SerializeField] bool mirror;
        [SerializeField] Sprite[] idle;
        [SerializeField] Sprite[] run;
        [SerializeField] Sprite[] receive;
        [SerializeField] Sprite[] smash;
        [SerializeField] Sprite[] block;
        [SerializeField] Sprite[] dive;
        [SerializeField] Sprite[] serve;
        Sprite[] serveContact;
        Sprite[] serveFollow;
        [SerializeField] Sprite[] serveToss;
        [SerializeField] Sprite[] dig;
        [SerializeField] Sprite[] set;
        [SerializeField] Sprite[] recover;
        [SerializeField] Sprite[] jumpLoad;
        [SerializeField] Sprite[] spikeWindup;
        [SerializeField] Sprite[] spikeContact;
        [SerializeField] Sprite[] spikeFollow;
        [SerializeField] Sprite[] land;
        [SerializeField] bool hasAuthoredSequences;

        AthleteAction? shown;
        int shownSequence = -1;
        int shownPhase = -1;
        public event Action<AthleteAction> ActionPresented;

        void Awake()
        {
            serveContact = Single(spikeContact);
            serveFollow = Single(spikeFollow);
        }

        public void Configure(SpriteRenderer renderer, SpriteFlipbook book, bool mirrored, Sprite[] idleFrames,
            Sprite[] runFrames, Sprite[] receiveFrames, Sprite[] smashFrames, Sprite[] blockFrames, Sprite[] diveFrames)
        {
            body = renderer;
            flipbook = book;
            mirror = mirrored;
            idle = idleFrames;
            run = runFrames;
            receive = receiveFrames;
            smash = smashFrames;
            block = blockFrames;
            dive = diveFrames;
            // Old scene/test fixtures remain useful, while the configurator supplies each role below.
            serve = smashFrames;
            serveToss = blockFrames;
            dig = receiveFrames;
            set = blockFrames;
            recover = receiveFrames;
            jumpLoad = blockFrames;
            spikeWindup = smashFrames;
            spikeContact = smashFrames;
            spikeFollow = smashFrames;
            land = blockFrames;
            serveContact = Single(spikeContact);
            serveFollow = Single(spikeFollow);
            hasAuthoredSequences = false;
            shown = null;
            shownSequence = -1;
            shownPhase = -1;
        }

        public void ConfigureSequences(Sprite[] serveFrames, Sprite[] serveTossFrames, Sprite[] digFrames,
            Sprite[] setFrames, Sprite[] recoverFrames, Sprite[] jumpLoadFrames, Sprite[] spikeWindupFrames,
            Sprite[] spikeContactFrames, Sprite[] spikeFollowFrames, Sprite[] landFrames)
        {
            hasAuthoredSequences = HasFrames(serveFrames) || HasFrames(serveTossFrames) || HasFrames(digFrames) ||
                                   HasFrames(setFrames) || HasFrames(recoverFrames) || HasFrames(jumpLoadFrames) ||
                                   HasFrames(spikeWindupFrames) || HasFrames(spikeContactFrames) ||
                                   HasFrames(spikeFollowFrames) || HasFrames(landFrames);
            serve = NonEmpty(serveFrames, smash);
            serveToss = NonEmpty(serveTossFrames, set);
            dig = NonEmpty(digFrames, receive);
            set = NonEmpty(setFrames, block);
            recover = NonEmpty(recoverFrames, receive);
            jumpLoad = NonEmpty(jumpLoadFrames, block);
            spikeWindup = NonEmpty(spikeWindupFrames, smash);
            spikeContact = NonEmpty(spikeContactFrames, smash);
            spikeFollow = NonEmpty(spikeFollowFrames, smash);
            land = NonEmpty(landFrames, block);
            serveContact = Single(spikeContact);
            serveFollow = Single(spikeFollow);
            shownSequence = -1;
            shownPhase = -1;
        }

        static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;

        static Sprite[] Single(Sprite[] value) => value != null && value.Length > 0 ? new[] { value[0] } : null;

        static Sprite[] NonEmpty(Sprite[] value, Sprite[] fallback) =>
            value != null && value.Length > 0 ? value : fallback;

        // A flat list is exposed to scene validation and art-contract tests.
        public Sprite[] FramesFor(AthleteAction action) => action switch
        {
            AthleteAction.Run => run,
            AthleteAction.Serve => NonEmpty(serve, smash),
            AthleteAction.ServeToss => NonEmpty(serveToss, set),
            AthleteAction.Smash => hasAuthoredSequences ? Combine(spikeWindup, spikeContact, spikeFollow, land) : smash,
            AthleteAction.Receive => hasAuthoredSequences ? Combine(receive, set, recover) : receive,
            AthleteAction.Dig => hasAuthoredSequences ? Combine(dig, recover) : receive,
            AthleteAction.JumpLoad => Combine(jumpLoad, spikeWindup, land),
            AthleteAction.Block => hasAuthoredSequences ? Combine(jumpLoad, set, land) : block,
            AthleteAction.Dive => hasAuthoredSequences ? Combine(dive, recover) : dive,
            _ => idle
        };

        static Sprite[] Combine(params Sprite[][] groups)
        {
            int size = 0;
            foreach (Sprite[] group in groups)
                if (group != null) size += group.Length;
            var result = new Sprite[size];
            int offset = 0;
            foreach (Sprite[] group in groups)
            {
                if (group == null) continue;
                Array.Copy(group, 0, result, offset, group.Length);
                offset += group.Length;
            }
            return result;
        }

        public void Render(VolleyAthlete athlete)
        {
            if (athlete == null || !body)
                return;

            transform.position = CourtSpace.ToWorld(athlete.Position, athlete.JumpHeight);
            body.sortingOrder = SortingOrderFor(athlete.Position.y);
            body.flipX = mirror;

            bool newSequence = shownSequence != athlete.ActionSequence;
            if (shown != athlete.Action || newSequence)
            {
                shown = athlete.Action;
                shownSequence = athlete.ActionSequence;
                ActionPresented?.Invoke(athlete.Action);
            }

            int phase = PhaseFor(athlete);
            bool loop = athlete.Action == AthleteAction.Idle || athlete.Action == AthleteAction.Run;
            Sprite[] frames = FramesForPhase(athlete.Action, phase);
            if (flipbook && (newSequence || phase != shownPhase || !ReferenceEquals(frames, flipbook.Frames)))
                flipbook.Play(frames, loop, newSequence || phase != shownPhase);
            shownPhase = phase;
        }

        public static int SortingOrderFor(float groundY) => NetSortingOrder - Mathf.RoundToInt(groundY * 10f);

        int PhaseFor(VolleyAthlete athlete)
        {
            float elapsed = athlete.ActionElapsed;
            switch (athlete.Action)
            {
                case AthleteAction.Receive:
                    return elapsed < .12f ? 0 : elapsed < .23f ? 1 : 2;
                case AthleteAction.Dig:
                    return elapsed < .19f ? 0 : 1;
                case AthleteAction.Serve:
                case AthleteAction.Smash:
                    if (athlete.IsAirborne && athlete.AirTimeLeft <= .16f) return 2;
                    return elapsed < .12f ? 0 : 1;
                case AthleteAction.Block:
                    if (athlete.IsAirborne && athlete.AirTimeLeft <= .16f) return 2;
                    return elapsed < .18f ? 0 : 1;
                case AthleteAction.JumpLoad:
                    if (athlete.IsAirborne && athlete.AirTimeLeft <= .16f) return 2;
                    return elapsed < .18f ? 0 : 1;
                case AthleteAction.Dive:
                    return elapsed < .35f ? 0 : 1;
                default:
                    return 0;
            }
        }

        Sprite[] FramesForPhase(AthleteAction action, int phase) => action switch
        {
            AthleteAction.Serve => hasAuthoredSequences ? (phase == 0 ? serveContact : serveFollow) : serve,
            AthleteAction.ServeToss => NonEmpty(serveToss, set),
            AthleteAction.Receive => hasAuthoredSequences ? (phase == 0 ? receive : phase == 1 ? set : recover) : receive,
            AthleteAction.Dig => hasAuthoredSequences ? (phase == 0 ? dig : recover) : receive,
            AthleteAction.Smash => hasAuthoredSequences ? (phase == 0 ? spikeContact : phase == 1 ? spikeFollow : land) : smash,
            AthleteAction.JumpLoad => phase == 0 ? NonEmpty(jumpLoad, block) : phase == 1 ? NonEmpty(spikeWindup, smash) : NonEmpty(land, block),
            AthleteAction.Block => phase == 0 ? NonEmpty(jumpLoad, block) : phase == 1 ? NonEmpty(set, block) : NonEmpty(land, block),
            AthleteAction.Dive => phase == 0 ? dive : recover,
            _ => FramesFor(action)
        };
    }
}
