using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        // Floor tile sheets load on demand: see TowerGameController.TowerTileMaterials.cs.

        // Coordinates are 32 px cells in the retail sheets. The right-hand block is the backdrop.
        private static readonly (Vector2Int Room, Vector2Int Passage, Vector2Int Wall)[] UpperTowerTiles =
        {
            (new(1, 0), new(3, 4), new(1, 4)), // 12–13
            (new(2, 0), new(2, 2), new(2, 6)), // 14–15
            (new(1, 0), new(2, 0), new(0, 4)), // 16–17
            (new(2, 0), new(2, 1), new(1, 4)), // 18–19
            (new(1, 4), new(1, 5), new(0, 4)), // 20–21
            (new(1, 2), new(2, 2), new(1, 5)), // 22–23
            (new(1, 4), new(2, 4), new(0, 4)), // 24–25
            (new(2, 4), new(3, 4), new(0, 4)), // 26–27
            (new(2, 2), new(2, 3), new(0, 4)), // 28–29
            (new(1, 2), new(2, 4), new(0, 4)), // 30–31
            (new(1, 0), new(2, 0), new(2, 5)), // 32–33
            (new(1, 0), new(2, 1), new(0, 4)), // 34–35
            (new(1, 0), new(2, 0), new(0, 4)), // 36–37
            (new(1, 0), new(0, 0), new(0, 4))  // 38–39
        };
        private readonly List<Mesh> _ownedManagedFloorMeshes = new List<Mesh>();

        private GameObject _managedFloorMesh, _managedWallMesh;
        private readonly Dictionary<Material, Material> _wallVisibilityMaterials = new();
        private Material WallVisibilityMaterial(Material source)
        {
            if (!source) return source;
            if (_wallVisibilityMaterials.TryGetValue(source, out var existing) && existing) return existing;
            var shader = AssetMode.Load<Shader>("Shaders/KohForegroundWall");
            if (!shader) return source;
            var material = new Material(shader) { name = source.name + " (foreground fade)", mainTexture = source.mainTexture };
            material.mainTextureScale = source.mainTextureScale;
            material.mainTextureOffset = source.mainTextureOffset;
            if (source.HasProperty("_Color")) material.color = source.color;
            _wallVisibilityMaterials[source] = material;
            return material;
        }
        private const float TowerWallHeight = 2.2f;
        private const float CutawayWallHeight = 0.16f;
        private const int TowerBackdropTextureSize = 128;
        private const int TowerBackdropScrollWrapPixels = 32;
        private const int TowerBackdropSegments = TowerBackdropTextureSize;
        private const float RetailBackdropScrollPixelsPerFrame = 2f;
        private const float RetailFrameRate = 30f;
        private const float TowerBackdropSpeedScale = 0.02f;
        private Transform _towerBackdrop;
        private List<Vector3> _towerWallVertices;
        private readonly List<int> _towerCutawayFaces = new();
        private Mesh _towerCutawayMesh;
        private readonly List<Vector3> _towerCutawayTargets = new(3);

        private void UpdateTowerWallCutaway()
        {
            if (!_managedWallMesh || !_towerCutawayMesh) return;
            var wallSpace = _managedWallMesh.transform;
            var eye = wallSpace.InverseTransformPoint(_gameCamera.transform.position);
            _towerCutawayTargets.Clear();
            AddTarget(_player);
            AddTarget(_familiar);
            AddTarget(_blueCollarAlly?.View);

            void AddTarget(GameObject actor)
            {
                if (!actor || !actor.activeInHierarchy) return;
                var feet = actor.transform.position - Vector3.up * (TownActorFootOffset - 0.08f);
                _towerCutawayTargets.Add(wallSpace.InverseTransformPoint(feet));
            }
            var shoulder = wallSpace.InverseTransformVector(_gameCamera.transform.right * 0.48f);
            bool changed = false;
            foreach (int index in _towerCutawayFaces)
            {
                var a = _towerWallVertices[index];
                var b = _towerWallVertices[index + 3];
                bool cutaway = false;
                foreach (var center in _towerCutawayTargets)
                {
                    cutaway = WallCrossesView(eye, center, a, b) ||
                        WallCrossesView(eye, center - shoulder, a, b) ||
                        WallCrossesView(eye, center + shoulder, a, b);
                    if (cutaway) break;
                }
                float height = cutaway ? CutawayWallHeight : TowerWallHeight;
                var top = a + Vector3.up * height;
                if (_towerWallVertices[index + 1] == top) continue;
                _towerWallVertices[index + 1] = top;
                _towerWallVertices[index + 2] = b + Vector3.up * height;
                changed = true;
            }
            if (changed) _towerCutawayMesh.SetVertices(_towerWallVertices);
        }

        private static bool WallCrossesView(Vector3 eye, Vector3 target, Vector3 a, Vector3 b)
        {
            // Intersect the sight line with the wall plane, then test the finite face.
            bool constantX = a.x == b.x;
            float along = constantX ? target.x - eye.x : target.z - eye.z;
            if (Mathf.Abs(along) < 0.0001f) return false;
            float t = (constantX ? a.x - eye.x : a.z - eye.z) / along;
            if (t <= 0f || t >= 1f) return false;
            var hit = Vector3.LerpUnclamped(eye, target, t);
            float across = constantX ? hit.z : hit.x;
            float start = constantX ? a.z : a.x;
            float end = constantX ? b.z : b.x;
            return across >= Mathf.Min(start, end) && across <= Mathf.Max(start, end) &&
                hit.y >= a.y - 0.05f && hit.y <= a.y + TowerWallHeight;
        }

        private void LevelTowerFloor()
        {
            if (!UsesManagedTowerLayout || _towerFloorLayout?.Heights == null || InTowerTutorial) return;
            bool uneven = false;
            foreach (var cell in _towerFloorLayout.OpenCells)
            {
                if (TowerTileHeight(cell) == 0) continue;
                uneven = true;
                break;
            }
            if (!uneven) return;

            _trapTerrainChanges.Clear();
            ApplyTowerHeightChanges(_ => 0);
        }

        private void ApplyTowerHeightChanges(System.Func<Vector2Int, int> newHeight)
        {
            void MoveWithTerrain(GameObject view, Vector2Int cell)
            {
                if (view) view.transform.position += Vector3.up * ((newHeight(cell) - TowerTileHeight(cell)) / 64f);
            }

            MoveWithTerrain(_player, _playerCell);
            MoveWithTerrain(_familiar, _familiarCell);
            if (_elevatorPlatform) MoveWithTerrain(_elevatorPlatform.gameObject, _stairCell);
            if (_hasLoot) MoveWithTerrain(_salvageMarker, _lootCell);
            foreach (var creature in _creatures) MoveWithTerrain(creature.View, creature.Cell);
            foreach (var ally in _thrownEggs) MoveWithTerrain(ally.View, ally.Cell);
            foreach (var pair in _itemMarkers) MoveWithTerrain(pair.Value, pair.Key);
            foreach (var pair in _trapMarkers) MoveWithTerrain(pair.Value, pair.Key);
            foreach (var pair in _sacredTiles) MoveWithTerrain(pair.Value.View, pair.Key);
            foreach (var block in _iceBlocks) MoveWithTerrain(block.view, block.cell);
            foreach (var visitor in new[] { _towerEventGhosh, _towerEventSelfi })
            {
                if (!visitor) continue;
                var position = visitor.transform.position;
                MoveWithTerrain(visitor.gameObject, new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z)));
            }

            var retail = _towerFloorLayout.Retail;
            foreach (var cell in _towerFloorLayout.OpenCells)
            {
                int height = newHeight(cell);
                if (retail != null)
                {
                    // Retail cells carry their own height value; move the cell's tile with the terrain change.
                    int index = retail.Index(cell.x, cell.y);
                    retail.Value[index] = (short)(retail.Value[index] + height - _towerFloorLayout.GroundHeight(cell.x, cell.y));
                }
                _towerFloorLayout.SetHeight(cell.x, cell.y, height);
            }
            RemoveSurface(_managedFloorMesh);
            RemoveSurface(_managedWallMesh);
            BuildManagedFloorMeshes(includeBackdrop: false);
            if (_carriedView) PresentCarry(1f);

            void RemoveSurface(GameObject surface)
            {
                if (!surface) return;
                var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
                _ownedManagedFloorMeshes.Remove(mesh);
                surface.SetActive(false);
                RemoveGenerated(surface);
                Destroy(mesh);
            }
        }

        private void OnDestroy()
        {
            if (_streamedTitleBackground) Destroy(_streamedTitleBackground);
            if (_retailTitleLensMaterial) Destroy(_retailTitleLensMaterial);
            if (_windowSkin) Destroy(_windowSkin);
            ReleaseAreaMusic();
            ReleaseStreamedPortraits();
            ReleaseStreamedTextures();
            ClearGenerated();
            if (_sacredTileSprite) Destroy(_sacredTileSprite);
            if (_towerTrapSprite) Destroy(_towerTrapSprite);
            foreach (var sprite in _trapSprites.Values) if (sprite) Destroy(sprite);
            foreach (var definition in _hatchedDefinitions.Values) if (definition) Destroy(definition);
            if (_summoningFrames != null)
                foreach (var sprite in _summoningFrames) if (sprite) Destroy(sprite);
            if (_pullBagFrames != null)
                foreach (var sprite in _pullBagFrames) if (sprite) Destroy(sprite);
            if (_closeBagFrames != null)
                foreach (var sprite in _closeBagFrames) if (sprite) Destroy(sprite);
            if (_elevatorSide) Destroy(_elevatorSide);
            if (_elevatorCrystalMesh) Destroy(_elevatorCrystalMesh);
            if (_elevatorFrames != null)
                foreach (var sprite in _elevatorFrames) if (sprite) Destroy(sprite);
            if (_spellOrbSprite) Destroy(_spellOrbSprite);
            if (_spellOrbTexture) Destroy(_spellOrbTexture);
            if (_swordMixtureFrames != null)
                foreach (var frame in _swordMixtureFrames) if (frame) Destroy(frame);
            if (_swordMixtureAtlas) Destroy(_swordMixtureAtlas);
            DisposeTowerEntranceArt();
        }

        private void BuildManagedFloorMeshes(bool includeBackdrop = true)
        {
            if (UsesRetailFloorMesh)
            {
                BuildRetailFloorMeshes(includeBackdrop);
                if (_managedFloorMesh) return;
            }
            int upperIndex = _floor is >= 12 and <= 39 ? (_floor - 12) / 2 : -1;
            Material retailTiles = TowerTilesForFloor(_floor);
            bool retail = retailTiles != null;
            var floorVertices = new List<Vector3>();
            var floorIndices = new List<int>();
            var floorUvs = new List<Vector2>();
            var wallVertices = new List<Vector3>();
            var wallIndices = new List<int>();
            var wallUvs = new List<Vector2>();
            _towerCutawayFaces.Clear();
            foreach (var cell in _towerFloorLayout.OpenCells)
            {
                float x = cell.x, z = cell.y;
                float y = TowerGroundY(cell);
                float floorSurface = cell == _towerFloorLayout.StairsUpCell && HasRetailElevatorArt && CanAscendFromFloor
                    ? y - 0.035f : y;
                bool corridor = _towerItemTraps.IsCorridor(cell);
                bool openCorridorEdge = corridor && _floor != 2 && !InTowerTutorial;
                AppendFloorQuad(floorVertices, floorIndices, floorUvs,
                    new Vector3(x - 0.5f, floorSurface, z - 0.5f), new Vector3(x - 0.5f, floorSurface, z + 0.5f),
                    new Vector3(x + 0.5f, floorSurface, z + 0.5f), new Vector3(x + 0.5f, floorSurface, z - 0.5f));
                if (retail)
                {
                    bool alternate = ((cell.x + cell.y) & 1) != 0;
                    if (_floor <= 2) SetTowerTileUvs(floorUvs, alternate ? 128 : 68, alternate ? 264 : 324, 24, 384);
                    else if (_floor == 3) SetTowerTileUvs(floorUvs, corridor ? 34 : 66, corridor || alternate ? 2 : 34, 28, 256);
                    else if (_floor <= 5) SetTowerTileUvs(floorUvs, corridor ? 34 : 66, 2, 28, 256);
                    else if (_floor <= 7) SetTowerTileUvs(floorUvs, corridor ? 66 : 34, corridor ? 66 : 2, 28, 256);
                    else if (_floor <= 9) SetTowerTileUvs(floorUvs, alternate ? 34 : 66, 2, 28, 256);
                    else if (upperIndex < 0) SetTowerTileUvs(floorUvs, alternate ? 66 : 34, 34, 28, 256);
                    else
                    {
                        var tiles = UpperTowerTiles[upperIndex];
                        var tile = corridor ? tiles.Passage : tiles.Room;
                        SetTowerTileUvs(floorUvs, tile.x * 32, tile.y * 32, 32, 256);
                    }
                }
                void AddEdge(Vector2Int direction, Vector3 a, Vector3 b)
                {
                    var next = cell + direction;
                    if (IsTowerEntranceBoundary(cell, direction)) return;
                    bool neighborOpen = _towerFloorLayout.IsWalkable(next.x, next.y);
                    if (!neighborOpen && openCorridorEdge)
                    {
                        var skirt = Vector3.down * 0.45f;
                        AppendBoundary(wallVertices, wallIndices, wallUvs, b + skirt, a + skirt, 0.45f);
                        return;
                    }
                    float height = neighborOpen ? TowerGroundY(next) - y : TowerWallHeight;
                    if (height <= 0f) return;
                    if (!neighborOpen) _towerCutawayFaces.Add(wallVertices.Count);
                    AppendBoundary(wallVertices, wallIndices, wallUvs, a, b, height);
                }
                AddEdge(Vector2Int.up, new Vector3(x - 0.5f, y, z + 0.5f), new Vector3(x + 0.5f, y, z + 0.5f));
                AddEdge(Vector2Int.right, new Vector3(x + 0.5f, y, z + 0.5f), new Vector3(x + 0.5f, y, z - 0.5f));
                AddEdge(Vector2Int.down, new Vector3(x + 0.5f, y, z - 0.5f), new Vector3(x - 0.5f, y, z - 0.5f));
                AddEdge(Vector2Int.left, new Vector3(x - 0.5f, y, z - 0.5f), new Vector3(x - 0.5f, y, z + 0.5f));
            }
            if (retail)
            {
                for (int index = 0; index < wallUvs.Count; index += 4)
                {
                    if (_floor <= 2) SetTowerTileUvs(wallUvs, 34, 130, 24, 384, index);
                    else if (_floor == 3) SetTowerTileUvs(wallUvs, 33, 194, 28, 256, index);
                    else if (_floor <= 5) SetTowerTileUvs(wallUvs, 130, 130, 30, 256, index);
                    else if (_floor <= 7) SetTowerTileUvs(wallUvs, 34, 66, 28, 256, index);
                    else if (_floor <= 9) SetTowerTileUvs(wallUvs, 34, 98, 28, 256, index);
                    else if (upperIndex < 0) SetTowerTileUvs(wallUvs, 2, 66, 28, 256, index);
                    else
                    {
                        var tile = UpperTowerTiles[upperIndex].Wall;
                        SetTowerTileUvs(wallUvs, tile.x * 32, tile.y * 32, 32, 256, index);
                    }
                }
            }
            _managedFloorMesh = CreateManagedMesh("Generated Tower Floor Mesh", floorVertices, floorIndices, floorUvs,
                retail ? retailTiles : _groundMaterial, true);
            _managedWallMesh = CreateManagedMesh("Generated Tower Wall Mesh", wallVertices, wallIndices, wallUvs,
                WallVisibilityMaterial(retail ? retailTiles : _wallMaterial), false);
            _towerWallVertices = wallVertices;
            _towerCutawayMesh = _managedWallMesh.GetComponent<MeshFilter>().sharedMesh;
            _towerCutawayMesh.MarkDynamic();
            if (includeBackdrop && retail) BuildTowerBackdrop(retailTiles);
        }

        private void BuildTowerBackdrop(Material material)
        {
            // Tutorial/legacy geometry uses the same retail background as its floor.
            int package = _floor <= 2 ? 2 : _floor == TowerFloorRules.BeldoFloor ? 1 : RetailTowerFloor.FloorVariant(_floor) + 3;
            if (BuildRetailBackdrop(package, _floor == TowerFloorRules.BeldoFloor ? 4 : 2)) return;
            var openCells = _towerFloorLayout?.OpenCells;
            if (!material || openCells == null || openCells.Count == 0) return;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;
            float baseY = float.PositiveInfinity;
            foreach (var cell in openCells)
            {
                minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x);
                minZ = Mathf.Min(minZ, cell.y); maxZ = Mathf.Max(maxZ, cell.y);
                baseY = Mathf.Min(baseY, TowerGroundY(cell));
            }

            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;
            // Generated layouts can expand close to the grid boundary. Keep the
            // enclosing wall well beyond their outermost walkable cells.
            float radius = Mathf.Max(maxX - minX, maxZ - minZ) * 0.5f + 12f;
            const float wallBottom = -3.5f;
            const float wallTop = 18.5f;
            const float textureRepeatHeight = 8f;
            const int bowlBands = 12;
            const float bowlDepth = 8f;

            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var uvs = new List<Vector2>();
            float atlasHeight = material.mainTexture ? material.mainTexture.height : 256f;
            float atlasWidth = material.mainTexture ? material.mainTexture.width : 256f;
            float left = (128f + 0.5f) / atlasWidth;
            float right = (128f + TowerBackdropTextureSize - 0.5f) / atlasWidth;
            float top = 1f - (128f + 0.5f) / atlasHeight;
            float bottom = 1f - (128f + TowerBackdropTextureSize - 0.5f) / atlasHeight;
            int verticalBands = Mathf.CeilToInt((wallTop - wallBottom) / textureRepeatHeight);

            for (int band = 0; band < verticalBands; band++)
            {
                float y0 = wallBottom + band * textureRepeatHeight;
                float y1 = Mathf.Min(wallBottom + (band + 1) * textureRepeatHeight, wallTop);
                for (int segment = 0; segment < TowerBackdropSegments; segment++)
                {
                    float a0 = segment * (Mathf.PI * 2f / TowerBackdropSegments);
                    float a1 = (segment + 1) * (Mathf.PI * 2f / TowerBackdropSegments);
                    int start = vertices.Count;
                    // Winding faces inward so the camera sees the tower wall from inside.
                    vertices.Add(new Vector3(Mathf.Sin(a0) * radius, y0, Mathf.Cos(a0) * radius));
                    vertices.Add(new Vector3(Mathf.Sin(a0) * radius, y1, Mathf.Cos(a0) * radius));
                    vertices.Add(new Vector3(Mathf.Sin(a1) * radius, y1, Mathf.Cos(a1) * radius));
                    vertices.Add(new Vector3(Mathf.Sin(a1) * radius, y0, Mathf.Cos(a1) * radius));
                    indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                    indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);

                    float u0 = Mathf.Lerp(left, right, segment / (float)TowerBackdropSegments);
                    float u1 = Mathf.Lerp(left, right, (segment + 1f) / TowerBackdropSegments);
                    uvs.Add(new Vector2(u0, bottom));
                    uvs.Add(new Vector2(u0, top));
                    uvs.Add(new Vector2(u1, top));
                    uvs.Add(new Vector2(u1, bottom));
                }
            }

            // Continue the tower shell below the wall as a concave bowl. The rim
            // joins the wall's lower edge and the center dips beneath the floor,
            // so the camera sees a closed environment instead of the outside void.
            for (int band = 0; band < bowlBands; band++)
            {
                float r0 = radius * band / bowlBands;
                float r1 = radius * (band + 1) / bowlBands;
                float y0 = wallBottom - bowlDepth * (1f - r0 / radius) * (1f - r0 / radius);
                float y1 = wallBottom - bowlDepth * (1f - r1 / radius) * (1f - r1 / radius);
                float v0 = Mathf.Lerp(bottom, top, band / (float)bowlBands);
                float v1 = Mathf.Lerp(bottom, top, (band + 1f) / bowlBands);
                for (int segment = 0; segment < TowerBackdropSegments; segment++)
                {
                    float a0 = segment * (Mathf.PI * 2f / TowerBackdropSegments);
                    float a1 = (segment + 1) * (Mathf.PI * 2f / TowerBackdropSegments);
                    int start = vertices.Count;
                    vertices.Add(new Vector3(Mathf.Sin(a0) * r0, y0, Mathf.Cos(a0) * r0));
                    vertices.Add(new Vector3(Mathf.Sin(a0) * r1, y1, Mathf.Cos(a0) * r1));
                    vertices.Add(new Vector3(Mathf.Sin(a1) * r1, y1, Mathf.Cos(a1) * r1));
                    vertices.Add(new Vector3(Mathf.Sin(a1) * r0, y0, Mathf.Cos(a1) * r0));
                    indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                    indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);

                    float u0 = Mathf.Lerp(left, right, segment / (float)TowerBackdropSegments);
                    float u1 = Mathf.Lerp(left, right, (segment + 1f) / TowerBackdropSegments);
                    uvs.Add(new Vector2(u0, v0));
                    uvs.Add(new Vector2(u0, v1));
                    uvs.Add(new Vector2(u1, v1));
                    uvs.Add(new Vector2(u1, v0));
                }
            }

            var backdrop = CreateManagedMesh("Tower backdrop", vertices, indices, uvs, material, false);
            backdrop.transform.localPosition = new Vector3(centerX, baseY, centerZ);
            _towerBackdrop = backdrop.transform;
        }

        private void UpdateTowerBackdropRotation()
        {
            if (!_towerBackdrop || _area != Area.Tower) return;
            if (_retailBackdropMesh)
            {
                UpdateRetailBackdrop();
                UpdateRetailWater();
                return;
            }
            // Retail floor setup calls create_wrapped_texture_scroll(2). Its source
            // offset wraps with a 0x1F mask, so two pixels advance through a 32 px
            // cycle on each 30 Hz game frame. Map that cycle to one full wall turn.
            float degreesPerSecond = 360f * RetailBackdropScrollPixelsPerFrame * RetailFrameRate /
                TowerBackdropScrollWrapPixels * TowerBackdropSpeedScale;
            _towerBackdrop.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
        }

        private static void SetTowerTileUvs(List<Vector2> uvs, int x, int y, int size, int atlasHeight, int start = -1)
        {
            if (start < 0) start = uvs.Count - 4;
            float left = (x + 0.5f) / 256f, right = (x + size - 0.5f) / 256f;
            float top = 1f - (y + 0.5f) / atlasHeight, bottom = 1f - (y + size - 0.5f) / atlasHeight;
            uvs[start] = new Vector2(left, bottom);
            uvs[start + 1] = new Vector2(left, top);
            uvs[start + 2] = new Vector2(right, top);
            uvs[start + 3] = new Vector2(right, bottom);
        }

        private static void AppendBoundary(List<Vector3> vertices, List<int> indices, List<Vector2> uvs,
            Vector3 a, Vector3 b, float wallHeight)
        {
            Vector3 height = Vector3.up * wallHeight;
            AppendFloorQuad(vertices, indices, uvs, a, a + height, b + height, b);
        }

        private static void AppendFloorQuad(List<Vector3> vertices, List<int> indices, List<Vector2> uvs,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.up); uvs.Add(Vector2.one); uvs.Add(Vector2.right);
        }

        private GameObject CreateManagedMesh(string objectName, List<Vector3> vertices, List<int> indices,
            List<Vector2> uvs, Material material, bool floorCollider)
        {
            var mesh = new Mesh { name = objectName };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _ownedManagedFloorMeshes.Add(mesh);
            var instance = new GameObject(objectName);
            instance.transform.SetParent(transform, true);
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            instance.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (floorCollider) instance.AddComponent<MeshCollider>().sharedMesh = mesh;
            _generated.Add(instance);
            return instance;
        }
    }
}
