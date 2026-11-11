using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    /// <summary>
    /// Creates reusable low-resolution Town materials and prefabs using an
    /// authored PS1-style palette. NPCs intentionally have no sampled body
    /// texture until the TOWN.BIN texture page and CLUT are identified.
    /// Town terrain, building, and character visuals here are reconstructions;
    /// retail model/texture bindings have not yet been decoded.
    /// </summary>
    public static class TownAssetPrefabBuilder
    {
        private const string Root = "Assets/AzureDreams/Resources/Town";
        private const int TextureSize = 32;

        [MenuItem("Azure Dreams/Town/Build Town Terrain, Houses & NPC Prefabs")]
        public static void BuildTownAssets()
        {
            EnsureFolder("Assets/AzureDreams/Resources");
            EnsureFolder(Root);
            EnsureFolder("Assets/AzureDreams/Textures/TownGenerated");
            EnsureFolder("Assets/AzureDreams/Materials/TownGenerated");

            CreateTexture("Town_Grass", Pattern.Grass);
            CreateTexture("Town_Path", Pattern.Path);
            CreateTexture("Town_Plaster", Pattern.Plaster);
            CreateTexture("Town_Roof_Red", Pattern.RedRoof);
            CreateTexture("Town_Roof_Blue", Pattern.BlueRoof);
            CreateTexture("Town_Wood", Pattern.Wood);
            CreateTexture("Town_Stone", Pattern.Stone);
            CreateTexture("Town_Water", Pattern.Water);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // The 32px tile is repeated once per town cell after the ground is
            // scaled to 12x12, keeping PS1-like texel density at gameplay scale.
            Material grass = CreateMaterial("Town_Grass", "Town_Grass", new Vector2(12f, 12f));
            Material path = CreateMaterial("Town_Path", "Town_Path", Vector2.one);
            Material plaster = CreateMaterial("Town_Plaster", "Town_Plaster", Vector2.one);
            Material redRoof = CreateMaterial("Town_Roof_Red", "Town_Roof_Red", Vector2.one);
            Material blueRoof = CreateMaterial("Town_Roof_Blue", "Town_Roof_Blue", Vector2.one);
            Material wood = CreateMaterial("Town_Wood", "Town_Wood", Vector2.one);
            Material stone = CreateMaterial("Town_Stone", "Town_Stone", Vector2.one);
            Material water = CreateMaterial("Town_Water", "Town_Water", Vector2.one);

            // The old Townsperson_Albedo is a preview of raw VRAM words, not a
            // semantically mapped/clut-decoded NPC texture. Keep these bodies
            // palette-lit until their real TPAGE/CLUT binding is recovered.
            Material miraBody = CreateCharacterMaterial("NPC_Mira_Body", null, new Color(0.95f, 0.72f, 0.86f));
            Material rowanBody = CreateCharacterMaterial("NPC_Rowan_Body", null, new Color(0.70f, 0.80f, 0.95f));

            CreateGroundPrefab(grass);
            CreatePathTilePrefab(path);
            CreateHousePrefab("House_Inn", plaster, blueRoof, wood, new Color(0.98f, 0.78f, 0.36f), true);
            CreateHousePrefab("House_Market", plaster, redRoof, wood, new Color(0.92f, 0.84f, 0.56f), true);
            CreateHousePrefab("House_Residence", plaster, blueRoof, wood, new Color(0.86f, 0.82f, 0.70f), false);
            CreateFencePrefab(wood);
            CreateTowerGatePrefab(stone);
            CreateFountainPrefab(stone, water);
            CreateGuildSignPrefab(wood, plaster);
            CreateChestPrefab(wood, stone);
            // Mira is the demo's tower guide; use the retail Hunter appearance for this role.
            var hunter = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AzureDreams/Prefabs/NPCs/NPC_Hunter.prefab");
            var miraVisual = hunter ? hunter.GetComponentInChildren<SpriteRenderer>() : null;
            CreateNpcPrefab("NPC_Mira", "mira", "Mira", miraBody, new Color(0.56f, 0.26f, 0.57f), new Color(0.95f, 0.72f, 0.28f), null,
                new[] { "The tower rearranges its halls.", "Bring back what you can, and look after your companion." }, miraVisual);
            CreateNpcPrefab("NPC_Rowan", "rowan", "Rowan", rowanBody, new Color(0.24f, 0.38f, 0.58f), new Color(0.76f, 0.59f, 0.34f), null,
                new[] { "Welcome to the town stores.", "A healing draught costs 20 coins. Press E to finish." });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TownAssetPrefabBuilder] Town terrain materials and 12 reusable Town prefabs are ready under " + Root);
        }

        [MenuItem("Azure Dreams/Town/Repair Existing Tower Gate Prefab")]
        public static void RepairTowerGatePrefab()
        {
            string path = Root + "/Tower_Gate.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hinge = root.transform.Find("Gate Leaf Pivot");
                if (!hinge)
                {
                    hinge = new GameObject("Gate Leaf Pivot").transform;
                    hinge.SetParent(root.transform, false);
                    hinge.localPosition = new Vector3(-0.53f, 0.08f, 0f);
                }
                if (!hinge.GetComponentInChildren<Renderer>(true))
                {
                    var wood = FindMaterial("Town_Wood");
                    if (!wood) throw new InvalidOperationException("Town_Wood material is required for the gate leaf.");
                    Block(hinge, "Wooden Gate Leaf", new Vector3(0.53f, 0.58f, 0f),
                        new Vector3(1.04f, 1.08f, 0.12f), wood, true);
                }
                var door = root.GetComponent<TownGateDoor>();
                if (!door) door = root.AddComponent<TownGateDoor>();
                door.Configure(hinge);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            VerifyTownAssets();
        }

        [MenuItem("Azure Dreams/Town/Verify Town Prefabs & Textures")]
        public static void VerifyTownAssets()
        {
            var errors = new List<string>();
            VerifyPrefab("TownGround", root => VerifyTexturedRenderer(root, "Grass Terrain", "Town_Grass", errors), errors);
            VerifyPrefab("TownPathTile", root => VerifyTexturedRenderer(root, "TownPathTile", "Town_Path", errors), errors);
            VerifyPrefab("House_Inn", root => VerifyHouse(root, errors), errors);
            VerifyPrefab("House_Market", root => VerifyHouse(root, errors), errors);
            VerifyPrefab("House_Residence", root => VerifyHouse(root, errors), errors);
            VerifyPrefab("Fence_Section", root => VerifyHasRenderer(root, errors), errors);
            VerifyPrefab("Tower_Gate", root =>
            {
                VerifyHasRenderer(root, errors);
                if (!root.GetComponent<TownGateDoor>()) errors.Add("Tower_Gate is missing its TownGateDoor animation component.");
                var hinge = root.transform.Find("Gate Leaf Pivot");
                if (!hinge) errors.Add("Tower_Gate is missing its hinged gate leaf.");
                else if (hinge.GetComponentsInChildren<Renderer>(true).Length != 1)
                    errors.Add("Tower_Gate must contain exactly one rendered leaf under Gate Leaf Pivot.");
            }, errors);
            VerifyPrefab("Guild_Sign", root => VerifyHasRenderer(root, errors), errors);
            VerifyPrefab("Town_Empty_Chest", root =>
            {
                var chest = root.GetComponent<TownObject>();
                if (!chest || chest.objectId != "town_empty_chest") errors.Add("Town_Empty_Chest is missing its TownObject identity.");
                VerifyHasRenderer(root, errors);
            }, errors);
            VerifyPrefab("Water_Fountain", root =>
            {
                if (!root.GetComponent<WaterFountain>()) errors.Add("Water_Fountain is missing WaterFountain.");
                VerifyHasRenderer(root, errors);
            }, errors);
            VerifyNpc("NPC_Mira", "mira", errors);
            VerifyNpc("NPC_Rowan", "rowan", errors);

            Texture2D grass = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AzureDreams/Textures/TownGenerated/Town_Grass.png");
            if (!grass || grass.filterMode != FilterMode.Point || grass.mipmapCount != 1)
                errors.Add("Town_Grass must be imported with point filtering and no mipmaps.");

            if (errors.Count > 0)
            {
                foreach (string error in errors) Debug.LogError("[TownAssetPrefabBuilder] " + error);
                throw new InvalidOperationException("Town asset verification failed with " + errors.Count + " issue(s).");
            }
            Debug.Log("[TownAssetPrefabBuilder] Verified all 12 Town prefabs, required gameplay components, and point-filtered terrain texture references.");
        }

        private static void VerifyPrefab(string name, Action<GameObject> verify, List<string> errors)
        {
            string path = $"{Root}/{name}.prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
            {
                errors.Add("Missing prefab: " + path);
                return;
            }
            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(path);
                verify(contents);
            }
            catch (Exception exception) { errors.Add(name + " could not be inspected: " + exception.Message); }
            finally { if (contents) PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void VerifyTexturedRenderer(GameObject root, string rendererName, string expectedTexture, List<string> errors)
        {
            var renderer = Array.Find(root.GetComponentsInChildren<Renderer>(true), x => x.name == rendererName);
            if (!renderer || !renderer.sharedMaterial || !renderer.sharedMaterial.mainTexture)
            {
                errors.Add(root.name + " has no textured renderer named " + rendererName + ".");
                return;
            }
            if (renderer.sharedMaterial.mainTexture.name != expectedTexture)
                errors.Add(rendererName + " references the wrong texture.");
        }

        private static void VerifyHouse(GameObject root, List<string> errors)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length < 10) errors.Add(root.name + " has fewer than 10 rendered house parts.");
            foreach (var renderer in renderers)
                if (!renderer.sharedMaterial || !renderer.sharedMaterial.mainTexture)
                    errors.Add(root.name + "/" + renderer.name + " is missing its pixel material texture.");
        }

        private static void VerifyHasRenderer(GameObject root, List<string> errors)
        {
            if (root.GetComponentInChildren<Renderer>(true) == null)
                errors.Add(root.name + " has no rendered mesh parts.");
        }

        private static void VerifyNpc(string prefabName, string id, List<string> errors)
        {
            VerifyPrefab(prefabName, root =>
            {
                var stats = root.GetComponent<CharacterStats>();
                if (!root.GetComponent<NPCController>() || !root.GetComponent<TownMovementController>() ||
                    !root.GetComponent<ActorLocomotionView>()) errors.Add(prefabName + " is missing an NPC movement or view component.");
                if (!stats || stats.characterId != id || !stats.characterMaterial || stats.portraitTexture || stats.characterMaterial.mainTexture)
                    errors.Add(prefabName + " must have identity and a palette material, without an unclassified raw-VRAM preview.");
                if (root.GetComponentsInChildren<Renderer>(true).Length < 6)
                    errors.Add(prefabName + " has an incomplete character mesh.");
            }, errors);
        }

        private enum Pattern { Grass, Path, Plaster, RedRoof, BlueRoof, Wood, Stone, Water }

        private static void CreateTexture(string name, Pattern pattern)
        {
            string path = $"Assets/AzureDreams/Textures/TownGenerated/{name}.png";
            var random = new System.Random(7919 + (int)pattern * 97);
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                int noise = random.Next(-9, 10);
                Color32 c = pattern switch
                {
                    Pattern.Grass => GrassPixel(x, y, noise),
                    Pattern.Path => PathPixel(x, y, noise),
                    Pattern.Plaster => Shift(new Color32(177, 151, 112, 255), noise),
                    Pattern.RedRoof => RoofPixel(x, y, noise, new Color32(132, 60, 45, 255), new Color32(177, 91, 60, 255)),
                    Pattern.BlueRoof => RoofPixel(x, y, noise, new Color32(53, 79, 104, 255), new Color32(80, 111, 135, 255)),
                    Pattern.Wood => WoodPixel(x, y, noise),
                    Pattern.Stone => StonePixel(x, y, noise),
                    _ => WaterPixel(x, y, noise)
                };
                pixels[y * TextureSize + x] = c;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }

        private static Color32 GrassPixel(int x, int y, int n)
        {
            int fleck = ((x * 13 + y * 7) % 19 == 0) ? 12 : 0;
            return Shift(new Color32((byte)(74 + fleck), (byte)(112 + fleck), (byte)(64 + fleck / 2), 255), n);
        }

        private static Color32 PathPixel(int x, int y, int n)
        {
            bool mortar = (y % 8 == 0) || ((x + ((y / 8) % 2) * 4) % 8 == 0);
            return Shift(mortar ? new Color32(82, 70, 57, 255) : new Color32(151, 126, 91, 255), n);
        }

        private static Color32 RoofPixel(int x, int y, int n, Color32 dark, Color32 light)
        {
            bool seam = y % 8 == 0 || (x + ((y / 8) % 2) * 4) % 8 == 0;
            return Shift(seam ? dark : light, n);
        }

        private static Color32 WoodPixel(int x, int y, int n)
        {
            int stripe = x % 8 == 0 || (x + y * 3) % 17 == 0 ? -18 : 0;
            return Shift(new Color32((byte)(116 + stripe), (byte)(76 + stripe / 2), (byte)(42 + stripe / 3), 255), n);
        }

        private static Color32 StonePixel(int x, int y, int n)
        {
            bool seam = y % 8 == 0 || (x + ((y / 8) % 2) * 5) % 10 == 0;
            return Shift(seam ? new Color32(72, 75, 73, 255) : new Color32(134, 137, 127, 255), n);
        }

        private static Color32 WaterPixel(int x, int y, int n)
        {
            int shimmer = (x + y * 3) % 11 < 2 ? 14 : 0;
            return Shift(new Color32((byte)(49 + shimmer), (byte)(116 + shimmer), (byte)(158 + shimmer), 255), n);
        }

        private static Color32 Shift(Color32 c, int amount) => new Color32(
            (byte)Mathf.Clamp(c.r + amount, 0, 255), (byte)Mathf.Clamp(c.g + amount, 0, 255),
            (byte)Mathf.Clamp(c.b + amount, 0, 255), c.a);

        private static Material CreateMaterial(string name, string textureName, Vector2 tiling)
        {
            string path = $"Assets/AzureDreams/Materials/TownGenerated/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                Shader shader = Shader.Find("AzureDreams/PSX Vertex Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/AzureDreams/Textures/TownGenerated/{textureName}.png");
            material.mainTextureScale = tiling;
            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateCharacterMaterial(string name, Texture2D albedo, Color tint)
        {
            string path = $"Assets/AzureDreams/Materials/TownGenerated/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var baseMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/AzureDreams/Materials/Townsperson_Body.mat");
                Shader shader = baseMaterial ? baseMaterial.shader : Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = albedo;
            material.color = tint;
            material.mainTextureScale = Vector2.one;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateGroundPrefab(Material grass)
        {
            var root = new GameObject("TownGround");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Grass Terrain";
            cube.transform.SetParent(root.transform, false);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one;
            cube.GetComponent<Renderer>().sharedMaterial = grass;
            SavePrefab(root, "TownGround");
        }

        private static void CreatePathTilePrefab(Material path)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "TownPathTile";
            tile.transform.localScale = new Vector3(1f, 0.025f, 1f);
            tile.GetComponent<Renderer>().sharedMaterial = path;
            UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
            SavePrefab(tile, "TownPathTile");
        }

        private static void CreateHousePrefab(string prefabName, Material plaster, Material roof, Material wood, Color signColor, bool sign)
        {
            var root = new GameObject(prefabName);
            Block(root.transform, "Stone Foundation", new Vector3(0f, 0.12f, 0f), new Vector3(2.75f, 0.24f, 2.05f), FindMaterial("Town_Stone"), true);
            Block(root.transform, "Plaster Walls", new Vector3(0f, 0.79f, 0f), new Vector3(2.5f, 1.1f, 1.8f), plaster, true);
            Block(root.transform, "Front Door", new Vector3(0f, 0.53f, 0.916f), new Vector3(0.48f, 0.82f, 0.045f), wood, false);
            Block(root.transform, "Door Header", new Vector3(0f, 1.0f, 0.95f), new Vector3(0.62f, 0.09f, 0.08f), wood, false);
            Block(root.transform, "Door Left Jamb", new Vector3(-0.29f, 0.52f, 0.95f), new Vector3(0.08f, 0.94f, 0.08f), wood, false);
            Block(root.transform, "Door Right Jamb", new Vector3(0.29f, 0.52f, 0.95f), new Vector3(0.08f, 0.94f, 0.08f), wood, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Block(root.transform, "Window " + side, new Vector3(side * 0.79f, 0.91f, 0.916f), new Vector3(0.40f, 0.38f, 0.05f), FindMaterial("Town_Water"), false);
                Block(root.transform, "Window Sill " + side, new Vector3(side * 0.79f, 0.68f, 0.96f), new Vector3(0.51f, 0.08f, 0.11f), wood, false);
                Block(root.transform, "Window Lintel " + side, new Vector3(side * 0.79f, 1.14f, 0.96f), new Vector3(0.51f, 0.08f, 0.11f), wood, false);
                Block(root.transform, "Roof Slope " + side, new Vector3(0f, 1.62f, side * 0.48f), new Vector3(2.92f, 0.16f, 1.22f), roof, false, Quaternion.Euler(side * 35f, 0f, 0f));
                Block(root.transform, "Rafter " + side, new Vector3(side * 0.96f, 1.56f, side * 0.46f), new Vector3(0.12f, 0.16f, 1.15f), wood, false, Quaternion.Euler(side * 35f, 0f, 0f));
            }
            Block(root.transform, "Roof Ridge", new Vector3(0f, 2.0f, 0f), new Vector3(3.02f, 0.15f, 0.18f), wood, false);
            Block(root.transform, "Front Eave", new Vector3(0f, 1.35f, 0.93f), new Vector3(3.0f, 0.14f, 0.18f), wood, false);
            Block(root.transform, "Rear Eave", new Vector3(0f, 1.35f, -0.93f), new Vector3(3.0f, 0.14f, 0.18f), wood, false);
            Block(root.transform, "Chimney", new Vector3(0.91f, 1.91f, -0.23f), new Vector3(0.28f, 0.65f, 0.34f), plaster, true);
            if (sign)
            {
                Block(root.transform, "Shop Sign", new Vector3(0f, 1.22f, 1.05f), new Vector3(0.92f, 0.29f, 0.10f), wood, false);
                var plate = Block(root.transform, "Sign Inlay", new Vector3(0f, 1.22f, 1.112f), new Vector3(0.73f, 0.16f, 0.02f), wood, false);
                plate.GetComponent<Renderer>().sharedMaterial.color = signColor;
            }
            SavePrefab(root, prefabName);
        }

        private static Material FindMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/AzureDreams/Materials/TownGenerated/{name}.mat");

        private static void CreateFencePrefab(Material wood)
        {
            var root = new GameObject("Fence_Section");
            Block(root.transform, "Post Left", new Vector3(-0.43f, 0.48f, 0f), new Vector3(0.13f, 0.96f, 0.16f), wood, true);
            Block(root.transform, "Post Right", new Vector3(0.43f, 0.48f, 0f), new Vector3(0.13f, 0.96f, 0.16f), wood, true);
            Block(root.transform, "Upper Rail", new Vector3(0f, 0.70f, 0f), new Vector3(0.88f, 0.12f, 0.13f), wood, true);
            Block(root.transform, "Lower Rail", new Vector3(0f, 0.32f, 0f), new Vector3(0.88f, 0.11f, 0.13f), wood, true);
            SavePrefab(root, "Fence_Section");
        }

        private static void CreateTowerGatePrefab(Material stone)
        {
            var root = new GameObject("Tower_Gate");
            Block(root.transform, "Left Pier", new Vector3(-0.72f, 0.82f, 0f), new Vector3(0.38f, 1.64f, 0.55f), stone, true);
            Block(root.transform, "Right Pier", new Vector3(0.72f, 0.82f, 0f), new Vector3(0.38f, 1.64f, 0.55f), stone, true);
            Block(root.transform, "Lintel", new Vector3(0f, 1.63f, 0f), new Vector3(1.85f, 0.34f, 0.64f), stone, true);
            Block(root.transform, "Crest", new Vector3(0f, 1.91f, 0f), new Vector3(0.82f, 0.28f, 0.68f), stone, false);
            Block(root.transform, "Threshold", new Vector3(0f, 0.04f, 0f), new Vector3(1.40f, 0.08f, 0.68f), stone, false);
            var hinge = new GameObject("Gate Leaf Pivot");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(-0.53f, 0.08f, 0f);
            Block(hinge.transform, "Wooden Gate Leaf", new Vector3(0.53f, 0.58f, 0f),
                new Vector3(1.04f, 1.08f, 0.12f), FindMaterial("Town_Wood"), true);
            root.AddComponent<TownGateDoor>().Configure(hinge.transform);
            SavePrefab(root, "Tower_Gate");
        }

        private static void CreateFountainPrefab(Material stone, Material water)
        {
            var root = new GameObject("Town_Water_Fountain");
            Cylinder(root.transform, "Basin", new Vector3(0f, 0.19f, 0f), new Vector3(0.90f, 0.30f, 0.90f), stone, true);
            Cylinder(root.transform, "Water", new Vector3(0f, 0.35f, 0f), new Vector3(0.69f, 0.035f, 0.69f), water, false);
            Cylinder(root.transform, "Pedestal", new Vector3(0f, 0.52f, 0f), new Vector3(0.31f, 0.43f, 0.31f), stone, true);
            Cylinder(root.transform, "Upper Bowl", new Vector3(0f, 0.77f, 0f), new Vector3(0.56f, 0.12f, 0.56f), stone, false);
            Cylinder(root.transform, "Fountain Water", new Vector3(0f, 0.85f, 0f), new Vector3(0.42f, 0.025f, 0.42f), water, false);
            var fountain = root.AddComponent<WaterFountain>();
            fountain.objectId = "town_fountain";
            fountain.message = "Your vitality and magic are restored.";
            SavePrefab(root, "Water_Fountain");
        }

        private static void CreateGuildSignPrefab(Material wood, Material plaster)
        {
            var root = new GameObject("Town_Guild_Sign");
            Block(root.transform, "Left Post", new Vector3(-0.26f, 0.48f, 0f), new Vector3(0.10f, 0.96f, 0.12f), wood, true);
            Block(root.transform, "Right Post", new Vector3(0.26f, 0.48f, 0f), new Vector3(0.10f, 0.96f, 0.12f), wood, true);
            Block(root.transform, "Sign Board", new Vector3(0f, 0.63f, 0f), new Vector3(0.72f, 0.48f, 0.11f), plaster, true);
            Block(root.transform, "Top Cap", new Vector3(0f, 0.92f, 0f), new Vector3(0.84f, 0.12f, 0.16f), wood, false);
            SavePrefab(root, "Guild_Sign");
        }

        private static void CreateChestPrefab(Material wood, Material stone)
        {
            var root = new GameObject("Town_Empty_Chest");
            Block(root.transform, "Chest Body", new Vector3(0f, 0.28f, 0f), new Vector3(0.70f, 0.48f, 0.52f), wood, true);
            Block(root.transform, "Lid", new Vector3(0f, 0.55f, -0.01f), new Vector3(0.74f, 0.12f, 0.56f), wood, true);
            Block(root.transform, "Front Lock", new Vector3(0f, 0.39f, 0.28f), new Vector3(0.12f, 0.16f, 0.04f), stone, false);
            var chest = root.AddComponent<TownObject>();
            chest.objectId = "town_empty_chest";
            chest.message = "The storage chest is empty. [E]";
            chest.repeatable = false;
            SavePrefab(root, "Town_Empty_Chest");
        }

        private static void CreateNpcPrefab(string prefabName, string id, string displayName, Material body, Color hairColor,
            Color clothesAccent, Texture2D portrait, string[] lines, SpriteRenderer visual = null)
        {
            var root = new GameObject(prefabName);
            var stats = root.AddComponent<CharacterStats>();
            stats.characterId = id;
            stats.displayName = displayName;
            stats.moveSpeed = 1.35f;
            stats.maxHealth = stats.currentHealth = 20;
            stats.portraitTexture = portrait;
            stats.characterMaterial = body;
            root.AddComponent<TownMovementController>().Speed = 1.35f;
            var npc = root.AddComponent<NPCController>();
            npc.npcId = id;
            npc.loopDialogue = false;
            npc.dialogueLines = lines;
            if (visual)
            {
                UnityEngine.Object.Instantiate(visual.gameObject, root.transform, false).name = "Mira Sprite";
                root.AddComponent<ActorLocomotionView>();
                SavePrefab(root, prefabName);
                return;
            }
            root.AddComponent<ActorLocomotionView>();

            var skin = FlatMaterial("NPC_Skin", new Color(0.78f, 0.58f, 0.43f));
            var hair = FlatMaterial(prefabName + "_Hair", hairColor);
            var pants = FlatMaterial(prefabName + "_Clothes", clothesAccent);
            var eyes = FlatMaterial("NPC_Eyes", new Color(0.10f, 0.08f, 0.08f));
            Capsule(root.transform, "Textured Tunic", new Vector3(0f, 0.72f, 0f), new Vector3(0.48f, 0.77f, 0.38f), body, false);
            Sphere(root.transform, "Head", new Vector3(0f, 1.36f, 0.015f), new Vector3(0.40f, 0.43f, 0.38f), skin, false);
            Sphere(root.transform, "Hair", new Vector3(0f, 1.54f, -0.005f), new Vector3(0.43f, 0.24f, 0.41f), hair, false);
            Block(root.transform, "Left Leg", new Vector3(-0.13f, 0.20f, 0f), new Vector3(0.17f, 0.42f, 0.22f), pants, false);
            Block(root.transform, "Right Leg", new Vector3(0.13f, 0.20f, 0f), new Vector3(0.17f, 0.42f, 0.22f), pants, false);
            Block(root.transform, "Left Arm", new Vector3(-0.31f, 0.78f, 0f), new Vector3(0.15f, 0.53f, 0.19f), body, false, Quaternion.Euler(0f, 0f, -8f));
            Block(root.transform, "Right Arm", new Vector3(0.31f, 0.78f, 0f), new Vector3(0.15f, 0.53f, 0.19f), body, false, Quaternion.Euler(0f, 0f, 8f));
            Block(root.transform, "Left Eye", new Vector3(-0.085f, 1.39f, 0.197f), new Vector3(0.045f, 0.055f, 0.025f), eyes, false);
            Block(root.transform, "Right Eye", new Vector3(0.085f, 1.39f, 0.197f), new Vector3(0.045f, 0.055f, 0.025f), eyes, false);
            Block(root.transform, "Scarf", new Vector3(0f, 1.04f, 0.19f), new Vector3(0.31f, 0.12f, 0.07f), FlatMaterial(prefabName + "_Accent", clothesAccent), false);
            SavePrefab(root, prefabName);
        }

        private static Material FlatMaterial(string name, Color color)
        {
            string path = $"Assets/AzureDreams/Materials/TownGenerated/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                Shader shader = Shader.Find("AzureDreams/PSX Vertex Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
            bool collider, Quaternion? rotation = null)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            if (rotation.HasValue) child.transform.localRotation = rotation.Value;
            child.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());
            return child;
        }

        private static void Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            child.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());
        }

        private static void Capsule(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            child.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());
        }

        private static void Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            child.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());
        }

        private static void SavePrefab(GameObject root, string name)
        {
            string path = $"{Root}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
