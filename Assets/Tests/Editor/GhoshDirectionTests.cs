using System;
using System.Reflection;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

public sealed class GhoshDirectionTests
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static readonly Type T=typeof(RetailTownResidentSpriteView);
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    [Test]
    public void GhoshFacesEightWaysWhileIdleWalkingAndTalkingAtAnyCameraYaw()
    {
        var root=new GameObject("Ghosh direction validation");root.SetActive(false);var cameraRoot=new GameObject("Ghosh camera");
        try
        {
            var atlas=Resources.Load<Texture2D>("Town/Residents/Retail/gosh");var data=Resources.Load<TextAsset>("Town/Residents/Retail/gosh");Check(atlas&&data,"Ghosh bank missing");
            var view=root.AddComponent<RetailTownResidentSpriteView>();view.Configure(atlas,data);var camera=cameraRoot.AddComponent<Camera>();T.GetField("_camera",F).SetValue(view,camera);
            var locomotion=root.AddComponent<ActorLocomotionView>();typeof(ActorLocomotionView).GetMethod("UseRetailTownSprite",F).Invoke(locomotion,new object[]{view});
            var visitor=root.AddComponent<TowerEventNpc>();typeof(TowerEventNpc).GetMethod("Awake",F).Invoke(visitor,null);
            var sprites=(Sprite[])T.GetField("_frames",F).GetValue(view);Check(sprites.Length==45,"Rear Ghosh frames are missing");
            foreach(float yaw in new[]{0f,90f,180f,270f})for(int mode=0;mode<3;mode++)for(int direction=0;direction<8;direction++)
            {
                camera.transform.rotation=Quaternion.Euler(35,yaw,0);var forward=camera.transform.forward;forward.y=0;forward.Normalize();float angle=(direction-2)*45*Mathf.Deg2Rad;
                var facing=camera.transform.right*Mathf.Sin(angle)-forward*Mathf.Cos(angle);
                if(mode==2)visitor.Face(root.transform.position+facing);
                else locomotion.Present(facing,0f,mode==1);
                T.GetField("_ticks",F).SetValue(view,0d);T.GetMethod("LateUpdate",F).Invoke(view,null);
                var clip=T.GetField("_clip",F).GetValue(view);var directions=(Array)clip.GetType().GetField("directions").GetValue(clip);Check(directions.Length==8,"Ghosh clip still discards rear/diagonal views");
                var sequence=directions.GetValue(direction);var indices=(int[])sequence.GetType().GetField("frames").GetValue(sequence);var renderer=(SpriteRenderer)T.GetField("_renderer",F).GetValue(view);
                int actual=Array.IndexOf(sprites,renderer.sprite);Check(Array.IndexOf(indices,actual)>=0,"Wrong Ghosh direction at yaw "+yaw+" / "+direction+" mode "+mode);
                Check(renderer.flipX==(direction>=3&&direction<=5),"Wrong Ghosh horizontal mirror");
            }
            var four=T.GetMethod("FieldSpriteDirection",BindingFlags.NonPublic|BindingFlags.Static);
            Check((int)four.Invoke(null,new object[]{-1f,0f,4})==0 && (int)four.Invoke(null,new object[]{0f,-1f,4})==1 && (int)four.Invoke(null,new object[]{1f,0f,4})==2 && (int)four.Invoke(null,new object[]{0f,1f,4})==3,"Four-direction/pool residents changed orientation");
        }
        finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraRoot);}
    }
}