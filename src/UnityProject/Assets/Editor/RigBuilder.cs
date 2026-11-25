using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace WrongTurn.EditorTools
{
    /// <summary>
    /// One-shot migration: bakes the procedural PixelArtHD sprites into real PNG
    /// assets, builds the spawn/prop prefabs, and assembles the editable scene
    /// hierarchy (three MapVariant roots, PlayerRig, Audio). Run from the
    /// "Wrong Turn/Migration" menu or via -executeMethod in batch mode.
    /// Re-running is safe: previously generated rig objects are rebuilt in place.
    /// </summary>
    public static class RigBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string BakedRoot = "Assets/Art/Baked";
        private const string TileRoot = BakedRoot + "/Tiles";
        private const string PrefabRoot = "Assets/Prefabs";
        private const int Ppu = 64;
        private const int CharacterPpu = 480;

        private static readonly Vector2 StartPosition = new Vector2(4.5f, 16.5f);

        private static readonly (string file, Func<Sprite> sprite, Vector2 pivot)[] BakeList =
        {
            ("floor-a", () => PixelArtHD.FloorA, new Vector2(0.5f, 0.5f)),
            ("floor-b", () => PixelArtHD.FloorB, new Vector2(0.5f, 0.5f)),
            ("floor-c", () => PixelArtHD.FloorC, new Vector2(0.5f, 0.5f)),
            ("wall", () => PixelArtHD.Wall, new Vector2(0.5f, 0f)),
            ("wall-movable", () => PixelArtHD.MovableWall, new Vector2(0.5f, 0f)),
            ("gap", () => PixelArtHD.Gap, new Vector2(0.5f, 0.5f)),
            ("player", () => PixelArtHD.Player, new Vector2(0.5f, 0f)),
            ("melee", () => PixelArtHD.Melee, new Vector2(0.5f, 0f)),
            ("ranged", () => PixelArtHD.Ranged, new Vector2(0.5f, 0f)),
            ("boss", () => PixelArtHD.Boss, new Vector2(0.5f, 0f)),
            ("crystal", () => PixelArtHD.Crystal, new Vector2(0.5f, 0.1f)),
            ("light", () => PixelArtHD.Light, new Vector2(0.5f, 0f)),
            ("ring", () => PixelArtHD.Ring, new Vector2(0.5f, 0.5f)),
            ("scout", () => PixelArtHD.Scout, new Vector2(0.5f, 0f)),
            ("target", () => PixelArtHD.Target, new Vector2(0.5f, 0f)),
            ("relic", () => PixelArtHD.Relic, new Vector2(0.5f, 0f)),
            ("door", () => PixelArtHD.Door, new Vector2(0.5f, 0f)),
            ("spark", () => PixelArtHD.Spark, new Vector2(0.5f, 0.5f)),
            ("shadow", () => PixelArtHD.Shadow, new Vector2(0.5f, 0.5f)),
            ("glow", () => PixelArtHD.Glow, new Vector2(0.5f, 0.5f))
        };

        // Stage data mirrored from ActionGameController (the runtime autotest keeps
        // the two in sync: 9/10/12 enemies, two crystals, five lights per map).
        private static readonly string[] StageNames = { "森林", "冰原", "沙漠" };
        private static readonly Color32[] StageBackgrounds =
        {
            new Color32(9, 31, 41, 255),
            new Color32(14, 31, 53, 255),
            new Color32(52, 31, 27, 255)
        };
        private static readonly Color[] EnemyTints =
        {
            new Color(0.82f, 1f, 0.88f),
            new Color(0.78f, 0.96f, 1f),
            new Color(1f, 0.83f, 0.63f)
        };
        private static readonly string[] CrystalTitles = { "裂纹里的初见", "墙缝里的旧名字" };
        private static readonly Vector2[][] CrystalPositions =
        {
            new[] { new Vector2(2.5f, 10.5f), new Vector2(25.3f, 7.3f) },
            new[] { new Vector2(3.5f, 9.5f), new Vector2(23.5f, 6.5f) },
            new[] { new Vector2(4.5f, 6.5f), new Vector2(24.2f, 8.2f) }
        };
        private static readonly Vector2[][] LightPositions =
        {
            new[]
            {
                new Vector2(3.2f, 15.2f), new Vector2(4.3f, 10.6f),
                new Vector2(12.5f, 10.6f), new Vector2(19.5f, 11.5f),
                new Vector2(19.5f, 4.5f)
            },
            new[]
            {
                new Vector2(3.4f, 15.3f), new Vector2(3.2f, 9.2f),
                new Vector2(11.3f, 11.4f), new Vector2(19.3f, 9.5f),
                new Vector2(14.2f, 5.3f)
            },
            new[]
            {
                new Vector2(3.4f, 15.4f), new Vector2(9.2f, 10.5f),
                new Vector2(13.4f, 12.2f), new Vector2(21.4f, 9.4f),
                new Vector2(18.4f, 3.5f)
            }
        };
        private static readonly (Vector2 pos, EnemyKindAction kind, int hp)[][] EnemyTables =
        {
            new (Vector2, EnemyKindAction, int)[]
            {
                (new Vector2(11.3f, 12.2f), EnemyKindAction.Melee, 4),
                (new Vector2(10.1f, 6.2f), EnemyKindAction.Melee, 4),
                (new Vector2(16.1f, 11.2f), EnemyKindAction.Ranged, 3),
                (new Vector2(24.1f, 10.2f), EnemyKindAction.Ranged, 3),
                (new Vector2(20.3f, 9.3f), EnemyKindAction.Melee, 4),
                (new Vector2(8.4f, 9.4f), EnemyKindAction.Melee, 4),
                (new Vector2(13.3f, 9.2f), EnemyKindAction.Ranged, 3),
                (new Vector2(24.2f, 6.5f), EnemyKindAction.Melee, 5),
                (new Vector2(22.1f, 4.1f), EnemyKindAction.Boss, 30)
            },
            new (Vector2, EnemyKindAction, int)[]
            {
                (new Vector2(10.5f, 12.2f), EnemyKindAction.Melee, 5),
                (new Vector2(13.1f, 11.2f), EnemyKindAction.Ranged, 3),
                (new Vector2(11.2f, 9.5f), EnemyKindAction.Melee, 5),
                (new Vector2(20.3f, 11.1f), EnemyKindAction.Ranged, 4),
                (new Vector2(18.3f, 9.2f), EnemyKindAction.Melee, 5),
                (new Vector2(23.3f, 10.4f), EnemyKindAction.Ranged, 4),
                (new Vector2(7f, 6.2f), EnemyKindAction.Melee, 5),
                (new Vector2(14.2f, 5.2f), EnemyKindAction.Ranged, 4),
                (new Vector2(20.4f, 6.3f), EnemyKindAction.Melee, 6),
                (new Vector2(21.3f, 3.2f), EnemyKindAction.Boss, 36)
            },
            new (Vector2, EnemyKindAction, int)[]
            {
                (new Vector2(10.3f, 12.3f), EnemyKindAction.Melee, 5),
                (new Vector2(14.2f, 11.2f), EnemyKindAction.Ranged, 4),
                (new Vector2(12.5f, 8.5f), EnemyKindAction.Melee, 5),
                (new Vector2(17.2f, 8.5f), EnemyKindAction.Ranged, 4),
                (new Vector2(20.5f, 10.5f), EnemyKindAction.Melee, 6),
                (new Vector2(22.3f, 8.5f), EnemyKindAction.Ranged, 4),
                (new Vector2(6.3f, 6.4f), EnemyKindAction.Melee, 6),
                (new Vector2(10.2f, 5.2f), EnemyKindAction.Melee, 6),
                (new Vector2(14.2f, 6.3f), EnemyKindAction.Ranged, 5),
                (new Vector2(22.4f, 5.5f), EnemyKindAction.Melee, 7),
                (new Vector2(24.2f, 2.5f), EnemyKindAction.Ranged, 5),
                (new Vector2(21.4f, 3.4f), EnemyKindAction.Boss, 46)
            }
        };

        [MenuItem("Wrong Turn/Migration/Run Full Migration")]
        public static void RunMigration()
        {
            try
            {
                Migrate();
                Debug.Log("MIGRATION COMPLETE — editable scene rig generated.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("MIGRATION FAILED: " + error);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        [MenuItem("Wrong Turn/Migration/Verify Scene Rig")]
        public static void RunVerify()
        {
            var failures = new List<string>();
            try
            {
                Verify(failures);
            }
            catch (Exception error)
            {
                failures.Add("Verification threw: " + error);
            }
            if (failures.Count == 0)
                Debug.Log("VERIFY PASS — scene rig matches the stage data (9/10/12 enemies).");
            else
                Debug.LogError("VERIFY FAIL:\n" + string.Join("\n", failures));
            if (Application.isBatchMode)
                EditorApplication.Exit(failures.Count == 0 ? 0 : 2);
        }

        private static void Migrate()
        {
            EnsureFolders();
            ConfigureCharacterImports();
            BakeSprites();
            AssetDatabase.Refresh();
            var tiles = CreateTileAssets();
            CreatePrefabs();
            BuildSceneLayout(tiles);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(BakedRoot);
            Directory.CreateDirectory(TileRoot);
            Directory.CreateDirectory(PrefabRoot);
        }

        private static void ConfigureCharacterImports()
        {
            foreach (var file in Directory.GetFiles("Assets/Resources/Art", "*.png"))
            {
                var path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = CharacterPpu;
                importer.spritePivot = new Vector2(0.5f, 0f);
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static void BakeSprites()
        {
            foreach (var entry in BakeList)
            {
                var path = $"{BakedRoot}/{entry.file}.png";
                File.WriteAllBytes(path, entry.sprite().texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("No texture importer for " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Ppu;
                importer.spritePivot = entry.pivot;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static Sprite LoadBaked(string file)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{BakedRoot}/{file}.png");
            if (sprite == null)
                throw new InvalidOperationException("Baked sprite missing: " + file);
            return sprite;
        }

        private static Dictionary<string, Tile> CreateTileAssets()
        {
            var result = new Dictionary<string, Tile>();
            result["floor"] = CreateTile($"{TileRoot}/Floor.asset", "Layout_Floor", LoadBaked("floor-a"));
            result["wall"] = CreateTile($"{TileRoot}/Wall.asset", "Layout_Wall", LoadBaked("wall"));
            result["movable"] = CreateTile($"{TileRoot}/MovableWall.asset", "Layout_MovableWall", LoadBaked("wall-movable"));
            result["gap"] = CreateTile($"{TileRoot}/Gap.asset", "Layout_Gap", LoadBaked("gap"));
            return result;
        }

        private static Tile CreateTile(string path, string name, Sprite sprite)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.name = name;
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static void CreatePrefabs()
        {
            CreateEnemyPrefab(EnemyKindAction.Melee, "melee", "Melee");
            CreateEnemyPrefab(EnemyKindAction.Ranged, "ranged", "Ranged");
            CreateEnemyPrefab(EnemyKindAction.Boss, "boss", "Boss");
            CreateCrystalPrefab();
            CreateLightPrefab();
            CreatePlayerPrefab();
        }

        private static void CreateEnemyPrefab(EnemyKindAction kind, string bakedName, string fileName)
        {
            var root = new GameObject("EnemySpawn");
            var marker = root.AddComponent<EnemySpawn>();
            marker.Kind = kind;
            marker.Hp = kind == EnemyKindAction.Boss ? 30 : kind == EnemyKindAction.Ranged ? 3 : 4;
            bool boss = kind == EnemyKindAction.Boss;
            SpriteChild(root, "Sprite", bakedName, boss ? 6100 : 5980,
                new Vector3(0f, boss ? -0.7f : -0.35f, 0f),
                boss ? Vector3.one * 1.42f : Vector3.one, Color.white);
            SpriteChild(root, "Shadow", "shadow", 4999,
                new Vector3(0f, 0.28f, 0f),
                new Vector3(boss ? 1.8f : 0.9f, 0.65f, 1f), new Color(0f, 0f, 0f, 0.6f));
            BuildPrefab($"{PrefabRoot}/EnemySpawn_{fileName}.prefab", root);
        }

        private static void CreateCrystalPrefab()
        {
            var root = new GameObject("MemoryCrystal");
            root.AddComponent<CrystalSpawn>();
            SpriteChild(root, "CrystalGlow", "glow", 520, new Vector3(0f, 0.2f, 0f),
                Vector3.one * 2.2f, new Color(0.25f, 0.9f, 1f, 0.26f));
            SpriteChild(root, "Crystal", "crystal", 535, Vector3.zero,
                Vector3.one, Color.white);
            BuildPrefab($"{PrefabRoot}/MemoryCrystal.prefab", root);
        }

        private static void CreateLightPrefab()
        {
            var root = new GameObject("LightPickup");
            root.AddComponent<LightSpawn>();
            SpriteChild(root, "LightGlow", "glow", 510, new Vector3(0f, 0.15f, 0f),
                Vector3.one * 2.1f, new Color(1f, 0.57f, 0.17f, 0.22f));
            SpriteChild(root, "Light", "light", 525, Vector3.zero, Vector3.one, Color.white);
            BuildPrefab($"{PrefabRoot}/LightPickup.prefab", root);
        }

        private static void CreatePlayerPrefab()
        {
            var root = new GameObject("PlayerRig");
            var rig = root.AddComponent<PlayerRig>();
            rig.Glow = SpriteChild(root, "PlayerGlow", "glow", 4300, Vector3.zero,
                Vector3.one * 2.8f, new Color(1f, 0.68f, 0.25f, 0.24f)).gameObject;
            rig.Shadow = SpriteChild(root, "PlayerShadow", "shadow", 5000, Vector3.zero,
                new Vector3(0.95f, 0.72f, 1f), new Color(0f, 0f, 0f, 0.65f)).gameObject;
            var body = SpriteChild(root, "Player", null, 6000, Vector3.zero,
                Vector3.one, Color.white).gameObject;
            var bodySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Character_Down.png");
            if (bodySprite != null)
                body.GetComponent<SpriteRenderer>().sprite = bodySprite;
            rig.Body = body;
            rig.Weapon = SpriteChild(body, "HeldRing", "ring", 6010, Vector3.zero,
                Vector3.one * 0.46f, Color.white).gameObject;
            BuildPrefab($"{PrefabRoot}/PlayerRig.prefab", root);
        }

        private static SpriteRenderer SpriteChild(GameObject root, string name, string bakedName,
            int order, Vector3 localPosition, Vector3 scale, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            if (bakedName != null)
                renderer.sprite = LoadBaked(bakedName);
            renderer.sortingOrder = order;
            renderer.color = color;
            return renderer;
        }

        private static GameObject BuildPrefab(string path, GameObject root)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject InstantiatePrefabAt(string path, Transform parent, Vector2 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("Prefab missing: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = new Vector3(position.x, position.y, 0f);
            return instance;
        }

        private static void BuildSceneLayout(Dictionary<string, Tile> tiles)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Idempotency: drop rig objects from a previous run, keep Camera + Game.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<MapVariant>() != null || root.GetComponent<PlayerRig>() != null ||
                    root.GetComponent<MusicRig>() != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }

            var enemyPrefabs = new Dictionary<EnemyKindAction, string>
            {
                { EnemyKindAction.Melee, $"{PrefabRoot}/EnemySpawn_Melee.prefab" },
                { EnemyKindAction.Ranged, $"{PrefabRoot}/EnemySpawn_Ranged.prefab" },
                { EnemyKindAction.Boss, $"{PrefabRoot}/EnemySpawn_Boss.prefab" }
            };
            var crystalPrefab = $"{PrefabRoot}/MemoryCrystal.prefab";
            var lightPrefab = $"{PrefabRoot}/LightPickup.prefab";
            var playerPrefab = $"{PrefabRoot}/PlayerRig.prefab";

            for (int mapIndex = 0; mapIndex < 3; mapIndex++)
            {
                var variantGo = new GameObject("Map_" + StageNames[mapIndex]);
                var variant = variantGo.AddComponent<MapVariant>();
                variant.MapId = mapIndex;
                variant.DisplayName = StageNames[mapIndex];
                variant.Background = StageBackgrounds[mapIndex];
                variant.EnemyTint = EnemyTints[mapIndex];

                var gridGo = new GameObject("Grid");
                gridGo.transform.SetParent(variantGo.transform, false);
                gridGo.AddComponent<Grid>();

                var layoutGo = new GameObject("Layout");
                layoutGo.transform.SetParent(gridGo.transform, false);
                var tilemap = layoutGo.AddComponent<Tilemap>();
                var tileRenderer = layoutGo.AddComponent<TilemapRenderer>();
                tileRenderer.sortingOrder = 20;

                // Empty cell = FixedWall; paint Floor to carve rooms.
                var grid = ActionGameController.BuildTilesForEditor(mapIndex);
                for (int x = 0; x < ActionGameController.EditorMapWidth; x++)
                for (int y = 0; y < ActionGameController.EditorMapHeight; y++)
                {
                    TileBase tile;
                    switch (grid[x, y])
                    {
                        case TileKind.Floor:
                            tile = tiles["floor"];
                            break;
                        case TileKind.MovableWall:
                            tile = tiles["movable"];
                            break;
                        case TileKind.Gap:
                            tile = tiles["gap"];
                            break;
                        default:
                            continue;
                    }
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }

                variant.Layout = tilemap;
                variant.FloorTile = tiles["floor"];
                variant.MovableWallTile = tiles["movable"];
                variant.GapTile = tiles["gap"];
                variant.WallTile = tiles["wall"];

                var spawnsGo = new GameObject("Spawns");
                spawnsGo.transform.SetParent(variantGo.transform, false);
                foreach (var (position, kind, hp) in EnemyTables[mapIndex])
                {
                    var marker = InstantiatePrefabAt(enemyPrefabs[kind], spawnsGo.transform, position)
                        .GetComponent<EnemySpawn>();
                    marker.Hp = hp;
                }

                var crystalsGo = new GameObject("Crystals");
                crystalsGo.transform.SetParent(variantGo.transform, false);
                for (int i = 0; i < CrystalPositions[mapIndex].Length; i++)
                {
                    var crystal = InstantiatePrefabAt(crystalPrefab, crystalsGo.transform,
                        CrystalPositions[mapIndex][i]).GetComponent<CrystalSpawn>();
                    crystal.Title = CrystalTitles[i];
                }

                var lightsGo = new GameObject("Lights");
                lightsGo.transform.SetParent(variantGo.transform, false);
                foreach (var position in LightPositions[mapIndex])
                    InstantiatePrefabAt(lightPrefab, lightsGo.transform, position);
            }

            InstantiatePrefabAt(playerPrefab, null, StartPosition);

            var audioGo = new GameObject("Audio");
            var musicRig = audioGo.AddComponent<MusicRig>();
            musicRig.TrackA = AudioSourceChild(audioGo, "MusicA");
            musicRig.TrackB = AudioSourceChild(audioGo, "MusicB");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log("Scene layout built: 3 map variants, spawn markers, crystals, lights, " +
                      "player rig and audio pair.");
        }

        private static AudioSource AudioSourceChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private static void Verify(List<string> failures)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var variants = scene.GetRootGameObjects()
                .Select(root => root.GetComponent<MapVariant>())
                .Where(v => v != null)
                .OrderBy(v => v.MapId)
                .ToList();
            Check(variants.Count == 3, "Expected 3 map variants, found " + variants.Count, failures);

            int[] expectedEnemies = { 9, 10, 12 };
            for (int mapIndex = 0; mapIndex < variants.Count; mapIndex++)
            {
                var variant = variants[mapIndex];
                var label = "Map " + variant.MapId;
                Check(variant.MapId == mapIndex, label + " has wrong MapId order.", failures);
                var markers = variant.GetComponentsInChildren<EnemySpawn>(true);
                Check(markers.Length == expectedEnemies[mapIndex],
                    $"{label} has {markers.Length} enemy spawns, expected {expectedEnemies[mapIndex]}.",
                    failures);
                Check(variant.GetComponentsInChildren<CrystalSpawn>(true).Length == 2,
                    label + " must hold two crystals.", failures);
                Check(variant.GetComponentsInChildren<LightSpawn>(true).Length == 5,
                    label + " must hold five lights.", failures);
                Check(variant.Layout != null, label + " has no layout tilemap.", failures);
                Check(variant.FloorTile != null && variant.MovableWallTile != null &&
                      variant.GapTile != null, label + " is missing tile asset refs.", failures);

                if (variant.Layout != null)
                {
                    var grid = ActionGameController.BuildTilesForEditor(mapIndex);
                    int mismatches = 0;
                    for (int x = 0; x < ActionGameController.EditorMapWidth; x++)
                    for (int y = 0; y < ActionGameController.EditorMapHeight; y++)
                    {
                        var painted = variant.Layout.GetTile(new Vector3Int(x, y, 0));
                        var expected = grid[x, y] == TileKind.Floor ? variant.FloorTile :
                            grid[x, y] == TileKind.MovableWall ? variant.MovableWallTile :
                            grid[x, y] == TileKind.Gap ? variant.GapTile : null;
                        if (painted != expected)
                            mismatches++;
                    }
                    Check(mismatches == 0,
                        $"{label} tilemap diverges from the carve data in {mismatches} cells.",
                        failures);
                }
            }

            var playerRig = UnityEngine.Object.FindFirstObjectByType<PlayerRig>(FindObjectsInactive.Include);
            Check(playerRig != null && playerRig.Body != null && playerRig.Glow != null &&
                  playerRig.Shadow != null && playerRig.Weapon != null,
                "Player rig missing or incomplete.", failures);
            var musicRig = UnityEngine.Object.FindFirstObjectByType<MusicRig>(FindObjectsInactive.Include);
            Check(musicRig != null && musicRig.TrackA != null && musicRig.TrackB != null,
                "Music rig missing or incomplete.", failures);
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/PlayerRig.prefab") != null,
                "PlayerRig prefab missing.", failures);
            Check(AssetDatabase.LoadAssetAtPath<Tile>(TileRoot + "/Floor.asset") != null,
                "Floor tile asset missing.", failures);
        }

        private static void Check(bool condition, string message, ICollection<string> failures)
        {
            if (!condition)
                failures.Add(message);
        }
    }
}
