using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using WrongTurn;
using WrongTurn.TurnBased;
using Object = UnityEngine.Object;

namespace WrongTurn.EditorTools
{
    public static class TurnBasedSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ArtRoot = "Assets/Resources/Art/TurnBased";
        private const string UiRoot = "Assets/Resources/UI/TurnBased";
        private const string BossOneRoot = ArtRoot + "/BossPhaseOne";
        private const string BossTwoRoot = ArtRoot + "/BossPhaseTwo";
        private const string BakedTileRoot = "Assets/Art/Baked/Tiles";
        private const string DungeonFloorTilePath = BakedTileRoot + "/DungeonFloor.asset";
        private const string DungeonWallTilePath = BakedTileRoot + "/DungeonWall.asset";
        private const string DungeonMovableTilePath =
            BakedTileRoot + "/DungeonMovableWall.asset";
        private const string DungeonGapTilePath = BakedTileRoot + "/DungeonGap.asset";
        private const string DungeonTrapTilePath = BakedTileRoot + "/DungeonTrap.asset";
        private const string DungeonDoorSpritePath =
            ArtRoot + "/Dungeon/Door.png";

        private static Sprite panelSprite;
        private static Sprite panelLargeSprite;
        private static Sprite buttonSprite;
        private static Sprite barSprite;
        private static Sprite portraitSprite;
        private static Sprite vignetteSprite;
        private static Sprite glowSprite;
        private static Sprite turnBandSprite;
        private static Sprite whiteSprite;
        private static Sprite meleeSprite;
        private static Sprite rangedSprite;
        private static Sprite bossSprite;
        private static Sprite[] bossFramesOne;
        private static Sprite[] bossFramesTwo;
        private static Sprite[] dungeonFloors;
        private static Sprite dungeonTrapSprite;

        [MenuItem("Wrong Turn/Turn-Based/Rebuild Turn-Based Project")]
        public static void BuildAll()
        {
            EnsureFolders();
            ImportTurnBasedArt();
            CreateTrapAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            LoadSprites();
            MigrateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("TURN-BASED PROJECT BUILT: scene, art, grid views and UGUI.");
        }

        [MenuItem("Wrong Turn/Turn-Based/Verify Turn-Based Project")]
        public static void Verify()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var failures = new List<string>();
            LoadSprites();
            var controller = Object.FindFirstObjectByType<GridTurnController>(
                FindObjectsInactive.Include);
            Check(controller != null, "GridTurnController missing.", failures);
            Check(Object.FindFirstObjectByType<GridTurnHud>(FindObjectsInactive.Include) != null,
                "UGUI GridTurnHud missing.", failures);

            var variants = Object.FindObjectsByType<MapVariant>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).OrderBy(variant => variant.MapId).ToArray();
            Check(variants.Length == 3, "Expected 3 map variants.", failures);
            foreach (var variant in variants)
            {
                Check(variant.GridView != null, variant.DisplayName + " GridView missing.",
                    failures);
                Check(variant.TrapTile != null, variant.DisplayName + " trap tile missing.",
                    failures);
                if (variant.GridView != null)
                {
                    Check(variant.GridView.Cells.Count == variant.Width * variant.Height,
                        variant.DisplayName + " grid cell count mismatch.", failures);
                }
            }

            Check(bossFramesOne != null && bossFramesOne.Length == 22,
                "Boss phase one should contain 22 frames.", failures);
            Check(bossFramesTwo != null && bossFramesTwo.Length == 3,
                "Boss phase two should contain 3 frames.", failures);
            Check(meleeSprite != null && rangedSprite != null, "Enemy artwork missing.",
                failures);
            Check(panelSprite != null && portraitSprite != null,
                "UGUI artwork missing.", failures);
            TurnBasedRuleTests.RunAll(failures);

            if (failures.Count == 0)
                Debug.Log("TURN-BASED VERIFY PASS: scene, UGUI, grid and artwork are complete.");
            else
                Debug.LogError("TURN-BASED VERIFY FAIL:\n" + string.Join("\n", failures));
        }

        public static void BatchBuildAll()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("TURN-BASED BATCH BUILD FAILED:\n" + error);
                EditorApplication.Exit(1);
            }
        }

        public static void BatchVerify()
        {
            try
            {
                Verify();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("TURN-BASED VERIFY THREW:\n" + error);
                EditorApplication.Exit(2);
            }
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(ArtRoot);
            Directory.CreateDirectory(BossOneRoot);
            Directory.CreateDirectory(BossTwoRoot);
            Directory.CreateDirectory(UiRoot);
            Directory.CreateDirectory(ArtRoot + "/Dungeon");
        }

        private static void ImportTurnBasedArt()
        {
            var images = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../assets/images"));
            var meleeSheet = LoadTexture(Path.Combine(images, "地头龙.png"));
            CropTexture(meleeSheet, ArtRoot + "/Melee.png",
                Mathf.RoundToInt(meleeSheet.width * 0.26f),
                Mathf.RoundToInt(meleeSheet.height * 0.02f),
                Mathf.RoundToInt(meleeSheet.width * 0.57f),
                Mathf.RoundToInt(meleeSheet.height * 0.96f));
            Object.DestroyImmediate(meleeSheet);
            CopyFile(Path.Combine(images, "困倦法师.png"), ArtRoot + "/Ranged.png");
            CopyFile(Path.Combine(images, "New Piskel (2).png"), ArtRoot + "/BossSheet.png");
            CopyFile(Path.Combine(images, "地牢图.png"), ArtRoot + "/DungeonSheet.png");

            AssetDatabase.Refresh();
            ExtractBossFrames(ArtRoot + "/BossSheet.png");
            CreateDungeonTiles(ArtRoot + "/DungeonSheet.png");
            ExtractUiSprites(Path.Combine(images, "1795444240784.png"));
            CreateUiEffects();
            AssetDatabase.Refresh();

            ConfigureSprite(ArtRoot + "/Melee.png", new Vector4(0f, 0f, 0f, 0f), 100f);
            ConfigureSprite(ArtRoot + "/Ranged.png", new Vector4(0f, 0f, 0f, 0f), 100f);
            foreach (var folder in new[] { BossOneRoot, BossTwoRoot })
            foreach (var path in Directory.GetFiles(folder, "*.png"))
                ConfigureSprite(Normalize(path), Vector4.zero, 100f);
            ConfigureSprite(UiRoot + "/Panel.png", new Vector4(54f, 54f, 54f, 54f), 100f);
            ConfigureSprite(UiRoot + "/PanelLarge.png", new Vector4(54f, 54f, 54f, 54f), 100f);
            ConfigureSprite(UiRoot + "/Button.png", new Vector4(48f, 22f, 48f, 22f), 100f);
            ConfigureSprite(UiRoot + "/Bar.png", new Vector4(34f, 18f, 34f, 18f), 100f);
            ConfigureSprite(UiRoot + "/Portrait.png", Vector4.zero, 100f);
            ConfigureSprite(UiRoot + "/Vignette.png", Vector4.zero, 100f);
            ConfigureSprite(UiRoot + "/Glow.png", Vector4.zero, 100f);
            ConfigureSprite(UiRoot + "/TurnBand.png", Vector4.zero, 100f);
            ConfigureSprite(DungeonDoorSpritePath, Vector4.zero, 96f);
        }

        private static void ExtractBossFrames(string sheetPath)
        {
            var sheet = LoadTexture(sheetPath);
            var frameWidth = sheet.width / 5;
            var frameHeight = sheet.height / 5;
            var phaseOneIndex = 0;
            for (var row = 0; row < 5; row++)
            for (var column = 0; column < 5; column++)
            {
                var phaseTwo = row == 4 && column >= 2;
                var targetRoot = phaseTwo ? BossTwoRoot : BossOneRoot;
                var index = phaseTwo ? column - 2 : phaseOneIndex++;
                var target = $"{targetRoot}/{(phaseTwo ? "boss_p2" : "boss_p1")}_{index:00}.png";
                var pixels = sheet.GetPixels(column * frameWidth,
                    sheet.height - (row + 1) * frameHeight, frameWidth, frameHeight);
                WriteTexture(target, frameWidth, frameHeight, pixels);
            }
            Object.DestroyImmediate(sheet);
        }

        private static void CreateDungeonTiles(string sheetPath)
        {
            var sheet = LoadTexture(sheetPath);
            var baseColor = sheet.GetPixel(80, 80);
            var blue = sheet.GetPixel(20, 20);
            var shadow = Color.Lerp(baseColor, Color.black, 0.42f);
            var highlight = Color.Lerp(blue, Color.white, 0.20f);

            for (var i = 0; i < 3; i++)
            {
                var floor = BuildFloorPixels(baseColor, blue, i);
                WriteTexture($"{ArtRoot}/Dungeon/Floor{i}.png", 96, 96, floor);
            }
            WriteTexture($"{ArtRoot}/Dungeon/Wall.png", 96, 96,
                BuildWallPixels(baseColor, blue, shadow, highlight, false));
            WriteTexture($"{ArtRoot}/Dungeon/MovableWall.png", 96, 96,
                BuildWallPixels(baseColor, blue, shadow, highlight, true));
            WriteTexture($"{ArtRoot}/Dungeon/Gap.png", 96, 96,
                BuildGapPixels(baseColor, blue));
            WriteTexture($"{ArtRoot}/Dungeon/Trap.png", 96, 96,
                BuildTrapPixels());
            WriteTexture(DungeonDoorSpritePath, 96, 96, BuildDoorPixels(blue, shadow));
            Object.DestroyImmediate(sheet);

            AssetDatabase.Refresh();
            for (var i = 0; i < 3; i++)
                ConfigureSprite($"{ArtRoot}/Dungeon/Floor{i}.png", Vector4.zero, 96f);
            ConfigureSprite($"{ArtRoot}/Dungeon/Wall.png", Vector4.zero, 96f);
            ConfigureSprite($"{ArtRoot}/Dungeon/MovableWall.png", Vector4.zero, 96f);
            ConfigureSprite($"{ArtRoot}/Dungeon/Gap.png", Vector4.zero, 96f);
            ConfigureSprite($"{ArtRoot}/Dungeon/Trap.png", Vector4.zero, 96f);

            CreateTileAsset(DungeonFloorTilePath,
                AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/Dungeon/Floor0.png"));
            CreateTileAsset(DungeonWallTilePath,
                AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/Dungeon/Wall.png"));
            CreateTileAsset(DungeonMovableTilePath,
                AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/Dungeon/MovableWall.png"));
            CreateTileAsset(DungeonGapTilePath,
                AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/Dungeon/Gap.png"));
            CreateTileAsset(DungeonTrapTilePath,
                AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/Dungeon/Trap.png"));
        }

        private static Color[] BuildFloorPixels(Color baseColor, Color blue, int variant)
        {
            const int size = 96;
            var pixels = new Color[size * size];
            var tone = Color.Lerp(baseColor, blue, 0.05f + variant * 0.025f);
            var edge = Color.Lerp(baseColor, Color.black, 0.48f);
            var accent = Color.Lerp(baseColor, blue, 0.26f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var color = tone;
                if (x < 2 || y < 2 || x > size - 3 || y > size - 3)
                    color = edge;
                if ((x == 8 || x == size - 9) && (y == 8 || y == size - 9))
                    color = accent;
                var chip = (x * 17 + y * 31 + variant * 13) % 211;
                if (chip == 0)
                    color = Color.Lerp(color, blue, 0.42f);
                pixels[y * size + x] = color;
            }
            return pixels;
        }

        private static Color[] BuildWallPixels(Color baseColor, Color blue, Color shadow,
            Color highlight, bool movable)
        {
            const int size = 96;
            var pixels = new Color[size * size];
            var dark = Color.Lerp(baseColor, Color.black, 0.25f);
            var block = movable
                ? Color.Lerp(blue, new Color(0.95f, 0.55f, 0.20f), 0.44f)
                : blue;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = dark;

            FillRect(pixels, size, 5, 5, 86, 82, block);
            FillRect(pixels, size, 5, 82, 86, 5, highlight);
            FillRect(pixels, size, 5, 5, 86, 6, shadow);
            for (var y = 14; y < 82; y += 18)
                FillRect(pixels, size, 5, y, 86, 2, shadow);
            for (var x = 36; x < 88; x += 37)
                FillRect(pixels, size, x, 5, 2, 82, shadow);
            FillRect(pixels, size, 10, 74, 8, 8,
                movable ? new Color32(255, 208, 92, 255) : highlight);
            FillRect(pixels, size, 78, 14, 8, 8,
                movable ? new Color32(255, 157, 54, 255) : shadow);
            return pixels;
        }

        private static Color[] BuildGapPixels(Color baseColor, Color blue)
        {
            const int size = 96;
            var pixels = new Color[size * size];
            var voidColor = Color.Lerp(baseColor, Color.black, 0.82f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var edge = x < 7 || y < 7 || x > size - 8 || y > size - 8;
                pixels[y * size + x] = edge
                    ? Color.Lerp(blue, baseColor, 0.38f)
                    : Color.Lerp(voidColor, blue, 0.03f);
            }
            return pixels;
        }

        private static Color[] BuildTrapPixels()
        {
            const int size = 96;
            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            var white = Color.white;
            for (var i = 16; i < 80; i++)
            {
                pixels[i * size + i] = white;
                pixels[i * size + (size - 1 - i)] = white;
            }
            FillRect(pixels, size, 42, 8, 12, 80, new Color(1f, 1f, 1f, 0.65f));
            FillRect(pixels, size, 8, 42, 80, 12, new Color(1f, 1f, 1f, 0.65f));
            return pixels;
        }

        private static Color[] BuildDoorPixels(Color blue, Color shadow)
        {
            const int size = 96;
            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            FillRect(pixels, size, 18, 8, 60, 78, Color.Lerp(shadow, Color.black, 0.35f));
            FillRect(pixels, size, 14, 82, 68, 8, blue);
            FillRect(pixels, size, 12, 10, 8, 74, blue);
            FillRect(pixels, size, 76, 10, 8, 74, blue);
            for (var y = 24; y < 76; y += 16)
                FillRect(pixels, size, 22, y, 52, 4,
                    Color.Lerp(blue, new Color(1f, 0.64f, 0.24f), 0.42f));
            return pixels;
        }

        private static void FillRect(Color[] pixels, int textureWidth, int x, int y,
            int width, int height, Color color)
        {
            for (var yy = y; yy < y + height; yy++)
            for (var xx = x; xx < x + width; xx++)
            {
                if (xx < 0 || yy < 0 || xx >= textureWidth ||
                    yy >= textureWidth)
                    continue;
                pixels[yy * textureWidth + xx] = color;
            }
        }

        private static void CreateTileAsset(string path, Sprite sprite)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
        }

        private static void ExtractUiSprites(string atlasPath)
        {
            var atlas = LoadTexture(atlasPath);
            CropTexture(atlas, UiRoot + "/Panel.png", 25, 20, 470, 255);
            CropTexture(atlas, UiRoot + "/PanelLarge.png", 2250, 970, 570, 350);
            CropTexture(atlas, UiRoot + "/Button.png", 1110, 45, 560, 165);
            CropTexture(atlas, UiRoot + "/Bar.png", 45, 430, 440, 90);
            CropTexture(atlas, UiRoot + "/Portrait.png", 1045, 280, 810, 950);
            TrimPortraitArtifacts(UiRoot + "/Portrait.png");
            Object.DestroyImmediate(atlas);
        }

        private static void TrimPortraitArtifacts(string assetPath)
        {
            var texture = LoadTexture(assetPath);
            var width = texture.width;
            var height = texture.height;
            var pixels = texture.GetPixels();
            Object.DestroyImmediate(texture);
            var visited = new bool[pixels.Length];
            var queue = new Queue<int>();
            var largestArea = 0;
            var minX = 0;
            var minY = 0;
            var maxX = width - 1;
            var maxY = height - 1;
            for (var start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || pixels[start].a < 0.04f)
                    continue;
                var area = 0;
                var componentMinX = width;
                var componentMinY = height;
                var componentMaxX = 0;
                var componentMaxY = 0;
                queue.Clear();
                queue.Enqueue(start);
                visited[start] = true;
                while (queue.Count > 0)
                {
                    var index = queue.Dequeue();
                    var x = index % width;
                    var y = index / width;
                    area++;
                    componentMinX = Mathf.Min(componentMinX, x);
                    componentMinY = Mathf.Min(componentMinY, y);
                    componentMaxX = Mathf.Max(componentMaxX, x);
                    componentMaxY = Mathf.Max(componentMaxY, y);
                    if (x > 0) EnqueuePortraitPixel(index - 1, width, pixels, visited,
                        queue);
                    if (x < width - 1) EnqueuePortraitPixel(index + 1, width, pixels,
                        visited, queue);
                    if (y > 0) EnqueuePortraitPixel(index - width, width, pixels, visited,
                        queue);
                    if (y < height - 1) EnqueuePortraitPixel(index + width, width, pixels,
                        visited, queue);
                }
                if (area <= largestArea)
                    continue;
                largestArea = area;
                minX = componentMinX;
                minY = componentMinY;
                maxX = componentMaxX;
                maxY = componentMaxY;
            }

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (x >= minX - 2 && x <= maxX + 2 && y >= minY - 2 &&
                    y <= maxY + 2)
                    continue;
                var color = pixels[y * width + x];
                color.a = 0f;
                pixels[y * width + x] = color;
            }
            WriteTexture(assetPath, width, height, pixels);
        }

        private static void EnqueuePortraitPixel(int index, int width, Color[] pixels,
            bool[] visited, Queue<int> queue)
        {
            if (visited[index] || pixels[index].a < 0.04f)
                return;
            visited[index] = true;
            queue.Enqueue(index);
        }

        private static void CreateUiEffects()
        {
            const int vignetteSize = 512;
            var vignette = new Color[vignetteSize * vignetteSize];
            for (var y = 0; y < vignetteSize; y++)
            for (var x = 0; x < vignetteSize; x++)
            {
                var nx = Mathf.Abs(x / (vignetteSize - 1f) * 2f - 1f);
                var ny = Mathf.Abs(y / (vignetteSize - 1f) * 2f - 1f);
                var edge = Mathf.Clamp01((Mathf.Max(nx, ny) - 0.38f) / 0.62f);
                edge = Mathf.Pow(edge, 1.55f);
                vignette[y * vignetteSize + x] =
                    new Color(0.006f, 0.012f, 0.025f, edge * 0.92f);
            }
            WriteTexture(UiRoot + "/Vignette.png", vignetteSize, vignetteSize, vignette);

            const int glowSize = 256;
            var glow = new Color[glowSize * glowSize];
            for (var y = 0; y < glowSize; y++)
            for (var x = 0; x < glowSize; x++)
            {
                var dx = x / (glowSize - 1f) * 2f - 1f;
                var dy = y / (glowSize - 1f) * 2f - 1f;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f);
                glow[y * glowSize + x] =
                    new Color(0.18f, 0.84f, 1f, alpha * 0.85f);
            }
            WriteTexture(UiRoot + "/Glow.png", glowSize, glowSize, glow);

            const int bandWidth = 512;
            const int bandHeight = 128;
            var band = new Color[bandWidth * bandHeight];
            for (var y = 0; y < bandHeight; y++)
            for (var x = 0; x < bandWidth; x++)
            {
                var nx = x / (bandWidth - 1f);
                var fadeX = Mathf.Pow(Mathf.Sin(nx * Mathf.PI), 0.45f);
                var line = y < 2 || y > bandHeight - 3 || x < 4 || x > bandWidth - 5;
                var core = Mathf.Clamp01(1f - Mathf.Abs(y / (bandHeight - 1f) - 0.5f) *
                    2f);
                var color = line
                    ? new Color(0.22f, 0.88f, 1f, 0.80f)
                    : new Color(0.025f, 0.08f, 0.13f,
                        0.88f * fadeX * Mathf.Lerp(0.48f, 1f, core));
                band[y * bandWidth + x] = color;
            }
            WriteTexture(UiRoot + "/TurnBand.png", bandWidth, bandHeight, band);
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(assetPath)))
                throw new InvalidOperationException("Could not load PNG: " + assetPath);
            return texture;
        }

        private static void CropTexture(Texture2D source, string destination, int xTop,
            int yTop, int width, int height)
        {
            var y = source.height - yTop - height;
            var pixels = source.GetPixels(xTop, y, width, height);
            WriteTexture(destination, width, height, pixels);
        }

        private static void WriteTexture(string assetPath, int width, int height,
            Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void CopyFile(string source, string destination)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException("Source artwork missing.", source);
            File.Copy(source, destination, true);
        }

        private static void ConfigureSprite(string assetPath, Vector4 border,
            float pixelsPerUnit)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Texture importer missing: " + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0f);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = border;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void CreateTrapAssets()
        {
            var whitePath = BakedTileRoot + "/TurnWhite.png";
            var whiteTexture = PixelArtHD.White.texture;
            File.WriteAllBytes(whitePath, whiteTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(whitePath);
            ConfigureSprite(whitePath, Vector4.zero, 64f);
        }

        internal static void PrepareSpriteReferences()
        {
            LoadSprites();
        }

        private static void LoadSprites()
        {
            panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/Panel.png");
            panelLargeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot +
                                                                      "/PanelLarge.png");
            buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/Button.png");
            barSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/Bar.png");
            portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/Portrait.png");
            vignetteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/Vignette.png");
            glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/Glow.png");
            turnBandSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + "/TurnBand.png");
            whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BakedTileRoot + "/TurnWhite.png");
            meleeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Melee.png");
            rangedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Ranged.png");
            bossFramesOne = AssetDatabase.FindAssets("t:Sprite", new[] { BossOneRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .OrderBy(sprite => sprite.name)
                .ToArray();
            bossFramesTwo = AssetDatabase.FindAssets("t:Sprite", new[] { BossTwoRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .OrderBy(sprite => sprite.name)
                .ToArray();
            bossSprite = bossFramesOne.Length > 0 ? bossFramesOne[0] : null;
            dungeonFloors = new[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Dungeon/Floor0.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Dungeon/Floor1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Dungeon/Floor2.png")
            };
            dungeonTrapSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                ArtRoot + "/Dungeon/Trap.png");
        }

        private static void MigrateScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var gameRoot = GameObject.Find("WrongTurnGame");
            if (gameRoot == null)
            {
                gameRoot = new GameObject("WrongTurnGame");
            }

            var oldController = gameRoot.GetComponent<ActionGameController>();
            if (oldController != null)
                Object.DestroyImmediate(oldController);
            var controller = gameRoot.GetComponent<GridTurnController>();
            if (controller == null)
                controller = gameRoot.AddComponent<GridTurnController>();

            var camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 7.6f;
                camera.transform.position = new Vector3(14f, 10f, -10f);
                camera.backgroundColor = new Color32(3, 6, 9, 255);
            }

            var player = Object.FindFirstObjectByType<PlayerRig>(FindObjectsInactive.Include);
            if (player == null)
                throw new InvalidOperationException("PlayerRig missing in Main scene.");
            BuildSoulRig(player);

            var variants = Object.FindObjectsByType<MapVariant>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).OrderBy(variant => variant.MapId).ToArray();
            foreach (var variant in variants)
            {
                variant.PlayerSpawnCell = new Vector2Int(4, 16);
                RebuildMapView(variant);
                UpdateEnemyMarkers(variant);
            }

            var presentation = BuildPresentationObjects(gameRoot.transform);
            var hud = BuildUgui();
            AssignControllerReferences(controller, hud, player, presentation);

            for (var i = 0; i < variants.Length; i++)
                variants[i].gameObject.SetActive(i == 0);
            if (variants.Length > 0 && variants[0].GridView != null)
            {
                var preview = new GridTurnSession(BuildMapDataForPreview(variants[0], player));
                preview.StartNewRun();
                variants[0].GridView.Render(preview);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void BuildSoulRig(PlayerRig player)
        {
            var oldSoul = player.transform.Find("SoulRig");
            if (oldSoul != null)
                Object.DestroyImmediate(oldSoul.gameObject);
            var soulRoot = new GameObject("SoulRig");
            soulRoot.transform.SetParent(player.transform, false);

            var glow = new GameObject("SoulGlow");
            glow.transform.SetParent(soulRoot.transform, false);
            var glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = PixelArtHD.Glow;
            glowRenderer.color = new Color(0.26f, 0.82f, 1f, 0.23f);
            glowRenderer.sortingOrder = 1090;
            glow.transform.localScale = Vector3.one * 1.9f;

            var body = new GameObject("SoulBody");
            body.transform.SetParent(soulRoot.transform, false);
            var bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/Art/Character_Down.png");
            bodyRenderer.color = new Color(0.44f, 0.82f, 1f, 0.58f);
            bodyRenderer.sortingOrder = 1100;
            body.transform.localScale = Vector3.one * 0.80f;

            player.SoulGlow = glow;
            player.SoulBody = body;
            glow.SetActive(false);
            body.SetActive(false);
        }

        internal static void RebuildMapView(MapVariant variant)
        {
            ApplyDungeonTiles(variant);
            var old = variant.transform.Find("TurnGrid");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("TurnGrid");
            root.transform.SetParent(variant.transform, false);
            var view = root.AddComponent<GridMapView>();
            view.Width = variant.Width;
            view.Height = variant.Height;
            view.Cells.Clear();

            var trapTile = AssetDatabase.LoadAssetAtPath<Tile>(DungeonTrapTilePath);
            variant.TrapTile = trapTile;
            AddTraps(variant, trapTile);

            var wallSprite = TileSprite(variant.WallTile);
            var floorSprite = TileSprite(variant.FloorTile);
            var movableSprite = TileSprite(variant.MovableWallTile) ?? wallSprite;
            var gapSprite = TileSprite(variant.GapTile) ?? floorSprite;
            var trapSprite = dungeonTrapSprite != null
                ? dungeonTrapSprite
                : trapTile != null ? trapTile.sprite : whiteSprite;
            var dropSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/Baked/light.png");
            var doorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DungeonDoorSpritePath);

            for (var y = 0; y < variant.Height; y++)
            for (var x = 0; x < variant.Width; x++)
            {
                var tile = variant.Layout != null
                    ? variant.Layout.GetTile(new Vector3Int(x, y, 0))
                    : null;
                var terrainSprite = tile == null ? wallSprite :
                    tile == trapTile ? FloorSpriteFor(x, y, floorSprite) :
                    tile == variant.GapTile ? gapSprite :
                    tile == variant.MovableWallTile ? movableSprite :
                    tile == variant.WallTile ? wallSprite :
                    FloorSpriteFor(x, y, floorSprite);
                var cellObject = new GameObject($"Cell_{x}_{y}");
                cellObject.transform.SetParent(root.transform, false);
                cellObject.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                var cell = cellObject.AddComponent<GridTileView>();
                cell.Terrain = AddRenderer(cellObject, "Terrain", terrainSprite, 0);
                cell.Trail = AddRenderer(cellObject, "Trail", whiteSprite, 20);
                cell.Trap = AddRenderer(cellObject, "Trap", trapSprite, 30);
                cell.Danger = AddRenderer(cellObject, "Danger", whiteSprite, 950);
                cell.Drop = AddRenderer(cellObject, "Drop", dropSprite, 50);
                cell.Fog = AddRenderer(cellObject, "Fog", whiteSprite, 900);
                view.Cells.Add(cell);
            }
            variant.GridView = view;

            var exitCell = variant.ExitSpawn != null
                ? variant.ExitSpawn.Cell
                : new Vector2Int(Mathf.Max(0, variant.Width - 3),
                    Mathf.Clamp(3, 0, Mathf.Max(0, variant.Height - 1)));
            var oldExit = variant.transform.Find("Exit");
            if (oldExit != null)
                Object.DestroyImmediate(oldExit.gameObject);
            var exit = new GameObject("Exit");
            exit.transform.SetParent(variant.transform, false);
            exit.transform.position = new Vector3(exitCell.x + 0.5f, exitCell.y + 0.5f, 0f);
            var exitSpawn = exit.AddComponent<GridExitSpawn>();
            exitSpawn.Cell = exitCell;
            exitSpawn.Renderer = exit.AddComponent<SpriteRenderer>();
            exitSpawn.Renderer.sprite = doorSprite;
            exitSpawn.Renderer.sortingOrder = 120;
            exitSpawn.Renderer.color = new Color(0.34f, 0.42f, 0.44f, 0.72f);
            variant.ExitSpawn = exitSpawn;
        }

        private static void ApplyDungeonTiles(MapVariant variant)
        {
            var oldFloor = variant.FloorTile;
            var oldWall = variant.WallTile;
            var oldMovable = variant.MovableWallTile;
            var oldGap = variant.GapTile;
            var oldTrap = variant.TrapTile;
            var newFloor = AssetDatabase.LoadAssetAtPath<Tile>(DungeonFloorTilePath);
            var newWall = AssetDatabase.LoadAssetAtPath<Tile>(DungeonWallTilePath);
            var newMovable = AssetDatabase.LoadAssetAtPath<Tile>(DungeonMovableTilePath);
            var newGap = AssetDatabase.LoadAssetAtPath<Tile>(DungeonGapTilePath);
            var newTrap = AssetDatabase.LoadAssetAtPath<Tile>(DungeonTrapTilePath);
            if (variant.Layout != null)
            {
                foreach (var position in variant.Layout.cellBounds.allPositionsWithin)
                {
                    var tile = variant.Layout.GetTile(position);
                    if (tile == null)
                        continue;
                    TileBase replacement;
                    if (tile == oldTrap)
                        replacement = newTrap;
                    else if (tile == oldMovable)
                        replacement = newMovable;
                    else if (tile == oldGap)
                        replacement = newGap;
                    else if (tile == oldWall)
                        replacement = newWall;
                    else
                        replacement = newFloor;
                    variant.Layout.SetTile(position, replacement);
                }
            }
            variant.FloorTile = newFloor;
            variant.WallTile = newWall;
            variant.MovableWallTile = newMovable;
            variant.GapTile = newGap;
            variant.TrapTile = newTrap;
            variant.Background = variant.MapId == 1
                ? new Color32(5, 10, 22, 255)
                : variant.MapId == 2
                    ? new Color32(20, 10, 17, 255)
                    : new Color32(4, 8, 19, 255);
            if (variant.Layout != null)
                EditorUtility.SetDirty(variant.Layout);
        }

        private static Sprite FloorSpriteFor(int x, int y, Sprite fallback)
        {
            if (dungeonFloors == null || dungeonFloors.Length == 0)
                return fallback;
            var index = Mathf.Abs(x * 17 + y * 31 + x * y) % dungeonFloors.Length;
            return dungeonFloors[index] != null ? dungeonFloors[index] : fallback;
        }

        private static void AddTraps(MapVariant variant, Tile trapTile)
        {
            if (variant.Layout == null || variant.FloorTile == null || trapTile == null)
                return;
            foreach (var position in variant.Layout.cellBounds.allPositionsWithin)
            {
                if (variant.Layout.GetTile(position) == trapTile)
                    variant.Layout.SetTile(position, variant.FloorTile);
            }
            var floor = new List<Vector3Int>();
            foreach (var position in variant.Layout.cellBounds.allPositionsWithin)
            {
                if (variant.Layout.GetTile(position) == variant.FloorTile)
                    floor.Add(position);
            }
            floor.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            if (floor.Count < 8)
                return;
            var indices = new[] { floor.Count / 5, floor.Count / 2, floor.Count * 4 / 5 };
            foreach (var index in indices)
            {
                var position = floor[Mathf.Clamp(index, 0, floor.Count - 1)];
                variant.Layout.SetTile(position, trapTile);
            }
            EditorUtility.SetDirty(variant.Layout);
        }

        private static SpriteRenderer AddRenderer(GameObject parent, string name, Sprite sprite,
            int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        internal static void UpdateEnemyMarkers(MapVariant variant)
        {
            var markers = variant.GetComponentsInChildren<EnemySpawn>(true);
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                marker.Id = i + 1;
                marker.Body = marker.transform.Find("Sprite")?.GetComponent<SpriteRenderer>();
                marker.Shadow = marker.transform.Find("Shadow")?.GetComponent<SpriteRenderer>();
                var sprite = marker.Kind == EnemyKindAction.Ranged
                    ? rangedSprite
                    : marker.Kind == EnemyKindAction.Boss
                        ? bossSprite
                        : meleeSprite;
                if (marker.Body != null)
                {
                    marker.Body.sprite = sprite;
                    marker.Body.color = Color.white;
                    marker.Body.sortingOrder = 1100;
                    FitSprite(marker.Body.transform, sprite,
                        marker.Kind == EnemyKindAction.Boss ? 1.85f : 1.08f);
                }
                if (marker.Shadow != null)
                {
                    marker.Shadow.sortingOrder = 1090;
                    marker.Shadow.enabled = true;
                }
                marker.HealthBar = EnsureEnemyHealthBar(marker,
                    marker.Kind == EnemyKindAction.Boss);
                EditorUtility.SetDirty(marker);
            }
        }

        private static GridEnemyHealthBar EnsureEnemyHealthBar(EnemySpawn marker, bool boss)
        {
            var old = marker.transform.Find("HealthBar");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("HealthBar");
            root.transform.SetParent(marker.transform, false);
            root.transform.localPosition = new Vector3(0f, boss ? 1.42f : 0.72f, 0f);
            var bar = root.AddComponent<GridEnemyHealthBar>();
            bar.Width = boss ? 2.0f : 0.86f;
            bar.Height = boss ? 0.12f : 0.075f;

            var background = new GameObject("Background");
            background.transform.SetParent(root.transform, false);
            bar.Background = background.AddComponent<SpriteRenderer>();
            bar.Background.sprite = whiteSprite;
            bar.Background.color = new Color(0.015f, 0.02f, 0.03f, 0.95f);
            bar.Background.sortingOrder = 2200;
            background.transform.localScale = new Vector3(bar.Width + 0.06f,
                bar.Height + 0.045f, 1f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(root.transform, false);
            bar.Fill = fill.transform;
            bar.FillRenderer = fill.AddComponent<SpriteRenderer>();
            bar.FillRenderer.sprite = whiteSprite;
            bar.FillRenderer.sortingOrder = 2201;
            fill.transform.localScale = new Vector3(bar.Width, bar.Height, 1f);
            bar.FillRenderer.enabled = false;
            bar.Background.enabled = false;
            return bar;
        }

        private static void FitSprite(Transform transform, Sprite sprite, float targetCells)
        {
            if (sprite == null)
                return;
            var size = sprite.rect.size;
            var max = Mathf.Max(size.x, size.y);
            if (max <= 0f)
                return;
            var ppu = sprite.pixelsPerUnit <= 0f ? 100f : sprite.pixelsPerUnit;
            var worldMax = max / ppu;
            var scale = targetCells / Mathf.Max(0.01f, worldMax);
            transform.localScale = Vector3.one * scale;
        }

        private sealed class PresentationObjects
        {
            public LineRenderer AimLine;
            public SpriteRenderer AimReticle;
            public LineRenderer AttackFlash;
            public Transform ProjectileRoot;
        }

        private static PresentationObjects BuildPresentationObjects(Transform parent)
        {
            var old = GameObject.Find("TurnPresentation");
            if (old != null)
                Object.DestroyImmediate(old);
            var root = new GameObject("TurnPresentation");
            root.transform.SetParent(parent, false);

            var lineObject = new GameObject("AimLine");
            lineObject.transform.SetParent(root.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.05f;
            line.endWidth = 0.11f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = 7000;
            line.enabled = false;

            var reticleObject = new GameObject("AimReticle");
            reticleObject.transform.SetParent(root.transform, false);
            var reticle = reticleObject.AddComponent<SpriteRenderer>();
            reticle.sprite = PixelArtHD.Target;
            reticle.sortingOrder = 7010;
            reticle.transform.localScale = Vector3.one * 0.62f;
            reticle.enabled = false;

            var flashObject = new GameObject("AttackFlash");
            flashObject.transform.SetParent(root.transform, false);
            var flash = flashObject.AddComponent<LineRenderer>();
            flash.useWorldSpace = true;
            flash.positionCount = 2;
            flash.startWidth = 0.16f;
            flash.endWidth = 0.52f;
            flash.material = new Material(Shader.Find("Sprites/Default"));
            flash.sortingOrder = 7050;
            flash.enabled = false;

            var projectileRoot = new GameObject("Projectiles");
            projectileRoot.transform.SetParent(root.transform, false);
            for (var i = 0; i < 32; i++)
            {
                var bullet = new GameObject("Projectile_" + i);
                bullet.transform.SetParent(projectileRoot.transform, false);
                var renderer = bullet.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 6900;
                bullet.SetActive(false);
            }
            return new PresentationObjects
            {
                AimLine = line,
                AimReticle = reticle,
                AttackFlash = flash,
                ProjectileRoot = projectileRoot.transform
            };
        }

        private static void AssignControllerReferences(GridTurnController controller,
            GridTurnHud hud, PlayerRig player, PresentationObjects presentation)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("hud").objectReferenceValue = hud;
            serialized.FindProperty("playerRig").objectReferenceValue = player;
            serialized.FindProperty("aimLine").objectReferenceValue = presentation.AimLine;
            serialized.FindProperty("aimReticle").objectReferenceValue = presentation.AimReticle;
            serialized.FindProperty("attackFlash").objectReferenceValue =
                presentation.AttackFlash;
            serialized.FindProperty("projectileRoot").objectReferenceValue =
                presentation.ProjectileRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GridMapData BuildMapDataForPreview(MapVariant variant, PlayerRig player)
        {
            var map = new GridMapData(variant.Width, variant.Height)
            {
                PlayerSpawn = variant.PlayerSpawnCell,
                Exit = new Vector2Int(25, 3)
            };
            if (variant.ExitSpawn != null)
                map.Exit = variant.ExitSpawn.Cell;
            for (var x = 0; x < variant.Width; x++)
            for (var y = 0; y < variant.Height; y++)
                map.InitialTiles[x, y] = GridTileKind.Wall;
            if (variant.Layout != null)
            {
                foreach (var pos in variant.Layout.cellBounds.allPositionsWithin)
                {
                    if (pos.x < 0 || pos.x >= variant.Width || pos.y < 0 ||
                        pos.y >= variant.Height)
                        continue;
                    var tile = variant.Layout.GetTile(pos);
                    if (tile == null)
                        continue;
                    map.InitialTiles[pos.x, pos.y] =
                        tile == variant.MovableWallTile ? GridTileKind.MovableWall :
                        tile == variant.GapTile ? GridTileKind.Gap :
                        tile == variant.TrapTile ? GridTileKind.DamageTrap :
                        tile == variant.WallTile ? GridTileKind.Wall : GridTileKind.Floor;
                }
            }
            return map;
        }

        private static GridTurnHud BuildUgui()
        {
            var oldCanvas = GameObject.Find("TurnBasedCanvas");
            if (oldCanvas != null)
                Object.DestroyImmediate(oldCanvas);
            var oldEventSystem = Object.FindFirstObjectByType<EventSystem>(
                FindObjectsInactive.Include);
            if (oldEventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }

            var canvasObject = new GameObject("TurnBasedCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var vignette = AddImage(canvasObject.transform, "DungeonVignette", Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, vignetteSprite);
            vignette.raycastTarget = false;

            var hudObject = new GameObject("GridTurnHud", typeof(RectTransform));
            hudObject.transform.SetParent(canvasObject.transform, false);
            Stretch(hudObject.GetComponent<RectTransform>());
            var hud = hudObject.AddComponent<GridTurnHud>();

            hud.TitleRoot = BuildTitleUi(hudObject.transform, hud);
            hud.GameRoot = BuildGameHud(hudObject.transform, hud);
            hud.PauseRoot = BuildPauseUi(hudObject.transform, hud);
            hud.MemoryRoot = BuildMemoryUi(hudObject.transform, hud);
            hud.EndRoot = BuildEndUi(hudObject.transform, hud);
            return hud;
        }

        private static GameObject BuildTitleUi(Transform parent, GridTurnHud hud)
        {
            var root = UiRootObject("TitleRoot", parent);
            AddImage(root.transform, "Backdrop", new Color32(2, 5, 12, 244),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panel = AddPanel(root.transform, "TitlePanel", panelLargeSprite,
                new Color(0.035f, 0.075f, 0.12f, 0.96f), new Vector2(70f, 0f),
                new Vector2(760f, 720f), new Vector2(0f, 0.5f));
            AddImage(panel.transform, "AccentLine", new Color(0.26f, 0.9f, 1f, 0.85f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -90f),
                new Vector2(5f, 500f), whiteSprite);
            AddImage(panel.transform, "GoldLine", new Color(1f, 0.72f, 0.24f, 0.75f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -110f),
                new Vector2(2f, 460f), whiteSprite);
            var title = AddText(panel.transform, "Title", "走错也算数",
                new Vector2(48f, -42f), new Vector2(620f, 100f), 64, TextAnchor.MiddleLeft,
                new Color32(236, 244, 255, 255));
            title.fontStyle = FontStyle.Bold;
            AddText(panel.transform, "Subtitle", "WRONG TURN / GRID PATH",
                new Vector2(50f, -132f), new Vector2(600f, 42f), 24,
                TextAnchor.MiddleLeft, new Color32(83, 220, 245, 255));
            hud.TitleDescription = AddText(panel.transform, "Description",
                "玩家回合 / 世界回合\n四向移动 · 八向选向 · 立即攻击",
                new Vector2(50f, -205f), new Vector2(620f, 130f), 27,
                TextAnchor.UpperLeft, new Color32(174, 203, 226, 255));

            hud.MapButtons = new Button[3];
            hud.MapButtonLabels = new Text[3];
            var names = new[] { "森林", "冰原", "沙漠" };
            for (var i = 0; i < 3; i++)
            {
                var button = AddButton(panel.transform, "Map" + i, names[i],
                    new Vector2(50f + i * 205f, -390f), new Vector2(185f, 82f), 26f);
                hud.MapButtons[i] = button;
                hud.MapButtonLabels[i] = button.GetComponentInChildren<Text>();
            }
            hud.StartButton = AddButton(panel.transform, "Start", "开始探索",
                new Vector2(50f, -520f), new Vector2(540f, 100f), 34f);
            hud.TitleStageText = hud.StartButton.GetComponentInChildren<Text>();

            hud.TitlePortraitGlow = AddImage(root.transform, "TitlePortraitGlow",
                new Color(0.12f, 0.72f, 1f, 0.18f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-380f, 0f),
                new Vector2(960f, 960f), glowSprite, new Vector2(1f, 0.5f));
            hud.TitlePortrait = AddImage(root.transform, "TitlePortrait", Color.white,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-380f, 0f),
                new Vector2(700f, 750f), portraitSprite, new Vector2(1f, 0.5f));
            hud.TitlePortrait.preserveAspect = true;
            AddText(root.transform, "Footer", "四向视野 · 虚影穿墙 · 影子格数 · Boss 二阶段",
                new Vector2(0f, -36f), new Vector2(1600f, 40f), 22, TextAnchor.MiddleCenter,
                new Color32(255, 199, 105, 255), new Vector2(0.5f, 0f));
            return root;
        }

        private static GameObject BuildGameHud(Transform parent, GridTurnHud hud)
        {
            var root = UiRootObject("GameRoot", parent);
            var status = AddPanel(root.transform, "StatusPanel", panelSprite,
                new Color(0.025f, 0.065f, 0.10f, 0.94f), new Vector2(28f, -28f),
                new Vector2(570f, 240f), new Vector2(0f, 1f));
            hud.Portrait = AddImage(status.transform, "Portrait", Color.white,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f),
                new Vector2(150f, 170f), portraitSprite, new Vector2(0f, 1f));
            hud.Portrait.preserveAspect = true;
            hud.HpText = AddText(status.transform, "HpText", "生命 3 / 3",
                new Vector2(190f, -22f), new Vector2(330f, 35f), 24,
                TextAnchor.MiddleLeft, new Color32(255, 151, 151, 255));
            hud.ShadowText = AddText(status.transform, "ShadowText", "影子 12 格",
                new Vector2(190f, -92f), new Vector2(330f, 35f), 24,
                TextAnchor.MiddleLeft, new Color32(91, 224, 245, 255));
            hud.LightText = AddText(status.transform, "LightText", "微光 5 格",
                new Vector2(190f, -162f), new Vector2(330f, 35f), 24,
                TextAnchor.MiddleLeft, new Color32(255, 207, 104, 255));
            hud.HpCells = BuildCells(status.transform, "HpCells", 3,
                new Vector2(190f, -58f), 46f, 12f);
            hud.ShadowCells = BuildCells(status.transform, "ShadowCells", 12,
                new Vector2(190f, -128f), 22f, 8f);
            hud.LightCells = BuildCells(status.transform, "LightCells", 10,
                new Vector2(190f, -198f), 26f, 8f);

            hud.TurnGlow = AddImage(root.transform, "TurnGlow",
                new Color(0.12f, 0.72f, 1f, 0.24f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f),
                new Vector2(720f, 230f), glowSprite, new Vector2(0.5f, 0.5f));
            AddImage(root.transform, "TurnBand", Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f),
                new Vector2(680f, 86f), turnBandSprite, new Vector2(0.5f, 1f));
            hud.TurnText = AddText(root.transform, "TurnText", "玩家回合",
                new Vector2(0f, -22f), new Vector2(430f, 60f), 34,
                TextAnchor.MiddleCenter, new Color32(236, 241, 255, 255),
                new Vector2(0.5f, 1f));
            hud.StageText = AddText(root.transform, "StageText", "1 / 3 森林",
                new Vector2(-28f, -28f), new Vector2(310f, 40f), 24,
                TextAnchor.MiddleRight, new Color32(200, 220, 240, 255),
                new Vector2(1f, 1f));
            var bossPanel = AddPanel(root.transform, "BossBar", panelSprite,
                new Color(0.055f, 0.025f, 0.03f, 0.95f), new Vector2(0f, -102f),
                new Vector2(720f, 66f), new Vector2(0.5f, 1f));
            hud.BossBarRoot = bossPanel.gameObject;
            AddImage(bossPanel, "BossTrack", new Color(0.03f, 0.015f, 0.02f, 0.96f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -17f),
                new Vector2(680f, 16f), whiteSprite);
            hud.BossFill = AddImage(bossPanel, "BossFill",
                new Color(1f, 0.54f, 0.18f, 0.96f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(20f, -17f), new Vector2(680f, 16f),
                whiteSprite);
            hud.BossFill.type = Image.Type.Filled;
            hud.BossFill.fillMethod = Image.FillMethod.Horizontal;
            hud.BossFill.fillOrigin = 0;
            hud.BossText = AddText(bossPanel, "BossText", "守门者",
                new Vector2(0f, -8f), new Vector2(700f, 42f), 24,
                TextAnchor.MiddleCenter, new Color32(255, 224, 190, 255),
                new Vector2(0.5f, 1f));
            hud.BossBarRoot.SetActive(false);

            var bottom = AddPanel(root.transform, "BottomPanel", panelSprite,
                new Color(0.055f, 0.08f, 0.12f, 0.94f), new Vector2(0f, 26f),
                new Vector2(1220f, 154f), new Vector2(0.5f, 0f));
            hud.ObjectiveText = AddText(bottom.transform, "Objective",
                "探索四向视野，寻找冰晶和 Boss。", new Vector2(24f, -10f),
                new Vector2(1170f, 36f), 23, TextAnchor.MiddleLeft,
                new Color32(226, 238, 255, 255));
            hud.ToastText = AddText(bottom.transform, "Toast",
                "WASD 移动 · 鼠标选向 / 左键攻击 · F 交互 · Q 虚影 · E 记录 · 空格等待",
                new Vector2(24f, -56f), new Vector2(1170f, 42f), 20,
                TextAnchor.MiddleLeft, new Color32(156, 192, 220, 255));
            hud.ControlHint = AddText(bottom.transform, "ControlHint",
                "鼠标选向攻击 · 近战红格可躲避 · 远程蓄力一回合后发射可见弹体",
                new Vector2(24f, -101f), new Vector2(1170f, 36f), 18,
                TextAnchor.MiddleLeft, new Color32(255, 192, 88, 220));
            return root;
        }

        private static GameObject BuildPauseUi(Transform parent, GridTurnHud hud)
        {
            var root = UiRootObject("PauseRoot", parent);
            AddImage(root.transform, "Shade", new Color(0.01f, 0.02f, 0.04f, 0.80f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panel = AddPanel(root.transform, "Panel", panelLargeSprite,
                new Color(0.025f, 0.07f, 0.11f, 0.98f), Vector2.zero,
                new Vector2(620f, 470f), new Vector2(0.5f, 0.5f));
            AddText(panel.transform, "Title", "暂停", new Vector2(0f, -42f),
                new Vector2(620f, 72f), 46, TextAnchor.MiddleCenter,
                new Color32(220, 244, 255, 255), new Vector2(0.5f, 1f));
            hud.ResumeButton = AddButton(panel.transform, "Resume", "继续",
                new Vector2(0f, -150f), new Vector2(470f, 74f), 28f,
                new Vector2(0.5f, 1f));
            hud.PauseRestartButton = AddButton(panel.transform, "Restart", "重新开始",
                new Vector2(0f, -245f), new Vector2(470f, 74f), 28f,
                new Vector2(0.5f, 1f));
            hud.PauseMapButton = AddButton(panel.transform, "Map", "返回选图",
                new Vector2(0f, -340f), new Vector2(470f, 74f), 28f,
                new Vector2(0.5f, 1f));
            return root;
        }

        private static GameObject BuildMemoryUi(Transform parent, GridTurnHud hud)
        {
            var root = UiRootObject("MemoryRoot", parent);
            AddImage(root.transform, "Shade", new Color(0.01f, 0.02f, 0.04f, 0.78f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(root.transform, "MemoryGlow", new Color(0.15f, 0.64f, 1f, 0.22f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1250f, 700f), glowSprite, new Vector2(0.5f, 0.5f));
            hud.MemoryTitle = AddText(root.transform, "Title", "触碰记忆冰晶",
                new Vector2(0f, -150f), new Vector2(1100f, 80f), 44,
                TextAnchor.MiddleCenter, new Color32(102, 230, 255, 255),
                new Vector2(0.5f, 1f));
            hud.MemoryButtons = new Button[2];
            hud.MemoryButtonTitles = new Text[2];
            hud.MemoryButtonDescriptions = new Text[2];
            for (var i = 0; i < 2; i++)
            {
                var button = AddButton(root.transform, "Memory" + i, "",
                    new Vector2(i == 0 ? -330f : 330f, -250f), new Vector2(580f, 310f),
                    28f, new Vector2(0.5f, 1f));
                hud.MemoryButtons[i] = button;
                hud.MemoryButtonTitles[i] = AddText(button.transform, "Title", "",
                    new Vector2(25f, -24f), new Vector2(530f, 55f), 30,
                    TextAnchor.MiddleLeft, new Color32(222, 246, 255, 255),
                    new Vector2(0f, 1f));
                hud.MemoryButtonDescriptions[i] = AddText(button.transform, "Desc", "",
                    new Vector2(25f, -92f), new Vector2(530f, 180f), 23,
                    TextAnchor.UpperLeft, new Color32(164, 201, 224, 255),
                    new Vector2(0f, 1f));
            }
            return root;
        }

        private static GameObject BuildEndUi(Transform parent, GridTurnHud hud)
        {
            var root = UiRootObject("EndRoot", parent);
            AddImage(root.transform, "Shade", new Color(0.01f, 0.02f, 0.04f, 0.86f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panel = AddPanel(root.transform, "Panel", panelLargeSprite,
                new Color(0.025f, 0.07f, 0.11f, 0.98f), Vector2.zero,
                new Vector2(760f, 540f), new Vector2(0.5f, 0.5f));
            hud.EndTitle = AddText(panel.transform, "Title", "走出错误之路",
                new Vector2(0f, -45f), new Vector2(700f, 80f), 46,
                TextAnchor.MiddleCenter, new Color32(255, 210, 118, 255),
                new Vector2(0.5f, 1f));
            hud.EndDescription = AddText(panel.transform, "Description", "",
                new Vector2(60f, -140f), new Vector2(640f, 130f), 26,
                TextAnchor.UpperCenter, new Color32(183, 215, 233, 255),
                new Vector2(0.5f, 1f));
            hud.EndRestartButton = AddButton(panel.transform, "Restart", "再走一次",
                new Vector2(-210f, -340f), new Vector2(360f, 82f), 28f,
                new Vector2(0.5f, 1f));
            hud.EndMapButton = AddButton(panel.transform, "Map", "选择地图",
                new Vector2(210f, -340f), new Vector2(360f, 82f), 28f,
                new Vector2(0.5f, 1f));
            return root;
        }

        private static GameObject UiRootObject(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            return root;
        }

        private static RectTransform AddPanel(Transform parent, string name, Sprite sprite,
            Color color, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var image = AddImage(parent, name, color, anchor, anchor, position, size, sprite);
            image.rectTransform.pivot = anchor;
            image.type = Image.Type.Sliced;
            return image.rectTransform;
        }

        private static Image AddImage(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size,
            Sprite sprite = null, Vector2? pivot = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot ?? new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null)
                image.type = Image.Type.Sliced;
            return image;
        }

        private static Text AddText(Transform parent, string name, string content,
            Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color,
            Vector2? anchor = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            var anchorValue = anchor ?? new Vector2(0f, 1f);
            rect.anchorMin = anchorValue;
            rect.anchorMax = anchorValue;
            rect.pivot = anchorValue;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = gameObject.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button AddButton(Transform parent, string name, string label,
            Vector2 position, Vector2 size, float fontSize,
            Vector2? anchor = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(Button));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            var anchorValue = anchor ?? new Vector2(0f, 1f);
            rect.anchorMin = anchorValue;
            rect.anchorMax = anchorValue;
            rect.pivot = anchorValue;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = gameObject.GetComponent<Image>();
            image.sprite = buttonSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.055f, 0.22f, 0.31f, 0.96f);
            var button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            var text = AddText(gameObject.transform, "Label", label, Vector2.zero,
                size, Mathf.RoundToInt(fontSize), TextAnchor.MiddleCenter,
                new Color32(220, 244, 255, 255), new Vector2(0.5f, 0.5f));
            Stretch(text.rectTransform);
            return button;
        }

        private static Image[] BuildCells(Transform parent, string name, int count,
            Vector2 position, float cellWidth, float gap)
        {
            var root = UiRootObject(name, parent);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(340f, 20f);
            var cells = new Image[count];
            for (var i = 0; i < count; i++)
            {
                var cell = AddImage(root.transform, "Cell" + i, new Color(0.1f, 0.2f, 0.25f),
                    new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(i * (cellWidth + gap), 0f),
                    new Vector2(cellWidth, 12f), barSprite);
                cells[i] = cell;
            }
            return cells;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite TileSprite(TileBase tileBase)
        {
            var tile = tileBase as Tile;
            return tile != null ? tile.sprite : null;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }

        private static void Check(bool condition, string message, ICollection<string> failures)
        {
            if (!condition)
                failures.Add(message);
        }
    }
}
