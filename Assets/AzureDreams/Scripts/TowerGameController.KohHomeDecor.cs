using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Pontos reais de piso livre, obtidos por engenharia reversa da malha 3D
        // capturada de cada casa (Town/Interiors/Interior_0N_koh_house_N/geometry.json):
        // para cada celula da grade foi medida a altura maxima dos vertices e mantidas
        // apenas as celulas planas (altura ~0), com vizinhanca tambem livre, fora da
        // cama, do box do banho (BathPoints/BathDoors) e das portas (BedroomDoors,
        // HutLinks). Nivel 1 e espelhado por HomeLayoutPoint, mantendo o layout
        // alinhado a geometria capturada (mesmo referencial de BathPoints/Beds/etc.).
        private static readonly Vector3[][] HomeDecorationSlots =
        {
            // Casa 1: 8 pontos de piso livre, extraidos da malha real de
            // Interior_01_koh_house_1/geometry.json (celulas com altura ~0,
            // vizinhanca livre, fora da cama, do box do banho e das portas).
            new[]
            {
                new Vector3(13f, 0f, 11f), new Vector3(18f, 0f, 11f), new Vector3(18f, 0f, 12f),
                new Vector3(18f, 0f, 13f), new Vector3(19f, 0f, 13f), new Vector3(18f, 0f, 14f),
                new Vector3(19f, 0f, 14f), new Vector3(20f, 0f, 14f)
            },
            // Casa 2: 19 pontos extraidos de Interior_02_koh_house_2/geometry.json.
            new[]
            {
                new Vector3(12f, 0f, 12f), new Vector3(17f, 0f, 12f), new Vector3(12f, 0f, 13f),
                new Vector3(17f, 0f, 13f), new Vector3(14f, 0f, 14f), new Vector3(19f, 0f, 14f),
                new Vector3(17f, 0f, 15f), new Vector3(15f, 0f, 16f), new Vector3(26f, 0f, 16f),
                new Vector3(32f, 0f, 16f), new Vector3(14f, 0f, 17f), new Vector3(26f, 0f, 17f),
                new Vector3(31f, 0f, 17f), new Vector3(14f, 0f, 18f), new Vector3(26f, 0f, 18f),
                new Vector3(31f, 0f, 18f), new Vector3(14f, 0f, 19f), new Vector3(19f, 0f, 19f),
                new Vector3(29f, 0f, 19f)
            },
            // Casa 3: 30 pontos extraidos de Interior_03_koh_house_3/geometry.json.
            new[]
            {
                new Vector3(12f, 0f, 12f), new Vector3(27f, 0f, 12f), new Vector3(61f, 0f, 12f),
                new Vector3(15f, 0f, 13f), new Vector3(60f, 0f, 13f), new Vector3(14f, 0f, 14f),
                new Vector3(46f, 0f, 14f), new Vector3(67f, 0f, 14f), new Vector3(27f, 0f, 15f),
                new Vector3(45f, 0f, 15f), new Vector3(60f, 0f, 15f), new Vector3(12f, 0f, 16f),
                new Vector3(26f, 0f, 16f), new Vector3(40f, 0f, 16f), new Vector3(48f, 0f, 16f),
                new Vector3(62f, 0f, 16f), new Vector3(14f, 0f, 17f), new Vector3(28f, 0f, 17f),
                new Vector3(42f, 0f, 17f), new Vector3(50f, 0f, 17f), new Vector3(64f, 0f, 17f),
                new Vector3(16f, 0f, 18f), new Vector3(30f, 0f, 18f), new Vector3(44f, 0f, 18f),
                new Vector3(62f, 0f, 18f), new Vector3(13f, 0f, 19f), new Vector3(25f, 0f, 19f),
                new Vector3(33f, 0f, 19f), new Vector3(45f, 0f, 19f), new Vector3(61f, 0f, 19f)
            }
        };

        private void ApplyPurchasedHomeItems()
        {
            int house = HouseIndex;
            if (house < 0 || !_interior) return;

            _interior.ApplyHomeWallpaper(TownSaveVariables.GetString(HomeWallpaperKey));
            int slot = 0;
            var positions = HomeDecorationSlots[Mathf.Clamp(house, 0, HomeDecorationSlots.Length - 1)];
            for (int itemIndex = 0; itemIndex < GeneralStock.Length && slot < positions.Length; itemIndex++)
            {
                var item = GeneralStock[itemIndex];
                if (item.Id.StartsWith("wallpaper_", System.StringComparison.Ordinal)) continue;
                int owned = GeneralStoreOwned(item.Id);
                int copies = HomeDecorationCopies(item.Id, owned);
                for (int copy = 0; copy < copies && slot < positions.Length; copy++, slot++)
                {
                    Vector3 point = HomeLayoutPoint(house, positions[slot]);
                    point.y = RetailTownHeight(point);
                    CreateHomeDecoration(item.Id, item.English, point, copy);
                }
            }
        }

        private static int HomeDecorationCopies(string itemId, int owned)
        {
            if (itemId is "music_box" or "aura_bike" or "booster") return 0;
            if (owned <= 0) return 0;
            // The original shop permits multiples for furnishings, while special
            // purchases are single installations.  Limit visible duplicates so a
            // save with a very large count cannot fill the whole home in one load.
            return itemId is "bed" or "canopy" or "tiger_fur" or "stuffed_deer" or "painting" or "scrolls" or
                "poster" or "sculpture" or "something" or "tv" or "fridge" or "washer" or "range" or "light" or
                "telephone" or "trophy" or "vest" or "jacket" or "trousers" or "shoes" or "aura_bike" or "booster" or
                "music_box" ? 1 : Mathf.Min(owned, 3);
        }

        private GameObject CreateHomeDecoration(string itemId, string displayName, Vector3 position, int copy)
        {
            var originalPrefab = AssetMode.Load<GameObject>("Town/Home/" + itemId);
            if (originalPrefab)
            {
                var original = Instantiate(originalPrefab, position, Quaternion.identity, transform);
                original.name = "Koh home purchase — " + displayName;
                _generated.Add(original);
                return original;
            }

            var root = new GameObject("Koh home purchase — " + displayName);
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            _generated.Add(root);

            Color wood = new(0.42f, 0.22f, 0.10f, 1f);
            Color cloth = new(0.75f, 0.22f + 0.08f * copy, 0.20f, 1f);
            Color brass = new(0.92f, 0.69f, 0.23f, 1f);
            Color porcelain = new(0.70f, 0.82f, 0.93f, 1f);
            switch (itemId)
            {
                case "bed": CreateBed(root, wood, cloth); break;
                case "canopy": CreateCanopy(root, wood, cloth); break;
                case "table": CreateTable(root, wood); break;
                case "bonsai": CreatePlant(root, wood, new Color(0.18f, 0.45f, 0.16f, 1f), true); break;
                case "plant": CreatePlant(root, porcelain, new Color(0.14f, 0.55f, 0.20f, 1f), false); break;
                case "carpet_1": CreateCarpet(root, new Color(0.64f, 0.23f, 0.18f, 1f)); break;
                case "carpet_2": CreateCarpet(root, new Color(0.16f, 0.35f, 0.72f, 1f)); break;
                case "carpet_3": CreateCarpet(root, new Color(0.56f, 0.31f, 0.68f, 1f)); break;
                case "tiger_fur": CreateCarpet(root, new Color(0.92f, 0.49f, 0.12f, 1f), true); break;
                case "stuffed_deer": CreateDeer(root, wood); break;
                case "painting": CreateWallPiece(root, new Color(0.84f, 0.62f, 0.21f, 1f), new Color(0.18f, 0.42f, 0.68f, 1f)); break;
                case "scrolls": CreateScrolls(root, parchment: new Color(0.92f, 0.80f, 0.56f, 1f)); break;
                case "poster": CreateWallPiece(root, new Color(0.12f, 0.12f, 0.17f, 1f), new Color(0.74f, 0.12f, 0.18f, 1f)); break;
                case "sculpture": CreateSculpture(root, brass); break;
                case "something": CreateMysteryObject(root, cloth, brass); break;
                case "tv": CreateAppliance(root, new Color(0.12f, 0.13f, 0.16f, 1f), new Color(0.22f, 0.52f, 0.75f, 1f)); break;
                case "fridge": CreateAppliance(root, new Color(0.74f, 0.79f, 0.82f, 1f), porcelain, tall: true); break;
                case "washer": CreateAppliance(root, new Color(0.72f, 0.75f, 0.82f, 1f), new Color(0.15f, 0.28f, 0.38f, 1f)); break;
                case "range": CreateRange(root, new Color(0.24f, 0.25f, 0.27f, 1f), brass); break;
                case "light": CreateLight(root, brass, new Color(1f, 0.91f, 0.53f, 1f)); break;
                case "telephone": CreateTelephone(root, new Color(0.22f, 0.18f, 0.15f, 1f), brass); break;
                case "trophy": CreateTrophy(root, brass); break;
                case "vest": CreateClothing(root, new Color(0.18f, 0.42f, 0.65f, 1f)); break;
                case "jacket": CreateClothing(root, new Color(0.40f, 0.24f, 0.17f, 1f)); break;
                case "trousers": CreateClothing(root, new Color(0.15f, 0.22f, 0.42f, 1f)); break;
                case "shoes": CreateShoes(root, new Color(0.26f, 0.13f, 0.08f, 1f)); break;
                case "aura_bike": CreateAuraBike(root, brass, new Color(0.21f, 0.52f, 0.86f, 1f)); break;
                case "booster": CreateBooster(root, new Color(0.30f, 0.33f, 0.38f, 1f), brass); break;
                case "music_box": CreateMusicBox(root, wood, brass); break;
                default: CreateMysteryObject(root, cloth, brass); break;
            }
            return root;
        }

        private GameObject HomePart(GameObject root, PrimitiveType shape, string label, Vector3 localPosition,
            Vector3 scale, Color tint, Quaternion? rotation = null)
        {
            var part = CreatePrimitive(shape, root.name + " — " + label,
                root.transform.TransformPoint(localPosition), scale, null, false);
            part.transform.SetParent(root.transform, true);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = rotation ?? Quaternion.identity;
            if (part.TryGetComponent<Renderer>(out var renderer))
            {
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_Color", tint);
                properties.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(properties);
            }
            return part;
        }

        private void CreateBed(GameObject root, Color wood, Color cloth)
        {
            HomePart(root, PrimitiveType.Cube, "bed frame", new Vector3(0f, 0.17f, 0f), new Vector3(1.35f, 0.28f, 1.75f), wood);
            HomePart(root, PrimitiveType.Cube, "bed cover", new Vector3(0f, 0.34f, -0.08f), new Vector3(1.22f, 0.16f, 1.45f), cloth);
            HomePart(root, PrimitiveType.Cube, "headboard", new Vector3(0f, 0.62f, 0.72f), new Vector3(1.35f, 0.74f, 0.12f), wood);
        }

        private void CreateCanopy(GameObject root, Color wood, Color cloth)
        {
            CreateBed(root, wood, cloth);
            foreach (float x in new[] { -0.57f, 0.57f })
            foreach (float z in new[] { -0.72f, 0.72f })
                HomePart(root, PrimitiveType.Cylinder, "canopy post", new Vector3(x, 0.93f, z), new Vector3(0.08f, 1.35f, 0.08f), wood);
            HomePart(root, PrimitiveType.Cube, "canopy", new Vector3(0f, 1.78f, 0f), new Vector3(1.35f, 0.07f, 1.75f), cloth);
        }

        private void CreateTable(GameObject root, Color wood)
        {
            HomePart(root, PrimitiveType.Cylinder, "tabletop", new Vector3(0f, 0.62f, 0f), new Vector3(1.2f, 0.13f, 1.2f), wood);
            HomePart(root, PrimitiveType.Cylinder, "table leg", new Vector3(0f, 0.30f, 0f), new Vector3(0.22f, 0.62f, 0.22f), wood);
        }

        private void CreatePlant(GameObject root, Color pot, Color leaves, bool compact)
        {
            HomePart(root, PrimitiveType.Cylinder, "pot", new Vector3(0f, 0.18f, 0f), new Vector3(0.38f, 0.36f, 0.38f), pot);
            float height = compact ? 0.50f : 0.82f;
            HomePart(root, PrimitiveType.Sphere, "leaves", new Vector3(0f, 0.35f + height * 0.5f, 0f),
                compact ? new Vector3(0.78f, height, 0.62f) : new Vector3(0.95f, height, 0.80f), leaves);
        }

        private void CreateCarpet(GameObject root, Color color, bool fur = false)
        {
            HomePart(root, PrimitiveType.Cube, fur ? "tiger fur" : "carpet", new Vector3(0f, 0.02f, 0f),
                fur ? new Vector3(1.8f, 0.04f, 1.22f) : new Vector3(1.55f, 0.04f, 1.05f), color);
            if (fur)
                HomePart(root, PrimitiveType.Sphere, "tiger head", new Vector3(0f, 0.09f, -0.68f), new Vector3(0.52f, 0.16f, 0.36f), color);
        }

        private void CreateDeer(GameObject root, Color wood)
        {
            HomePart(root, PrimitiveType.Capsule, "deer body", new Vector3(0f, 0.54f, 0f), new Vector3(0.42f, 0.82f, 0.42f), wood,
                Quaternion.Euler(0f, 0f, 90f));
            HomePart(root, PrimitiveType.Sphere, "deer head", new Vector3(0.52f, 0.68f, 0f), new Vector3(0.36f, 0.36f, 0.36f), wood);
        }

        private void CreateWallPiece(GameObject root, Color frame, Color image)
        {
            HomePart(root, PrimitiveType.Cube, "frame", new Vector3(0f, 0.76f, 0f), new Vector3(1.2f, 0.86f, 0.08f), frame);
            HomePart(root, PrimitiveType.Cube, "image", new Vector3(0f, 0.76f, -0.06f), new Vector3(0.95f, 0.63f, 0.05f), image);
        }

        private void CreateScrolls(GameObject root, Color parchment)
        {
            for (int i = -1; i <= 1; i++)
                HomePart(root, PrimitiveType.Cylinder, "scroll", new Vector3(i * 0.32f, 0.64f, 0f),
                    new Vector3(0.12f, 0.65f, 0.12f), parchment);
        }

        private void CreateSculpture(GameObject root, Color brass)
        {
            HomePart(root, PrimitiveType.Cylinder, "plinth", new Vector3(0f, 0.17f, 0f), new Vector3(0.62f, 0.34f, 0.62f), brass);
            HomePart(root, PrimitiveType.Sphere, "sculpture", new Vector3(0f, 0.72f, 0f), new Vector3(0.52f, 0.82f, 0.52f), brass);
        }

        private void CreateMysteryObject(GameObject root, Color cloth, Color brass)
        {
            HomePart(root, PrimitiveType.Cube, "mysterious case", new Vector3(0f, 0.30f, 0f), new Vector3(0.90f, 0.60f, 0.70f), cloth);
            HomePart(root, PrimitiveType.Sphere, "mysterious ornament", new Vector3(0f, 0.72f, 0f), new Vector3(0.34f, 0.34f, 0.34f), brass);
        }

        private void CreateAppliance(GameObject root, Color body, Color screen, bool tall = false)
        {
            float height = tall ? 1.48f : 0.92f;
            HomePart(root, PrimitiveType.Cube, "appliance body", new Vector3(0f, height * 0.5f, 0f), new Vector3(0.94f, height, 0.78f), body);
            HomePart(root, PrimitiveType.Cube, "appliance panel", new Vector3(0f, height * 0.62f, -0.405f), new Vector3(0.62f, 0.34f, 0.03f), screen);
        }

        private void CreateRange(GameObject root, Color body, Color brass)
        {
            HomePart(root, PrimitiveType.Cube, "range", new Vector3(0f, 0.44f, 0f), new Vector3(0.95f, 0.88f, 0.76f), body);
            for (int x = -1; x <= 1; x += 2)
                HomePart(root, PrimitiveType.Cylinder, "burner", new Vector3(x * 0.21f, 0.91f, 0f), new Vector3(0.20f, 0.03f, 0.20f), brass);
        }

        private void CreateLight(GameObject root, Color stem, Color shade)
        {
            // Unity's cylinder is two units tall; use half the intended height.
            HomePart(root, PrimitiveType.Cylinder, "lamp stand", new Vector3(0f, 0.36f, 0f), new Vector3(0.07f, 0.36f, 0.07f), stem);
            HomePart(root, PrimitiveType.Cylinder, "lamp shade", new Vector3(0f, 0.74f, 0f), new Vector3(0.32f, 0.10f, 0.32f), shade);
        }

        private void CreateTelephone(GameObject root, Color body, Color brass)
        {
            HomePart(root, PrimitiveType.Cube, "telephone base", new Vector3(0f, 0.14f, 0f), new Vector3(0.68f, 0.24f, 0.48f), body);
            HomePart(root, PrimitiveType.Capsule, "telephone receiver", new Vector3(0f, 0.34f, 0f), new Vector3(0.14f, 0.52f, 0.14f), brass,
                Quaternion.Euler(0f, 0f, 90f));
        }

        private void CreateTrophy(GameObject root, Color brass)
        {
            HomePart(root, PrimitiveType.Cylinder, "trophy base", new Vector3(0f, 0.12f, 0f), new Vector3(0.42f, 0.24f, 0.42f), brass);
            HomePart(root, PrimitiveType.Sphere, "trophy cup", new Vector3(0f, 0.54f, 0f), new Vector3(0.46f, 0.56f, 0.46f), brass);
        }

        private void CreateClothing(GameObject root, Color cloth)
        {
            HomePart(root, PrimitiveType.Cylinder, "wardrobe rail", new Vector3(0f, 0.72f, 0f), new Vector3(0.07f, 1.44f, 0.07f), new Color(0.28f, 0.16f, 0.10f, 1f));
            HomePart(root, PrimitiveType.Cube, "outfit", new Vector3(0f, 0.72f, 0f), new Vector3(0.70f, 0.90f, 0.18f), cloth);
        }

        private void CreateShoes(GameObject root, Color leather)
        {
            HomePart(root, PrimitiveType.Cube, "left shoe", new Vector3(-0.24f, 0.12f, 0f), new Vector3(0.34f, 0.20f, 0.62f), leather);
            HomePart(root, PrimitiveType.Cube, "right shoe", new Vector3(0.24f, 0.12f, 0f), new Vector3(0.34f, 0.20f, 0.62f), leather);
        }

        private void CreateAuraBike(GameObject root, Color frame, Color aura)
        {
            foreach (float x in new[] { -0.48f, 0.48f })
                HomePart(root, PrimitiveType.Cylinder, "aura bike wheel", new Vector3(x, 0.30f, 0f), new Vector3(0.42f, 0.08f, 0.42f), aura,
                    Quaternion.Euler(90f, 0f, 0f));
            HomePart(root, PrimitiveType.Cylinder, "aura bike frame", new Vector3(0f, 0.53f, 0f), new Vector3(0.10f, 1.16f, 0.10f), frame,
                Quaternion.Euler(0f, 0f, 57f));
        }

        private void CreateBooster(GameObject root, Color body, Color brass)
        {
            HomePart(root, PrimitiveType.Cylinder, "booster engine", new Vector3(0f, 0.32f, 0f), new Vector3(0.40f, 0.64f, 0.40f), body,
                Quaternion.Euler(90f, 0f, 0f));
            HomePart(root, PrimitiveType.Sphere, "booster cap", new Vector3(0f, 0.32f, -0.36f), new Vector3(0.38f, 0.38f, 0.22f), brass);
        }

        private void CreateMusicBox(GameObject root, Color wood, Color brass)
        {
            HomePart(root, PrimitiveType.Cube, "music box", new Vector3(0f, 0.16f, 0f), new Vector3(0.72f, 0.32f, 0.58f), wood);
            HomePart(root, PrimitiveType.Cylinder, "music box key", new Vector3(0.45f, 0.28f, 0f), new Vector3(0.08f, 0.38f, 0.08f), brass,
                Quaternion.Euler(0f, 0f, 90f));
        }
    }
}
