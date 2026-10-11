using System;
using System.Collections.Generic;
namespace KMA.Gameplay
{
    /// Independent preparations share a start request without releasing one another.
    public sealed class MinigameStartGate
    {
        readonly HashSet<Token> holds = new HashSet<Token>();
        int epoch;
        bool requested, signalled;
        public bool CanStart => requested && holds.Count == 0;
        public bool IsHeld => holds.Count != 0;
        public event Action Ready;
        public IDisposable Hold(string owner)
        {
            var token = new Token(this, epoch);
            holds.Add(token);
            return token;
        }
        public void RequestStart() { requested = true; Check(); }
        public void Reset() { epoch++; holds.Clear(); requested = signalled = false; }
        void Release(Token token, int generation)
        {
            if (generation != epoch) return;
            holds.Remove(token);
            Check();
        }
        void Check()
        {
            if (!CanStart || signalled) return;
            signalled = true;
            Ready?.Invoke();
        }
        sealed class Token : IDisposable
        {
            MinigameStartGate gate;
            readonly int generation;
            public Token(MinigameStartGate gate, int generation) { this.gate=gate; this.generation=generation; }
            public void Dispose() { var old=gate; gate=null; old?.Release(this,generation); }
        }
    }
}
