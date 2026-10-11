using System;
using System.Reflection;
using KMA.Gameplay.UI;
using NUnit.Framework;
namespace KMA.Tests.Presentation
{
    public sealed class GameplayDialogueSchedulerTests
    {
        static Type TypeOf(string name)
        {
            var t = typeof(MinigameGuideHost).Assembly.GetType("KMA.Gameplay.UI." + name);
            Assert.That(t, Is.Not.Null, "Missing production dialogue type " + name);
            return t;
        }
        static void Set(object o, string key, object value) => o.GetType().GetField(key).SetValue(o, value);
        static object Event(string id, string group = "fall", int priority = 2, int max = 2, float cooldown = 10)
        {
            var lt = TypeOf("GameplayDialogueLine");
            var lines = Array.CreateInstance(lt, 2);
            for (int i=0; i<2; i++)
            {
                var l=Activator.CreateInstance(lt);
                Set(l,"Text",i==0 ? "Ui da!" : "Đứng dậy và tiếp tục ngay.");
                Set(l,"DurationSeconds",i==0 ? .7f : 1.8f); lines.SetValue(l,i);
            }
            var e=Activator.CreateInstance(TypeOf("GameplayDialogueEvent"));
            Set(e,"Id",id);Set(e,"ReactionGroup",group);Set(e,"Priority",Enum.ToObject(TypeOf("DialoguePriority"),priority));
            Set(e,"MaximumPlays",max);Set(e,"CooldownSeconds",cooldown);Set(e,"Lines",lines);return e;
        }
        static object Scheduler() => Activator.CreateInstance(TypeOf("GameplayDialogueScheduler"));
        static bool Enqueue(object s,object e,Func<bool> valid=null,float budget=float.PositiveInfinity) =>
            (bool)s.GetType().GetMethod("Enqueue").Invoke(s,new object[]{e,valid,budget});
        static void Tick(object s,float dt,bool paused=false) => s.GetType().GetMethod("Tick").Invoke(s,new object[]{dt,paused});
        static string Text(object s)
        {
            var l=s.GetType().GetProperty("Current").GetValue(s);
            return l==null ? null : (string)l.GetType().GetField("Text").GetValue(l);
        }
        [Test] public void SharedReactionGroupHonoursTenSecondsAndTwoPlays()
        {
            var s=Scheduler();Assert.That(Enqueue(s,Event("first")),Is.True);Tick(s,9.99f);
            Assert.That(Enqueue(s,Event("repeat")),Is.False);Tick(s,.011f);
            Assert.That(Enqueue(s,Event("repeat")),Is.True);Tick(s,10);
            Assert.That(Enqueue(s,Event("third")),Is.False);
            s.GetType().GetMethod("ResetAttempt").Invoke(s,null);Assert.That(Enqueue(s,Event("new")),Is.True);
        }
        [Test] public void InvalidContextDropsSecondSpeaker()
        {
            var s=Scheduler();bool valid=true;Enqueue(s,Event("first"),()=>valid);
            valid=false;Tick(s,.7f);Assert.That(Text(s),Is.Null);
        }
        [Test] public void PauseFreezesDurationAndCooldown()
        {
            var s=Scheduler();Enqueue(s,Event("first"));Tick(s,20,true);
            Assert.That(Text(s),Is.EqualTo("Ui da!"));Tick(s,.7f);
            Assert.That(Text(s),Is.EqualTo("Đứng dậy và tiếp tục ngay."));
            Assert.That(Enqueue(s,Event("repeat")),Is.False);
        }
        [Test] public void ResultPreemptsAndDropsPendingGameplay()
        {
            var s=Scheduler();Enqueue(s,Event("error"));Enqueue(s,Event("pending","other",1));
            var result=Event("result","result",3,1,0);Set(result,"Lines",((Array)result.GetType().GetField("Lines").GetValue(result)));
            Enqueue(s,result);Tick(s,3);Assert.That(Text(s),Is.Null);
        }
        [Test] public void BudgetFitsFallDialogue()
        {
            var s=Scheduler();Enqueue(s,Event("fall"),null,2.5f);Tick(s,.7f);
            Assert.That(Text(s),Is.EqualTo("Đứng dậy và tiếp tục ngay."));Tick(s,1.8f);Assert.That(Text(s),Is.Null);
        }
        [Test] public void MandatoryChessFeedbackIsNotThrottled()
        {
            var s=Scheduler();Assert.That(Enqueue(s,Event("hint","hint",2,0,0)),Is.True);Tick(s,3);
            Assert.That(Enqueue(s,Event("hint","hint",2,0,0)),Is.True);
        }
    }
}
