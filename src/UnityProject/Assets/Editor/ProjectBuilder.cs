using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WrongTurn;

namespace WrongTurn.EditorTools
{
    public static class ProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Wrong Turn/Rebuild Main Scene")]
        public static void InitializeProject()
        {
            if (SceneRigExists() && !ConfirmRigWipe())
            {
                Debug.LogWarning("Rebuild aborted — the editable scene rig is intact. " +
                                 "Use Wrong Turn/Migration if you intend to regenerate it.");
                return;
            }

            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Build/Windows");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.orthographicSize = 7.35f;
            camera.transform.position = new Vector3(7.5f, 6.75f, -10f);
            camera.backgroundColor = new Color32(5, 8, 12, 255);

            var game = new GameObject("WrongTurnGame");
            game.AddComponent<ActionGameController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "Fresh Lantern Studio";
            PlayerSettings.productName = "走错也算数 | Wrong Turn Counts";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.visibleInBackground = true;

            ConfigureResourceImports();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Wrong Turn project initialized: " + ScenePath);
        }

        private static bool SceneRigExists()
        {
            if (!File.Exists(ScenePath))
                return false;
            var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return existing.GetRootGameObjects()
                .Any(root => root.GetComponent<WrongTurn.MapVariant>() != null);
        }

        private static bool ConfirmRigWipe()
        {
            if (Application.isBatchMode)
                return false;
            return EditorUtility.DisplayDialog("Rebuild Main Scene",
                "Main.unity 已包含可编辑的场景对象（MapVariant/PlayerRig 等）。\n\n" +
                "重建会把它们全部冲掉，恢复为最早的空场景。确定继续吗？",
                "仍然重建", "取消");
        }

        private static void ConfigureResourceImports()
        {
            foreach (var path in Directory.GetFiles("Assets/Resources/Art", "*.png"))
            {
                var normalized = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(normalized) as TextureImporter;
                if (importer == null)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 480f;
                importer.spritePivot = new Vector2(0.5f, 0f);
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            foreach (var path in Directory.GetFiles("Assets/Resources/Music", "*.mp3"))
            {
                var normalized = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(normalized) as AudioImporter;
                if (importer == null)
                    continue;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.8f;
                settings.preloadAudioData = false;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }
        }

        [MenuItem("Wrong Turn/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            if (!File.Exists(ScenePath))
                InitializeProject();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/Windows/WrongTurnCounts.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result +
                                                    " errors=" + report.summary.totalErrors);
            Debug.Log("Windows player built at Build/Windows/WrongTurnCounts.exe");
        }

        [MenuItem("Wrong Turn/Run Smoke Tests")]
        public static void RunSmokeTests()
        {
            var failures = new List<string>();
            var session = new GameSession();
            session.StartNewRun();

            Check(session.Status == RunStatus.Playing, "Run does not start.", failures);
            Check(session.Body == session.Spawn, "Spawn position mismatch.", failures);
            Check(session.Shadow == GameSession.MaxShadow, "Shadow does not start full.", failures);
            Check(session.Light == 5, "Light does not start at 5.", failures);
            Check(HasPath(session, session.Spawn, new Vector2Int(13, 4)),
                "No route from start to second memory.", failures);
            Check(HasPath(session, session.Spawn, new Vector2Int(12, 2)),
                "No route from start to boss.", failures);

            session.TryDirection(Vector2Int.left);
            Check(session.Body == new Vector2Int(3, 10), "Basic move failed.", failures);
            Check(session.Actions == 1, "Valid body move did not consume one action.", failures);

            session.ToggleRecord();
            int energyAtRecord = session.Shadow;
            session.TryDirection(Vector2Int.left);
            Check(session.Shadow == energyAtRecord - 1, "Recorded move did not cost shadow.", failures);
            session.ToggleRecord();
            Check(session.BattleGhost != null, "Battle ghost was not created.", failures);
            Check(session.Mode == ControlMode.Body, "Mode did not return to body.", failures);

            session.ToggleScout();
            Check(session.Mode == ControlMode.Scout, "Scout did not start.", failures);
            int shadowBeforeScoutStep = session.Shadow;
            int lightBeforeScoutStep = session.Light;
            session.TryDirection(Vector2Int.up);
            Check(session.Shadow == shadowBeforeScoutStep - 1 &&
                  session.Light == lightBeforeScoutStep - 1,
                "Scout step did not pay both resources.", failures);
            session.ToggleScout();
            Check(session.Mode == ControlMode.Body, "Scout did not return to body.", failures);

            var rangedSession = new GameSession();
            rangedSession.StartNewRun();
            rangedSession.Body = new Vector2Int(8, 8);
            rangedSession.Scout = rangedSession.Body;
            rangedSession.WaitTurn();
            var ranged = rangedSession.Enemies.First(e => e.Kind == EnemyKind.Ranged &&
                                                        e.Cell == new Vector2Int(12, 8));
            Check(ranged.Phase == EnemyPhase.Telegraph &&
                  ranged.MoveKind == EnemyMoveKind.RangedShot &&
                  ranged.DangerCells.Count > 0,
                "Dark ranged enemy did not create a readable locked warning.", failures);
            int rangedHp = rangedSession.BodyHp;
            rangedSession.WaitTurn();
            Check(rangedSession.BodyHp < rangedHp, "Ranged warning did not resolve on a later turn.",
                failures);

            var ghostSession = new GameSession();
            ghostSession.StartNewRun();
            var melee = ghostSession.Enemies.First(e => e.Kind == EnemyKind.Melee);
            ghostSession.BattleGhost = new BattleGhost
            {
                Damage = 2,
                Life = 6
            };
            ghostSession.BattleGhost.DamageCells.Add(melee.Cell);
            ghostSession.WaitTurn();
            Check(!melee.Alive, "Battle ghost did not damage an enemy in a valid turn.", failures);
            Check(ghostSession.EnergyDrops.Contains(melee.Cell),
                "Defeated enemy did not leave a shadow pickup.", failures);

            var bossSession = new GameSession();
            bossSession.StartNewRun();
            var boss = bossSession.Boss;
            boss.Hp = 1;
            bossSession.Body = boss.Cell + Vector2Int.left;
            bossSession.Facing = Vector2Int.right;
            bossSession.Tiles[boss.Cell.x, boss.Cell.y].Explored = true;
            bossSession.Attack();
            Check(bossSession.BossDefeated && bossSession.ExitOpen,
                "Boss defeat did not open the exit.", failures);
            bossSession.Body = new Vector2Int(13, 1);
            bossSession.Facing = Vector2Int.right;
            bossSession.Tiles[14, 1].Explored = true;
            bossSession.TryDirection(Vector2Int.right);
            Check(bossSession.Status == RunStatus.Won,
                "Walking into the opened exit did not win the run.", failures);

            var deathSession = new GameSession();
            deathSession.StartNewRun();
            deathSession.CurrentTrail.Add(new Vector2Int(5, 11));
            deathSession.BodyHp = 1;
            var damageMethod = typeof(GameSession).GetMethod("ApplyBodyDamage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            damageMethod.Invoke(deathSession, new object[] { 1, "Smoke test hit." });
            Check(deathSession.Status == RunStatus.Dead && deathSession.RemainingLives == 2,
                "Death did not consume exactly one life.", failures);
            deathSession.Respawn();
            Check(deathSession.Status == RunStatus.Playing &&
                  deathSession.BodyHp == GameSession.BodyMaxHp &&
                  deathSession.InheritedTrail.Contains(new Vector2Int(5, 11)),
                "Respawn did not restore health or inherit the current light trail.", failures);

            if (failures.Count > 0)
                throw new InvalidOperationException("Smoke test failures:\n" + string.Join("\n", failures));

            Debug.Log("SMOKE TESTS PASSED — map, turns, shadow routes, ranged warnings, " +
                      "ghost damage, boss exit, death and inherited light.");
        }

        private static bool HasPath(GameSession session, Vector2Int start, Vector2Int target)
        {
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            var directions = new[]
            {
                Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
            };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell == target)
                    return true;
                foreach (var dir in directions)
                {
                    var next = cell + dir;
                    if (next.x < 0 || next.x >= GameSession.Width ||
                        next.y < 0 || next.y >= GameSession.Height)
                        continue;
                    if (visited.Contains(next) || !session.Tiles[next.x, next.y].Walkable)
                        continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        private static void Check(bool condition, string message, ICollection<string> failures)
        {
            if (!condition)
                failures.Add(message);
        }
    }
}
