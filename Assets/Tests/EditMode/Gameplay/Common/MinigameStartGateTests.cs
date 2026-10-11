using System;
using System.Linq;
using NUnit.Framework;
namespace KMA.Tests.Gameplay.Common
{
 public sealed class MinigameStartGateTests
 {
  object NewGate() {var t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("KMA.Gameplay.MinigameStartGate")).FirstOrDefault(x=>x!=null);Assert.That(t,Is.Not.Null);return Activator.CreateInstance(t);}
  static object Call(object g,string method,params object[] args)=>g.GetType().GetMethod(method).Invoke(g,args);
  static bool Ready(object g)=>(bool)g.GetType().GetProperty("CanStart").GetValue(g);
  [Test] public void GuideAndIntroSameFrameStartOnceAfterDialogue()
  {
   var g=NewGate();int count=0;g.GetType().GetEvent("Ready").AddEventHandler(g,(Action)(()=>count++));
   var guide=(IDisposable)Call(g,"Hold","guide");var dialogue=(IDisposable)Call(g,"Hold","dialogue");
   Call(g,"RequestStart");Call(g,"RequestStart");guide.Dispose();Assert.That(Ready(g),Is.False);Assert.That(count,Is.Zero);
   dialogue.Dispose();Assert.That(Ready(g),Is.True);Assert.That(count,Is.EqualTo(1));Call(g,"RequestStart");Assert.That(count,Is.EqualTo(1));
  }
  [Test] public void DoubleDisposeAndResetIgnoreOldHold()
  {
   var g=NewGate();var old=(IDisposable)Call(g,"Hold","old");Call(g,"Reset");var current=(IDisposable)Call(g,"Hold","new");Call(g,"RequestStart");
   old.Dispose();old.Dispose();Assert.That(Ready(g),Is.False);current.Dispose();current.Dispose();Assert.That(Ready(g),Is.True);
  }
 }
}
