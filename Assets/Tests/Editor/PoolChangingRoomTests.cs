using System;
using System.Reflection;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

public sealed class PoolChangingRoomTests
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(TowerGameController c,string name,object value)=>typeof(TowerGameController).GetField(name,F).SetValue(c,value);
    static object Call(TowerGameController c,string name,params object[] args)=>typeof(TowerGameController).GetMethod(name,F).Invoke(c,args);
    static object SpriteCall(KohSpriteView view,string name,params object[] args)=>typeof(KohSpriteView).GetMethod(name,F).Invoke(view,args);
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

    [Test]
    public void ChangingAndCancellingReleaseInputAndUseOriginalDirectionalFrames()
    {
        var root=new GameObject("Pool changing regression");root.SetActive(false);
        var roomRoot=new GameObject("Pool interior");roomRoot.SetActive(false);
        var flags=TownRetailScriptFlags.CreatePersistent();bool restored=flags.IsFlagSet(150),swimsuit=flags.IsFlagSet(4610);
        var bodyTexture=new Texture2D(64,64);
        try
        {
            var c=root.AddComponent<TowerGameController>();var flow=root.AddComponent<TownEventController>();flow.State.ForceSet(TownGameState.State.TownIdle);Set(c,"_townFlow",flow);
            Set(c,"_area",Enum.Parse(typeof(TowerGameController).GetNestedType("Area",BindingFlags.NonPublic),"Town"));
            var room=roomRoot.AddComponent<TownInteriorScene>();room.interiorId="Interior_0d_pool_quest_done";Set(c,"_interior",room);Set(c,"_townHeights",new byte[128*128*2]);flags.SetBit(150);
            var player=new GameObject("Koh");player.transform.SetParent(root.transform,false);player.transform.position=new Vector3(10,.55f,10);Set(c,"_player",player);
            var view=player.AddComponent<KohSpriteView>();Set(c,"_kohSprite",view);var visual=new GameObject("Body");visual.transform.SetParent(player.transform,false);var body=visual.AddComponent<SpriteRenderer>();body.sprite=Sprite.Create(bodyTexture,new Rect(0,0,64,64),new Vector2(.5f,11f/64),48);
            typeof(KohSpriteView).GetField("_renderer",F).SetValue(view,body);var clothed=new Sprite[85];for(int i=0;i<85;i++)clothed[i]=body.sprite;typeof(KohSpriteView).GetField("_retailFrames",F).SetValue(view,clothed);
            Call(c,"OpenPoolChangingRoom");flow.State.TrySet(TownGameState.State.TownDialogue);Call(c,"PickStoryChoice",0);
            Check(flow.State.Current==TownGameState.State.TownIdle,"Choosing swimwear left movement locked in dialogue");Check(flags.IsFlagSet(4610),"Swimsuit flag not set");
            var frames=(Sprite[])typeof(KohSpriteView).GetField("_swimsuitFrames",F).GetValue(view);Check(frames!=null && frames.Length==85,"Original swimsuit frames missing");
            int[] rows={4,3,2,1,0,1,2,3};typeof(KohSpriteView).GetField("_clock",F).SetValue(view,3f);
            for(int octant=0;octant<8;octant++)for(int mode=0;mode<3;mode++)
            {
                SpriteCall(view,"PresentBody",octant,mode>0,mode==2);int column=mode==0?0:mode==1?4:12;
                Check(body.sprite==frames[rows[octant]*17+column],"Wrong swimsuit direction or walking frame");Check(body.flipX==(octant>=1&&octant<=3),"Wrong swimsuit mirroring");
            }
            var before=player.transform.position;c.MoveInTown(Vector2.right,.1f);Check(player.transform.position.x>before.x,"Koh still cannot move after changing");
            Call(c,"OpenPoolChangingRoom");flow.State.TrySet(TownGameState.State.TownDialogue);Call(c,"PickStoryChoice",0);
            Check(flow.State.Current==TownGameState.State.TownIdle && !flags.IsFlagSet(4610),"Changing back did not restore input or outfit flag");SpriteCall(view,"PresentBody",4,false,false);Check(body.sprite==clothed[0],"Regular outfit not restored");
            Call(c,"OpenPoolChangingRoom");flow.State.TrySet(TownGameState.State.TownDialogue);Call(c,"PickStoryChoice",1);Check(flow.State.Current==TownGameState.State.TownIdle,"Not now kept movement locked");
            Call(c,"OpenPoolChangingRoom");flow.State.TrySet(TownGameState.State.TownDialogue);Call(c,"CloseStoryPrompt",true);Check(flow.State.Current==TownGameState.State.TownIdle,"Cancel kept movement locked");
        }
        finally
        {
            if(restored)flags.SetBit(150);else flags.ClearBit(150);if(swimsuit)flags.SetBit(4610);else flags.ClearBit(4610);
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(roomRoot);UnityEngine.Object.DestroyImmediate(bodyTexture);
        }
    }
}