using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string FamilyHouse = "Interior_23_generic_house";
        private const string AuntPortrait = "Dialogue/Aunt_variant0_frame0";
        private const string GrandpaPortrait = "Dialogue/Grandpa_variant0_frame0";

        private static bool IsSeatedInteriorResident(string room, string id) =>
            (room == BarRoom && id is "bar_hunter_2" or "bar_hunter_3") ||
            (room == RestaurantRoom && id is "dar" or "bet" or "restaurant_hunter");

        private void SpawnAmbientInteriorResidents(string room)
        {
            switch (room)
            {
                case RhodesHouse:
                    if(!FindStoryActor("rhodes_butler"))
                    {
                        SpawnStoryResident("dar","rhodes_butler",T("Butler","Mordomo"),default);
                        var butler=FindStoryActor("rhodes_butler");
                        if(butler){butler.dialogueLines=System.Array.Empty<string>();butler.transform.rotation=Quaternion.LookRotation(Vector3.forward);}
                    }
                    break;
                case LibraryRoom:
                    foreach(var resident in new[]{("library_keeper",T("Librarian","Bibliotecário")),("library_reader_woman",T("Reader","Leitora")),("library_reader_man",T("Reader","Leitor"))})
                    {
                        if(FindStoryActor(resident.Item1)) continue;
                        SpawnStoryResident("dar",resident.Item1,resident.Item2,default);
                        var person=FindStoryActor(resident.Item1);
                        if(person){person.dialogueLines=System.Array.Empty<string>();person.transform.rotation=Quaternion.LookRotation(Vector3.back);}
                    }
                    break;
                case BarRoom:
                    if (!OkamiBarClosed) SpawnRetailResident("bar_barten", "Barten", "Town/Residents/Barten");
                    break;
                case GymRoom:
                    SpawnGymResidents();
                    break;
                case FamilyHouse:
                    SpawnRetailResident("aunt", T("Aunt", "Tia"), "Title/AuntRain");
                    SpawnRetailResident("grandpa", T("Grandpa", "Vovô"), "Title/GrandpaRain");
                    break;
            }
        }

        private void SpawnRetailResident(string id, string name, string spritePath)
        {
            var sprite = AssetMode.Load<Sprite>(spritePath);
            if (!sprite) return;
            SpawnStoryResident(FindStoryResidentPrefab(id) ? id : "dar", id, name, default);
            var npc = FindStoryActor(id);
            if (npc && npc.TryGetComponent<RetailTownResidentSpriteView>(out _)) return;
            if (npc && npc.GetComponentInChildren<SpriteRenderer>(true) is { } renderer)
                renderer.sprite = sprite;
        }

        private bool TryAmbientInteriorDialogue(NPCController npc)
        {
            if(npc.npcId=="rhodes_butler" && StoryRoom==RhodesHouse)
            {
                const string met="dialogue.rhodes_butler.met";
                bool repeat=TownSaveVariables.GetBool(met);
                // Original speaker 0x50, TOWN.BIN 0x6D0009 / 0x6D0084.
                return StoryTalk(npc,
                    repeat ? "Hello Mr. {name}.\nMaster Ghosh and Miss Selfi are\nout again today." : "Hello Mr. {name}.\nMaster Ghosh and Miss Selfi are\npresently out.",
                    repeat ? "Olá, senhor {name}.\nO senhor Ghosh e a senhorita Selfi\nestão fora novamente hoje." : "Olá, senhor {name}.\nO senhor Ghosh e a senhorita Selfi\nestão fora no momento.",
                    ()=>{TownSaveVariables.SetBool(met,true);PersistTownProgress();});
            }
            if (ArcadeResidentDialogue(npc)) return true;
            if (InRaceLobby && RacetrackResidentDialogue(npc)) return true;
            if (GymResidentDialogue(npc)) return true;
            if (CasinoResidentDialogue(npc)) return true;
            if(StoryRoom==LibraryRoom)
            {
                // Authored ambience until the retail library speaker scripts
                // are mapped; do not pass these off as extracted game dialogue.
                if(npc.npcId=="library_keeper") return StoryTalk(npc,
                    "Welcome to the library. You can learn about the tower and its monsters here. Please keep your voice down.",
                    "Bem-vindo à biblioteca. Aqui você pode aprender sobre a torre e seus monstros. Por favor, fale baixo.");
                if(npc.npcId=="library_reader_woman") return StoryTalk(npc,
                    "I'm reading about the monsters in the tower. There is still so much to learn.",
                    "Estou lendo sobre os monstros da torre. Ainda há tanto para aprender.");
                if(npc.npcId=="library_reader_man") return StoryTalk(npc,
                    "It's good to have a quiet place to read in Monsbaiya.",
                    "É bom ter um lugar tranquilo para ler em Monsbaiya.");
            }
            int floor = TownReputation.Read().HighestFloor;
            return npc.npcId switch
            {
                "bar_barten" when StoryRoom == BarRoom => BartenDrinks(npc),
                "bar_hunter_1" when StoryRoom == BarRoom => BarHunterOne(npc, floor),
                "bar_hunter_2" when StoryRoom == BarRoom => BarHunterTwo(npc, floor),
                "bar_hunter_3" when StoryRoom == BarRoom => BarHunterThree(npc, floor),
                "okami" when StoryRoom == BarRoom => OkamiAmbientGreeting(npc),
                "nico_dad" when StoryRoom == NicoHouse => NicoFatherAmbient(npc),
                "aunt" when StoryRoom == FamilyHouse => AuntHouseDialogue(npc, floor),
                "grandpa" when StoryRoom == FamilyHouse => GrandpaHouseDialogue(npc),
                "coppe" or "dar" or "bet" or "restaurant_hunter" when StoryRoom == RestaurantRoom =>
                    RestaurantAmbientDialogue(npc),
                _ => false
            };
        }

        // TOWN.BIN 0x6A177D–0x6A1D8B.
        private bool BartenDrinks(NPCController npc) =>
            StoryAsk(npc, "Welcome Mr. {name}.\nWhat would you like today?",
                "Bem-vindo, senhor {name}.\nO que vai querer hoje?",
                (T("I’ll have milk.", "Quero leite."), () => BartenServeDrink(npc, milk: true)),
                (T("I’ll have juice.", "Quero suco."), () => BartenServeDrink(npc, milk: false)),
                (T("I don’t want anything.", "Não quero nada."), () =>
                    StoryTalk(npc, "It couldn’t hurt to order something once in a while, could it?",
                        "Não custaria pedir alguma coisa de vez em quando, custaria?")));

        private void BartenServeDrink(NPCController npc, bool milk)
        {
            // The original price table at TOWN.BIN 0x6920D0 stores milk followed by juice.
            int price = milk ? 120 : 100;
            void Serve()
            {
                if (milk)
                    TownDialogueSequence(npc, null, null,
                        ("(Gulp, gulp...)", "(Glup, glup...)"),
                        ("The milk here is special. It goes down smooth because it’s not too thick, and the natural sweetness is just right.",
                            "O leite daqui é especial. Desce fácil porque não é grosso demais, e a doçura natural é na medida."),
                        ("(Gulp, gulp...)", "(Glup, glup...)"));
                else
                    TownDialogueSequence(npc, null, null,
                        ("(Gulp, gulp...)", "(Glup, glup...)"),
                        ("This should be popular because it’s squeezed in front of the customer.",
                            "Isto devia fazer sucesso, já que é espremido na frente do cliente."),
                        ("As you see, it’s 100% freshly squeezed orange juice.",
                            "Como pode ver, é suco de laranja 100% espremido na hora."),
                        ("But I’m making no money on this, because you’re about the only one that orders orange juice in a bar.",
                            "Mas não ganho nada com isso, porque você é praticamente o único que pede suco de laranja num bar."),
                        ("(Gulp, gulp...)", "(Glup, glup...)"));
            }

            string english = milk ? $"(Glug, glug)\nHere you go.\nThat will be {price} G." : $"(Squirt, squirt)\nThat will be {price} G.";
            string portuguese = milk ? $"(Glug, glug)\nAqui está.\nSão {price} G." : $"(Espremer, espremer)\nSão {price} G.";
            StoryTalk(npc, english, portuguese, () =>
                {
                    if (_gold < price)
                    {
                        StoryTalk(npc, "You say you have no money?\nThen it’s on me.\nDon’t tell the boss.",
                            "Diz que está sem dinheiro?\nEntão é por minha conta.\nNão conte à chefe.", Serve);
                        return;
                    }
                    _gold -= price;
                    PersistTownProgress();
                    Serve();
                });
        }

        // TOWN.BIN 0x6A3FB2–0x6A49FC, actor 0x34.
        private bool BarHunterOne(NPCController npc, int floor)
        {
            if (floor >= 40)
                return TownDialogueSequence(npc, null, null,
                    ("Yo!  So you’ve made it to the top level!", "Ei! Então você chegou ao topo!"),
                    ("I knew all along that you could do it!", "Eu sempre soube que você conseguiria!"),
                    ("No wonder, you’re Guy’s kid... no, I’d say you’re a better monster tamer than Guy, why, you’re Beast Master {name}!",
                        "Claro, é filho de Guy... não, eu diria que é um domador melhor que Guy. Ora, você é o Mestre das Feras, {name}!"),
                    ("There, doesn’t that feel good?\nWell then, buy me a drink!", "Viu só, não é bom ouvir isso?\nEntão me pague uma bebida!"));
            if (floor >= 20)
                return TownDialogueSequence(npc, null, null,
                    ("Yo!  Rumors are you’ve made it all the way to level 20!", "Ei! Dizem que você chegou ao andar 20!"),
                    ("By the way, I remember something else Guy said.", "A propósito, lembrei de outra coisa que Guy disse."),
                    ("Monsters that you release at the tower will not return to the hut.", "Os monstros que você soltar na torre jamais voltarão ao alojamento."),
                    ("In which case it would be a waste to release them for nothing.", "Nesse caso, seria um desperdício soltá-los à toa."),
                    ("It would be wise to fuse the monsters together at the hut.", "Seria melhor fundir os monstros no alojamento."),
                    ("Well?  Pretty useful information, yeah?", "E aí? Informação bem útil, hein?"),
                    ("Why, fusion will also recover their magical powers.", "A fusão ainda recupera o poder mágico deles."));
            if (floor >= 10)
                return TownDialogueSequence(npc, null, null,
                    ("Yo!  How’re ya doin’!", "Ei! Como vai!"),
                    ("I’ll tell ya something I heard from Guy, this incredible monster tamer.",
                        "Vou contar uma coisa que ouvi de Guy, aquele domador incrível."),
                    ("There’s no need to bring out two familiars at the same time.", "Não precisa tirar dois familiares da bolsa ao mesmo tempo."),
                    ("It might make fighting easier, but it will drain your magical powers twice as quickly.",
                        "Pode facilitar a luta, mas gasta o poder mágico duas vezes mais rápido."),
                    ("What you must be careful about, though, is that monsters cannot increase their experience while in your pouch.",
                        "Mas tome cuidado, pois os monstros não ganham experiência enquanto estão na bolsa."));
            if (floor >= 5)
                return TownDialogueSequence(npc, null, null,
                    ("Yo!  I hear you’ve finally made it up a few levels.", "Ei! Ouvi dizer que finalmente subiu alguns andares."),
                    ("I gotta hand it to ya, still a kid and all, to make it that far.", "Tenho que dar o braço a torcer. Ainda é garoto e já chegou tão longe."),
                    ("I’ll give you this piece of advice as a gift.", "Vou dar este conselho de presente."),
                    ("Bomb traps will destroy all items in its vicinity, but rest assured that items cannot be destroyed by magic.",
                        "Armadilhas explosivas destroem todos os itens por perto, mas fique tranquilo, magia não destrói itens."));
            return TownDialogueSequence(npc, null, null,
                ("Yo!  You’ve started going to the tower?", "Ei! Começou a ir à torre?"),
                ("Ha-ha-ha-ha-ha!  Y’all listen to that?", "Ha-ha-ha-ha-ha! Vocês ouviram essa?"),
                ("He’s calling himself a hunter!\nYou could get yourself killed taking the tower too lightly.",
                    "Ele está se chamando de caçador!\nVocê pode acabar morto se não levar a torre a sério."),
                ("Well... we’ll educate you on what we’ve experienced.\nConsider yourself lucky!",
                    "Bem... vamos ensinar o que aprendemos na prática.\nConsidere-se com sorte!"),
                ("But we’re not going to repeat everything we say, so listen carefully.",
                    "Mas não vamos repetir tudo o que dissermos, então escute bem."));
        }

        // TOWN.BIN 0x6A4BEA–0x6A536F, actor 0x35.
        private bool BarHunterTwo(NPCController npc, int floor)
        {
            if (floor >= 40)
                return StoryTalk(npc, "Hey!  You’re amazing!\nSo you got the Ultimate Egg! Let me see it next time you’re here.",
                    "Ei! Você é incrível!\nEntão conseguiu o Ovo Supremo! Mostre pra mim da próxima vez que vier.");
            if (floor >= 20)
                return TownDialogueSequence(npc, null, null,
                    ("What is it, kid?\nIs it true that you’ve already made it past level 20!?", "O que foi, garoto?\nÉ verdade que já passou do andar 20!?"),
                    ("It’s no longer a dream, you just might get that Ultimate Egg on the top level!",
                        "Já não é só um sonho. Talvez você consiga pegar o Ovo Supremo lá no topo!"));
            if (floor >= 10)
                return TownDialogueSequence(npc, null, null,
                    ("Have you heard about the monster den?", "Já ouviu falar do ninho de monstros?"),
                    ("In the tower, it can suddenly start raining monsters, and before you know it, you have a monster den.",
                        "Na torre, de repente pode começar a chover monstros e, quando você percebe, tem um ninho de monstros."),
                    ("And they never said anything about it on the weather forecast!", "E a previsão do tempo não avisou nada!"));
            if (floor >= 5)
                return TownDialogueSequence(npc, null, null,
                    ("What is it, kid?\nI hear you’ve made it to the fifth level.", "O que foi, garoto?\nOuvi dizer que chegou ao quinto andar."),
                    ("So you’ve got some abilities.\nBy the way...", "Então você tem algum talento.\nA propósito..."),
                    ("Have you heard?  This is well known amongst us hunters, but apparently there is an [Ultimate Egg] on the top level!",
                        "Já ouviu falar? Nós, caçadores, já sabemos disso faz tempo, mas parece que tem um [Ovo Supremo] no último andar!"),
                    ("I wonder what monster’s going to hatch out of that thing!?", "Que monstro será que vai sair daquilo!?"),
                    ("Well it would be a dream of a dream for you to ever get that far.",
                        "Bem, pra você chegar tão longe, só mesmo num sonho dentro de um sonho."));
            return TownDialogueSequence(npc, null, null,
                ("What is it, kid?", "O que foi, garoto?"),
                ("You’re going to the tower!?\nHaw-haw-haw, the best joke I’ve heard all day.",
                    "Vai pra torre!?\nHa-ha-ha, é a melhor piada que ouvi hoje."),
                ("Well, here’s a piece of information.", "Bem, aqui vai uma informação."),
                ("Medicinal herbs are cheaper at the weapons store than at the hospital. he-he-he.  (Not true)",
                    "Ervas medicinais são mais baratas na loja de armas que no hospital. He-he-he. (Mentira)"));
        }

        // TOWN.BIN 0x6A53CE–0x6A5DCC, actor 0x36.
        private bool BarHunterThree(NPCController npc, int floor)
        {
            if (floor >= 40)
                return StoryTalk(npc, "Howdy!\nI give up, you win.", "Opa!\nEu desisto, você venceu.");
            if (floor >= 20)
                return TownDialogueSequence(npc, null, null,
                    ("Howdy!\nWhy don’t I give you another piece of information today...", "Opa!\nQue tal mais uma dica hoje..."),
                    ("Be cautious of the meat that the monster Weadog has.", "Tome cuidado com a carne que o monstro Weadog carrega."),
                    ("Your familiars consider it a delicacy.", "Seus familiares acham que é uma iguaria."),
                    ("Familiars with less magical abilities, in other words those that are hungry, will jump at the meat.",
                        "Os familiares com pouco poder mágico, ou seja, os que estão com fome, vão se jogar na carne."),
                    ("You’ve got to watch out for your familiars.", "Fique de olho nos seus familiares."));
            if (floor >= 10)
                return TownDialogueSequence(npc, null, null,
                    ("Howdy!\nWhy don’t I give you another piece of information today...", "Opa!\nQue tal mais uma dica hoje..."),
                    ("When you find a trap using the Trap Lupe, the trap is still active. The device has to be terminated.",
                        "Quando encontrar uma armadilha com a Lupa de Armadilhas, ela ainda estará ativa. O mecanismo tem que ser desativado."),
                    ("And to terminate it, you must keep swinging your weapon in front of the device.",
                        "Para desativar, continue balançando sua arma em frente ao mecanismo."),
                    ("Also, watch out for traps while you’re using the Trap Lupe.",
                        "E tome cuidado com as armadilhas enquanto usa a Lupa de Armadilhas."),
                    ("Otherwise, all I can say is that terminating the activating device on the trap takes experience.",
                        "Fora isso, só posso dizer que desativar o mecanismo da armadilha exige experiência."));
            if (floor >= 5)
                return TownDialogueSequence(npc, null, null,
                    ("Howdy!\nYou fighting more like a man now?", "Opa!\nJá está lutando mais como um homem?"),
                    ("Well then, I’ll tell you something that you’d be wise to remember.", "Então, vou lhe contar uma coisa que seria bom lembrar."),
                    ("You can strengthen your sword by using items, but it will have no effect on any mixture magic.",
                        "Você pode fortalecer sua espada com itens, mas isso não afeta nenhuma magia combinada."),
                    ("On the other hand, although the cane cannot be strengthened, it can increase the power of mixture magic, as well as often having its own special powers.",
                        "Por outro lado, embora o cajado não possa ser fortalecido, pode aumentar o poder da magia combinada e muitas vezes tem poderes próprios."),
                    ("... well, keep trying.", "... Bem, continue tentando."));
            return TownDialogueSequence(npc, null, null,
                ("Howdy!\nWhat, you’re a monster tamer!?", "Opa!\nComo é, você é domador de monstros!?"),
                ("Can’t you come up with a better joke?", "Não consegue pensar numa piada melhor?"),
                ("If you’re serious, you’d be better off talking to those guys at the Monster Shop for information on the monsters.",
                    "Se está falando sério, é melhor perguntar sobre os monstros ao pessoal da Loja de Monstros."),
                ("Give it a try.", "Tente lá."));
        }

        // TOWN.BIN 0x6A7DCE–0x6A7F35. Quest conversations take priority.
        private bool OkamiAmbientGreeting(NPCController npc)
        {
            if (StoryStage("vivian") == 2) return VivianStory(npc);
            if (StoryStage("vivian") >= StoryComplete && !TownSaveVariables.GetBool(OkamiReopenedThanksKey))
                return StoryTalk(npc, "Welcome, {name}.", "Bem-vindo, {name}.");
            return StoryAsk(npc, "Welcome, {name}.\nLooking cute, as always.", "Bem-vindo, {name}.\nBonitinho como sempre.",
                (T("Would you cut that out.", "Dá pra parar com isso."), () =>
                    StoryAsk(npc, "Then what are you?", "Então, o que você é?",
                        (T("Tell me I’m awesome.", "Diga que eu sou incrível."), () =>
                            StoryTalk(npc, "he, he, now that’s cute.", "He, he, isso é bonitinho.")),
                        (T("Tell me I’m handsome.", "Diga que eu sou bonito."), () =>
                            StoryTalk(npc, "he, he, now that’s cute.", "He, he, isso é bonitinho.")))),
                (T("Thanks.", "Obrigado."), () => StoryTalk(npc, "he, he, now that’s cute.", "He, he, isso é bonitinho.")));
        }

        // TOWN.BIN 0x66880C and 0x669C41–0x669E10.
        private bool NicoFatherAmbient(NPCController npc)
        {
            if (StoryStage("nico") is < NicoHouseGuard or > NicoHouseTalkSecond)
                return StoryTalk(npc, "Well hello!  {name}!", "Ora, olá, {name}!");
            return StoryTalk(npc, "Nico has been acting a little strange lately.", "A Nico anda meio estranha ultimamente.", () =>
                StoryAsk(npc, "Nico isn’t feeling well... did you guys have a fight or something?",
                    "A Nico não está se sentindo bem... vocês brigaram ou aconteceu alguma coisa?",
                    (T("Yes, we had a fight.", "Sim, brigamos."), () =>
                        StoryTalk(npc, "That’s what I suspected.\nHope you guys would make up soon.",
                            "Era o que eu suspeitava.\nEspero que façam as pazes logo.")),
                    (T("We did not have a fight.", "Nós não brigamos."), () =>
                        StoryTalk(npc, "Then I wonder what it could be...  It’s unusual for her to be under the weather like this.",
                            "Então o que será... Não é de costume ela ficar abatida assim."))));
        }

        // TOWN.BIN 0x66B80D–0x66BD76.
        private bool GrandpaHouseDialogue(NPCController npc)
        {
            const string met = "dialogue.grandpa.house_met";
            if (TownSaveVariables.GetBool(met))
                return StoryTalk(npc, "Ha ha!  Go get ’em!\nYoungsters now a days, they’re so pushy.",
                    "Ha ha! Vá atrás deles!\nOs jovens de hoje em dia são tão afoitos.", GrandpaPortrait);
            return TownDialogueSequence(npc, GrandpaPortrait, () =>
                {
                    TownSaveVariables.SetBool(met, true);
                    PersistTownProgress();
                },
                ("Well!  If it isn’t {name}.", "Ora! Se não é {name}."),
                ("I thought you was Guy comin’ in!\nYou look all fired up!", "Achei que era o Guy entrando!\nParece cheio de energia!"));
        }

        private bool AuntHouseDialogue(NPCController npc, int floor)
        {
            const string met = "dialogue.aunt.house_met";
            if (TownSaveVariables.GetBool(met))
                return floor >= 5
                    ? TownDialogueSequence(npc, AuntPortrait, () => AuntMemories(npc),
                        ($"Oh, wait a minute, {{name}}. I heard you’ve already made it to level {floor} at the tower.",
                            $"Ah, espere um pouco, {{name}}. Ouvi dizer que você já chegou ao andar {floor} da torre."),
                        ("You’re making adults look bad. I knew, though, that this kid was going to be something else.",
                            "Você está fazendo os adultos passarem vergonha. Mas eu sabia que esse garoto ia longe."),
                        ("Things at home also seem to be looking better;  things are looking positive, aren’t they {name}?",
                            "As coisas lá em casa também parecem estar melhorando. Está tudo indo bem, não é, {name}?"),
                        ("Keep out of trouble, and good luck.", "Não se meta em encrenca e boa sorte."))
                    : StoryTalk(npc, "If there’s anything you need to know, ask the treasure hunters at the bar.\nI’m sure they’ll be helpful.",
                        "Se tiver alguma dúvida, pergunte aos caçadores de tesouros no bar.\nTenho certeza de que vão ajudar.", AuntPortrait, () => AuntMemories(npc));

            void Finish()
            {
                StoryTalk(npc, "Well, take care of yourself.", "Bem, cuide-se.", AuntPortrait, () =>
                {
                    TownSaveVariables.SetBool(met, true);
                    PersistTownProgress();
                    AuntMemories(npc);
                });
            }

            return TownDialogueSequence(npc, AuntPortrait, () =>
                    StoryAsk(npc, "By the way, do you know anything about the Dungeon?",
                        "A propósito, você sabe alguma coisa sobre a masmorra?", AuntPortrait,
                        (T("Yes.", "Sim."), Finish),
                        (T("Not at all.", "Nada."), () => TownDialogueSequence(npc, AuntPortrait, Finish,
                            ("You should talk to the treasure hunters for details on traps and things.",
                                "Fale com os caçadores de tesouros para saber mais sobre armadilhas e essas coisas."),
                            ("They hang out at the local bar, try them there.", "Eles costumam ficar no bar daqui, procure lá."),
                            ("You can be certain to always find them at either the bar or the market.",
                                "Pode ter certeza de que sempre vai encontrá-los no bar ou no mercado.")))),
                ("Oh my, you’re the son of Guy!\nWhat’s the matter!?", "Nossa, você é o filho do Guy!\nO que houve!?"),
                ("To the tower?", "Vai à torre?"),
                ("You’ve already reached that age?\nHey, you’ve grown big.", "Já chegou a essa idade?\nNossa, como cresceu."),
                ("I wish Guy could see you like this.", "Queria que o Guy pudesse ver você assim."));
        }

        private bool AuntMemories(NPCController npc) => StoryTalk(npc,
            "I lived in the west as a child, but these days I wouldn't live anywhere else. Monsbaya's true treasure is its people.",
            "Eu morava no oeste quando criança, mas hoje em dia não moraria em nenhum outro lugar. O verdadeiro tesouro de Monsbaya são as pessoas daqui.",
            AuntPortrait, () => StoryAsk(npc,
                "This town brings back so many good memories. I have photos of everyone. Would you like to see them?",
                "Essa cidade me traz tantas memórias boas, eu tenho fotos de todo mundo, Gostaria de ver?", AuntPortrait,
                (T("Of course", "Claro"), OpenAuntGallery),
                (T("Maybe later", "Talvez mais Tarde"), null)));

        private void OpenAuntGallery() => OpenTownCollection(TitlePage.Gallery);

        // TOWN.BIN 0x569006–0x5696B6.
        private bool RestaurantAmbientDialogue(NPCController npc) => npc.npcId switch
        {
            "coppe" => StoryTalk(npc, "Hi-ya {name}!\nMaking good food comes from the heart!",
                "Oi, {name}!\nFazer boa comida vem do coração!"),
            "dar" => TownDialogueSequence(npc, null, null,
                ("It’s good, it’s good, it’s great.This is a great place.", "É bom, é bom, é ótimo. Este lugar é ótimo."),
                ("With this place, we don’t need any other restaurant.", "Com este lugar, não precisamos de outro restaurante.")),
            "bet" => TownDialogueSequence(npc, null, null,
                ("The food here is the best in Monsbaiya. Well, it’s the only restaurant in Monsbaiya.",
                    "A comida daqui é a melhor de Monsbaiya. Bem, é o único restaurante de Monsbaiya."),
                ("But I don’t like this place.", "Mas não gosto deste lugar."),
                ("Everything on the menu is delicious, so there’s no risk in ordering anything.",
                    "Tudo no cardápio é delicioso, então não há risco em pedir nada."),
                ("As a gambler, the lack of risk is hard to take.", "Como apostador, é difícil aguentar a falta de risco.")),
            "restaurant_hunter" => StoryTalk(npc, "The food’s great and Patty’s got a great personality.\nThis is a superb store.",
                "A comida é ótima e a Patty é muito simpática.\nEste lugar é excelente."),
            _ => false
        };
    }
}
