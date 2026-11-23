using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Legacy instance ids retained for saves. Native dialogue speaker ids
        // (TOWN.BIN 0x2CE000) identify narder, kiku, nouhuko, oyaji, der, obasan, ran.
        private void SpawnFountainTownspeople()
        {
            if(_interior || !StoryBuilding("fountain")) return;
            var residents=new (string Id,string Name,Vector2Int Cell)[]
            {
                ("center_nada",T("Townswoman","Moradora"),new(62,65)),
                ("center_hear",T("Child","Criança"),new(67,65)),
                ("center_jacof",T("Farmer","Fazendeiro"),new(72,69)),
                ("center_pop",T("Townsman","Morador"),new(72,75)),
                ("center_uncle",T("Townsman","Morador"),new(67,79)),
                ("center_aunt",T("Aunt","Tia"),new(60,79)),
                ("center_aunt_2",T("Townswoman","Moradora"),new(55,73)),
            };
            foreach(var resident in residents)
            {
                if(FindStoryActor(resident.Id)) continue;
                SpawnStoryResident("dar",resident.Id,resident.Name,resident.Cell);
                var npc=FindStoryActor(resident.Id);
                if(!npc) continue;
                // Never inherit the restaurant customer's lines from the fallback prefab.
                npc.dialogueLines=System.Array.Empty<string>();
                npc.loopDialogue=false;
                var toward=new Vector3(64f,0f,72f)-npc.transform.position;toward.y=0;
                if(toward.sqrMagnitude>.001f) npc.transform.rotation=Quaternion.LookRotation(toward);
            }
        }
        // Original English pages, not the fallback restaurant prefab dialogue.
        // 0x2CE009 (61), 0x2CE108 (60), 0x2CE1F1 (45), 0x2CEE5A (62),
        // 0x2CEFE0 (44), 0x2CF120 (31), 0x2CE87F (64).
        private bool FountainResidentTalk(NPCController npc)
        {
            if (StoryRoom.Length != 0 || !StoryBuilding("fountain")) return false;
            switch (npc.npcId)
            {
                case "center_nada":
                    return TownTalkSequence(npc,null,
                        new TownStoryLine("They say children's songs are messages from God, I wonder if it's true.",
                            "Dizem que as canções das crianças são mensagens de Deus. Será que é verdade?"),
                        new TownStoryLine("But I still have no idea what they are singing about.",
                            "Mas ainda não faço ideia do que elas estão cantando."));
                case "center_hear":
                    string attack = _attackAction?.GetBindingDisplayString() ?? "Attack";
                    string inventory = _inventoryAction?.GetBindingDisplayString() ?? "I";
                    string run = _runAction?.GetBindingDisplayString() ?? "Shift";
                    string jump = _wait?.GetBindingDisplayString() ?? "T";
                    return TownTalkSequence(npc,null,
                        new TownStoryLine($"The attack button is {attack} button.\nThe item button is {inventory}.",
                            $"O botão de ataque é {attack}.\nO botão de itens é {inventory}."),
                        new TownStoryLine($"To hurry, hurry, {run} button.\nTo jump, use {jump} button.",
                            $"Para correr, correr, use {run}.\nPara pular, use {jump}."));
                case "center_jacof":
                    return TownTalkSequence(npc,null,
                        new TownStoryLine("You're so cool, man.\nIntelligent agricultural methods mustn't be abandoned,",
                            "Você é demais, cara.\nOs métodos agrícolas inteligentes não devem ser abandonados,"),
                        new TownStoryLine("but...\nI'm going to be a monster tamer just like you!",
                            "mas...\nVou ser um domador de monstros como você!"));
                case "center_pop":
                    return TownTalkSequence(npc,null,
                        new TownStoryLine("How about a T-shirt for a souvenir ?\nA T-shirt with the face of the hero {name}.",
                            "Que tal uma camiseta de lembrança?\nUma camiseta com o rosto do herói {name}."),
                        new TownStoryLine("It would scare monsters away when you put it on.",
                            "Ela espantaria os monstros quando você a vestisse."),
                        new TownStoryLine("Oh my god, it's really {name}?\nYou're giving us good business.",
                            "Meu Deus, é mesmo o {name}?\nVocê está fazendo bem aos negócios."));
                case "center_uncle":
                    return TownTalkSequence(npc,null,
                        new TownStoryLine("Cool, cool, he's so cool,\nThat monster tamer!",
                            "Demais, demais, ele é demais,\naquele domador de monstros!"),
                        new TownStoryLine("They say he's climbing up the monster tower without missing a beat.",
                            "Dizem que ele está subindo a torre dos monstros sem perder o ritmo."),
                        new TownStoryLine("Hey, you look like the guy in this picture.",
                            "Ei, você parece o cara desta imagem."));
                case "center_aunt_2":
                    return TownTalkSequence(npc,null,
                        new TownStoryLine("Are you.....by any chance ?\nAhhh !!!!!",
                            "Você é... por acaso?\nAhhh!!!!!"));
                case "center_aunt":
                    int floor = TownReputation.Read().HighestFloor;
                    if (floor >= 20)
                        return TownTalkSequence(npc,null,
                            new TownStoryLine("Hey, do you know ?\n{name} the monster tamer? They say he reached the 20th floor.",
                                "Ei, você sabe?\nO domador de monstros {name}? Dizem que chegou ao 20º andar."),
                            new TownStoryLine("Wow, I'm so thrilled.\nHey, do you know about {name}?",
                                "Nossa, estou tão animada.\nEi, você conhece o {name}?"),
                            new TownStoryLine("He's so cool and young, and his long red hair is ponytailed in the back. He's only a 15 year-old young man.",
                                "Ele é tão bonito e jovem, com o cabelo ruivo comprido preso num rabo de cavalo. Só tem quinze anos."),
                            new TownStoryLine("A boy really!\nRed......Ponytail.....\n{name} !\n{name} is right here !",
                                "Um menino, na verdade!\nRuivo... Rabo de cavalo...\n{name}!\nO {name} está bem aqui!"));
                    if (floor >= 15)
                        return TownTalkSequence(npc,null,
                            new TownStoryLine("Did you hear the news ?\nThat {name} the monster tamer who went up to the 15th floor,",
                                "Soube da novidade?\nO domador de monstros {name}, que chegou ao 15º andar,"),
                            new TownStoryLine("they say he's only a 15 year-old boy who attained manhood this year.",
                                "dizem que é um garoto de quinze anos que atingiu a maioridade este ano."),
                            new TownStoryLine("Also, he started constructing buildings, one after another, with the money he earned.",
                                "Além disso, começou a construir prédios, um após o outro, com o dinheiro que ganhou."),
                            new TownStoryLine("Wow, Impressive.\nHey, are you a monster tamer too? You've got to keep it up.",
                                "Nossa, impressionante.\nEi, você também é domador? Tem que continuar assim."));
                    if (floor >= 10)
                        return TownTalkSequence(npc,null,
                            new TownStoryLine("Did you hear the news ?\nI heard a monster tamer called {name} has reached the 10th floor of the monster tower.",
                                "Soube da novidade?\nOuvi dizer que um domador chamado {name} chegou ao 10º andar da torre dos monstros."),
                            new TownStoryLine("What a guy, huh ? He must be like a monster himself.",
                                "Que sujeito, hein? Ele mesmo deve ser um monstro."));
                    return StoryTalk(npc,"This place is so nice and peaceful.","Este lugar é tão agradável e tranquilo.");
                default: return false;
            }
        }
    }
}
