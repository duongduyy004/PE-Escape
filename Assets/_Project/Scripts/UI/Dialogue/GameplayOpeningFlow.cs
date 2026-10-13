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
        Func<bool> ready;
        bool configured, emitted;
        int configuredFrame;
        MinigameGuideHost guide;
        public bool IsComplete => emitted && !configured;
        void Awake() { if (game != null) hold = game.AcquireStartHold("opening dialogue"); }
        public void Configure(MinigameBase source, GameplayDialogueHost dialogue, string id,
            IReadOnlyDictionary<string,string> values = null, Func<bool> ready = null)
        {
            if (game != source) { hold?.Dispose(); hold = null; }
            game=source; host=dialogue; eventId=id; tokens=values; this.ready=ready;
            if (game != null && hold == null) hold=game.AcquireStartHold("opening dialogue");
            configured=true; emitted=false; configuredFrame=Time.frameCount;
            if (game != null && !game.OwnsStartGate) game.RequestStart();
        }
        void Update()
        {
            // Start has run before the first Update. A missing adapter/host must not
            // leave the serialized Awake preparation hold blocking gameplay forever.
            if (!configured && hold != null)
            {
                Debug.LogWarning("[KMA] Opening dialogue was not configured; continuing without speech.");
                emitted=true;
                if (game != null && !game.OwnsStartGate) game.RequestStart();
                Release();
                return;
            }
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
                // The line may read data that is not there yet (a generated puzzle); hold the start until it is.
                if (ready != null && !ready()) return;
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
