using System;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Retail tower floor rendering: the cell meshes of render_map_cell_mesh.c built from the retail tileset and
    /// texture set of the floor, and the cylindrical backdrop of process_circular_segments /
    /// project_depth_strips_and_quads (dungeon/actor/depth_strip_projection_g00337.c).
    /// </summary>
    public sealed partial class TowerGameController
    {
        private const float RetailUnit = 1f / 64f;
        private const int RetailBackdropSegments = 32;   // 0x80 of 0x1000 per segment
        private const int RetailBackdropStrips = 14;
        private const int RetailStripHeight = 0xE00;
        private const int RetailBackdropRadius = 6 * 4096;
        private readonly Dictionary<string, Material> _retailTileMaterials = new();
        private readonly Dictionary<string, (ushort Page, ushort Clut)[]> _retailAtlasCombos = new();
        private readonly List<Material> _retailWaterMaterials = new();
        private Mesh _retailBackdropMesh;
        private Material _retailBackdropMaterial;
        private int _retailBackdropTicks;
        private float _retailBackdropClock;
        private int _retailBackdropSpeed = 2;
        private int _retailBackdropMode;
        private int _retailWaterOffset;
        private float _retailWaterClock;

        private bool UsesRetailFloorMesh => _towerFloorLayout?.Retail != null;

        private Shader RetailShader(string name) =>
            AssetMode.Load<Shader>("Shaders/" + name) ?? Shader.Find("AzureDreams/" + name);

        private Material RetailTileMaterial(string set, bool translucent)
        {
            string key = set + (translucent ? "#t" : "");
            if (_retailTileMaterials.TryGetValue(key, out var material) && material) return material;
            var texture = AssetMode.Load<Texture2D>("Tower/Retail/" + set);
            var shader = RetailShader(translucent ? "RetailTowerTranslucent" : "RetailTowerTile");
            if (!texture || !shader) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            material = new Material(shader) { name = "Retail tower " + key, mainTexture = texture };
            _retailTileMaterials[key] = material;
            return material;
        }

        private (ushort Page, ushort Clut)[] RetailAtlasCombos(string set)
        {
            if (_retailAtlasCombos.TryGetValue(set, out var combos)) return combos;
            var asset = AssetMode.Load<TextAsset>("Tower/Retail/" + set);
            var data = asset ? asset.bytes : null;
            if (data == null || data.Length < 6) return Array.Empty<(ushort, ushort)>();
            int count = BitConverter.ToUInt16(data, 4);
            combos = new (ushort, ushort)[count];
            for (int i = 0; i < count && 6 + i * 4 + 4 <= data.Length; i++)
                combos[i] = (BitConverter.ToUInt16(data, 6 + i * 4), BitConverter.ToUInt16(data, 8 + i * 4));
            _retailAtlasCombos[set] = combos;
            return combos;
        }

        private sealed class RetailMeshBuffer
        {
            public readonly List<Vector3> Vertices = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<Color32> Colors = new();
            public readonly List<int> Indices = new();
        }

        // Approximation of the GTE light the dungeon view installs: a key light from above and the camera side.
        private static readonly Vector3 RetailLight = new Vector3(0.35f, 0.85f, -0.4f).normalized;

        private readonly List<GameObject> _retailSurfaces = new();

        private void BuildRetailFloorMeshes(bool includeBackdrop)
        {
            foreach (var surface in _retailSurfaces)
            {
                if (!surface) continue;
                var filter = surface.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh)
                {
                    _ownedManagedFloorMeshes.Remove(filter.sharedMesh);
                    Destroy(filter.sharedMesh);
                }
                surface.SetActive(false);
                RemoveGenerated(surface);
            }
            _retailSurfaces.Clear();
            var retail = _towerFloorLayout.Retail;
            var geometry = retail.Geometry;
            string set = retail.TextureSet;
            var opaque = RetailTileMaterial(set, false);
            var translucent = RetailTileMaterial(set, true);
            if (!opaque)
            {
                Debug.LogWarning($"Retail tower textures '{set}' are missing; using the generated floor mesh.");
                return;
            }
            var combos = RetailAtlasCombos(set);
            var lookup = new Dictionary<int, int>();
            for (int i = 0; i < combos.Length; i++) lookup[(combos[i].Page << 16) | combos[i].Clut] = i;
            int atlasColumns = 4;
            int atlasRows = Mathf.Max(1, (combos.Length + atlasColumns - 1) / atlasColumns);
            float atlasWidth = atlasColumns * 256f, atlasHeight = atlasRows * 256f;
            var solid = new RetailMeshBuffer();
            var glass = new RetailMeshBuffer();
            var water = new Dictionary<int, RetailMeshBuffer>();
            var points = new Vector3[4];
            var uvs = new Vector2[4];
            var waterUvs = new Vector2[4];

            for (int y = 0; y < RetailTowerFloor.Size; y++)
            for (int x = 0; x < RetailTowerFloor.Size; x++)
            {
                int index = retail.Index(x, y);
                int kind = retail.Kind[index];
                if (kind == 0 || kind >= geometry.Count) continue;
                int offset = retail.Value[index];
                foreach (int pos in geometry.ReachableFaces(kind))
                {
                    uint uv0 = geometry.U32(pos + 8), uv1 = geometry.U32(pos + 12);
                    uint attr = geometry.U32(pos + 16), info = geometry.U32(pos + 20);
                    if ((uv0 >> 31) != 0 || uv1 == 0) continue;
                    int normal = (int)(attr & 0xFFFF);
                    if (normal >= geometry.NormalCount) continue;
                    bool valid = true;
                    int lowest = int.MaxValue;
                    for (int c = 0; c < 4; c++)
                    {
                        int vertex = geometry.U16(pos + c * 2);
                        if (vertex >= geometry.VertexCount) { valid = false; break; }
                        int vx = geometry.Vertices[vertex * 3], vy = geometry.Vertices[vertex * 3 + 1];
                        int vz = geometry.Vertices[vertex * 3 + 2] - offset;
                        lowest = Math.Min(lowest, vz);
                        points[c] = new Vector3(x - 0.5f + vx * RetailUnit, -vz * RetailUnit, y - 0.5f + vy * RetailUnit);
                        _retailFaceZ[c] = vz;
                    }
                    if (!valid) continue;
                    int nx = geometry.Normals[normal * 3], ny = geometry.Normals[normal * 3 + 1], nz = geometry.Normals[normal * 3 + 2];
                    if (RetailFaceHiddenByNeighbor(retail, x, y, nx, ny, nz, _retailFaceZ)) continue;
                    int page = (int)(uv1 >> 16), clut = (int)(uv0 >> 16) & 0x7FFF;
                    if (!lookup.TryGetValue((page << 16) | clut, out int cell)) continue;
                    int row = cell / atlasColumns, column = cell % atlasColumns;
                    int[] u = { (int)(uv0 & 255), (int)(uv1 & 255), (int)((attr >> 16) & 255), (int)(info & 255) };
                    int[] v = { (int)((uv0 >> 8) & 255), (int)((uv1 >> 8) & 255), (int)((attr >> 24) & 255), (int)((info >> 8) & 255) };
                    bool semi = ((info >> 24) & 1) != 0;
                    bool animated = (page & 0x1F) == 0x19 && Mathf.Min(u[0], Mathf.Min(u[1], Mathf.Min(u[2], u[3]))) >= 96 &&
                        Mathf.Max(v[0], Mathf.Max(v[1], Mathf.Max(v[2], v[3]))) < 64;
                    for (int c = 0; c < 4; c++)
                    {
                        uvs[c] = new Vector2((column * 256 + u[c] + 0.5f) / atlasWidth, 1f - (row * 256 + v[c] + 0.5f) / atlasHeight);
                        waterUvs[c] = new Vector2((u[c] - 96 + 0.5f) / 32f, -(v[c] + 0.5f) / 32f);
                    }
                    var faceNormal = new Vector3(nx, -nz, ny) / 4096f;
                    float light = 0.62f + 0.38f * Mathf.Max(0f, Vector3.Dot(faceNormal.normalized, RetailLight));
                    byte shade = (byte)Mathf.Clamp(Mathf.RoundToInt(light * 255f), 0, 255);
                    var color = new Color32(shade, shade, shade, 255);
                    RetailMeshBuffer target = solid;
                    if (animated)
                    {
                        if (!water.TryGetValue(clut, out target)) water[clut] = target = new RetailMeshBuffer();
                        AppendRetailQuad(target, points, waterUvs, color, faceNormal);
                        continue;
                    }
                    if (semi && translucent) target = glass;
                    AppendRetailQuad(target, points, uvs, color, faceNormal);
                }
            }

            _managedFloorMesh = CreateRetailMeshObject("Retail Tower Floor", solid, opaque, collider: true);
            if (glass.Vertices.Count > 0)
                _managedWallMesh = CreateRetailMeshObject("Retail Tower Translucent", glass, translucent, collider: false);
            _retailWaterMaterials.Clear();
            foreach (var pair in water)
            {
                var material = RetailWaterMaterial(set, pair.Key) ?? translucent;
                if (!material) continue;
                CreateRetailMeshObject("Retail Tower Water", pair.Value, material, collider: false);
                _retailWaterMaterials.Add(material);
            }
            _towerCutawayMesh = null;
            _towerWallVertices = null;
            _towerCutawayFaces.Clear();
            if (includeBackdrop) BuildRetailBackdrop(retail);
        }

        private readonly int[] _retailFaceZ = new int[4];

        // render_map_cell_mesh: axis-aligned side faces completely below the neighbouring cell's surface are skipped.
        private static bool RetailFaceHiddenByNeighbor(RetailTowerFloor retail, int x, int y, int nx, int ny, int nz, int[] z)
        {
            if (nz < 0 || (nx & 0x0FFF) != 0 || (ny & 0x0FFF) != 0 || (nx == 0 && ny == 0)) return false;
            int ox = x + Math.Sign(nx), oy = y + Math.Sign(ny);
            if (ox < 0 || oy < 0 || ox >= RetailTowerFloor.Size || oy >= RetailTowerFloor.Size) return false;
            int neighbor = retail.Index(ox, oy);
            int flags = retail.Flags[neighbor];
            if ((flags & 0xC1) != 0) return false;
            int limit = -retail.Value[neighbor];
            return z[0] >= limit && z[1] >= limit && z[2] >= limit && z[3] >= limit;
        }

        private static void AppendRetailQuad(RetailMeshBuffer buffer, Vector3[] points, Vector2[] uvs, Color32 color, Vector3 normal)
        {
            // PS1 quads are drawn as (0, 1, 2) and (1, 3, 2); wind each so its front faces along the face normal.
            AppendRetailTriangle(buffer, points, uvs, color, normal, 0, 1, 2);
            if (points[3] != points[2] && points[3] != points[1])
                AppendRetailTriangle(buffer, points, uvs, color, normal, 1, 3, 2);
        }

        private static void AppendRetailTriangle(RetailMeshBuffer buffer, Vector3[] points, Vector2[] uvs, Color32 color,
            Vector3 normal, int a, int b, int c)
        {
            var cross = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
            if (cross.sqrMagnitude < 1e-10f) return;
            if (Vector3.Dot(cross, normal) < 0f) (b, c) = (c, b);
            int start = buffer.Vertices.Count;
            foreach (int corner in new[] { a, b, c })
            {
                buffer.Vertices.Add(points[corner]);
                buffer.Uvs.Add(uvs[corner]);
                buffer.Colors.Add(color);
            }
            buffer.Indices.Add(start); buffer.Indices.Add(start + 1); buffer.Indices.Add(start + 2);
        }

        private GameObject CreateRetailMeshObject(string objectName, RetailMeshBuffer buffer, Material material, bool collider)
        {
            var mesh = new Mesh { name = objectName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(buffer.Vertices);
            mesh.SetUVs(0, buffer.Uvs);
            mesh.SetColors(buffer.Colors);
            mesh.SetTriangles(buffer.Indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _ownedManagedFloorMeshes.Add(mesh);
            var instance = new GameObject(objectName);
            instance.transform.SetParent(transform, true);
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = instance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collider) instance.AddComponent<MeshCollider>().sharedMesh = mesh;
            _generated.Add(instance);
            _retailSurfaces.Add(instance);
            return instance;
        }

        // update_wrapped_texture_scroll: the water band (VRAM 0x270,0x140+offset) moves `step` rows per 30 Hz frame.
        private Material RetailWaterMaterial(string set, int clut)
        {
            string key = $"water_{set}_{clut:x4}";
            if (_retailTileMaterials.TryGetValue(key, out var material) && material) return material;
            var texture = AssetMode.Load<Texture2D>("Tower/Retail/" + key);
            var shader = RetailShader("RetailTowerTranslucent");
            if (!texture || !shader) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            material = new Material(shader) { name = "Retail tower " + key, mainTexture = texture };
            _retailTileMaterials[key] = material;
            return material;
        }

        private void UpdateRetailWater()
        {
            if (_retailWaterMaterials.Count == 0) return;
            _retailWaterClock += Time.deltaTime * RetailFrameRate;
            bool changed = false;
            while (_retailWaterClock >= 1f)
            {
                _retailWaterClock -= 1f;
                _retailWaterOffset = (_retailWaterOffset - 2) & 0x1F;
                changed = true;
            }
            if (!changed) return;
            var offset = new Vector2(0f, -_retailWaterOffset / 32f);
            foreach (var material in _retailWaterMaterials)
                if (material) material.mainTextureOffset = offset;
        }

        // ---------------------------------------------------------------- backdrop
        private void BuildRetailBackdrop(RetailTowerFloor retail) =>
            BuildRetailBackdrop(retail.BackdropPackage, retail.BackdropSpeed);

        private bool BuildRetailBackdrop(int package, int speed)
        {
            var texture = AssetMode.Load<Texture2D>($"Tower/Retail/backdrop_{package:00}");
            var shader = RetailShader("RetailTowerBackdrop");
            if (!texture || !shader) return false;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (_retailBackdropMaterial) Destroy(_retailBackdropMaterial);
            _retailBackdropMaterial = new Material(shader) { name = "Retail tower backdrop", mainTexture = texture };
            _retailBackdropSpeed = speed;
            _retailBackdropMode = InSecondTower ? 2 : 0;
            _retailBackdropTicks = 0;
            _retailBackdropClock = 0f;
            _retailBackdropMesh = new Mesh { name = "Retail tower backdrop" };
            _retailBackdropMesh.MarkDynamic();
            _ownedManagedFloorMeshes.Add(_retailBackdropMesh);
            var backdrop = new GameObject("Retail tower backdrop");
            backdrop.transform.SetParent(transform, false);
            backdrop.AddComponent<MeshFilter>().sharedMesh = _retailBackdropMesh;
            var renderer = backdrop.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _retailBackdropMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _generated.Add(backdrop);
            _towerBackdrop = backdrop.transform;
            RebuildRetailBackdrop();
            return true;
        }

        /// <summary>
        /// project_depth_strips_and_quads: 14 strips of 0xE00 units on a radius 6*4096 cylinder, two 32 px quads per
        /// 0x80 angle step; texture columns alternate per step, rows per strip; strips darken with depth.
        /// Native radius/height are preserved; the background shader bypasses the world far clip.
        /// </summary>
        private void RebuildRetailBackdrop()
        {
            if (!_retailBackdropMesh) return;
            const float scale = RetailUnit;
            // +0x06 is angular phase, +0x08 is vertical phase. Normal modes
            // update only the angle; mode 2 scrolls vertically by 0x40 per tick.
            int verticalPhase = _retailBackdropMode == 2 ? (_retailBackdropTicks % 112) * 0x40 : 0;
            int phase = verticalPhase;
            bool shadeOffset = verticalPhase >= 0xE00;
            var vertices = new List<Vector3>(RetailBackdropSegments * 2 * RetailBackdropStrips * 4);
            var uvs = new List<Vector2>(vertices.Capacity);
            var colors = new List<Color32>(vertices.Capacity);
            var indices = new List<int>(RetailBackdropSegments * 2 * RetailBackdropStrips * 6);
            float radius = RetailBackdropRadius * scale;
            for (int strip = 0; strip < RetailBackdropStrips; strip++)
            {
                int top = (short)(phase - 0x6200 + RetailStripHeight * (strip + 1));
                int bottom = (short)(phase - 0x5400 + RetailStripHeight * (strip + 1));
                int shadeHeight = (short)(verticalPhase - 0x6200 + RetailStripHeight * (strip + 1));
                int depth = shadeHeight % RetailStripHeight / 256 + (shadeOffset ? 16 : 0);
                int shadeTop = Mathf.Clamp(0xE0 - (depth + (strip << 4)), 0, 0x80);
                int shadeBottom = Mathf.Clamp(0xE0 - (depth + (strip << 4)) - 0x10, 0, 0x80);
                byte topByte = (byte)Mathf.Min(255, shadeTop * 2), bottomByte = (byte)Mathf.Min(255, shadeBottom * 2);
                // The object's +0x4000 height is a transform, not part of the
                // signed 16-bit strip coordinates (adding before casting wraps).
                float yTop = -(top + 0x4000) * scale, yBottom = -(bottom + 0x4000) * scale;
                float v0 = (strip & 1) * 64, v1 = v0 + 64;
                for (int segment = 0; segment < RetailBackdropSegments; segment++)
                {
                    int textureIndex = segment & 1;
                    for (int half = 0; half < 2; half++)
                    {
                        float a0 = (segment * 2 + half) * Mathf.PI * 2f / (RetailBackdropSegments * 2);
                        float a1 = (segment * 2 + half + 1) * Mathf.PI * 2f / (RetailBackdropSegments * 2);
                        float u0 = textureIndex * 64 + half * 32, u1 = u0 + 32;
                        int start = vertices.Count;
                        vertices.Add(new Vector3(Mathf.Cos(a0) * radius, yTop, Mathf.Sin(a0) * radius));
                        vertices.Add(new Vector3(Mathf.Cos(a1) * radius, yTop, Mathf.Sin(a1) * radius));
                        vertices.Add(new Vector3(Mathf.Cos(a1) * radius, yBottom, Mathf.Sin(a1) * radius));
                        vertices.Add(new Vector3(Mathf.Cos(a0) * radius, yBottom, Mathf.Sin(a0) * radius));
                        uvs.Add(new Vector2((u0 + 0.5f) / 128f, 1f - (v0 + 0.5f) / 128f));
                        uvs.Add(new Vector2((u1 - 0.5f) / 128f, 1f - (v0 + 0.5f) / 128f));
                        uvs.Add(new Vector2((u1 - 0.5f) / 128f, 1f - (v1 - 0.5f) / 128f));
                        uvs.Add(new Vector2((u0 + 0.5f) / 128f, 1f - (v1 - 0.5f) / 128f));
                        colors.Add(new Color32(topByte, topByte, topByte, 255));
                        colors.Add(new Color32(topByte, topByte, topByte, 255));
                        colors.Add(new Color32(bottomByte, bottomByte, bottomByte, 255));
                        colors.Add(new Color32(bottomByte, bottomByte, bottomByte, 255));
                        indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                        indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
                    }
                }
            }
            _retailBackdropMesh.Clear();
            _retailBackdropMesh.SetVertices(vertices);
            _retailBackdropMesh.SetUVs(0, uvs);
            _retailBackdropMesh.SetColors(colors);
            _retailBackdropMesh.SetTriangles(indices, 0);
            _retailBackdropMesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 4f);
        }

        // Native angular units are 1/4096 turn. Only the low byte is read by
        // process_circular_segments, matching the texture's two-segment repeat.
        private void UpdateRetailBackdrop()
        {
            if (!_towerBackdrop || !_retailBackdropMesh) return;
            AdvanceRetailBackdrop(Time.deltaTime);
        }

        private void AdvanceRetailBackdrop(float deltaTime)
        {
            _retailBackdropClock += Mathf.Max(0f, deltaTime) * RetailFrameRate;
            int ticks = Mathf.FloorToInt(_retailBackdropClock);
            if (ticks == 0) return;
            _retailBackdropClock -= ticks;
            _retailBackdropTicks = (_retailBackdropTicks + ticks) % (128 * 112);
            // Keep the native world origin. Following the camera removed retail parallax.
            _towerBackdrop.localPosition = Vector3.zero;
            float angle = _retailBackdropMode == 2 ? 0f :
                ((_retailBackdropTicks * _retailBackdropSpeed) & 0xFF) * (360f / 4096f);
            _towerBackdrop.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (_retailBackdropMode == 2) RebuildRetailBackdrop();
        }
    }
}
