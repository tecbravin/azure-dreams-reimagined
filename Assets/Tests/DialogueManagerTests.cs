using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class DialogueManagerTests
{
    [Test]
    public void Awake_CreatesSingleton()
    {
        var go = new GameObject("DialogueManager");
        var manager = go.AddComponent<DialogueManager>();

        Assert.AreEqual(manager, DialogueManager.Instance);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Awake_DuplicateSingleton_DestroysDuplicate()
    {
        var go1 = new GameObject("DM1");
        var dm1 = go1.AddComponent<DialogueManager>();

        var go2 = new GameObject("DM2");
        var dm2 = go2.AddComponent<DialogueManager>();

        Assert.AreEqual(dm1, DialogueManager.Instance);

        Object.Destroy(go1);
        Object.Destroy(go2);
    }

    [Test]
    public void ShowLine_SetsText_AndActivatesPanel()
    {
        var go = new GameObject("DM");
        var manager = go.AddComponent<DialogueManager>();
        manager.dialoguePanel = new GameObject("Panel");
        manager.dialoguePanel.AddComponent<Canvas>(); // Required for TextMeshProUGUI
        var speakerGO = new GameObject("Speaker");
        speakerGO.transform.SetParent(manager.dialoguePanel.transform);
        manager.speakerNameText = speakerGO.AddComponent<TMPro.TextMeshProUGUI>();
        var dialogueGO = new GameObject("Dialogue");
        dialogueGO.transform.SetParent(manager.dialoguePanel.transform);
        manager.dialogueText = dialogueGO.AddComponent<TMPro.TextMeshProUGUI>();

        manager.ShowLine("Mira", "Hello there.");

        Assert.IsTrue(manager.dialoguePanel.activeSelf);
        Assert.AreEqual("Mira", manager.speakerNameText.text);
        Assert.AreEqual("Hello there.", manager.dialogueText.text);

        Object.DestroyImmediate(manager.dialoguePanel);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Hide_DeactivatesPanel()
    {
        var go = new GameObject("DM");
        var manager = go.AddComponent<DialogueManager>();
        manager.dialoguePanel = new GameObject("Panel");

        manager.ShowLine("X", "Y");
        manager.Hide();

        Assert.IsFalse(manager.dialoguePanel.activeSelf);

        Object.DestroyImmediate(manager.dialoguePanel);
        Object.DestroyImmediate(go);
    }
}
