using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string GuyPortrait = "Dialogue/Guy_variant0_frame0";
        private const string GuyStartledPortrait = "Dialogue/GuyStartledHD";
        private const string GuyKneelingPortrait = "Dialogue/GuyKneelingHD";

        private Texture2D GuyDialoguePortrait(string path)
        {
            var portrait = StreamPortrait(path);
            if (portrait) return portrait;
            return path switch
            {
                GuyStartledPortrait => StreamPortrait("Dialogue/Guy_variant1_frame0"),
                GuyKneelingPortrait => StreamPortrait("Dialogue/Guy_variant2_frame0"),
                _ => null
            };
        }

        // DUNGEON.BIN 0x135CFA3–0x135E31C. The first lines introduce
        // Beldo's recollection, and the last lines return to Koh in the arena.
        private static readonly (string Speaker, string English, string Portuguese, string Portrait)[] BeldoIntroductionPages =
        {
            ("Beldo", "Now I shall tell you everything, just as I promised.",
                "Agora vou contar tudo, como prometi.", null),
            ("Beldo", "Seven years ago, your father Guy and I met in the tower, and together we tried to reach the top floor.",
                "Há sete anos, conheci seu pai, Guy, na torre. Juntos, tentamos chegar ao último andar.", null),
            ("Beldo", "The top floor is where the ultimate egg can be found.",
                "É no último andar que se encontra o Ovo Supremo.", null),
            ("Guy", "Wow! This is the ultimate egg!", "Uau! Este é o Ovo Supremo!", GuyPortrait),
            ("Guy", "W...what’re you doing, Beldo!", "O... o que você está fazendo, Beldo?!", GuyStartledPortrait),
            ("Beldo", "I owe you for what you have done so far, Guy. I appreciate that. But, this belongs to me.",
                "Sou grato por tudo que você fez até aqui, Guy. Mas isto me pertence.", null),
            ("Beldo", "I am the one who is going use its power. I’m the proper bearer of the ultimate power.",
                "Sou eu quem vai usar seu poder. Sou o legítimo portador do poder supremo.", null),
            ("Guy", "B... Beldo... You are...", "B... Beldo... Você é...", GuyKneelingPortrait),
            ("Beldo", "I am the best and the only monster tamer in the world who can fuse with monsters.",
                "Sou o melhor domador do mundo e o único capaz de se fundir com monstros.", null),
            ("Beldo", "I shall take this egg. It will be part of me and the world will be mine.",
                "Vou tomar esse ovo. Ele será parte de mim e o mundo será meu.", null),
            ("Guy", "I won’t let you.", "Eu não vou deixar.", GuyKneelingPortrait),
            ("Beldo", "Wooooow...!", "Uooooou...!", null),
            ("Guy", "Ha Ha Ha.", "Ha, ha, ha.", GuyKneelingPortrait),
            ("Guy", "The Fire, The Water, The Wind. And the power of all nature!",
                "O Fogo, a Água, o Vento. E o poder de toda a natureza!", GuyKneelingPortrait),
            ("Guy", "Born between the fine line of humans and monsters, let this egg be sealed for the sake of my blood. Abra cadabra!",
                "Nascido na tênue fronteira entre humanos e monstros, que este ovo seja selado em nome do meu sangue. Abracadabra!",
                GuyKneelingPortrait),
            ("Beldo", "You Idiot!", "Seu idiota!", null),
            ("Beldo", "You will regret your stupidity! Although... Almost.. But you have died and I have survived.",
                "Vai se arrepender da sua estupidez! Embora... Quase... Mas você morreu e eu sobrevivi.", null),
            ("Beldo", "Haaaa!", "Haaaa!", null),
            ("Beldo", "Huh...", "Hã...", null),
            ("Beldo", "The netherworld... Sealed by the blood of a monster tamer...",
                "O mundo inferior... Selado pelo sangue de um domador de monstros...", null),
            ("Beldo", "Well, that’s alright. All I need is the blood of a monster tamer.",
                "Bem, tudo bem. Só preciso do sangue de um domador de monstros.", null),
            ("Beldo", "The blood of someone who is strong enough to reach the top floor or the blood of a monster tamer who is even stronger.",
                "O sangue de alguém forte o bastante para alcançar o último andar ou o de um domador ainda mais forte.", null),
            ("Beldo", "Do you... understand, Mr.{name}?", "Está... entendendo, Sr. {name}?", null),
            ("Beldo", "That’s right! The blood is yours.", "Isso mesmo! É o seu sangue.", null),
            ("Beldo", "Now I’ve got new blood to open the seal of the monster tamer’s blood.",
                "Agora tenho sangue novo para romper o selo do sangue do domador.", null),
            ("Beldo", "Three gods and souls. I command here on the blood of the monster tamer.",
                "Três deuses e almas. Eu vos ordeno, pelo sangue do domador de monstros.", null),
            ("Beldo", "Break this spell!", "Quebrem este feitiço!", null),
            ("Beldo", "You are going to be killed by the sword of your own father.",
                "Você vai morrer pela espada do seu próprio pai.", null)
        };
    }
}
