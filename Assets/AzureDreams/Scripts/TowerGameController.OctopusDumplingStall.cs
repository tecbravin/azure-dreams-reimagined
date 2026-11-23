using AzureDreams.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    /// <summary>Shin's windmill-quest food stand and its single-item shop.</summary>
    public sealed partial class TowerGameController
    {
        private const string OctopusDumplingStandId = "octopus_dumpling_stall";
        private const byte OctopusDumplingId = 3;
        private const int OctopusDumplingPrice = 100;
        // The retail stall opens on the field northwest of Isaac after the windmill repair.
        private static readonly Vector2 OctopusDumplingStandPosition = new(37f, 55f);

        private bool _octopusDumplingStallOpen;
        private bool _octopusDumplingConfirm;
        private string _octopusDumplingFeedback;

        private bool OctopusDumplingStallAvailable => UsingRetailTown &&
            TownRetailScriptFlags.CreatePersistent().IsFlagSet(WindmillQuestCompleteFlag);

        private static bool IsOctopusDumplings(DungeonItemEntry item) =>
            item != null && item.Kind == QuestItemKind && item.Subtype == OctopusDumplingId;

        private DungeonItemEntry CreateOctopusDumplings() =>
            new(OctopusDumplingId, QuestItemKind, 0, 0);

        private void CreateOctopusDumplingStall()
        {
            if (!OctopusDumplingStallAvailable) return;

            var center = new Vector3(OctopusDumplingStandPosition.x, 0f, OctopusDumplingStandPosition.y);
            center.y = TownTerrainHeight(center);
            var root = new GameObject("Shin's Octopus Dumpling Stand");
            root.transform.SetParent(transform, true);
            root.transform.position = center;
            _generated.Add(root);

            GameObject Part(string label, Vector3 offset, Vector3 size, Material material)
            {
                var part = CreatePrimitive(PrimitiveType.Cube, label, center + offset, size, material, false);
                part.transform.SetParent(root.transform, true);
                return part;
            }

            // A compact open counter, deliberately built from the existing town materials
            // until the original stand mesh is isolated from the retail town package.
            Part("Octopus stall counter", new Vector3(0f, 0.34f, 0.12f), new Vector3(1.75f, 0.68f, 0.55f), _wallMaterial);
            Part("Octopus stall awning", new Vector3(0f, 1.62f, 0.05f), new Vector3(2.18f, 0.16f, 1.14f), _accentMaterial);
            Part("Octopus stall post left", new Vector3(-0.88f, 0.92f, 0.05f), new Vector3(0.12f, 1.46f, 0.12f), _wallMaterial);
            Part("Octopus stall post right", new Vector3(0.88f, 0.92f, 0.05f), new Vector3(0.12f, 1.46f, 0.12f), _wallMaterial);

            var icon = ItemDisplaySprite(CreateOctopusDumplings());
            if (icon)
            {
                var sign = new GameObject("Octopus dumpling sign");
                sign.transform.SetParent(root.transform, false);
                sign.transform.localPosition = new Vector3(0f, 1.14f, -0.33f);
                sign.transform.localScale = Vector3.one * 0.72f;
                var renderer = sign.AddComponent<SpriteRenderer>();
                renderer.sprite = icon;
                renderer.sortingOrder = 3;
                sign.AddComponent<CameraFacingSprite>();
            }

            var vendor=new GameObject("Shin - original animated vendor");
            vendor.transform.SetParent(root.transform,false);
            vendor.transform.localPosition=new Vector3(0f,0f,.68f);
            vendor.transform.rotation=Quaternion.LookRotation(Vector3.back);
            RetailResidentBanks.Apply(vendor,"shin");
            vendor.AddComponent<ActorLocomotionView>();
            vendor.AddComponent<ActorSpriteDepth>();

            var interaction = root.AddComponent<TownObject>();
            interaction.objectId = OctopusDumplingStandId;
            interaction.message = T("Shin's Octopus Dumplings: 100G.", "Bolinhos de polvo do Shin: 100G.");
            interaction.interactionRadius = 1.65f;
            interaction.persistInteraction = false;
            _townObjects.Add(interaction);
            _townBuildings.Add(new Rect(center.x - 1.08f, center.z - 0.62f, 2.16f, 1.24f));
        }

        private void OpenOctopusDumplingStall()
        {
            _townObjectDialogueOpen = false;
            _rowanOfferPending = false;
            _octopusDumplingStallOpen = true;
            _octopusDumplingConfirm = false;
            _octopusDumplingFeedback = null;
            if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownDialogue);
            PlayRetailSound(RetailSfx.Confirm);
        }

        private void CloseOctopusDumplingStall()
        {
            _octopusDumplingStallOpen = false;
            _octopusDumplingConfirm = false;
            _octopusDumplingFeedback = null;
            if (_townFlow) _townFlow.State.TrySet(TownGameState.State.TownIdle);
        }

        private bool HandleOctopusDumplingStallInput()
        {
            if (!_octopusDumplingStallOpen) return false;
            if (_area != Area.Town || !OctopusDumplingStallAvailable)
            {
                CloseOctopusDumplingStall();
                return false;
            }

            if (_cancel?.WasPressedThisFrame() == true)
            {
                PlayRetailSound(RetailSfx.Cancel);
                if (_octopusDumplingConfirm)
                {
                    _octopusDumplingConfirm = false;
                    _octopusDumplingFeedback = null;
                }
                else CloseOctopusDumplingStall();
            }
            else if (_interact?.WasPressedThisFrame() == true)
            {
                if (_octopusDumplingConfirm) BuyOctopusDumplings();
                else
                {
                    _octopusDumplingConfirm = true;
                    _octopusDumplingFeedback = null;
                    PlayRetailSound(RetailSfx.Confirm);
                }
            }
            return true;
        }

        private void BuyOctopusDumplings()
        {
            if (!_octopusDumplingStallOpen || !_octopusDumplingConfirm) return;
            if (_gold < OctopusDumplingPrice)
            {
                _octopusDumplingFeedback = T("You don't have enough gold.", "Você não tem ouro suficiente.");
                return;
            }
            if (!_townInventory.HasSpace())
            {
                _octopusDumplingFeedback = T("Your inventory is full.", "Seu inventário está cheio.");
                return;
            }

            _townInventory.AddDungeonItem(CreateOctopusDumplings());
            _gold -= OctopusDumplingPrice;
            PersistTownProgress();
            _octopusDumplingConfirm = false;
            _octopusDumplingFeedback = T("Thank you. Enjoy the dumplings!", "Obrigado. Aproveite os bolinhos!");
            PlayRetailSound(RetailSfx.Confirm);
            TownEventBus.Publish(TownEventBus.EventId.ShopPurchased, "octopus_dumplings");
        }

        private void DrawOctopusDumplingStall()
        {
            if (!_octopusDumplingStallOpen) return;
            DrawHudPanel(new Rect(85, 120, 470, 235));
            GUI.Label(new Rect(103, 136, 320, 24), T("Shin's Octopus Dumplings", "Bolinhos de polvo do Shin"));
            if (_octopusDumplingConfirm)
            {
                GUI.Label(new Rect(103, 180, 432, 46), _octopusDumplingFeedback ??
                    T("Buy Dumplings for 100G?", "Comprar bolinhos por 100G?"));
                if (GUI.Button(new Rect(103, 252, 215, 26), WithInteractHint("Confirm", "Confirmar"))) BuyOctopusDumplings();
                if (GUI.Button(new Rect(320, 252, 215, 26), WithCancelHint("Back", "Voltar")))
                {
                    _octopusDumplingConfirm = false;
                    _octopusDumplingFeedback = null;
                }
                return;
            }

            GUI.Label(new Rect(103, 180, 432, 42), _octopusDumplingFeedback ??
                T("Delicious Octopus Dumplings. They restore HP and MP in the Tower.",
                    "Deliciosos bolinhos de polvo. Eles recuperam PV e PM na Torre."));
            if (GUI.Button(new Rect(103, 244, 432, 26), T("Dumplings   100G", "Bolinhos de polvo   100G")))
            {
                _octopusDumplingConfirm = true;
                _octopusDumplingFeedback = null;
                PlayRetailSound(RetailSfx.Confirm);
            }
            if (GUI.Button(new Rect(416, 306, 119, 26), WithCancelHint("Back", "Voltar"))) CloseOctopusDumplingStall();
        }
    }
}
