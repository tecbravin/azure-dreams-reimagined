using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

public sealed class TowerPresentationPerformanceTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(TowerGameController c,string name,params object[] args) => typeof(TowerGameController).GetMethod(name,Flags).Invoke(c,args);
    static FieldInfo Field(string name) => typeof(TowerGameController).GetField(name,Flags);
    static void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }

    [Test]
    public void LogCachePreservesAppendClearResizeAndFontChanges()
    {
        var root=new GameObject("Log cache test");root.SetActive(false);
        try
        {
            var c=root.AddComponent<TowerGameController>();var messages=(List<string>)Field("_messageStack").GetValue(c);
            var message=typeof(TowerGameController).GetProperty("_message",Flags);
            var style=new GUIStyle {font=Resources.Load<Font>(WindowDesignSystem.FontResource),fontSize=13,richText=true};
            void Compare(float width)
            {
                var cached=(List<string>)Call(c,"WrapTowerLogLines",messages,width,style);
                var full=(List<string>)Call(c,"BuildTowerLogLines",messages,width,style,null,0);
                Check(string.Join("\n",cached)==string.Join("\n",full),"Cached log changed its text or wrapping");
                Check(ReferenceEquals(cached,Call(c,"WrapTowerLogLines",messages,width,style)),"Unchanged log was rebuilt");
            }
            message.SetValue(c,"Koh hit Troll for <color=#FF5454>6</color> damage.\nYour familiar gains experience."); Compare(350);Compare(327);
            message.SetValue(c,"A new item is lying on the floor beside Koh.");Compare(350);Compare(327);
            style.fontSize=19;Compare(350);Compare(327);Compare(180);
            message.SetValue(c,string.Empty);Compare(350);
            message.SetValue(c,"You reach floor 2.");Compare(350);
            Check(messages.Count==1,"Floor reset retained previous messages");
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }

    [Test]
    public void OnlyExplicitFamiliarCommandsAddAnExtraPresentationPhase()
    {
        var root=new GameObject("Turn cadence test");root.SetActive(false);var familiar=new GameObject("Familiar");
        try
        {
            var c=root.AddComponent<TowerGameController>();Field("_familiar").SetValue(c,familiar);
            Check(!(bool)Call(c,"ShouldDelayFamiliarTurn",true,null,false),"Autonomous following added a pause to every step");
            var actor=Field("_familiarActor").GetValue(c);var commands=(IDictionary)Field("_pendingFamiliarCommands").GetValue(c);
            var order=Enum.Parse(typeof(TowerGameController).GetNestedType("ManualOrder",BindingFlags.NonPublic),"Attack");
            var tuple=commands.GetType().GetGenericArguments()[1];commands[actor]=Activator.CreateInstance(tuple,order,Vector2Int.up);
            Check((bool)Call(c,"ShouldDelayFamiliarTurn",true,null,false),"Manual command no longer acts after Koh");
            Check(!(bool)Call(c,"ShouldDelayFamiliarTurn",false,null,false),"A skipped command added a pause");
            Check(!(bool)Call(c,"ShouldDelayFamiliarTurn",true,actor,false),"Yielding familiar added a second action phase");
        }
        finally {UnityEngine.Object.DestroyImmediate(familiar);UnityEngine.Object.DestroyImmediate(root);}
    }
}