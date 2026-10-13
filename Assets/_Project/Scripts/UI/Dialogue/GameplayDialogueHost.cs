using System;
using System.Collections.Generic;
using UnityEngine;
namespace KMA.Gameplay.UI
{
 public sealed class GameplayDialogueHost : MonoBehaviour
 {
  [SerializeField] GameplayDialogueLibrary library;
  [SerializeField] MonoBehaviour presenter;
  readonly GameplayDialogueScheduler scheduler=new GameplayDialogueScheduler();
  bool resultStarted,reported;PausePanel pause;
  public string PreferredResultEventId {get;private set;}
  public bool IsIdle=>scheduler.IsIdle;
  void Awake(){scheduler.LineChanged+=Present;if(library==null)library=GameplayDialogueLibrary.LoadDefault();}
  public void Configure(GameplayDialogueLibrary content,IGameplayDialogueView view){library=content;presenter=view as MonoBehaviour;}
  public void SetView(IGameplayDialogueView view){if(presenter!=null)((IGameplayDialogueView)presenter).Hide();presenter=view as MonoBehaviour;if(presenter!=null && scheduler.Current!=null)view.Show(scheduler.Current);}
  void Present(GameplayDialogueLine line,bool fadeOut){if(presenter==null)return;var view=(IGameplayDialogueView)presenter;if(line!=null)view.Show(line);else if(fadeOut && presenter is GameplayDialogueView dialogueView)dialogueView.FadeOut();else view.Hide();}
  public bool Emit(string id,Func<bool> valid=null,float budgetSeconds=float.PositiveInfinity,IReadOnlyDictionary<string,string> tokens=null)
  {
   try {
    if(library==null)throw new InvalidOperationException("Library is missing");
    var e=library.Resolve(id,tokens);
    if(e.Id=="frog.fall.repeat" && e.Lines.Length>1 && budgetSeconds<3.7f)e.Lines=new[]{e.Lines[e.Lines.Length-1]};
    return scheduler.Enqueue(e,valid,budgetSeconds);
   } catch(Exception ex){return Unavailable(ex);}
  }
  public bool EmitText(string id,string words,float seconds=2.5f,Func<bool> valid=null)
  {
   try {
    if(library==null)throw new InvalidOperationException("Library is missing");
    var source=library.Selected(id);
    var original=source.Lines[source.Lines.Length-1];
    return EnqueueValidated(new GameplayDialogueEvent {Id=id,Priority=source.Priority,MaximumPlays=source.MaximumPlays,ReactionGroup=source.ReactionGroup,CooldownSeconds=source.CooldownSeconds,
     Lines=new[]{new GameplayDialogueLine {Speaker=original.Speaker,Text=words,DurationSeconds=seconds,PoseStepSeconds=original.PoseStepSeconds,Poses=original.Poses,Icon=original.Icon}}},valid);
   } catch(Exception ex){return Unavailable(ex);}
  }
  public bool EmitLine(GameplayDialogueLine line)
  {
   try {return EnqueueValidated(new GameplayDialogueEvent { Id="manual", Priority=DialoguePriority.Technical,MaximumPlays=0,Lines=new[]{line} });}
   catch(Exception ex){return Unavailable(ex);}
  }
  bool EnqueueValidated(GameplayDialogueEvent content,Func<bool> valid=null)
  {
   if(!GameplayDialogueLibrary.ValidateEvent(content,out var error))throw new InvalidOperationException(error);
   return scheduler.Enqueue(content,valid);
  }
  bool Unavailable(Exception error)
  {
   if(!reported){reported=true;Debug.LogError("[KMA] Gameplay dialogue unavailable: "+error.Message);}
   return false;
  }
  void Update()
  {
   if(pause==null)pause=FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include);
   bool suspended=(pause!=null && pause.IsOpen) || (MinigameGuideHost.Current!=null && MinigameGuideHost.Current.Panel.IsOpen) || (!resultStarted && Time.timeScale==0);
   scheduler.Tick(Time.unscaledDeltaTime,suspended);
  }
  public void BeginResult(string preferredEventId=null){if(!resultStarted){resultStarted=true;scheduler.Clear();}if(preferredEventId!=null)PreferredResultEventId=preferredEventId;}
  public void Clear()=>scheduler.Clear();
  public void ResetAttempt(){resultStarted=false;reported=false;PreferredResultEventId=null;scheduler.ResetAttempt();}
  void OnDisable()=>Clear();
  void OnDestroy(){scheduler.LineChanged-=Present;scheduler.Clear();}
 }
}
