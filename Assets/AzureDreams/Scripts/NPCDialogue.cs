using UnityEngine;

namespace AzureDreamsReimagined
{
    [CreateAssetMenu(menuName = "Azure Dreams/Town/NPC Dialogue")]
    public sealed class NPCDialogue : ScriptableObject
    {
        [TextArea] public string[] lines = new string[0];
        public bool loop = true;

        public readonly struct TalkEvent
        {
            public readonly string NpcId;
            public readonly string Dialogue;
            public readonly int LineIndex;
            public readonly bool Completed;
            public readonly GameObject Source;
            public TalkEvent(string npcId, string dialogue, int lineIndex, bool completed, GameObject source = null)
            { NpcId = npcId; Dialogue = dialogue; LineIndex = lineIndex; Completed = completed; Source = source; }
        }
    }
}
