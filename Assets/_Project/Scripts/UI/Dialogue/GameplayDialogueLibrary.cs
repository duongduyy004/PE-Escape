using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace KMA.Gameplay.UI
{
    [CreateAssetMenu(menuName="KMA/Gameplay Dialogue")]
    public sealed class GameplayDialogueLibrary : ScriptableObject
    {
        [SerializeField] GameplayDialogueEvent[] events = Array.Empty<GameplayDialogueEvent>();
        public IReadOnlyList<GameplayDialogueEvent> Events => events;
        public static GameplayDialogueLibrary LoadDefault() => Resources.Load<GameplayDialogueLibrary>("Journey/GameplayDialogues");
        public void SetContent(IEnumerable<GameplayDialogueEvent> content) => events=content.ToArray();
        public bool TryGet(string id,out GameplayDialogueEvent value)
        { value=events?.FirstOrDefault(e=>e!=null && e.Id==id);return value!=null; }
        internal static bool ValidateEvent(GameplayDialogueEvent e,out string error)
        {
            if(e==null || string.IsNullOrWhiteSpace(e.Id) || e.Lines==null || e.Lines.Length==0)
            { error="Missing dialogue event or lines: "+e?.Id;return false; }
            foreach(var line in e.Lines)
                if(line==null || string.IsNullOrWhiteSpace(line.Text) || line.DurationSeconds<=0 ||
                    float.IsNaN(line.DurationSeconds) || float.IsInfinity(line.DurationSeconds) ||
                    line.Poses==null || line.Poses.Length==0 || line.Poses.Any(p=>p==null))
                { error="Missing sprite/text/duration: "+e.Id;return false; }
            if((e.Id.StartsWith("frog.fall.") || e.Id=="frog.lowtime") && e.Lines.Last().Icon==null)
            { error="Missing PointingHandRight icon: "+e.Id;return false; }
            error=null;return true;
        }
        public bool Validate(out string error)
        {
            if(events==null || events.Length==0){error="Empty dialogue library";return false;}
            var ids=new HashSet<string>();
            foreach(var e in events)
            {
                if(!ValidateEvent(e,out error))return false;
                if(!ids.Add(e.Id)){error="Duplicate dialogue event: "+e.Id;return false;}
            }
            error=null;return true;
        }
        internal GameplayDialogueEvent Selected(string id)
        {
            if(!TryGet(id,out var value)) throw new KeyNotFoundException(id);
            if(!ValidateEvent(value,out var error))throw new InvalidOperationException(error);
            return value;
        }
        public GameplayDialogueEvent Resolve(string id,IReadOnlyDictionary<string,string> tokens)
        {
            var value=Selected(id);
            var lines=value.Lines.Select(l=>new GameplayDialogueLine { Speaker=l.Speaker,Text=l.Text,
                Poses=l.Poses,PoseStepSeconds=l.PoseStepSeconds,DurationSeconds=l.DurationSeconds,Icon=l.Icon }).ToArray();
            foreach(var l in lines)
            {
                if(tokens!=null) foreach(var pair in tokens) l.Text=l.Text.Replace("{"+pair.Key+"}",pair.Value);
                if(Regex.IsMatch(l.Text,@"\{[a-zA-Z]+\}")) throw new InvalidOperationException("Unresolved token in "+id);
            }
            return new GameplayDialogueEvent { Id=value.Id,ReactionGroup=value.ReactionGroup,Priority=value.Priority,
                CooldownSeconds=value.CooldownSeconds,MaximumPlays=value.MaximumPlays,Lines=lines };
        }
    }
}
