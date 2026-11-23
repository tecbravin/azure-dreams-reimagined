using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string OkamiBarPhaseKey = StoryPrefix + "vivian.capeBarPhase";
        private const string OkamiSadTalkKey = StoryPrefix + "vivian.closedBarTalk";
        private const string OkamiReopenedThanksKey = StoryPrefix + "vivian.reopenedThanks";
        private enum OkamiBarPhase { BeforeCape, Silent, Closed, Reopened }
        private static OkamiBarPhase CurrentOkamiBarPhase
        {
            get
            {
                int stage = StoryStage("vivian");
                if (stage < 2) return OkamiBarPhase.BeforeCape;
                if (stage > 2) return OkamiBarPhase.Reopened;
                // Preserve existing stage-2 saves, without resetting the route.
                return (OkamiBarPhase)TownSaveVariables.GetInt(OkamiBarPhaseKey,
                    StoryNewDay("vivian") ? (int)OkamiBarPhase.Closed : (int)OkamiBarPhase.Silent);
            }
        }
        private static bool OkamiBarClosed => CurrentOkamiBarPhase == OkamiBarPhase.Closed;

        private void AdvanceOkamiBarAfterTowerReturn()
        {
            if (StoryStage("vivian") != 2) return;
            switch (CurrentOkamiBarPhase)
            {
                case OkamiBarPhase.Silent:
                    TownSaveVariables.SetInt(OkamiBarPhaseKey,(int)OkamiBarPhase.Closed);
                    break;
                case OkamiBarPhase.Closed:
                    TownSaveVariables.SetInt(OkamiBarPhaseKey,(int)OkamiBarPhase.Reopened);
                    // Called before the town day increments, so Vivian is
                    // immediately available on this return rather than a day later.
                    AdvanceTownStory("vivian",3);
                    break;
            }
        }

        private bool OkamiReopeningThanks(NPCController npc) =>
            TownDialogueSequence(npc,null,()=>
            {
                TownSaveVariables.SetBool(OkamiReopenedThanksKey,true);
                PersistTownProgress();
            },
            ("Welcome, {name}.","Bem-vindo, {name}."),
            ("Thanks to the time I took off, look, the store looks better, doesn't it?",
                "Graças ao tempo que tirei de folga, veja, o bar ficou melhor, não acha?"),
            ("Business is booming, every day!\nI'm sure my man is smiling up in heaven about now.",
                "O movimento aumenta a cada dia!\nTenho certeza de que meu amado está sorrindo lá no céu agora."));

        private bool OkamiSadConversation(NPCController npc) =>
            TownDialogueSequence(npc,null,()=>StoryAsk(npc,
                "You look kind of lonely.","Você parece meio solitário.",
                (T("It's cause there's no bar.","É porque o bar está fechado."),()=>
                    TownDialogueSequence(npc,null,()=>OkamiSadRealization(npc),
                        ("Does that make you lonely...?","Isso faz você se sentir sozinho...?"),
                        ("Well whose fault was that!","Bem, de quem será a culpa!"))),
                (T("It's cause you're depressed.","É porque você está deprimida."),()=>OkamiSadRealization(npc))),
                ("Hm?\nLooking good.","Hm?\nEstá com boa aparência."),
                ("You've got the look of an adventurer filled with curiosity.",
                    "Você tem o olhar de um aventureiro cheio de curiosidade."),
                ("But...\nYour eyes lack that look you've had, {name}.",
                    "Mas...\nSeus olhos perderam aquele olhar que tinham, {name}."));

        private bool OkamiSadRealization(NPCController npc) =>
            StoryTalk(npc,".....!?\n!!!!\n......",".....!?\n!!!!\n......",()=>StoryAsk(npc,
                "I wonder if everyone is feeling that way?","Será que todos estão se sentindo assim?",
                (T("Of course!","Claro!"),()=>OkamiSadFinale(npc)),
                (T("I think so.","Acho que sim."),()=>OkamiSadFinale(npc))));

        private bool OkamiSadFinale(NPCController npc) =>
            StoryTalk(npc,"!!!!\nGet out of my bar!","!!!!\nFora do meu bar!",OpenVivianBar);

        private bool OkamiClosedLastLine(NPCController npc) =>
            StoryTalk(npc,"I've got some work to do.\nI've got to get this bar open for business, right?",
                "Tenho trabalho a fazer.\nPreciso reabrir este bar, não é?");
    }
}
