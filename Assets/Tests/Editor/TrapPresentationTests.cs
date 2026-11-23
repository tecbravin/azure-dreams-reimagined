using System;
using System.Collections;
using System.Reflection;
using AzureDreams.Core;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

public sealed class TrapPresentationTests
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Type T=typeof(TowerGameController);
    static object Call(TowerGameController c,string name,params object[] args)=>T.GetMethod(name,F).Invoke(c,args);
    static void Set(TowerGameController c,string name,object value)=>T.GetField(name,F).SetValue(c,value);
    static object Get(TowerGameController c,string name)=>T.GetField(name,F).GetValue(c);
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static ActorRecord Sleeping(){var a=new ActorRecord{Hp=20,MaxHp=20};a.SetStatus(0,StatusId.SLEEP,30);a.SetStatus(1,StatusId.POISON,5);return a;}

    [Test]
    public void BumpImmediatelyWakesPlayerCompanionsAndDistantEnemies()
    {
        var root=new GameObject("Bump trap test");root.SetActive(false);var player=new GameObject("Koh");var familiar=new GameObject("Familiar");
        try
        {
            var c=root.AddComponent<TowerGameController>();Set(c,"_player",player);Set(c,"_familiar",familiar);var koh=(ActorRecord)Get(c,"_playerActor");koh.SetStatus(0,StatusId.SLEEP,20);var companion=Sleeping();Set(c,"_familiarActor",companion);
            var creatures=(IList)Get(c,"_creatures");var creatureType=T.GetNestedType("CreatureState",BindingFlags.NonPublic);var distant=Sleeping();var state=Activator.CreateInstance(creatureType);creatureType.GetField("Actor").SetValue(state,distant);creatureType.GetField("Cell").SetValue(state,new Vector2Int(28,28));creatures.Add(state);
            var allies=(IList)Get(c,"_thrownEggs");var allyType=T.GetNestedType("ThrownEggAlly",BindingFlags.NonPublic);var ally=Activator.CreateInstance(allyType);var blue=Sleeping();allyType.GetField("Actor").SetValue(ally,blue);allies.Add(ally);Set(c,"_blueCollarAlly",ally);
            var carried=Sleeping();state=Activator.CreateInstance(creatureType);creatureType.GetField("Actor").SetValue(state,carried);Set(c,"_carriedMonster",state);
            var traps=(IDictionary)Get(c,"_demoTraps");var trap=new TowerTrapState();var effect=typeof(TowerTrapState).GetProperty("Effect",F);effect.SetValue(trap,Enum.Parse(effect.PropertyType,"Bump"));var cell=new Vector2Int(3,3);traps[cell]=trap;
            Call(c,"TriggerTrapFor",cell,koh,false);
            foreach(var actor in new[]{koh,companion,distant,blue,carried})Check(!Combat.HasStatus(actor,StatusId.SLEEP),"A floor unit remained asleep after Bump");
            Check(Combat.HasStatus(distant,StatusId.POISON),"Bump cleared unrelated conditions");Check(!trap.IsActive,"Bump did not trigger");
        }
        finally{UnityEngine.Object.DestroyImmediate(familiar);UnityEngine.Object.DestroyImmediate(player);UnityEngine.Object.DestroyImmediate(root);}
    }

    [Test]
    public void SpringCompressesReleasesAndSettlesAndBlastBecomesSmoke()
    {
        float Height(float t)=>(float)Call(null,"GoUpSpringHeight",t);
        Check(Height(.18f)<Height(0),"Spring did not compress");Check(Height(.44f)>Height(0)*3,"Spring did not extend");Check(Mathf.Abs(Height(1)-.3f)<.001f,"Spring did not settle");
        for(int i=0;i<=100;i++)Check(Height(i/100f)>0,"Spring inverted below the floor");
        var fire=(Color)Call(null,"BlastTrapColor",.25f);var smoke=(Color)Call(null,"BlastTrapColor",.8f);var end=(Color)Call(null,"BlastTrapColor",1f);
        Check(fire.r>fire.g&&fire.g>fire.b,"Blast has no orange fire stage");Check(smoke.r<fire.r&&smoke.a<1,"Blast has no fading smoke stage");Check(end.a==0,"Blast did not fade out");
    }
}