using System.IO;
using AzureDreamsReimagined;
using AzureDreamsReimagined.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AzureDreams.Reimagined.EditorTests
{
    public sealed class AssetModeTests
    {
        // Every music cue the game plays (TowerGameController.*): each needs an original and a redesigned file.
        private static readonly string[] MusicCues =
        {
            "AngelSong", "ArmShop", "BeldoDuel", "Casino", "CherrlTheme", "DanceAtPub", "EntranceOfComedians",
            "ExitComedians", "FurTheme", "GhoshTheme", "HeroFinale", "KohHouseTheme", "MiaTheme", "MonsterRaceCourse",
            "MonsterRacing", "MonsterShop", "NicoTheme", "PattyTheme", "Playroom", "SelfiTheme", "SwimmingPool",
            "Temple1", "Theatre", "Tower1", "Tower2", "Tower3", "Tower4", "Tower5", "Tower6", "Tower7", "Tower8",
            "TowerFinale", "Town", "VeiledGenuine", "VivianTheme"
        };

        private ArtStyle _previous;
        private bool _hadPreference;
        private int _savedPreference;

        [SetUp]
        public void SetUp()
        {
            _hadPreference = PlayerPrefs.HasKey(AssetMode.Preference);
            _savedPreference = PlayerPrefs.GetInt(AssetMode.Preference, 0);
            _previous = AssetMode.Current;
        }

        [TearDown]
        public void TearDown()
        {
            AssetMode.Select(_previous, save: false);
            if (_hadPreference) PlayerPrefs.SetInt(AssetMode.Preference, _savedPreference);
            else PlayerPrefs.DeleteKey(AssetMode.Preference);
            AssetMode.SetMediaOverlay(File.ReadAllText(ReimaginedMediaManifest.ManifestPath));
        }

        [Test]
        public void Select_RaisesChangedOnlyWhenStyleChanges()
        {
            AssetMode.Select(ArtStyle.Reimagined, save: false);
            int raised = 0;
            void Count() => raised++;
            AssetMode.Changed += Count;
            try
            {
                AssetMode.Select(ArtStyle.Reimagined, save: false);
                AssetMode.Select(ArtStyle.Classic, save: false);
                AssetMode.Toggle();
            }
            finally { AssetMode.Changed -= Count; }
            Assert.That(raised, Is.EqualTo(2));
            Assert.That(AssetMode.Current, Is.EqualTo(ArtStyle.Reimagined));
        }

        [Test]
        public void Select_PersistsChoice()
        {
            AssetMode.Select(ArtStyle.Classic);
            Assert.That(PlayerPrefs.GetInt(AssetMode.Preference), Is.EqualTo((int)ArtStyle.Classic));
        }

        [Test]
        public void ResolveMedia_UsesOverlayOnlyInReimagined()
        {
            AssetMode.SetMediaOverlay("Portraits/Dialogue/Nico.png\nPortraits/Dialogue/KohHD.png\t*\n");

            AssetMode.Select(ArtStyle.Reimagined, save: false);
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/Nico.png"), Is.EqualTo("Reimagined/Portraits/Dialogue/Nico.png"));
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/KohHD.png"), Is.EqualTo("Reimagined/Portraits/Dialogue/KohHD.png"));
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/Weedy.png"), Is.EqualTo("Portraits/Dialogue/Weedy.png"));

            AssetMode.Select(ArtStyle.Classic, save: false);
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/Nico.png"), Is.EqualTo("Portraits/Dialogue/Nico.png"));
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/KohHD.png"), Is.Null, "Classic must not show redesigned-only art.");
        }

        [Test]
        public void HeroinePortraits_ResolveAliasesAndKeepMissingBlinkInSameArtStyle()
        {
            AssetMode.SetMediaOverlay("Portraits/Dialogue/Nico_variant0_frame0.png\t*\nPortraits/Dialogue/NicoRetail_variant1_frame0.png\nPortraits/Dialogue/Nico_variant1_frame0.png\t*\n");
            AssetMode.Select(ArtStyle.Reimagined, save: false);
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/NicoRetail_variant0_frame0.png"), Is.EqualTo("Reimagined/Portraits/Dialogue/Nico_variant0_frame0.png"));
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/NicoRetail_variant0_frame2.png"), Is.EqualTo("Reimagined/Portraits/Dialogue/Nico_variant0_frame0.png"));
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/NicoRetail_variant1_frame0.png"), Is.EqualTo("Reimagined/Portraits/Dialogue/NicoRetail_variant1_frame0.png"));
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/PattyRetail_variant0_frame0.png"), Is.EqualTo("Portraits/Dialogue/PattyRetail_variant0_frame0.png"));
            AssetMode.Select(ArtStyle.Classic, save: false);
            Assert.That(AssetMode.ResolveMedia("Portraits/Dialogue/NicoRetail_variant0_frame2.png"), Is.EqualTo("Portraits/Dialogue/NicoRetail_variant0_frame2.png"));
        }

        [Test]
        public void Load_PrefersReimaginedOverlayAndFallsBackToOriginal()
        {
            AssetMode.Select(ArtStyle.Classic, save: false);
            var classic = AssetMode.Load<Texture2D>("Tower/Familiar_genus_fire");
            AssetMode.Select(ArtStyle.Reimagined, save: false);
            var reimagined = AssetMode.Load<Texture2D>("Tower/Familiar_genus_fire");
            var fallback = AssetMode.Load<Texture2D>("Tower/Familiar_hp");

            Assert.That(classic, Is.Not.Null);
            Assert.That(reimagined, Is.Not.Null);
            Assert.That(reimagined, Is.Not.SameAs(classic));
            Assert.That(fallback, Is.SameAs(Resources.Load<Texture2D>("Tower/Familiar_hp")));
        }

        [Test]
        public void MediaManifest_IsUpToDate()
        {
            string expected = ReimaginedMediaManifest.Build(ReimaginedMediaManifest.MediaRoot, ReimaginedMediaManifest.OverlayRoot);
            Assert.That(File.ReadAllText(ReimaginedMediaManifest.ManifestPath), Is.EqualTo(expected),
                "Run Azure Dreams > Rebuild Reimagined Media Manifest.");
        }

        [Test]
        public void LevelUpFanfare_HasOriginalAndRedesignedVersion()
        {
            Assert.That(File.Exists("Assets/AzureDreams/Resources/Music/LevelUp.ogg"), "Classic fanfare missing.");
            Assert.That(File.Exists("Assets/AzureDreams/Resources/Reimagined/Music/LevelUp.mp3"), "Reimagined fanfare missing.");
        }

        [Test]
        public void EveryMusicCue_HasOriginalAndRedesignedTrack()
        {
            foreach (string cue in MusicCues)
            {
                string original = Path.Combine(ReimaginedMediaManifest.MediaRoot, "Music", cue);
                string redesigned = Path.Combine(ReimaginedMediaManifest.OverlayRoot, "Music", cue);
                Assert.That(File.Exists(original + ".ogg"), $"Classic track missing (rendered from the disc by Tools/adsnd): {cue}");
                Assert.That(File.Exists(redesigned + ".ogg") || File.Exists(redesigned + ".mp3"), $"Reimagined track missing: {cue}");
            }
        }
    }
}
