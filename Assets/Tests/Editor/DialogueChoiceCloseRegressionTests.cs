using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using AzureDreamsReimagined;

public sealed class DialogueChoiceCloseRegressionTests
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    [UnityTest]
    public IEnumerator ClickingChangingRoomChoiceCanClearDialogueDuringDrawing()
    {
        var root=new GameObject("Dialogue regression fixture");root.SetActive(false);
        var controller=root.AddComponent<TowerGameController>();
        var speaker=root.AddComponent<NPCController>();speaker.enabled=false;speaker.npcId="changing_room_regression";
        void Set(string name,object value)=>typeof(TowerGameController).GetField(name,Fields).SetValue(controller,value);
        Set("_area",Enum.Parse(typeof(TowerGameController).GetField("_area",Fields).FieldType,"Town"));
        Set("_storySpeaker",speaker);Set("_poolChangingRoomSpeaker",speaker);
        Set("_dialogueLine","Use the changing room?");Set("_visibleDialogue",string.Empty);Set("_dialogueStarted",Time.unscaledTime-10f);
        Set("_storyChoices",new (string Label,Action Apply)[]{("Not now",null),("Keep talking",null)});
        var window=ScriptableObject.CreateInstance<DialogueChoiceRegressionWindow>();window.Initialize(controller);window.position=new Rect(100,100,640,480);
        try
        {
            window.ShowUtility();window.Repaint();yield return null;yield return null;
            // The choice list is bottom-aligned inside the dialogue window.
            var point=new Vector2(150f,480f*.96f-9f-WindowDesignSystem.Footer-56f+13f);
            window.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=point});
            window.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=point});
            yield return null;
            Assert.That(typeof(TowerGameController).GetField("_dialogueLine",Fields).GetValue(controller),Is.Null,"Click must actually close the changing-room dialogue.");
            Assert.That(window.Failure,Is.Null,"Drawing continued to dereference dialogue state cleared by its own choice callback.");
            LogAssert.NoUnexpectedReceived();
        }
        finally{window.Close();UnityEngine.Object.DestroyImmediate(root);}
    }
}

public sealed class DialogueChoiceRegressionWindow : EditorWindow
{
    private TowerGameController controller;
    internal Exception Failure {get;private set;}
    internal void Initialize(TowerGameController value)=>controller=value;
    private void OnGUI()
    {
        if(!controller || Failure!=null)return;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        foreach(string field in new[]{"_bodyStyle","_messageStyle","_dialogueTextStyle","_logLineStyle"})
            if(typeof(TowerGameController).GetField(field,flags).GetValue(controller)==null)
                typeof(TowerGameController).GetField(field,flags).SetValue(controller,new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true});
        if(typeof(TowerGameController).GetField("_dialogueLine",flags).GetValue(controller)==null)return;
        try{typeof(TowerGameController).GetMethod("DrawMessageWindow",flags).Invoke(controller,new object[]{640f,480f});}
        catch(TargetInvocationException e){Failure=e.InnerException;}
    }
}
