using System;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string RoostQuestKey = "town.roost.stage";
        private const string RoostFirstRewardDayKey = "town.roost.firstRewardDay";
        private const string RoostClaimedDayKey = "town.roost.claimedDay";
        private static readonly Vector2Int RoostFieldCell = new(37, 86);

        private void SpawnRoost()
        {
            if (!UsingRetailTown || !TownRetailScriptFlags.CreatePersistent().IsFlagSet(WindmillQuestCompleteFlag))
                return;

            var cell = RoostFieldCell;
            for (int radius = 0; radius <= 4; radius++)
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != radius) continue;
                cell = RoostFieldCell + new Vector2Int(x, z);
                if (StoryResidentCellOpen(cell))
                {
                    PlaceRoost(cell);
                    return;
                }
            }
        }

        private void PlaceRoost(Vector2Int cell)
        {
            var root = new GameObject("Roost");
            root.transform.SetParent(transform, false);
            root.transform.position = RetailTownActorPosition(CellPosition(cell), 0f);
            root.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            _generated.Add(root);
            var stats = root.AddComponent<CharacterStats>();
            stats.characterId = "roost";
            stats.displayName = "Roost";
            stats.portraitTexture = Resources.Load<Texture2D>("Town/RoostPortrait");
            var npc = root.AddComponent<NPCController>();
            npc.npcId = "roost";
            npc.ConfigureConversation(_townFlow.State);
            _townPeople.Add(npc);
            _npcCells.Add(cell);
            _townBuildings.Add(new Rect(cell.x - .38f, cell.y - .30f, .76f, .60f));

            var shirt = new Color32(154, 41, 33, 255);
            var stripe = new Color32(222, 103, 82, 255);
            var denim = new Color32(41, 72, 105, 255);
            var skin = new Color32(207, 139, 88, 255);
            var straw = new Color32(224, 181, 84, 255);
            var leather = new Color32(77, 46, 34, 255);
            var tint = new MaterialPropertyBlock();

            void Part(string name, PrimitiveType shape, Vector3 position, Vector3 size, Color color)
            {
                var part = GameObject.CreatePrimitive(shape);
                part.name = name;
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = position;
                part.transform.localScale = size;
                var collider = part.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                tint.SetColor("_Color", color);
                part.GetComponent<Renderer>().SetPropertyBlock(tint);
            }

            Part("Plaid shirt", PrimitiveType.Cube, new(0, .91f, 0), new(.84f, .72f, .48f), shirt);
            for (int i = -1; i <= 1; i++)
            {
                Part("Plaid horizontal", PrimitiveType.Cube, new(0, .96f + i * .15f, .244f), new(.84f, .044f, .012f), stripe);
                Part("Plaid vertical", PrimitiveType.Cube, new(i * .22f, .93f, .247f), new(.044f, .67f, .016f), stripe);
            }
            Part("Jeans bib", PrimitiveType.Cube, new(0, .74f, .266f), new(.55f, .35f, .08f), denim);
            foreach (int side in new[] { -1, 1 })
            {
                Part("Suspender", PrimitiveType.Cube, new(side * .24f, 1.06f, .27f), new(.10f, .40f, .055f), denim);
                Part("Buckle", PrimitiveType.Cube, new(side * .24f, .89f, .307f), new(.105f, .08f, .025f), straw);
                Part("Sleeve", PrimitiveType.Capsule, new(side * .48f, .99f, 0), new(.28f, .25f, .31f), shirt);
                Part("Hand", PrimitiveType.Sphere, new(side * .50f, .72f, .055f), new(.23f, .29f, .24f), skin);
                Part("Jeans leg", PrimitiveType.Cube, new(side * .20f, .33f, 0), new(.31f, .53f, .38f), denim);
                Part("Work boot", PrimitiveType.Cube, new(side * .20f, .09f, .09f), new(.34f, .18f, .51f), leather);
            }
            Part("Head", PrimitiveType.Sphere, new(0, 1.47f, .025f), new(.65f, .59f, .54f), skin);
            Part("Broad mouth", PrimitiveType.Sphere, new(0, 1.40f, .278f), new(.39f, .15f, .075f), leather);
            Part("Smile", PrimitiveType.Sphere, new(0, 1.43f, .312f), new(.27f, .05f, .025f), new Color32(245, 217, 165, 255));
            Part("Hat brim", PrimitiveType.Cylinder, new(0, 1.64f, .045f), new(1.03f, .055f, .88f), straw);
            Part("Hat band", PrimitiveType.Cylinder, new(0, 1.73f, 0), new(.65f, .06f, .61f), leather);
            Part("Hat crown", PrimitiveType.Cylinder, new(0, 1.83f, 0), new(.60f, .115f, .57f), straw);
        }

        private bool RoostDialogue(NPCController npc)
        {
            int stage = TownSaveVariables.GetInt(RoostQuestKey);
            if (stage == 0)
            {
                if (HasRoostFamiliar(MonsterSpecies.BLUME))
                    return OfferRoostFamiliar(npc, MonsterSpecies.BLUME);
                return StoryTalk(npc,
                    "Could you help me out? I could use a Blume to tend this herb field. Bring one along if you find one.",
                    "Pode me dar uma mão? Um Blume me ajudaria a cuidar deste campo de ervas. Se encontrar um, traga-o aqui.");
            }

            if (stage == 1 && HasRoostFamiliar(MonsterSpecies.SNOWMAN))
                return OfferRoostFamiliar(npc, MonsterSpecies.SNOWMAN);
            if (StoryDay < TownSaveVariables.GetInt(RoostFirstRewardDayKey) ||
                TownSaveVariables.GetInt(RoostClaimedDayKey, -1) == StoryDay)
                return StoryTalk(npc, "Come see me after your next trip to the Tower.",
                    "Passe aqui quando voltar da próxima viagem à Torre.");

            var random = new System.Random(unchecked(StoryDay * 7919 + 731));
            byte first = (byte)random.Next(1, 8);
            if (stage == 1)
                return StoryTalk(npc, "I've got an herb for you from today's harvest.",
                    "Separei uma erva da colheita de hoje para você.", () => GiveRoostHerb(npc, first));

            byte second = (byte)(((first - 1 + random.Next(1, 7)) % 7) + 1);
            byte third;
            do { third = (byte)random.Next(1, 8); } while (third == first || third == second);
            return StoryAsk(npc, "Pick one herb from today's harvest.", "Escolha uma erva da colheita de hoje.",
                RoostHerbChoice(npc, first), RoostHerbChoice(npc, second), RoostHerbChoice(npc, third),
                (T("Later", "Depois"), null));
        }

        private bool HasRoostFamiliar(MonsterSpecies species) =>
            (_familiarDefinition && _familiarActor.Species == (byte)species && _parkedFamiliarActor == null) ||
            _bagFamiliars.Exists(entry => !entry.Temporary && entry.Actor.Species == (byte)species);

        private bool OfferRoostFamiliar(NPCController npc, MonsterSpecies species)
        {
            bool blume = species == MonsterSpecies.BLUME;
            return StoryAsk(npc,
                blume ? "Is that a Blume? Would you leave it here to help me grow herbs?" :
                    "A Snowman could keep my herbs cool. Would you let it stay here?",
                blume ? "Esse é um Blume? Você o deixaria aqui para me ajudar a cultivar ervas?" :
                    "Um Snowman poderia manter minhas ervas frescas. Você o deixaria ficar aqui?",
                (blume ? T("Give Blume", "Dar Blume") : T("Give Snowman", "Dar Snowman"),
                    () => CompleteRoostTrade(npc, species)),
                (T("Keep my familiar", "Ficar com meu familiar"), () =>
                {
                    if (!blume) ShowRoostExistingReward(npc);
                }));
        }

        private void ShowRoostExistingReward(NPCController npc)
        {
            if (StoryDay < TownSaveVariables.GetInt(RoostFirstRewardDayKey) ||
                TownSaveVariables.GetInt(RoostClaimedDayKey, -1) == StoryDay) return;
            byte herb = (byte)new System.Random(unchecked(StoryDay * 7919 + 731)).Next(1, 8);
            StoryTalk(npc, "I've got an herb for you from today's harvest.",
                "Separei uma erva da colheita de hoje para você.", () => GiveRoostHerb(npc, herb));
        }

        private void CompleteRoostTrade(NPCController npc, MonsterSpecies species)
        {
            bool blume = species == MonsterSpecies.BLUME;
            if (TownSaveVariables.GetInt(RoostQuestKey) != (blume ? 0 : 1) || !HasRoostFamiliar(species)) return;
            var reserve = _bagFamiliars.Find(entry => !entry.Temporary && entry.Actor.Species == (byte)species);
            if (reserve != null)
            {
                _bagFamiliars.Remove(reserve);
                if (reserve == _blueCollarFamiliar) _blueCollarFamiliar = null;
            }
            else
            {
                PutFamiliarInBag();
                _familiarDefinition = null;
                _hatchedSpecies = 0;
                _familiarActor = new ActorRecord();
                _familiarHealth = 0;
                _familiarInBag = _familiarDefeated = _familiarWaiting = false;
                TownSaveVariables.SetString(FamiliarNicknameKey, "");
                if (_bagFamiliars.Count > 0) ActivateBagFamiliar(_bagFamiliars[0]);
            }
            TownSaveVariables.SetInt(RoostQuestKey, blume ? 1 : 2);
            if (blume) TownSaveVariables.SetInt(RoostFirstRewardDayKey, StoryDay + 1);
            RefreshInventoryCommands();
            PersistTownProgress();
            StoryTalk(npc, blume ? "Thanks! After your next Tower trip, I'll share my harvest with you." :
                    "Thank you! With Snowman's help, I'll have three herbs for you to choose from.",
                blume ? "Obrigado! Depois da sua próxima viagem à Torre, vou dividir a colheita com você." :
                    "Obrigado! Com a ajuda do Snowman, vou ter três ervas para você escolher.");
        }

        private (string Label, Action Apply) RoostHerbChoice(NPCController npc, byte subtype) =>
            (InventoryItemName(RoostHerb(subtype)), () => GiveRoostHerb(npc, subtype));

        private static DungeonItemEntry RoostHerb(byte subtype) =>
            ItemSystem.CreateFloorItem(subtype, ItemSystem.Kind.Herb, 0);

        private void GiveRoostHerb(NPCController npc, byte subtype)
        {
            if (TownSaveVariables.GetInt(RoostClaimedDayKey, -1) == StoryDay) return;
            if (_townInventory.TotalItems + FamiliarInventoryCount >= Inventory.SlotCapacity)
            {
                StoryTalk(npc, "Make some room in your bag first.", "Abra um espaço na sua bolsa primeiro.");
                return;
            }
            var herb = RoostHerb(subtype);
            _townInventory.AddDungeonItem(herb);
            TownSaveVariables.SetInt(RoostClaimedDayKey, StoryDay);
            PersistTownProgress();
            string name = InventoryItemName(herb);
            StoryTalk(npc, $"Here you go: {name}.", $"Aqui está: {name}.");
        }
    }
}
