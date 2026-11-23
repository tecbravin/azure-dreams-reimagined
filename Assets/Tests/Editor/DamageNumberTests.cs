using System;
using System.Collections;
using System.Reflection;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

public sealed class DamageNumberTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    static object Call(TowerGameController c, string name, params object[] args) => typeof(TowerGameController).GetMethod(name, Flags).Invoke(c, args);
    static FieldInfo Field(string name) => typeof(TowerGameController).GetField(name, Flags);
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    [Test]
    public void DamageNumberJumpsFromHeadAndLandsAtFeet()
    {
        var head = new Vector3(2, 2, 3); var feet = new Vector3(2, 0, 3);
        Vector3 Point(float progress) => (Vector3)Call(null, "DamageNumberPosition", head, feet, Vector3.up, Vector3.right, .25f, progress);
        Check(Point(0f) == head, "Number did not start at the head");
        Check(Point(.1f).y > head.y, "Number did not jump upwards");
        Check(Point(.9f).y < head.y, "Number did not fall back down");
        Check(Point(1f) == feet && Point(2f) == feet, "Number did not land at the feet");
    }

    [Test]
    public void DamageNumberUsesTheAmountAndActualSpriteHead()
    {
        var root = new GameObject("Damage popup test"); root.SetActive(false);
        var unit = new GameObject("Short unit"); var cameraObject = new GameObject("Damage popup camera");
        var texture = new Texture2D(16,16);
        try
        {
            var c = root.AddComponent<TowerGameController>(); var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0,3,-6); camera.transform.LookAt(Vector3.zero);
            Field("_gameCamera").SetValue(c,camera);
            unit.transform.position = new Vector3(0,.55f,0);
            var visual = new GameObject("Body"); visual.transform.SetParent(unit.transform,false); visual.transform.localPosition = Vector3.down*.55f; visual.transform.rotation = camera.transform.rotation;
            var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = Sprite.Create(texture,new Rect(0,0,16,16),new Vector2(.5f,0),32);
            Call(c,"ShowDamageNumber",unit,27); Call(c,"ShowDamageNumber",unit,0);
            var list = (IList)Field("_damageNumbers").GetValue(c); Check(list.Count==1,"Zero damage created a popup");
            var popup = list[0]; const BindingFlags fields=BindingFlags.NonPublic|BindingFlags.Instance;
            var head = (Vector3)popup.GetType().GetField("Head",fields).GetValue(popup);
            var feet = (Vector3)popup.GetType().GetField("Feet",fields).GetValue(popup);
            var label = (GUIContent)popup.GetType().GetField("Label",fields).GetValue(popup);
            Check(label.text=="27","Wrong displayed damage");
            Check(feet==Vector3.zero,"Wrong landing position");
            float top=camera.WorldToScreenPoint(visual.transform.TransformPoint(new Vector3(0,.5f,0))).y;
            Check(Mathf.Abs(camera.WorldToScreenPoint(head).y-top-5f)<.01f,"Short unit popup did not start at its head");
            Call(c,"ShowDamageNumber",unit,5); Check(list.Count==2,"A second hit replaced the first number");
        }
        finally { UnityEngine.Object.DestroyImmediate(unit); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(texture); }
    }
}