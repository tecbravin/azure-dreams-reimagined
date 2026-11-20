using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // English text decoded from TOWN.BIN 0x4B2059–0x4B2712 and
        // 0x4B4011–0x4B4907. Portuguese is a translation, controls are rebound.
        private bool ArcadeResidentDialogue(NPCController npc)
        {
            if (!InArcade || !npc || !npc.npcId.StartsWith("arcade_", System.StringComparison.Ordinal)) return false;
            switch (npc.npcId)
            {
                case "arcade_attendant":
                    if (_heldTownPot || _carriedItem != null)
                        return StoryTalk(npc, "Put down what you’re holding first.", "Coloque no chão o que está segurando primeiro.");
                    return StoryAsk(npc, ArcadeBatting
                            ? "Welcome to the entertainment center.\nDo you want to play at the batting center?"
                            : "Welcome to the amusement center.\nDo you want to play ”Hit the Mole”?",
                        ArcadeBatting ? "Bem-vindo ao centro de diversão.\nQuer jogar no centro de rebatidas?" : "Bem-vindo ao centro de diversão.\nQuer jogar Acerte a Toupeira?",
                        (T("Yes, let me try.", "Sim, quero tentar."), () => AskArcadeInstructions(npc)),
                        (T("No, not today.", "Hoje não."), () => StoryTalk(npc,
                            "We’ll see you again next time.", "Esperamos vê-lo na próxima vez.")));
                case "arcade_bet": return ArcadeBetDialogue(npc);
                case "arcade_boy":
                    return StoryTalk(npc, ArcadeBatting ? "{name}, this entertainment center is really fun, isn’t it?" : "{name}, this amusement center is really fun, isn’t it?",
                        "{name}, este centro de diversão é muito divertido, não acha?");
                case "arcade_girl": return StoryTalk(npc, "Eeeeek!\nWooow!", "Aaaah!\nUaaau!");
                case "arcade_visitor":
                    if (!ArcadeBatting) return StoryTalk(npc, "Those two seem to really enjoy this place!", "Esses dois parecem estar se divertindo muito aqui!");
                    return StoryAsk(npc, "Do you want to know the secret of Bet?", "Quer saber o segredo do Bet?",
                        (T("Yes.", "Sim."), () => StoryTalk(npc, "He practices for hours after you leave.", "Ele pratica por horas depois que você vai embora.")),
                        (T("No.", "Não."), () => StoryTalk(npc, "I’m not telling you.", "Não vou contar.")));
                default: return false;
            }
        }

        private void AskArcadeInstructions(NPCController npc)
        {
            StoryAsk(npc, "Would you like an explanation on the game?", "Quer uma explicação do jogo?",
                (T("I don’t need it.", "Não preciso."), () => ReadyArcadeGame(npc)),
                (T("Let me hear it.", "Quero ouvir."), () =>
                {
                    string attack = _attackAction?.GetBindingDisplayString() ?? "Attack";
                    string jump = _wait?.GetBindingDisplayString() ?? "Jump";
                    string run = _runAction?.GetBindingDisplayString() ?? "Run";
                    if (ArcadeBatting)
                        TownDialogueSequence(npc, null, () => ReadyArcadeGame(npc),
                            ($"Press up or down on Directional button to move.\nPress {attack} to hit.", $"Aperte cima ou baixo para se mover.\nAperte {attack} para rebater."),
                            ("Attack the balls of ink the Takopoos shoot.", "Rebata as bolas de tinta que os Takopoos lançam."),
                            ("By hitting it at the appropriate timing, you can return the balls.", "Ao acertar no momento certo, você pode devolver as bolas."));
                    else
                        TownDialogueSequence(npc, null, () => ReadyArcadeGame(npc),
                            ($"Press {jump} to jump, press {run} for dash, press Directional button to move, and press {attack} to attack.",
                                $"Aperte {jump} para pular, {run} para correr, as direções para se mover e {attack} para atacar."),
                            ("Hit the moles accurately!", "Acerte as toupeiras com precisão!"));
                }));
        }

        private void ReadyArcadeGame(NPCController npc) =>
            StoryTalk(npc, "Ready, set,\nGO!", "Preparar, apontar,\nJÁ!", BeginArcadeGame);

        private bool ArcadeBetDialogue(NPCController npc)
        {
            string key = ArcadeScoreKey;
            if (TownSaveVariables.GetBool(key + ".played"))
            {
                int score = TownSaveVariables.GetInt(key + ".last");
                if (TownSaveVariables.GetBool(key + ".new_record"))
                    return TownDialogueSequence(npc, null, null,
                        ("Wow, you’re really something.", "Uau, você é impressionante."),
                        (ArcadeBatting ? "You’re not some famous gamer by chance, are you?" : "You’re not some famous gambler by chance, are you?",
                            "Por acaso você é algum jogador famoso?"));
                if (score > 0)
                    return TownDialogueSequence(npc, null, null,
                        (ArcadeBatting ? $"{score} points!  Not bad at all!" : $"{score} moles!\nNot bad at all!",
                            ArcadeBatting ? $"{score} pontos! Nada mal!" : $"{score} toupeiras!\nNada mal!"),
                        (ArcadeBatting ? "By the way, have you ever made the Takopoo completely black?" : "But you’ve got promise.",
                            ArcadeBatting ? "A propósito, já deixou o Takopoo completamente preto?" : "Você tem potencial."));
                return TownDialogueSequence(npc, null, null,
                    ("Well, good try.", "Bem, boa tentativa."),
                    ("But you’ve got promise.", "Você tem potencial."));
            }
            return TownDialogueSequence(npc, null, null,
                ("Yo, bro.\nI’ve done this game enough to know it inside out.", "E aí, irmão.\nJá joguei tanto que conheço o jogo de trás para frente."),
                (ArcadeBatting ? "I’m pretty strong when it comes to betting." : "I’m pretty good when it comes to placing bets.", "Sou muito bom quando o assunto é apostar."),
                (ArcadeBatting ? "That’s because I’m a genuine gambler right down to the core." : "That’s because I’m a gambler right down to the core.",
                    "Isso porque sou um apostador até o último fio de cabelo."));
        }

        private bool TryArcadePassage()
        {
            if (!InArcade || !_player || _changingInterior || _storyChoices != null || _arcadePhase != ArcadePhase.Closed || _heldTownPot || _carriedItem != null) return false;
            Vector2 door = ArcadeBatting ? new(8.5f, 20.5f) : new(15.5f, 17.5f);
            var playerPoint = new Vector2(_player.transform.position.x, _player.transform.position.z);
            if (Vector2.Distance(playerPoint, door) > .65f) return false;
            StartCoroutine(LoadInterior(ArcadeBatting ? ArcadeMoleRoom : ArcadeBattingRoom));
            return true;
        }
    }
}
