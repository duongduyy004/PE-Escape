using System;
using UnityEngine;
namespace KMA.Gameplay.UI
{
    public enum DialogueSpeaker { Student, Teacher }
    public enum DialoguePriority { Success, Technical, Error, Result }
    [Serializable] public sealed class GameplayDialogueLine
    {
        public DialogueSpeaker Speaker;
        public string Text;
        public Sprite[] Poses = Array.Empty<Sprite>();
        public float PoseStepSeconds = .3f;
        public float DurationSeconds = 2.5f;
        public Sprite Icon;
    }
    [Serializable] public sealed class GameplayDialogueEvent
    {
        public string Id;
        public string ReactionGroup;
        public DialoguePriority Priority;
        public float CooldownSeconds;
        public int MaximumPlays = 1;
        public GameplayDialogueLine[] Lines = Array.Empty<GameplayDialogueLine>();
    }
}
