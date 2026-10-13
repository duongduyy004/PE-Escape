using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KMA.Gameplay
{
    public enum DialoguePose { Idle, Cheer, Hurt, Jump, Duck }

    [Serializable]
    public sealed class JourneyPoseSprite
    {
        [SerializeField] DialoguePose pose;
        [SerializeField] Sprite sprite;

        public DialoguePose Pose => pose;
        public Sprite Sprite => sprite;

        public JourneyPoseSprite(DialoguePose pose, Sprite sprite)
        {
            this.pose = pose;
            this.sprite = sprite;
        }
    }

    [Serializable]
    public sealed class JourneyCharacter
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        // Retained only for asset compatibility; the NameTag chip is Shell gold for every speaker.
        [SerializeField] Color tagColor = Color.white;
        [SerializeField] bool isPlayer;
        [SerializeField] List<JourneyPoseSprite> poses = new List<JourneyPoseSprite>();

        public string Id => id;
        public string DisplayName => displayName;
        public Color TagColor => tagColor;
        public bool IsPlayer => isPlayer;

        public JourneyCharacter(string id, string displayName, Color tagColor, bool isPlayer,
            IEnumerable<JourneyPoseSprite> poses)
        {
            this.id = id;
            this.displayName = displayName;
            this.tagColor = tagColor;
            this.isPlayer = isPlayer;
            this.poses = poses == null ? new List<JourneyPoseSprite>() : poses.ToList();
        }

        public bool HasPose(DialoguePose pose) => Find(pose) != null;

        /// The sprite for a pose, falling back to Idle, or null when neither exists.
        public Sprite GetPose(DialoguePose pose) => Find(pose) ?? Find(DialoguePose.Idle);

        Sprite Find(DialoguePose pose)
        {
            if (poses == null) return null;
            foreach (JourneyPoseSprite entry in poses)
                if (entry != null && entry.Pose == pose && entry.Sprite != null) return entry.Sprite;
            return null;
        }
    }

    [Serializable]
    public sealed class JourneyDialogueLine
    {
        [SerializeField] string characterId;
        [SerializeField] DialoguePose pose;
        [SerializeField, TextArea] string text;
        [SerializeField] string sticker;

        public string CharacterId => characterId;
        public DialoguePose Pose => pose;
        public string Text => text;
        public string Sticker => sticker;

        public JourneyDialogueLine(string characterId, DialoguePose pose, string text, string sticker = "")
        {
            this.characterId = characterId;
            this.pose = pose;
            this.text = text;
            this.sticker = sticker ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class JourneyDialogueNode
    {
        [SerializeField] string id;
        [SerializeField] List<JourneyDialogueLine> lines = new List<JourneyDialogueLine>();

        public string Id => id;
        public IReadOnlyList<JourneyDialogueLine> Lines => lines;

        public JourneyDialogueNode(string id, List<JourneyDialogueLine> lines)
        {
            this.id = id;
            this.lines = lines ?? new List<JourneyDialogueLine>();
        }
    }

    [CreateAssetMenu(menuName = "KMA/Journey/Dialogue Library", fileName = "JourneyDialogues")]
    public sealed class JourneyDialogueLibrary : ScriptableObject
    {
        [SerializeField] List<JourneyCharacter> cast = new List<JourneyCharacter>();
        [SerializeField] List<JourneyDialogueNode> nodes = new List<JourneyDialogueNode>();
        Dictionary<string, JourneyDialogueNode> byId;
        Dictionary<string, JourneyCharacter> castById;

        public IReadOnlyList<JourneyCharacter> Cast => cast;
        public IReadOnlyList<JourneyDialogueNode> Nodes => nodes;
        public JourneyCharacter Player => cast.FirstOrDefault(character => character != null && character.IsPlayer);

        public static JourneyDialogueLibrary LoadDefault() =>
            Resources.Load<JourneyDialogueLibrary>("Journey/JourneyDialogues");

        public void SetContent(IEnumerable<JourneyCharacter> characters, IEnumerable<JourneyDialogueNode> dialogueNodes)
        {
            cast = characters == null ? new List<JourneyCharacter>() : characters.ToList();
            nodes = dialogueNodes == null ? new List<JourneyDialogueNode>() : dialogueNodes.ToList();
            byId = null;
            castById = null;
        }

        public JourneyDialogueNode Get(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("A dialogue node ID is required.", nameof(nodeId));
            byId ??= nodes.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            if (!byId.TryGetValue(nodeId, out JourneyDialogueNode node))
                throw new KeyNotFoundException($"Journey dialogue '{nodeId}' was not found.");
            return node;
        }

        public JourneyCharacter GetCharacter(string characterId)
        {
            castById ??= cast.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            if (characterId == null || !castById.TryGetValue(characterId, out JourneyCharacter character))
                throw new KeyNotFoundException($"Journey character '{characterId}' was not found.");
            return character;
        }

        public bool Validate(out string error)
        {
            error = null;
            if (nodes == null || nodes.Count == 0) { error = "Dialogue library has no nodes."; return false; }
            if (cast == null || cast.Count == 0) { error = "Dialogue library has no cast."; return false; }
            var characters = new Dictionary<string, JourneyCharacter>(StringComparer.Ordinal);
            foreach (JourneyCharacter character in cast)
            {
                if (character == null || string.IsNullOrWhiteSpace(character.Id) || !characters.TryAdd(character.Id, character))
                { error = "Character IDs must be present and unique."; return false; }
            }
            if (cast.Count(character => character.IsPlayer) != 1)
            { error = "Dialogue cast must contain exactly one player."; return false; }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JourneyDialogueNode node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || !seen.Add(node.Id))
                { error = "Dialogue IDs must be present and unique."; return false; }
                if (node.Lines == null || node.Lines.Count is < 2 or > 4 ||
                    node.Lines.Any(line => line == null || string.IsNullOrWhiteSpace(line.CharacterId) ||
                                           string.IsNullOrWhiteSpace(line.Text)))
                { error = $"Dialogue '{node.Id}' must contain two to four complete lines."; return false; }
                foreach (JourneyDialogueLine line in node.Lines)
                {
                    if (!characters.TryGetValue(line.CharacterId, out JourneyCharacter speaker))
                    { error = $"Dialogue '{node.Id}' uses unknown character '{line.CharacterId}'."; return false; }
                    if (!speaker.HasPose(line.Pose))
                    { error = $"Character '{speaker.Id}' has no sprite for pose {line.Pose}."; return false; }
                    foreach (string code in DialogueEmoji.FindCodes(line.Text))
                        if (!DialogueEmoji.IsKnown(code))
                        { error = $"Dialogue '{node.Id}' uses unknown emoji ':{code}:'."; return false; }
                }
            }
            return true;
        }
    }
}
