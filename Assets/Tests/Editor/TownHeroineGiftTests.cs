using System.Linq;
using AzureDreamsReimagined;
using NUnit.Framework;

namespace AzureDreams.Reimagined.EditorTests
{
    public sealed class TownHeroineGiftTests
    {
        [Test]
        public void RetailCatalogHasEveryMatrixAndResponsePage()
        {
            Assert.That(TownHeroineGifts.Data.heroes.Length, Is.EqualTo(7));
            Assert.That(TownHeroineGifts.Data.responses.Length, Is.EqualTo(47));
            foreach (var hero in TownHeroineGifts.Data.heroes)
            {
                Assert.That(hero.matrix.Length, Is.EqualTo(16));
                foreach (uint address in hero.matrix)
                    Assert.That(TownHeroineGifts.Data.responses.Any(r => r.address == address && r.pages.Length > 0), Is.True);
            }
            foreach (var response in TownHeroineGifts.Data.responses)
                Assert.That(response.portuguese.Length, Is.EqualTo(response.pages.Length));
        }

        [Test]
        public void NicoGiftThresholdsAndRelationshipMatchRetail()
        {
            var nico = TownHeroineGifts.Find("nico");
            var cheap = TownHeroineGifts.Select(nico, 4, 0, 99, 0, _ => false);
            Assert.That(cheap.accepted && cheap.increment, Is.True);
            Assert.That(TownHeroineGifts.Select(nico, 4, 0, 100, 0, _ => false).accepted, Is.False);
            Assert.That(TownHeroineGifts.Select(nico, 4, 0, 100, 3, _ => false).accepted, Is.True);
            Assert.That(TownHeroineGifts.Select(nico, 4, 0, 5000, 3, _ => false).accepted, Is.False);
            Assert.That(TownHeroineGifts.Select(nico, 4, 0, 5000, 3, f => f == 5230).accepted, Is.True);
            Assert.That(TownHeroineGifts.Select(nico, 4, 0, 1, 3, f => f == 4602).accepted, Is.False);
        }

        [Test]
        public void QuestObjectsAndSpecialDumplingsKeepTheirOwnStories()
        {
            var fur = TownHeroineGifts.Find("fur");
            Assert.That(TownHeroineGifts.Select(fur, 13, 3, 0, 0, _ => false), Is.Null);
            Assert.That(TownHeroineGifts.Select(fur, 13, 3, 0, 0, f => f == 4811).address, Is.EqualTo(0x8006BC7Au));
            Assert.That(TownHeroineGifts.Select(fur, 13, 4, 0, 0, _ => false).accepted, Is.False);
            Assert.That(TownHeroineGifts.Select(fur, 13, 1, 0, 0, _ => false), Is.Null);
            Assert.That(TownHeroineGifts.Select(fur, 1, 8, 0, 0, _ => false), Is.Null);
            Assert.That(TownHeroineGifts.Find("pool_selfi").id, Is.EqualTo("selfi"));
            Assert.That(TownHeroineGifts.Find("master"), Is.Null);
        }
    }
}
