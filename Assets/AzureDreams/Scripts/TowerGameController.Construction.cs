using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string CarpenterInterior = "Interior_13_carpenter";
        private const string ConstructionResources = "Town/Retail/Construction/";
        private const string ConstructionKey = "town.construction.";

        private sealed class BuildingProject
        {
            public readonly string Id, English, Portuguese, Interior, Lot;
            public readonly int Price;
            public readonly int HutRank;
            public readonly Rect Bounds, Footprint;
            public readonly Vector2 Entrance;

            public BuildingProject(string id, string english, string portuguese, int price,
                string interior = null, Rect bounds = default, Rect footprint = default, Vector2 entrance = default,
                string lot = null, int hutRank = 0)
            {
                Id = id; English = english; Portuguese = portuguese; Price = price;
                Interior = interior; Bounds = bounds; Footprint = footprint; Entrance = entrance; Lot = lot ?? id;
                HutRank = hutRank;
            }

            public string Name => T(English, Portuguese);
        }

        private static readonly BuildingProject[] BuildingProjects =
        {
            new("home_1", "Expand Koh's house I", "Ampliar a casa do Koh I", 6000),
            new("home_2", "Expand Koh's house II", "Ampliar a casa do Koh II", 60000, "KohHome",
                Rect.MinMaxRect(48, 44, 63, 58), Rect.MinMaxRect(50, 47, 61, 53), new(54, 53), "home"),
            new("hut_1", "Expand the monster hut to 3×3", "Ampliar o alojamento para 3×3", 4000, hutRank: 1),
            new("hut_2", "Expand the monster hut to 4×4", "Ampliar o alojamento para 4×4", 6000, hutRank: 2),
            new("hut_3", "Expand the monster hut to 6×6", "Ampliar o alojamento para 6×6", 20000, hutRank: 3),
            new("hut_4", "Expand the monster hut to 8×8", "Ampliar o alojamento para 8×8", 60000, hutRank: 4),
            new("hospital", "Renovate the hospital", "Reformar o hospital", 4000, "Interior_10_hospital_2",
                Rect.MinMaxRect(8, 62, 23, 72), Rect.MinMaxRect(11, 65, 21, 69), new(16, 69.25f)),
            new("temple", "Renovate the temple", "Reformar o templo", 4000, "Interior_12_temple_2",
                Rect.MinMaxRect(64, 27, 84, 44), Rect.MinMaxRect(66, 31, 82, 41), new(74, 41)),
            new("library", "Build the library", "Construir a biblioteca", 8000, "Interior_07_library",
                Rect.MinMaxRect(90, 30, 102, 41), Rect.MinMaxRect(92, 35, 100, 38), new(96, 38)),
            new("fountain", "Build the fountain", "Construir a fonte", 2000, null,
                Rect.MinMaxRect(55, 64, 73, 80), Rect.MinMaxRect(60, 69, 66, 74)),
            new("alley", "Build the bowling alley", "Construir o boliche", 40000, "Interior_0c_alley",
                Rect.MinMaxRect(42, 29, 54, 41), Rect.MinMaxRect(43, 33, 53, 37), new(48, 37)),
            new("gym", "Build the gym", "Construir a academia", 30000, "Interior_08_gym",
                Rect.MinMaxRect(25, 88, 38, 98), Rect.MinMaxRect(27, 91, 37, 95), new(32, 95)),
            new("circuit", "Build the racetrack", "Construir a pista de corrida", 30000, "Interior_0a_racetrack_entrance",
                Rect.MinMaxRect(7, 23, 25, 44), Rect.MinMaxRect(9, 25, 24, 40), new(16, 40)),
            new("theater", "Build the theater", "Construir o teatro", 8000, "Interior_0b_theater",
                Rect.MinMaxRect(26, 28, 38, 40), Rect.MinMaxRect(27, 32, 37, 36), new(32, 36)),
            new("casino", "Build the casino", "Construir o cassino", 100000, "Interior_2b_casino_1",
                Rect.MinMaxRect(91, 88, 102, 98), Rect.MinMaxRect(92, 91.5f, 100, 95), new(96, 95)),
            new("arcade", "Build the amusement center", "Construir o centro de diversão", 3000, "Interior_09_arcade_1",
                Rect.MinMaxRect(104, 62, 119, 73), Rect.MinMaxRect(107, 65, 117, 69), new(112, 69))
        };

        private const int ConstructionRows = 5;
        private readonly List<BuildingProject> _builtTownLots = new();
        private readonly List<BuildingProject> _availableConstructionProjects = new();
        private readonly Dictionary<int, float> _constructionGround = new();
        private readonly List<UnityEngine.Object> _constructionGeometry = new();
        private bool _constructionOpen, _constructionConfirm;
        private int _constructionRow;
        private string _constructionMessage;
        private bool InCarpenterShop => _interior && _interior.interiorId == CarpenterInterior;

        private static bool ProjectBuilt(BuildingProject project) => ProjectBuilt(project, CurrentHomeRank, CurrentHutRank,
            TownSaveVariables.GetBool(ConstructionKey + project.Id));

        private static bool ProjectBuilt(BuildingProject project, int homeRank, int hutRank, bool completed) => project.Id switch
        {
            "home_1" => homeRank >= 1,
            "home_2" => homeRank >= 2,
            _ => project.HutRank > 0 ? hutRank >= project.HutRank : completed
        };

        private static string ProjectRequirement(BuildingProject project) => project.Id switch
        {
            "home_2" or "temple" when CurrentHomeRank == 0 =>
                T("Expand Koh's house first.", "Amplie a casa do Koh primeiro."),
            "alley" when !TownSaveVariables.GetBool(ConstructionKey + "temple") =>
                T("Renovate the temple first.", "Reforme o templo primeiro."),
            "casino" when !TownSaveVariables.GetBool(ConstructionKey + "alley") =>
                T("Build the bowling alley first.", "Construa o boliche primeiro."),
            _ when project.HutRank > CurrentHutRank + 1 =>
                T("Order the previous hut expansion first.", "Encomende a ampliação anterior do alojamento primeiro."),
            _ => null
        };

        private void OpenCarpenter()
        {
            _constructionOpen = true;
            _constructionConfirm = false;
            _constructionRow = 0;
            _constructionMessage = null;
            RefreshConstructionProjects();
        }

        private void RefreshConstructionProjects()
        {
            _availableConstructionProjects.Clear();
            foreach (var project in BuildingProjects)
                if (!ProjectBuilt(project) && ProjectRequirement(project) == null)
                    _availableConstructionProjects.Add(project);

            _constructionRow = Mathf.Clamp(_constructionRow, 0, Mathf.Max(0, _availableConstructionProjects.Count - 1));
        }

        private void BackFromCarpenter()
        {
            PlayRetailSound(RetailSfx.Cancel);
            if (_constructionConfirm) _constructionConfirm = false;
            else _constructionOpen = false;
            _constructionMessage = null;
        }

        private bool HandleCarpenterInput()
        {
            if (!_constructionOpen) return false;
            if (!InCarpenterShop) { _constructionOpen = false; return false; }
            if (_cancel?.WasPressedThisFrame() == true) BackFromCarpenter();
            else if (_interact?.WasPressedThisFrame() == true)
            {
                if (_constructionConfirm) ConfirmConstruction();
                else ChooseConstruction(_constructionRow);
            }
            else if (!_constructionConfirm && _availableConstructionProjects.Count > 0 && Keyboard.current is { } keys)
            {
                int step = keys.upArrowKey.wasPressedThisFrame ? -1 : keys.downArrowKey.wasPressedThisFrame ? 1 : 0;
                int next = Mathf.Clamp(_constructionRow + step, 0, _availableConstructionProjects.Count - 1);
                if (next != _constructionRow) PlayRetailSound(RetailSfx.Cursor);
                _constructionRow = next;
            }
            return true;
        }

        private void ChooseConstruction(int row)
        {
            if (row < 0 || row >= _availableConstructionProjects.Count) return;
            _constructionRow = row;
            var project = _availableConstructionProjects[row];
            _constructionMessage = ProjectBuilt(project) ? T("Already built.", "Obra já concluída.") : ProjectRequirement(project);
            _constructionConfirm = _constructionMessage == null;
            if (_constructionConfirm) PlayRetailSound(RetailSfx.Confirm);
        }

        private void ConfirmConstruction()
        {
            if (!_constructionConfirm || !InCarpenterShop) return;
            var project = _availableConstructionProjects[_constructionRow];
            if (ProjectBuilt(project) || ProjectRequirement(project) != null) return;
            if (_gold < project.Price)
            {
                _constructionMessage = T("You don't have enough gold.", "Você não tem ouro suficiente.");
                return;
            }
            string scene = project.HutRank > 0 ? HutScenes[project.HutRank] :
                project.Id == "home_1" ? HomeScenes[1] : project.Interior == "KohHome" ? HomeScenes[2] : project.Interior;
            if ((project.Bounds.width > 0 && !Resources.Load<TextAsset>(ConstructionResources + project.Lot)) ||
                (scene != null && !Application.CanStreamedLevelBeLoaded("Assets/AzureDreams/Scenes/Interiors/" + scene + ".unity")))
            {
                _constructionMessage = T("This project is unavailable in this build.", "Esta obra não está disponível nesta versão.");
                return;
            }
            if (project.Id == "home_1" || project.Id == "home_2")
                TownSaveVariables.SetInt(HomeRankKey, project.Id == "home_1" ? 1 : 2);
            else if (project.HutRank > 0)
                TownSaveVariables.SetInt(HutRankKey, project.HutRank);
            else TownSaveVariables.SetBool(ConstructionKey + project.Id, true);
            _gold -= project.Price;
            PersistTownProgress();
            _constructionConfirm = false;
            RefreshConstructionProjects();
            _constructionMessage = T("Construction complete.", "Obra concluída.");
            PlayRetailSound(RetailSfx.Confirm);
        }

        private void DrawCarpenter()
        {
            if (!_constructionOpen || !InCarpenterShop) return;
            DrawHudPanel(new Rect(85, 86, 470, 301));
            GUI.Label(new Rect(103, 101, 310, 24), T("Carpenter · Tonka", "Carpintaria · Tonka"));
            GUI.Label(new Rect(429, 101, 110, 24), _gold + " G");
            if (_constructionConfirm)
            {
                var project = _availableConstructionProjects[_constructionRow];
                GUI.Label(new Rect(103, 140, 432, 50), _constructionMessage ?? $"{project.Name} · {project.Price} G?");
                if (GUI.Button(new Rect(103, 212, 215, 26), WithInteractHint("Confirm", "Confirmar"))) ConfirmConstruction();
                if (GUI.Button(new Rect(320, 212, 215, 26), T("Back", "Voltar"))) BackFromCarpenter();
                return;
            }
            string prompt = _availableConstructionProjects.Count > 0 ? T("Choose a project.", "Escolha uma obra.") :
                T("No projects are available.", "Nenhuma obra disponível.");
            GUI.Label(new Rect(103, 139, 432, 38), _constructionMessage ?? prompt);
            int first = _constructionRow / ConstructionRows * ConstructionRows;
            for (int i = first; i < Mathf.Min(first + ConstructionRows, _availableConstructionProjects.Count); i++)
            {
                var project = _availableConstructionProjects[i];
                if (GUI.Button(new Rect(103, 195 + (i - first) * 28, 432, 25),
                    (i == _constructionRow ? "▸ " : "") + project.Name + $"   {project.Price} G"))
                { ChooseConstruction(i); break; }
            }
            if (first > 0 && GUI.Button(new Rect(103, 350, 110, 26), T("Previous", "Anterior"))) _constructionRow = first - ConstructionRows;
            if (first + ConstructionRows < _availableConstructionProjects.Count &&
                GUI.Button(new Rect(218, 350, 110, 26), T("Next", "Próxima"))) _constructionRow = first + ConstructionRows;
            if (GUI.Button(new Rect(416, 350, 119, 26), T("Back [Esc]", "Voltar [Esc]"))) BackFromCarpenter();
        }

        private void ResetConstructionView()
        {
            _constructionOpen = false;
            _builtTownLots.Clear();
            _constructionGround.Clear();
            foreach (var asset in _constructionGeometry)
                if (asset) Destroy(asset);
            _constructionGeometry.Clear();
        }

        private void ApplyTownConstruction(GameObject town)
        {
            var shader = Shader.Find("AzureDreams/TownCaptured");
            if (!shader) return;
            foreach (var project in BuildingProjects)
            {
                if (project.Bounds.width <= 0 || !ProjectBuilt(project)) continue;
                var data = Resources.Load<TextAsset>(ConstructionResources + project.Lot);
                if (!data) continue;
                _builtTownLots.Add(project);
                if (project.Footprint.width > 0) _townBuildings.Add(project.Footprint);
                // The mansion's side wing projects past the façade containing its entrance.
                if (project.Interior == "KohHome") _townBuildings.Add(Rect.MinMaxRect(58f, 53f, 61f, 56f));
                CreateConstructionMesh(town.transform.parent, shader, data, project);
            }
            if (_builtTownLots.Count == 0) return;

            foreach (var filter in town.GetComponentsInChildren<MeshFilter>())
            {
                var source = filter.sharedMesh;
                if (!source || !source.isReadable) continue;
                var positions = source.vertices;
                var mesh = Instantiate(source);
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    int[] indices = source.GetTriangles(sub);
                    var kept = new List<int>(indices.Length);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var center = filter.transform.TransformPoint(
                            (positions[indices[i]] + positions[indices[i + 1]] + positions[indices[i + 2]]) / 3f);
                        if (IsBuiltLot(new Vector2(center.x, center.z))) continue;
                        kept.Add(indices[i]); kept.Add(indices[i + 1]); kept.Add(indices[i + 2]);
                    }
                    mesh.SetTriangles(kept, sub, false);
                }
                filter.sharedMesh = mesh;
                _constructionGeometry.Add(mesh);
            }
        }

        private bool IsBuiltLot(Vector2 position)
        {
            foreach (var project in _builtTownLots)
                if (project.Bounds.Contains(position)) return true;
            return false;
        }

        private void CreateConstructionMesh(Transform parent, Shader shader, TextAsset asset, BuildingProject project)
        {
            using var reader = new BinaryReader(new MemoryStream(asset.bytes));
            int sections = reader.ReadInt32();
            var materials = new Material[sections];
            var triangles = new int[sections][];
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            for (int sub = 0; sub < sections; sub++)
            {
                string textureName = reader.ReadString();
                int count = reader.ReadInt32();
                var texture = Resources.Load<Texture2D>(ConstructionResources + "Textures/" + textureName);
                var material = new Material(shader) { mainTexture = texture };
                materials[sub] = material;
                _constructionGeometry.Add(material);
                triangles[sub] = new int[count];
                for (int i = 0; i < count; i++)
                {
                    triangles[sub][i] = vertices.Count;
                    vertices.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                    uv.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                }
            }
            int sampleCount = reader.ReadInt32();
            for (int i = 0; i < sampleCount; i++)
            {
                int x = reader.ReadByte(), z = reader.ReadByte();
                _constructionGround[z * RetailTownSize + x] = reader.ReadInt16() / 256f;
            }
            var mesh = new Mesh { name = project.English, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = sections;
            for (int i = 0; i < sections; i++) mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
            _constructionGeometry.Add(mesh);
            var root = new GameObject(project.English);
            root.transform.SetParent(parent, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = materials;
            _generated.Add(root);
        }

        private BuildingProject BuiltKohHome() => _builtTownLots.Find(project => project.Interior == "KohHome");

        private Vector3 ConstructionReturnPosition(BuildingProject project)
        {
            var outside = new Vector3(project.Entrance.x, 0f, project.Entrance.y + 1f);
            outside.y = TownTerrainHeight(outside) + TownActorFootOffset;
            return outside;
        }

        private void EnterConstruction(BuildingProject project)
        {
            string interior = project.Interior == "KohHome" ? CurrentHomeScene() : project.Interior;
            StartCoroutine(LoadInterior(interior, ConstructionReturnPosition(project)));
        }

        private bool TryConstructionEntrance()
        {
            if (_interior || !_player || _heldTownPot || _carriedItem != null) return false;
            var position = _player.transform.position;
            foreach (var project in _builtTownLots)
            {
                if (project.Interior == null || Mathf.Abs(position.x - project.Entrance.x) > 1.05f ||
                    position.z < project.Entrance.y - 0.2f || position.z > project.Entrance.y + 1.7f) continue;
                EnterConstruction(project);
                return true;
            }
            return false;
        }
    }
}
