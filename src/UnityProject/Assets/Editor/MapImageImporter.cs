using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using WrongTurn;
using WrongTurn.TurnBased;
using Object = UnityEngine.Object;

namespace WrongTurn.EditorTools
{
    public sealed class MapImageImporterWindow : EditorWindow
    {
        private MapVariant variant;
        private string terrainPath = "";
        private string objectPath = "";
        private int columns = 28;
        private int rows = 20;

        [MenuItem("Wrong Turn/Turn-Based/Import Color Map...")]
        public static void Open()
        {
            var selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<MapVariant>()
                : null;
            var window = GetWindow<MapImageImporterWindow>("纯色地图导入");
            window.minSize = new Vector2(520f, 310f);
            if (selected != null)
            {
                window.variant = selected;
                window.columns = selected.Width;
                window.rows = selected.Height;
            }
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("地形颜色", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "黑 = 墙，白/浅灰 = 地板，黄 = 可搬墙，红 = 缺口，绿 = 伤害陷阱。\n" +
                "工具按每格中心像素采样，不会把整张图片直接贴到地板上。",
                MessageType.Info);
            variant = (MapVariant)EditorGUILayout.ObjectField("目标地图", variant,
                typeof(MapVariant), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("地形图片");
                terrainPath = EditorGUILayout.TextField(terrainPath);
                if (GUILayout.Button("选择", GUILayout.Width(60f)))
                    terrainPath = OpenPng(terrainPath);
            }
            columns = Mathf.Max(1, EditorGUILayout.IntField("列数", columns));
            rows = Mathf.Max(1, EditorGUILayout.IntField("行数", rows));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("可选物件层", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("透明物件图片");
                objectPath = EditorGUILayout.TextField(objectPath);
                if (GUILayout.Button("选择", GUILayout.Width(60f)))
                    objectPath = OpenPng(objectPath);
            }
            EditorGUILayout.HelpBox(
                "物件层颜色：品红=玩家出生点，橙=近战，青=远程，紫=Boss，水蓝=冰晶，" +
                "亮黄=微光，白=出口。",
                MessageType.None);

            EditorGUI.BeginDisabledGroup(variant == null ||
                                         string.IsNullOrWhiteSpace(terrainPath) ||
                                         !File.Exists(terrainPath));
            if (GUILayout.Button("导入并重建地图", GUILayout.Height(46f)))
                Import();
            EditorGUI.EndDisabledGroup();
        }

        private void Import()
        {
            try
            {
                MapImageImportUtility.Import(variant, terrainPath,
                    string.IsNullOrWhiteSpace(objectPath) ? null : objectPath, columns, rows);
                EditorUtility.DisplayDialog("导入完成", "地图已经转换并保存到场景。", "好");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("导入失败", error.Message, "关闭");
            }
        }

        private static string OpenPng(string current)
        {
            var start = string.IsNullOrEmpty(current)
                ? Application.dataPath
                : Path.GetDirectoryName(current);
            return EditorUtility.OpenFilePanel("选择 PNG 地图", start, "png");
        }
    }

    public static class MapImageImportUtility
    {
        private static readonly Color32 Wall = new Color32(0, 0, 0, 255);
        private static readonly Color32 Floor = new Color32(128, 128, 128, 255);
        private static readonly Color32 Movable = new Color32(255, 220, 0, 255);
        private static readonly Color32 Gap = new Color32(220, 30, 30, 255);
        private static readonly Color32 Trap = new Color32(30, 200, 60, 255);

        public static void Import(MapVariant variant, string terrainPath,
            string objectPath, int columns, int rows)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));
            if (!File.Exists(terrainPath))
                throw new FileNotFoundException("Terrain PNG missing.", terrainPath);
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            TurnBasedSceneBuilder.PrepareSpriteReferences();
            var trapTile = AssetDatabase.LoadAssetAtPath<Tile>(
                "Assets/Art/Baked/Tiles/DungeonTrap.asset");
            if (trapTile == null)
                throw new InvalidOperationException("DungeonTrap tile asset is missing.");

            var terrain = LoadTexture(terrainPath);
            variant.Width = columns;
            variant.Height = rows;
            variant.Layout.ClearAllTiles();
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var color = SampleCenter(terrain, x, y, columns, rows);
                var tile = ClassifyTerrain(color, variant, trapTile);
                if (tile != null)
                    variant.Layout.SetTile(new Vector3Int(x, y, 0), tile);
            }
            Object.DestroyImmediate(terrain);
            EditorUtility.SetDirty(variant);
            EditorUtility.SetDirty(variant.Layout);

            if (!string.IsNullOrEmpty(objectPath))
                ImportObjects(variant, objectPath, columns, rows);
            else
                CreateDefaultExit(variant, columns, rows);

            TurnBasedSceneBuilder.RebuildMapView(variant);
            TurnBasedSceneBuilder.UpdateEnemyMarkers(variant);
            EditorSceneManager.MarkSceneDirty(variant.gameObject.scene);
            EditorSceneManager.SaveScene(variant.gameObject.scene, "Assets/Scenes/Main.unity");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("COLOR MAP IMPORTED: " + variant.DisplayName + "  " + columns + "x" +
                      rows);
        }

        private static TileBase ClassifyTerrain(Color32 color, MapVariant variant,
            TileBase trapTile)
        {
            if (color.a < 16)
                return variant.WallTile;
            var cell = Nearest(color, new[]
            {
                (Wall, "wall"),
                (Floor, "floor"),
                (Movable, "movable"),
                (Gap, "gap"),
                (Trap, "trap")
            });
            switch (cell)
            {
                case "floor": return variant.FloorTile;
                case "movable": return variant.MovableWallTile;
                case "gap": return variant.GapTile;
                case "trap": return trapTile;
                default: return variant.WallTile;
            }
        }

        private static void ImportObjects(MapVariant variant, string objectPath, int columns,
            int rows)
        {
            var texture = LoadTexture(objectPath);
            DestroyChildren(variant.transform, "Spawns");
            DestroyChildren(variant.transform, "Crystals");
            DestroyChildren(variant.transform, "Lights");
            DestroyChildren(variant.transform, "Exit");

            var spawns = new GameObject("Spawns");
            spawns.transform.SetParent(variant.transform, false);
            var crystals = new GameObject("Crystals");
            crystals.transform.SetParent(variant.transform, false);
            var lights = new GameObject("Lights");
            lights.transform.SetParent(variant.transform, false);

            var meleePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/EnemySpawn_Melee.prefab");
            var rangedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/EnemySpawn_Ranged.prefab");
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/EnemySpawn_Boss.prefab");
            var crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/MemoryCrystal.prefab");
            var lightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/LightPickup.prefab");

            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var color = SampleCenter(texture, x, y, columns, rows);
                if (color.a < 16)
                    continue;
                var marker = NearestObject(color);
                var position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                switch (marker)
                {
                    case "melee":
                        Instantiate(meleePrefab, spawns.transform, position);
                        break;
                    case "ranged":
                        Instantiate(rangedPrefab, spawns.transform, position);
                        break;
                    case "boss":
                        Instantiate(bossPrefab, spawns.transform, position);
                        break;
                    case "crystal":
                        Instantiate(crystalPrefab, crystals.transform, position);
                        break;
                    case "light":
                        Instantiate(lightPrefab, lights.transform, position);
                        break;
                    case "player":
                        var player = Object.FindFirstObjectByType<PlayerRig>(
                            FindObjectsInactive.Include);
                        if (player != null)
                            player.transform.position = position;
                        break;
                    case "exit":
                        CreateExit(variant, new Vector2Int(x, y));
                        break;
                }
            }
            Object.DestroyImmediate(texture);
            if (variant.ExitSpawn == null)
                CreateDefaultExit(variant, columns, rows);
        }

        private static void Instantiate(GameObject prefab, Transform parent, Vector3 position)
        {
            if (prefab == null)
                return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
        }

        private static void CreateDefaultExit(MapVariant variant, int columns, int rows)
        {
            CreateExit(variant, new Vector2Int(Mathf.Max(0, columns - 3),
                Mathf.Max(0, rows - 17)));
        }

        private static void CreateExit(MapVariant variant, Vector2Int cell)
        {
            var old = variant.transform.Find("Exit");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var exit = new GameObject("Exit");
            exit.transform.SetParent(variant.transform, false);
            exit.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            var marker = exit.AddComponent<GridExitSpawn>();
            marker.Cell = cell;
            marker.Renderer = exit.AddComponent<SpriteRenderer>();
            marker.Renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/Art/TurnBased/Dungeon/Door.png");
            marker.Renderer.sortingOrder = 120;
            variant.ExitSpawn = marker;
        }

        private static Texture2D LoadTexture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidOperationException("Could not decode PNG: " + path);
            return texture;
        }

        private static Color32 SampleCenter(Texture2D texture, int column, int row,
            int columns, int rows)
        {
            var cellWidth = texture.width / (float)columns;
            var cellHeight = texture.height / (float)rows;
            var x = Mathf.Clamp(Mathf.FloorToInt((column + 0.5f) * cellWidth), 0,
                texture.width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt((row + 0.5f) * cellHeight), 0,
                texture.height - 1);
            return texture.GetPixel(x, y);
        }

        private static string Nearest(Color32 color, (Color32 value, string name)[] palette)
        {
            var best = palette[0].name;
            var bestDistance = float.MaxValue;
            foreach (var entry in palette)
            {
                var dr = color.r - entry.value.r;
                var dg = color.g - entry.value.g;
                var db = color.b - entry.value.b;
                var distance = dr * dr + dg * dg + db * db;
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = entry.name;
            }
            return best;
        }

        private static string NearestObject(Color32 color)
        {
            var palette = new[]
            {
                (new Color32(255, 0, 255, 255), "player"),
                (new Color32(255, 128, 0, 255), "melee"),
                (new Color32(0, 200, 255, 255), "ranged"),
                (new Color32(128, 0, 255, 255), "boss"),
                (new Color32(0, 255, 255, 255), "crystal"),
                (new Color32(255, 231, 80, 255), "light"),
                (new Color32(255, 255, 255, 255), "exit")
            };
            return Nearest(color, palette);
        }

        private static void DestroyChildren(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }
    }
}
