using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Original (retail) field sprites for town residents, recovered from TOWN.BIN by
    /// Tools/extract_retail_town_residents.py. Each bank is one native person slot
    /// (register_person 0x800C3050: slot, idle/walk tables, action tables) rendered with the
    /// CLUT offset its initializer stores in SpriteFrameState.clut. Atlases live in
    /// Resources/Town/Residents/Retail/&lt;bank&gt;.png + .json and are played by
    /// <see cref="RetailTownResidentSpriteView"/> with the original ticks.
    /// Excludes the seven heroines, Beldo, Koh and Kewne (they keep their own views).
    /// </summary>
    internal static class RetailResidentBanks
    {
        public const string Folder = "Town/Residents/Retail/";

        // Unity npc id -> native bank (F_act_* name; slot = F_act - 62 = dialogue speaker id).
        // Only identities confirmed by symbol name, scene speaker tables or visual match.
        // Gym Kims and casino residents use their scene-specific minigame banks instead.
        private static readonly Dictionary<string, string> Banks = new()
        {
            ["center_nada"] = "narder",
            ["center_hear"] = "kiku",
            ["center_jacof"] = "nouhuko",
            ["center_pop"] = "oyaji",
            ["center_uncle"] = "der",
            ["center_aunt"] = "obasan",
            ["center_aunt_2"] = "ran",
            ["library_keeper"] = "gigi",
            ["library_reader_woman"] = "narder",
            ["library_reader_man"] = "nouhuko",
            ["rhodes_butler"] = "slot80",
            ["shin"] = "sinn",
            ["nico"] = "niko",
            ["selfi"] = "selfy_equipped",
            ["patty"] = "paty",
            ["vivian"] = "vivian",
            ["mia"] = "mia",
            ["cherrl"] = "cheril",
            ["nadia"] = "nerdia",
            ["fur"] = "far",
            ["hal"] = "hal",
            ["hush"] = "hush",
            ["silver"] = "silver",
            ["race_host"] = "der",
            ["tonka"] = "tonka",
            ["barry"] = "bary",
            ["gym_barry"] = "bary",
            ["coppe"] = "koppe",
            ["bar_barten"] = "brten",
            ["okami"] = "okami",
            ["bet"] = "bet",
            ["race_bet"] = "bet",
            ["arcade_bet"] = "bet",
            ["dar"] = "der",
            ["construction_pen"] = "pen",
            ["wookie"] = "wata",
            // Gym (speaker table TOWN.BIN 0x47159C: speaker id = person slot).
            ["gym_woman"] = "narder",
            ["gym_developer"] = "ayasii",
            ["gym_legs"] = "oyaji",
            ["gym_hunter"] = "hunter1",
            // Monster shop keeper (table 0x601750: mamonoya + hunters 1-3).
            ["master"] = "mamonoya",
            ["monster_shop_hunter_1"] = "hunter1",
            ["monster_shop_hunter_2"] = "hunter2",
            ["monster_shop_hunter_3"] = "hunter3",
            ["bar_hunter_1"] = "hunter1",
            ["bar_hunter_2"] = "hunter2",
            ["bar_hunter_3"] = "hunter3",
            // Racetrack lobby (table 0x4D1104: bet, kicca, silver, dai, setumeiin, horses).
            ["race_clerk"] = "kicca",
            ["race_husband"] = "dai",
            // Temple priest: appears with Koh's family in the prologue tables (0x3F55E0).
            ["jorda"] = "yoda",
            // Isaac (windmill): TOWN.BIN 0x2ADC20 tests Ftt_fkoya_isaku_scene00 flags for speaker 30.
            ["isaac"] = "ethrk",
            // Shiela (fortune teller): only speaker of her house overlay (0x62A408, slot 79,
            // beyond the F_act_* list); her text lives at 0x62C028.
            ["shiela"] = "slot79",
            // Chero, the travelling cellist (F_act_serohiki = "cello player", green outfit).
            ["chero"] = "serohiki",
            // Restored pool (TOWN.BIN overlay 0x51E000; swimsuit bodies from package 0x8B8000).
            ["pool_ghosh"] = "pool_gosh",
            ["pool_nico"] = "pool_niko",
            ["pool_selfi"] = "pool_selfy",
            ["pool_patty"] = "pool_paty",
            ["pool_fur"] = "pool_far",
            ["pool_cherrl"] = "pool_cheril",
            ["pool_swimmer_a"] = "pool_slot83",
            ["pool_swimmer_b"] = "pool_slot84",
            ["pool_girl"] = "pool_kiku",
            ["pool_kid"] = "pool_miru",
            ["pool_grandpa"] = "pool_gigi",
            ["pool_woman"] = "narder",
            ["pool_hunter"] = "pool_hunter1",
            ["pool_aunt"] = "pool_obasan",
            ["pool_man"] = "pool_oyaji",
            ["pool_grandma"] = "pool_baba",
            ["pool_barry"] = "pool_bary",
            ["pool_kim_big"] = "pool_kimani",
            ["pool_kim_little"] = "pool_kimotouto",
            ["pool_heckler_big"] = "pool_aniki",
            ["pool_heckler_little"] = "pool_otouto",
            ["nico_dad"] = "nikotiti",
            ["grandpa"] = "gigi",
            ["aunt"] = "obasan",
            ["guy"] = "gy",
            ["wreath"] = "rees",
            ["weedy"] = "wedy",
            ["cat"] = "neko",
        };

        internal static string BankFor(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return null;
            if (Banks.TryGetValue(npcId, out var bank)) return bank;
            // Story variants of Fon and Ghosh ("construction_fon", "vivian_walk_ghosh", ...).
            if (npcId == "fon" || npcId.EndsWith("_fon")) return "fon";
            if (npcId == "ghosh" || npcId.StartsWith("ghosh_") || npcId.EndsWith("_ghosh") ||
                npcId.Contains("_ghosh_")) return "gosh";
            return null;
        }

        /// <summary>Replaces any reconstructed/captured body with the retail field sprite.</summary>
        internal static bool Apply(GameObject instance, string npcId)
        {
            if (!instance) return false;
            string bank = BankFor(npcId);
            if (npcId == "cherrl" && TownSaveVariables.GetInt("town.story.cherrl.stage") == 0)
                bank = TownSaveVariables.GetBool("town.story.cherrl.toyThrown") &&
                    !TownSaveVariables.GetBool("town.story.cherrl.toyReturned") ? "cheril_window" : "cheril_bed";
            if (bank == null) return false;
            return ApplyBank(instance, bank);
        }

        internal static bool ApplyBank(GameObject instance, string bank)
        {
            if (!instance || string.IsNullOrEmpty(bank)) return false;
            var atlas = AssetMode.Load<Texture2D>(Folder + bank);
            var data = AssetMode.Load<TextAsset>(Folder + bank);
            if (!atlas || !data) return false;
            var retail = instance.GetComponent<RetailTownResidentSpriteView>();
            if (retail && retail.UsesAtlas(atlas)) return true;
            atlas.filterMode = FilterMode.Point;

            // Remove every competing presentation before the retail view creates its child.
            foreach(var view in instance.GetComponentsInChildren<MonsterTurnSpriteView>(true)) view.enabled=false;
            foreach (var view in instance.GetComponentsInChildren<NicoSpriteView>(true)) { view.enabled=false; Object.Destroy(view); }
            foreach (var view in instance.GetComponentsInChildren<OriginalTownSpriteView>(true)) Object.Destroy(view);
            foreach (var view in instance.GetComponentsInChildren<RacetrackResidentSpriteView>(true)) Object.Destroy(view);
            foreach (var view in instance.GetComponentsInChildren<SilverSpriteView>(true)) Object.Destroy(view);
            foreach (var view in instance.GetComponentsInChildren<TownCharacterSpriteView>(true)) Object.Destroy(view);
            foreach (var view in instance.GetComponentsInChildren<TownHumanoidView>(true)) Object.Destroy(view);
            foreach (var view in instance.GetComponentsInChildren<WeedyIdleAnimation>(true)) Object.Destroy(view);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;

            if (!retail) retail = instance.AddComponent<RetailTownResidentSpriteView>();
            retail.Configure(atlas, data);
            if (instance.TryGetComponent<ActorLocomotionView>(out var locomotion))
                locomotion.UseRetailTownSprite(retail);
            return true;
        }
    }
}
