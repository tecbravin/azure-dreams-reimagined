using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private enum KewneGateQuestion { Reason, Alone, Join, JoinAgain, HeroName, Tutorial }
        private KewneGateQuestion _kewneGateQuestion;
        private (string En,string Pt)[] _kewneGateLines;
        private Action _kewneGateNext;
        private string _kewneNameDraft="Kewne";
        private bool KewneMeetingHasChoices => _kewneMeeting is KewneMeeting.Question or KewneMeeting.Naming;

        private void BeginKewneGateDialogue()
        {
            _kewneNameDraft="Kewne";
            KewneGateLines(AskKewneReason,
                ("Are you headed to the tower to tame monsters?","Vai à torre para domar monstros?"));
        }
        private void KewneGateLines(Action next,params (string En,string Pt)[] lines)
        {
            _kewneGateLines=lines;_kewneGateNext=next;_kewneIntroPage=0;
            _kewneMeeting=KewneMeeting.Introduction;
            SetKewneMeetingLine(T(lines[0].En,lines[0].Pt));
        }
        private void KewneAsk(KewneGateQuestion question,string english,string portuguese)
        {
            _kewneGateQuestion=question;_kewneMeetingSelection=0;_kewneMeeting=KewneMeeting.Question;
            SetKewneMeetingLine(T(english,portuguese));
        }
        private void AskKewneReason()=>KewneAsk(KewneGateQuestion.Reason,
            "Easy there. I came to talk, not to fight.","Ei, ei, não se assuste. Não sou seu inimigo.");
        private void AskKewneAlone()=>KewneAsk(KewneGateQuestion.Alone,
            "Wait. Is anyone going into the tower with you?","Humm......\nVai entrar sozinho?");
        private void TellKewneStory()=>KewneGateLines(AskKewneJoin,
            ("You can't take this by yourself right now.\nYou have a magic collar, don't you? I can smell it.","Você ainda não pode encarar isso sozinho.\nTem um colar mágico, não tem? Consigo sentir o cheiro."),
            ("Believe or not, I once used to be a familiar for a great monster tamer.","Acredite se quiser, já fui o familiar de um grande domador de monstros."),
            ("But my master passed away. And now I'm a lonely familiar.","Mas meu mestre faleceu. E agora sou um familiar solitário."),
            ("And people want to have nothing to do with me, since I know the human language.","E as pessoas não querem saber de mim, porque conheço a língua dos humanos."),
            ("But I can't lower myself to become a monster now, they just live by instinct.","Mas não consigo voltar a ser um monstro agora. Eles vivem só por instinto."));
        private void AskKewneJoin()=>KewneAsk(KewneGateQuestion.Join,
            "How about it? Will you take me with you?","Pode colocar seu colar em mim, se quiser.");
        private void AskKewneJoinAgain()=>KewneAsk(KewneGateQuestion.JoinAgain,
            "What do you say? That collar could make us partners.","Vamos, coloque o colar mágico em mim agora.");
        private void BeginKewneNaming()
        {
            _kewneMeeting=KewneMeeting.Naming;
            SetKewneMeetingLine(T("Then we're partners! What should I answer to?",
                "Ótimo! De agora em diante, você é meu mestre. Me dê um nome."));
        }
        private void ConfirmKewneName()
        {
            _kewneNameDraft=string.IsNullOrWhiteSpace(_kewneNameDraft)?"Kewne":_kewneNameDraft.Trim();
            KewneGateLines(AskKewneHeroName,
                ($"Hmm... {_kewneNameDraft}.\nI could get used to that.",$"Hmm... {_kewneNameDraft}.\nAcho que vou me acostumar com esse nome."));
        }
        private void AskKewneHeroName()=>KewneAsk(KewneGateQuestion.HeroName,"And what should I call you?","E como eu devo chamar você?");
        private (string First,string Second) KewneGateAnswers()=>_kewneGateQuestion switch
        {
            KewneGateQuestion.Reason=>(T("Why did you stop me?","O que você quer?"),T("How can you talk?","Como você consegue falar?")),
            KewneGateQuestion.Join=>(T("Alright, You can join me.","Está bem, pode vir comigo."),T("No, you look mean.","Não, você parece malvado.")),
            KewneGateQuestion.JoinAgain=>(T("OK. I'll let you join me.","Está bem. Vou deixar você vir comigo."),T("I'm still not sure.","Ainda não tenho certeza.")),
            KewneGateQuestion.HeroName=>(T($"I'm {HeroName}.",$"Eu sou {HeroName}."),T($"I am Mr. {HeroName}.",$"Eu sou o senhor {HeroName}.")),
            _=>(T("Yes","Sim"),T("No","Não"))
        };
        private void AnswerKewneGateQuestion(int selection)
        {
            bool first=selection==0;
            switch(_kewneGateQuestion)
            {
                case KewneGateQuestion.Reason:
                    if(first)AskKewneAlone();else KewneGateLines(AskKewneAlone,
                        ("Is it because I speak the human language?\nI'm just smart enough to learn the human language.","É porque eu falo a língua dos humanos?\nSou esperto o bastante para aprender a língua dos humanos."));
                    break;
                case KewneGateQuestion.Alone:
                    if(first)TellKewneStory();else KewneGateLines(TellKewneStory,
                        ("Liar.","Mentiroso."),
                        ("Monster tamers use monsters to fight, but you don't even have a single familiar on your side.","Domadores usam monstros para lutar, mas você não tem nem um familiar ao seu lado."),
                        ("They're very important for fighting.","Eles são muito importantes para lutar."));
                    break;
                case KewneGateQuestion.Join:
                    if(first)BeginKewneNaming();else KewneGateLines(AskKewneJoinAgain,
                        ("I'm saying that I can be a familiar for you.","Estou dizendo que posso ser seu familiar."));
                    break;
                case KewneGateQuestion.JoinAgain:
                    if(first)BeginKewneNaming();else KewneGateLines(AskKewneJoinAgain,
                        ("Oh, please.\nPlease let me join you.","Ah, por favor.\nPor favor, deixe eu ir com você."),
                        ("C'mon, put the magic collar on me.","Vamos, coloque o colar mágico em mim."));
                    break;
                case KewneGateQuestion.HeroName:
                    KewneAsk(KewneGateQuestion.Tutorial,$"Yes, Master {HeroName}!\nYou are a beginner, aren't you?",$"Sim, mestre {HeroName}!\nVocê é iniciante, não é?");
                    break;
                case KewneGateQuestion.Tutorial:
                    _kewneTutorialChoice=first;_kewneMeeting=KewneMeeting.Reply;
                    SetKewneMeetingLine(first?T("OK, I will teach you many things about the tower. Let's go.","Certo, vou ensinar muita coisa sobre a torre. Vamos lá."):
                        T("I can see you're a champ! Let's go.","Já vi que você é fera! Vamos lá."));
                    break;
            }
        }
    }
}
