using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxFxPrefabBuilder
    {
        [MenuItem("Azure Dreams/Rendering/Build FX Prefabs")]
        public static void BuildFxPrefabs()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fxDir = "Assets/AzureDreams/Prefabs/FX";
            Directory.CreateDirectory(Path.Combine(projectRoot, fxDir));

            // Create materials
            var fireMat = CreateMaterial(projectRoot, "FX_Fire", new Color(1f, 0.5f, 0.1f), BlendMode.SrcAlpha, BlendMode.One);
            var smokeMat = CreateMaterial(projectRoot, "FX_Smoke", new Color(0.5f, 0.5f, 0.5f), BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
            var dustMat = CreateMaterial(projectRoot, "FX_Dust", new Color(0.6f, 0.5f, 0.4f), BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
            var lightningMat = CreateMaterial(projectRoot, "FX_Lightning", new Color(0.5f, 0.8f, 1f), BlendMode.SrcAlpha, BlendMode.One);
            var waterMat = CreateMaterial(projectRoot, "FX_Water", new Color(0.2f, 0.5f, 0.9f), BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
            var magicMat = CreateMaterial(projectRoot, "FX_Magic", new Color(0.8f, 0.2f, 1f), BlendMode.SrcAlpha, BlendMode.One);

            // Create prefabs
            CreateFirePrefab(fxDir, fireMat);
            CreateSmokePrefab(fxDir, smokeMat);
            CreateDustPrefab(fxDir, dustMat);
            CreateLightningPrefab(fxDir, lightningMat);
            CreateWaterPrefab(fxDir, waterMat);
            CreateMagicPrefab(fxDir, magicMat);

            Debug.Log("[PsxFxPrefabBuilder] FX prefabs created in " + fxDir);
        }

        private static Material CreateMaterial(string projectRoot, string name, Color color, BlendMode src, BlendMode dst)
        {
            string matPath = "Assets/AzureDreams/Materials/" + name + ".mat";
            Material mat;
            if (File.Exists(Path.Combine(projectRoot, matPath)))
                mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            else
            {
                var shader = Shader.Find("Particles/Standard Unlit");
                if (!shader) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                mat = new Material(shader);
                mat.name = name;
                mat.color = color;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(projectRoot, matPath)));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        private static void CreateFirePrefab(string fxDir, Material mat)
        {
            var go = new GameObject("FX_Fire");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.8f, 0.2f), new Color(1f, 0.3f, 0.1f));
            main.gravityModifier = -0.5f;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 50;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.1f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f), new GradientColorKey(new Color(0.8f, 0.2f, 0.1f), 0.5f), new GradientColorKey(new Color(0.2f, 0.1f, 0.1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.alignment = ParticleSystemRenderSpace.View;

            PrefabUtility.SaveAsPrefabAsset(go, fxDir + "/FX_Fire.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateSmokePrefab(string fxDir, Material mat)
        {
            var go = new GameObject("FX_Smoke");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 3f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.6f, 0.6f, 0.6f, 0.8f), new Color(0.3f, 0.3f, 0.3f, 0.5f));
            main.gravityModifier = -0.2f;
            main.maxParticles = 100;

            var emission = ps.emission;
            emission.rateOverTime = 20;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 0f), new GradientColorKey(new Color(0.3f, 0.3f, 0.3f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1f, 2f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.alignment = ParticleSystemRenderSpace.View;

            PrefabUtility.SaveAsPrefabAsset(go, fxDir + "/FX_Smoke.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateDustPrefab(string fxDir, Material mat)
        {
            var go = new GameObject("FX_Dust");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.7f, 0.6f, 0.5f, 0.6f), new Color(0.5f, 0.4f, 0.3f, 0.4f));
            main.gravityModifier = 0.1f;
            main.maxParticles = 50;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.7f, 0.6f, 0.5f), 0f), new GradientColorKey(new Color(0.5f, 0.4f, 0.3f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.alignment = ParticleSystemRenderSpace.World;

            PrefabUtility.SaveAsPrefabAsset(go, fxDir + "/FX_Dust.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateLightningPrefab(string fxDir, Material mat)
        {
            var go = new GameObject("FX_Lightning");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.5f, 0.8f, 1f, 1f), new Color(0.8f, 0.9f, 1f, 1f));
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.05f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.8f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.5f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.alignment = ParticleSystemRenderSpace.View;

            PrefabUtility.SaveAsPrefabAsset(go, fxDir + "/FX_Lightning.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateWaterPrefab(string fxDir, Material mat)
        {
            var go = new GameObject("FX_Water");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.3f, 0.6f, 0.9f, 0.8f), new Color(0.1f, 0.4f, 0.8f, 0.6f));
            main.gravityModifier = 0.5f;
            main.maxParticles = 100;

            var emission = ps.emission;
            emission.rateOverTime = 30;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.7f, 1f), 0f), new GradientColorKey(new Color(0.1f, 0.4f, 0.8f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1f, 1.5f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.alignment = ParticleSystemRenderSpace.World;

            PrefabUtility.SaveAsPrefabAsset(go, fxDir + "/FX_Water.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CreateMagicPrefab(string fxDir, Material mat)
        {
            var go = new GameObject("FX_Magic");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.8f, 0.2f, 1f, 1f), new Color(0.4f, 0.1f, 0.8f, 0.8f));
            main.gravityModifier = -0.3f;
            main.maxParticles = 150;

            var emission = ps.emission;
            emission.rateOverTime = 40;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.9f, 0.3f, 1f), 0f), new GradientColorKey(new Color(0.5f, 0.1f, 0.8f), 0.5f), new GradientColorKey(new Color(0.2f, 0.05f, 0.4f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.alignment = ParticleSystemRenderSpace.View;

            PrefabUtility.SaveAsPrefabAsset(go, fxDir + "/FX_Magic.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
