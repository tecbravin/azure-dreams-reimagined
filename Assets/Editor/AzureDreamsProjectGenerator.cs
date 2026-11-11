using System.IO;
using System.Text.RegularExpressions;
using AzureDreamsReimagined;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AzureDreamsProjectGenerator
{
    private const string Root = "Assets/AzureDreams";
    private const string Definitions = Root + "/Definitions";
    private const string Materials = Root + "/Materials";
    private const string Textures = Root + "/Textures";
    private const string Prefabs = Root + "/Prefabs";
    private const string Scenes = Root + "/Scenes";

    [MenuItem("Azure Dreams/Run Play Mode Smoke Test")]
    public static void RunPlayModeSmokeTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode before starting the smoke test.");
        EditorSceneManager.OpenScene(Scenes + "/ReimaginedDemo.unity");
        SessionState.SetBool("AzureDreams.SmokePending", true);
        SessionState.SetBool("AzureDreams.SmokePassed", false);
        SessionState.SetInt("AzureDreams.SmokeFrames", 0);
        EditorApplication.update -= PollPlayModeSmokeTest;
        EditorApplication.update += PollPlayModeSmokeTest;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void ResumePlayModeSmokeTest()
    {
        if (SessionState.GetBool("AzureDreams.SmokePending", false))
        {
            EditorApplication.update -= PollPlayModeSmokeTest;
            EditorApplication.update += PollPlayModeSmokeTest;
        }
    }

    private static void PollPlayModeSmokeTest()
    {
        if (!SessionState.GetBool("AzureDreams.SmokePending", false))
        {
            EditorApplication.update -= PollPlayModeSmokeTest;
            return;
        }
        if (EditorApplication.isPlaying)
        {
            int frames = SessionState.GetInt("AzureDreams.SmokeFrames", 0) + 1;
            SessionState.SetInt("AzureDreams.SmokeFrames", frames);
            GameObject player = GameObject.Find("Player - editable prefab");
            GameObject mira = GameObject.Find("Townsperson - Mira");
            GameObject controller = GameObject.Find("Tower Game Controller");
            if (player && mira && controller && player.GetComponent<ActorView>())
            {
                SessionState.SetBool("AzureDreams.SmokePassed", true);
                Debug.Log("Play-mode smoke test passed: controller started and spawned the configured player and NPC prefabs.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (frames > 180)
            {
                Debug.LogError("Play-mode smoke test timed out while waiting for the outpost scene to initialize.");
                EditorApplication.isPlaying = false;
            }
            return;
        }
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            bool passed = SessionState.GetBool("AzureDreams.SmokePassed", false);
            SessionState.SetBool("AzureDreams.SmokePending", false);
            EditorApplication.update -= PollPlayModeSmokeTest;
            if (!passed)
                Debug.LogError("Play-mode smoke test failed before the player and town NPCs were initialized.");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    [MenuItem("Azure Dreams/Generate Reimagined Prototype")]
    public static void Generate()
    {
        EnsureFolders();
        PlayerSettings.productName = "Azure Dreams Reimagined";
        PlayerSettings.companyName = "Independent Prototype";
        Texture2D playerTexture = CreateTexture("Player_Albedo.png", new Color(0.12f, 0.44f, 0.72f), new Color(0.3f, 0.78f, 0.92f));
        Texture2D creatureTexture = CreateTexture("Familiar_Albedo.png", new Color(0.12f, 0.58f, 0.46f), new Color(0.42f, 0.92f, 0.68f));
        Texture2D npcTexture = CreateTexture("Townsperson_Albedo.png", new Color(0.56f, 0.33f, 0.65f), new Color(0.92f, 0.66f, 0.42f));

        Material blue = CreateMaterial("Player_Body", new Color(0.22f, 0.56f, 0.9f));
        Material warm = CreateMaterial("Wanderer_Accent", new Color(0.98f, 0.52f, 0.17f));
        Material green = CreateMaterial("Familiar_Body", new Color(0.23f, 0.78f, 0.56f));
        Material cyan = CreateMaterial("Familiar_Accent", new Color(0.47f, 0.9f, 0.86f));
        Material violet = CreateMaterial("Townsperson_Body", new Color(0.68f, 0.43f, 0.78f));
        Material gold = CreateMaterial("Townsperson_Accent", new Color(0.96f, 0.73f, 0.29f));
        Material ground = CreateMaterial("Environment_Ground", new Color(0.21f, 0.27f, 0.32f));
        Material wall = CreateMaterial("Environment_Stone", new Color(0.28f, 0.34f, 0.39f));
        Material accent = CreateMaterial("Environment_Azure", new Color(0.16f, 0.72f, 0.87f), 0.15f);

        ActorDefinition playerData = CreateDefinition("Wanderer.asset", "Wanderer", ActorKind.Player, 30, 5, 0.16f,
            new Color(0.82f, 0.94f, 1f), playerTexture);
        ActorDefinition creatureData = CreateDefinition("Mossling.asset", "Mossling", ActorKind.TowerCreature, 12, 3, 0.16f,
            new Color(0.82f, 1f, 0.89f), creatureTexture);
        ActorDefinition npcData = CreateDefinition("Mira.asset", "Mira", ActorKind.Townsperson, 16, 0, 0.16f,
            new Color(1f, 0.92f, 0.98f), npcTexture);

        GameObject playerPrefab = CreatePlayerPrefab(playerData, blue, warm);
        GameObject creaturePrefab = CreateCreaturePrefab(creatureData, green, cyan);
        GameObject npcPrefab = CreateNpcPrefab(npcData, violet, gold);
        CreateDemoScene(playerPrefab, creaturePrefab, npcPrefab, playerData, creatureData, npcData, ground, wall, accent);
        SetInputSystemMode();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Azure Dreams Reimagined: generated actor definitions, editable textures, three prefabs, and the playable demo scene.");
    }

    private static void EnsureFolders()
    {
        CreateFolder("Assets", "AzureDreams");
        CreateFolder(Root, "Definitions");
        CreateFolder(Root, "Materials");
        CreateFolder(Root, "Textures");
        CreateFolder(Root, "Prefabs");
        CreateFolder(Root, "Scenes");
    }

    private static void CreateFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static Texture2D CreateTexture(string fileName, Color baseColor, Color accentColor)
    {
        string path = Textures + "/" + fileName;
        string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
        if (!File.Exists(absolutePath))
        {
            const int size = 32;
            Texture2D generated = new Texture2D(size, size, TextureFormat.RGBA32, false);
            generated.name = Path.GetFileNameWithoutExtension(fileName);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool stripe = ((x / 4) + (y / 4)) % 2 == 0;
                    bool glint = (x + y * 2) % 13 == 0;
                    pixels[y * size + x] = glint ? Color.white : (stripe ? baseColor : Color.Lerp(baseColor, accentColor, 0.62f));
                }
            }
            generated.SetPixels(pixels);
            generated.Apply(false, false);
            File.WriteAllBytes(absolutePath, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 64;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material CreateMaterial(string name, Color color, float metallic = 0f)
    {
        string path = Materials + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static ActorDefinition CreateDefinition(string fileName, string displayName, ActorKind kind,
        int health, int damage, float moveDelay, Color tint, Texture2D texture)
    {
        string path = Definitions + "/" + fileName;
        ActorDefinition data = AssetDatabase.LoadAssetAtPath<ActorDefinition>(path);
        if (!data)
        {
            data = ScriptableObject.CreateInstance<ActorDefinition>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.Configure(displayName, kind, health, damage, moveDelay, tint, texture);
        EditorUtility.SetDirty(data);
        return data;
    }

    private static GameObject CreatePlayerPrefab(ActorDefinition data, Material body, Material accent)
    {
        GameObject root = NewRoot("Wanderer");
        Renderer bodyRenderer = AddPart(root.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.46f, 0f), new Vector3(0.66f, 0.88f, 0.56f), body);
        AddPart(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.12f, 0f), Vector3.one * 0.48f, body);
        AddPart(root.transform, PrimitiveType.Cube, "Scarf", new Vector3(0f, 0.79f, 0.29f), new Vector3(0.72f, 0.16f, 0.18f), accent);
        AddPart(root.transform, PrimitiveType.Cylinder, "TravelPack", new Vector3(0f, 0.48f, -0.33f), new Vector3(0.28f, 0.44f, 0.25f), accent);
        root.AddComponent<ActorView>().Configure(data, bodyRenderer);
        return SavePrefab(root, "Wanderer.prefab");
    }

    private static GameObject CreateCreaturePrefab(ActorDefinition data, Material body, Material accent)
    {
        GameObject root = NewRoot("Mossling");
        Renderer bodyRenderer = AddPart(root.transform, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.48f, 0f), new Vector3(0.92f, 0.78f, 0.82f), body);
        AddPart(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.98f, 0.02f), Vector3.one * 0.57f, body);
        AddPart(root.transform, PrimitiveType.Sphere, "Crest", new Vector3(0f, 1.42f, 0f), new Vector3(0.3f, 0.48f, 0.3f), accent);
        AddPart(root.transform, PrimitiveType.Sphere, "EyeL", new Vector3(-0.17f, 1.04f, 0.48f), Vector3.one * 0.09f, accent);
        AddPart(root.transform, PrimitiveType.Sphere, "EyeR", new Vector3(0.17f, 1.04f, 0.48f), Vector3.one * 0.09f, accent);
        root.AddComponent<ActorView>().Configure(data, bodyRenderer);
        return SavePrefab(root, "Mossling.prefab");
    }

    private static GameObject CreateNpcPrefab(ActorDefinition data, Material body, Material accent)
    {
        GameObject root = NewRoot("Townsperson");
        Renderer bodyRenderer = AddPart(root.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.43f, 0f), new Vector3(0.6f, 0.82f, 0.52f), body);
        AddPart(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.05f, 0f), Vector3.one * 0.44f, accent);
        AddPart(root.transform, PrimitiveType.Cylinder, "Hat", new Vector3(0f, 1.32f, 0f), new Vector3(0.48f, 0.12f, 0.48f), body);
        AddPart(root.transform, PrimitiveType.Cube, "Satchel", new Vector3(0.28f, 0.48f, 0.28f), new Vector3(0.24f, 0.34f, 0.2f), accent);
        root.AddComponent<ActorView>().Configure(data, bodyRenderer);
        return SavePrefab(root, "Mira.prefab");
    }

    private static GameObject NewRoot(string name) => new GameObject(name);

    private static Renderer AddPart(Transform parent, PrimitiveType shape, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        if (collider) Object.DestroyImmediate(collider);
        return renderer;
    }

    private static GameObject SavePrefab(GameObject root, string fileName)
    {
        string path = Prefabs + "/" + fileName;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreateDemoScene(GameObject playerPrefab, GameObject creaturePrefab, GameObject npcPrefab,
        ActorDefinition playerData, ActorDefinition creatureData, ActorDefinition npcData,
        Material ground, Material wall, Material accent)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.055f, 0.075f);
        camera.orthographic = true;
        camera.orthographicSize = 9.5f;

        GameObject lightObject = new GameObject("Key Light", typeof(Light));
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.color = new Color(0.82f, 0.9f, 1f);
        lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.35f, 0.42f);

        GameObject controllerObject = new GameObject("Tower Game Controller");
        TowerGameController controller = controllerObject.AddComponent<TowerGameController>();
        controller.Configure(playerPrefab, creaturePrefab, npcPrefab, playerData, creatureData, npcData, ground, wall, accent, camera);

        string scenePath = Scenes + "/ReimaginedDemo.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets();
    }

    private static void SetInputSystemMode()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string settingsPath = Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset");
        if (!File.Exists(settingsPath))
            return;
        string original = File.ReadAllText(settingsPath);
        string updated = Regex.Replace(original, @"(?m)^\s*activeInputHandler:\s*-?\d+\s*$", "  activeInputHandler: 1");
        if (updated != original)
            File.WriteAllText(settingsPath, updated);
        else
            Debug.LogWarning("Could not find activeInputHandler in ProjectSettings.asset; choose Input System Package (New) in Player Settings.");
    }
}
