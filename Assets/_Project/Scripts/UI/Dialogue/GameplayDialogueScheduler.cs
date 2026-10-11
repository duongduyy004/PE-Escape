using System;
using System.Collections.Generic;
using System.Linq;
namespace KMA.Gameplay.UI
{
    public sealed class GameplayDialogueScheduler
    {
        sealed class Request { public GameplayDialogueEvent Event;public Func<bool> Valid;public float Until; }
        readonly List<Request> pending=new List<Request>();
        readonly Dictionary<string,int> plays=new Dictionary<string,int>();
        readonly Dictionary<string,float> last=new Dictionary<string,float>();
        Request active;int index;float remaining,clock;
        public GameplayDialogueLine Current { get;private set; }
        public bool IsIdle => active==null && pending.Count==0;
        public event Action<GameplayDialogueLine> LineChanged;
        string Group(GameplayDialogueEvent e)=>string.IsNullOrEmpty(e.ReactionGroup)?e.Id:e.ReactionGroup;
        bool Allowed(GameplayDialogueEvent e)
        {
            string g=Group(e);
            return (e.MaximumPlays==0 || !plays.TryGetValue(g,out int n) || n<e.MaximumPlays) &&
                (!last.TryGetValue(g,out float t) || clock-t+1e-5f>=e.CooldownSeconds);
        }
        public bool Enqueue(GameplayDialogueEvent value,Func<bool> valid=null,float budgetSeconds=float.PositiveInfinity)
        {
            if(value==null || value.Lines==null || value.Lines.Length==0 || budgetSeconds<=0 ||
                float.IsNaN(budgetSeconds) || !Allowed(value) || (valid!=null && !valid()) ||
                (value.MaximumPlays!=0 && (active?.Event.Id==value.Id || pending.Any(r=>r.Event.Id==value.Id)))) return false;
            if(value.MaximumPlays==0)
            {
                pending.RemoveAll(x=>x.Event.Id==value.Id);
                if(active?.Event.Id==value.Id)Hide();
            }
            var r=new Request {Event=value,Valid=valid,Until=clock+budgetSeconds};
            if(value.Priority==DialoguePriority.Result) Clear();
            else if(active!=null && value.Priority>active.Event.Priority) Hide();
            if(active==null) Start(r);
            else { if(pending.Count>=4)return false;pending.Add(r); }
            return true;
        }
        bool Valid(Request r)=>clock<r.Until && (r.Valid==null || r.Valid());
        void Start(Request r)
        {
            if(!Valid(r) || !Allowed(r.Event))return;
            active=r;index=0;string g=Group(r.Event);
            plays.TryGetValue(g,out int n);plays[g]=n+1;last[g]=clock;Show();
        }
        void Show()
        {
            if(active==null)return;
            var l=active.Event.Lines[index];
            if(l==null || l.DurationSeconds<=0 || float.IsNaN(l.DurationSeconds) || float.IsInfinity(l.DurationSeconds)) { Hide();return; }
            Current=l;remaining=Math.Min(l.DurationSeconds,active.Until-clock);LineChanged?.Invoke(l);
        }
        void Hide() { active=null;Current=null;remaining=0;LineChanged?.Invoke(null); }
        void NextRequest()
        {
            while(active==null && pending.Count>0)
            {
                var r=pending.OrderByDescending(x=>x.Event.Priority).First();pending.Remove(r);Start(r);
            }
        }
        public void Tick(float dt,bool paused)
        {
            if(paused || dt<0 || float.IsNaN(dt) || float.IsInfinity(dt))return;
            float end=clock+dt;
            int safety=32;
            while(safety-->0)
            {
                if(active!=null && !Valid(active))Hide();
                NextRequest();
                if(active==null){clock=end;return;}
                float step=Math.Min(end-clock,remaining);clock+=step;remaining-=step;
                if(remaining>1e-5f)return;
                if(Valid(active) && ++index<active.Event.Lines.Length)Show();else Hide();
                if(clock>=end-1e-5f){NextRequest();return;}
            }
            clock=end;Clear();
        }
        public void Clear() { pending.Clear();Hide(); }
        public void ResetAttempt() { Clear();plays.Clear();last.Clear();clock=0; }
    }
}
