using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// After the Nico route is complete (StoryComplete), Chero returns to the fountain and
    /// plays any of the game's songs on request. The chosen cue goes through PlayAreaMusic, so
    /// MusicFileName resolves the Classic (Media/Music) or Reimagined overlay version, and a
    /// mode switch restarts it via RestartAreaMusicForAssetMode. The town theme comes back when
    /// Koh walks away from the fountain.
    /// </summary>
    public sealed partial class TowerGameController
    {
        private const float CheroConcertRange = 9f;
        private string _cheroConcertCue;

        private static readonly (string Cue, string En, string Pt)[] CheroSongs =
        {
            ("Town", "Town Theme", "Tema da Cidade"),
            ("KohHouseTheme", "Koh's House", "Casa do Koh"),
            ("AngelSong", "Angel's Song", "Canção do Anjo"),
            ("Tower1", "Tower I", "Torre I"),
            ("Tower2", "Tower II", "Torre II"),
            ("Tower3", "Tower III", "Torre III"),
            ("Tower4", "Tower IV", "Torre IV"),
            ("Tower5", "Tower V", "Torre V"),
            ("Tower6", "Tower VI", "Torre VI"),
            ("Tower7", "Tower VII", "Torre VII"),
            ("Tower8", "Tower VIII", "Torre VIII"),
            ("TowerFinale", "Tower Finale", "Final da Torre"),
            ("VeiledGenuine", "Veiled Genuine", "Veiled Genuine"),
            ("BeldoDuel", "Duel with Beldo", "Duelo com Beldo"),
            ("HeroFinale", "Hero's Finale", "Final do Herói"),
            ("Temple1", "Temple", "Templo"),
            ("ArmShop", "Weapon Shop", "Loja de Armas"),
            ("MonsterShop", "Monster Shop", "Loja de Monstros"),
            ("Casino", "Casino", "Cassino"),
            ("DanceAtPub", "Dance at the Pub", "Dança no Bar"),
            ("Theatre", "Theater", "Teatro"),
            ("EntranceOfComedians", "Entrance of the Comedians", "Entrada dos Comediantes"),
            ("ExitComedians", "Exit of the Comedians", "Saída dos Comediantes"),
            ("Playroom", "Playroom", "Fliperama"),
            ("SwimmingPool", "Swimming Pool", "Piscina"),
            ("MonsterRacing", "Monster Racing", "Corrida de Monstros"),
            ("MonsterRaceCourse", "Race Course", "Pista de Corrida"),
            ("CherrlTheme", "Cherrl's Theme", "Tema da Cherrl"),
            ("NicoTheme", "Nico's Theme", "Tema da Nico"),
            ("SelfiTheme", "Selfi's Theme", "Tema da Selfi"),
            ("MiaTheme", "Mia's Theme", "Tema da Mia"),
            ("PattyTheme", "Patty's Theme", "Tema da Patty"),
            ("VivianTheme", "Vivian's Theme", "Tema da Vivian"),
            ("FurTheme", "Fur's Theme", "Tema da Fur"),
            ("GhoshTheme", "Ghosh's Theme", "Tema do Ghosh"),
        };

        private bool CheroConcertOffer(NPCController chero) =>
            StoryAsk(chero, "Hello again, {name}. Would you like to hear a song?",
                "Olá de novo, {name}. Quer ouvir uma música?",
                (T("Yes, please.", "Sim, por favor."), () => CheroSongPicker(chero)),
                (T("Not now.", "Agora não."), () => StoryTalk(chero, "Come back anytime. My cello is always ready.",
                    "Volte quando quiser. Meu violoncelo está sempre pronto.")));

        private void CheroSongPicker(NPCController chero)
        {
            var choices = new (string Label, System.Action Apply)[CheroSongs.Length + 1];
            for (int i = 0; i < CheroSongs.Length; i++)
            {
                var song = CheroSongs[i];
                choices[i] = (T(song.En, song.Pt), () => CheroPlaySong(chero, song.Cue, T(song.En, song.Pt)));
            }
            choices[CheroSongs.Length] = (T("Never mind.", "Deixa pra lá."), () => StoryTalk(chero,
                "Another time, then.", "Fica para outra vez, então."));
            StoryAsk(chero, "Which song shall I play?", "Qual música devo tocar?", choices);
        }

        private void CheroPlaySong(NPCController chero, string cue, string title)
        {
            _cheroConcertCue = cue;
            PlayAreaMusic(cue);
            StoryTalk(chero, $"♪ ... \"{title}\". I hope you enjoy it.",
                $"♪ ... \"{title}\". Espero que goste.");
        }

        /// <summary>Called every frame from UpdateOutdoorSelfiMusic.</summary>
        private void UpdateCheroConcertMusic()
        {
            if (_cheroConcertCue == null) return;
            if (_playingMusicCue != _cheroConcertCue) { _cheroConcertCue = null; return; }
            if (_area != Area.Town || _interior || !_player) return;
            var chero = FindStoryActor("chero");
            if (chero && Vector3.Distance(_player.transform.position, chero.transform.position) <= CheroConcertRange) return;
            _cheroConcertCue = null;
            PlayAreaMusic("Town");
        }
    }
}
