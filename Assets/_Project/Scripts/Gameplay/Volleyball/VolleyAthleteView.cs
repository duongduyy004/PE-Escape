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

        AthleteAction? shown;
        public event System.Action<AthleteAction> ActionPresented;

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
            shown = null;
        }

        // Nearer to the camera (lower court y) draws in front. The court spans y -5..5 plus
        // margins, so athletes stay between 45 and 155, around the net at 100.
        public static int SortingOrderFor(float groundY) => NetSortingOrder - Mathf.RoundToInt(groundY * 10f);

        public Sprite[] FramesFor(AthleteAction action) => action switch
        {
            AthleteAction.Run => run,
            AthleteAction.Serve => smash,
            AthleteAction.Smash => smash,
            AthleteAction.Receive => receive,
            AthleteAction.Block => block,
            AthleteAction.Dive => dive,
            _ => idle
        };

        public void Render(VolleyAthlete athlete)
        {
            if (athlete == null || !body)
                return;

            transform.position = CourtSpace.ToWorld(athlete.Position, athlete.JumpHeight);
            body.sortingOrder = SortingOrderFor(athlete.Position.y);
            body.flipX = mirror;
            if (shown == athlete.Action)
                return;

            shown = athlete.Action;
            if (flipbook)
                flipbook.Play(FramesFor(athlete.Action),
                    athlete.Action == AthleteAction.Idle || athlete.Action == AthleteAction.Run);
            ActionPresented?.Invoke(athlete.Action);
        }
    }
}
