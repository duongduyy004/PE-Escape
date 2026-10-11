using UnityEngine;

namespace KMA.Gameplay.FrogJump
{
    [CreateAssetMenu(menuName = "KMA/Frog Jump/Balance", fileName = "FrogJumpBalance")]
    public sealed class FrogJumpBalanceConfig : ScriptableObject, IFrogJumpTimingSource
    {
        [SerializeField] FrogJumpTuning tuning = new FrogJumpTuning();
        public float TimeLimitSeconds => Tuning.timeLimitSeconds;
        public FrogJumpTuning Tuning => tuning ?? new FrogJumpTuning();
    }
}
