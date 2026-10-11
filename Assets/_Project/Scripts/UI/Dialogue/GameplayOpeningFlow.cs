using System;
using System.Collections.Generic;
using UnityEngine;
namespace KMA.Gameplay.UI
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameplayOpeningFlow : MonoBehaviour
    {
        [SerializeField] MinigameBase game;
        [SerializeField] GameplayDialogueHost host;
        IDisposable hold;
        string eventId;
        IReadOnlyDictionary<string,string> tokens;
        bool configured, emitted;
        int configuredFrame;
        MinigameGuideHost guide;
        void Awake() { if (game != null) hold = game.AcquireStartHold("opening dialogue"); }
        public void Configure(MinigameBase source, GameplayDialogueHost dialogue, string id,
            IReadOnlyDictionary<string,string> values = null)
        {
            if (game != source) { hold?.Dispose(); hold = null; }
            game=source; host=dialogue; eventId=id; tokens=values;
            if (game != null && hold == null) hold=game.AcquireStartHold("opening dialogue");
            configured=true; emitted=false; configuredFrame=Time.frameCount;
            if (game != null && !game.OwnsStartGate) game.RequestStart();
        }
        void Update()
        {
            if (!configured || game == null || Time.frameCount <= configuredFrame) return;
            if (guide != MinigameGuideHost.Current)
            {
                if (guide != null) guide.Closed -= OnGuideClosed;
                guide=MinigameGuideHost.Current;
                if (guide != null) guide.Closed += OnGuideClosed;
            }
            if (Time.timeScale == 0 || (guide != null && guide.Panel.IsOpen)) return;
            GuideClosed();
        }
        void OnGuideClosed(GuideMode mode) { if (mode == GuideMode.FirstRun) GuideClosed(); }
        public void GuideClosed()
        {
            if (!configured) return;
            if (!emitted)
            {
                emitted=true;
                if (host == null || string.IsNullOrEmpty(eventId) || !host.Emit(eventId,tokens:tokens)) Release();
            }
            else if (host == null || host.IsIdle) Release();
        }
        void Release() { hold?.Dispose(); hold=null; configured=false; }
        public void ResetAttempt() { Release(); host?.ResetAttempt(); emitted=false; }
        void OnDisable() { Release(); if (guide != null) guide.Closed -= OnGuideClosed; guide=null; }
    }
}
