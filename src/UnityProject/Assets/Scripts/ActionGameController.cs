using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WrongTurn
{
    public sealed class ActionGameController : MonoBehaviour
    {
        private enum GameMode
        {
            Title,
            Playing,
            Paused,
            MemoryChoice,
            Dead,
            Won,
            Lost
        }

        private enum EnemyKindAction
        {
            Melee,
            Ranged,
            Boss
        }

        private enum EnemyStateAction
        {
            Idle,
            Chase,
            Telegraph,
            Attack,
            Recover
        }

        private enum TutorialStep
        {
            Move,
            Shoot,
            Dash,
            Interact,
            Record,
            Complete
        }

        private sealed class TileView
        {
            public SpriteRenderer Floor;
            public SpriteRenderer Wall;
            public SpriteRenderer Shadow;
            public SpriteRenderer Trail;
            public SpriteRenderer Inherited;
            public SpriteRenderer Danger;
        }

        private sealed class LiveEnemy
        {
            public int Id;
            public EnemyKindAction Kind;
            public Vector2 Position;
            public Vector2 Home;
            public int Hp;
            public int MaxHp;
            public bool Alive = true;
            public EnemyStateAction State = EnemyStateAction.Idle;
            public float Timer;
            public float Cooldown;
            public float Flash;
            public float Invulnerable;
            public Vector2 LockedDirection;
            public Vector2 LockedTarget;
            public bool ChargeHit;
            public Color BaseTint = Color.white;
            public GameObject View;
            public SpriteRenderer Renderer;
        }

        private sealed class Projectile
        {
            public bool Friendly;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float Damage;
            public float Radius;
            public int Bounces;
            public GameObject View;
            public SpriteRenderer Renderer;
        }

        private sealed class GhostTrail
        {
            public readonly List<Vector2Int> Cells = new List<Vector2Int>();
            public readonly List<Vector2> Points = new List<Vector2>();
            public float Life;
            public float Tick;
            public float Damage;
            public GameObject View;
            public LineRenderer Line;
        }

        private sealed class ScoutOrb
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public GameObject View;
            public LineRenderer Tail;
            public readonly List<Vector2> Trail = new List<Vector2>();
        }

        private sealed class MemoryNodeAction
        {
            public Vector2 Position;
            public string Title;
            public bool Used;
            public GameObject View;
        }

        private sealed class LightNodeAction
        {
            public Vector2 Position;
            public bool Available = true;
            public GameObject View;
        }

        private sealed class TutorialTarget
        {
            public Vector2 Position;
            public int Hp = 3;
            public bool Alive = true;
            public GameObject View;
            public SpriteRenderer Renderer;
            public float Flash;
        }

        private sealed class MemoryOption
        {
            public string Id;
            public string Title;
            public string Tag;
            public string Description;
            public Color Color;
        }

        private const int MapWidth = 28;
        private const int MapHeight = 20;
        private const float PlayerRadius = 0.28f;
        private const float DesignWidth = 1280f;
        private const float DesignHeight = 720f;

        private static readonly Vector2 StartPosition = new Vector2(4.5f, 16.5f);
        private static readonly Vector2 ExitPosition = new Vector2(25.5f, 3.5f);
        private static readonly string[] StageNames = { "森林", "冰原", "沙漠" };
        private static readonly string[] BossNames = { "苔岩守门者", "霜壳守门者", "赤砂守门者" };
        private static readonly Color[] StageBackgrounds =
        {
            new Color32(9, 31, 41, 255),
            new Color32(14, 31, 53, 255),
            new Color32(52, 31, 27, 255)
        };
        private static readonly Vector2[][] StageCrystals =
        {
            new[] { new Vector2(2.5f, 10.5f), new Vector2(25.3f, 7.3f) },
            new[] { new Vector2(3.5f, 9.5f), new Vector2(23.5f, 6.5f) },
            new[] { new Vector2(4.5f, 6.5f), new Vector2(24.2f, 8.2f) }
        };
        private static readonly Vector2[][] StageLights =
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
        private static readonly Color Ink = new Color32(7, 18, 25, 255);
        private static readonly Color Paper = new Color32(240, 235, 215, 255);
        private static readonly Color Muted = new Color32(152, 177, 178, 255);
        private static readonly Color Gold = new Color32(251, 184, 69, 255);
        private static readonly Color Cyan = new Color32(92, 231, 239, 255);
        private static readonly Color Coral = new Color32(248, 92, 83, 255);
        private static readonly Color Mint = new Color32(145, 231, 174, 255);

        private static readonly string[][] MemoryText =
        {
            new[]
            {
                "尖刻回声|刃痕加深|战斗残影每跳伤害提高。让误入的敌人付出更高代价。|M1A",
                "长夜残响|余光延长|战斗残影持续时间从 6 秒提升到 9 秒。|M1B"
            },
            new[]
            {
                "惊险回身|痛感蓄能|每条生命首次受伤时立即获得 2 影能。|M2A",
                "灯下留步|路线返能|生成战斗残影时返还 3 影能。|M2B"
            }
        };

        private GameMode mode = GameMode.Title;
        private readonly TileKind[,] tiles = new TileKind[MapWidth, MapHeight];
        private readonly TileView[,] tileViews = new TileView[MapWidth, MapHeight];
        private readonly bool[,] explored = new bool[MapWidth, MapHeight];
        private readonly float[,] tileLight = new float[MapWidth, MapHeight];
        private readonly HashSet<Vector2Int> currentTrail = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> inheritedTrail = new HashSet<Vector2Int>();
        private readonly HashSet<string> memories = new HashSet<string>();
        private readonly List<LiveEnemy> enemies = new List<LiveEnemy>();
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly List<GhostTrail> ghosts = new List<GhostTrail>();
        private readonly List<MemoryNodeAction> crystals = new List<MemoryNodeAction>();
        private readonly List<LightNodeAction> lights = new List<LightNodeAction>();
        private readonly List<Vector2> recordPoints = new List<Vector2>();
        private readonly List<Vector2Int> recordCells = new List<Vector2Int>();
        private readonly List<MemoryOption> activeChoices = new List<MemoryOption>();

        private Transform world;
        private Camera cam;
        private ProceduralAudio audio;
        private GameObject playerView;
        private GameObject playerShadow;
        private GameObject playerGlow;
        private GameObject weaponView;
        private GameObject reticleView;
        private GameObject bossDoor;
        private GameObject bossSeal;
        private GameObject activeGhostLineObject;
        private LineRenderer activeGhostLine;
        private TutorialTarget tutorialTarget;
        private GameObject tutorialMarker;
        private GameObject tutorialRelic;
        private GameObject damageFlash;
        private ScoutOrb scout;
        private MemoryNodeAction openMemory;

        private Vector2 playerPos = StartPosition;
        private Vector2 playerVelocity;
        private Vector2 aimDirection = Vector2.right;
        private Vector3 cameraVelocity;
        private Vector2 dashDirection = Vector2.right;
        private float hp = 3f;
        private int lives = 3;
        private float shadowEnergy = 12f;
        private float lightEnergy = 5f;
        private float fireCooldown;
        private float dashCooldown;
        private float dashTime;
        private float invulnerable;
        private float hurtFlash;
        private float deathTimer;
        private float actionTime;
        private float shakeTime;
        private float shakeStrength;
        private bool carryingWall;
        private bool bossActive;
        private bool bossDefeated;
        private bool exitOpen;
        private bool hitResponseUsed;
        private bool recording;
        private float recordSampleTimer;
        private float stepTimer;
        private bool tutorialSkipped;
        private TutorialStep tutorialStep;
        private bool tutorialDashed;
        private bool tutorialRelicCollected;
        private int tutorialShootHits;
        private bool tutorialRecordCompleted;
        private int selectedMap;
        private int currentMap;
        private float toastTimer;
        private string toastText = "";
        private Color toastColor = Paper;
        private float lightingTimer;
        private int memoryChoiceIndex;
        private int bossChoice;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle tinyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle centerStyle;
        private GUIStyle monoStyle;
        private bool styleReady;
        private Texture2D scanlines;
        private Sprite heroDown;
        private Sprite heroUp;
        private Sprite heroLeft;
        private Sprite heroRight;
        private Texture2D heroPortrait;
        private AudioSource musicA;
        private AudioSource musicB;
        private string activeMusic = "";
        private int musicVersion;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            LoadExternalArt();
            SetupCamera();
            BuildMap();
            BuildWorld();
            CreatePlayer();
            CreateInteractiveObjects();
            SpawnEnemies();
            BuildTitleScene();
            audio = new ProceduralAudio(gameObject);
            audio.SetAmbienceVolume(0.025f);
            SetupMusic();
            PlayMusic("Exploration_Mist", true, 0.2f);

            var args = Environment.GetCommandLineArgs();
            int mapArgument = Array.FindIndex(args, a =>
                string.Equals(a, "-stage", StringComparison.OrdinalIgnoreCase));
            if (mapArgument >= 0 && mapArgument + 1 < args.Length)
            {
                int requestedMap;
                if (int.TryParse(args[mapArgument + 1], out requestedMap))
                    selectedMap = Mathf.Clamp(requestedMap - 1, 0, 2);
            }
            if (args.Any(a => string.Equals(a, "-autostart", StringComparison.OrdinalIgnoreCase)))
                StartNewRun();
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int screenshotIndex = Array.FindIndex(args, a =>
                string.Equals(a, "-screenshot", StringComparison.OrdinalIgnoreCase));
            if (screenshotIndex >= 0 && screenshotIndex + 1 < args.Length)
                StartCoroutine(ScreenshotAndQuit(args[screenshotIndex + 1]));
            int testIndex = Array.FindIndex(args, a =>
                string.Equals(a, "-autotest", StringComparison.OrdinalIgnoreCase));
            if (testIndex >= 0 && testIndex + 1 < args.Length)
                StartCoroutine(SelfTestAndQuit(args[testIndex + 1]));
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            actionTime += dt;
            UpdateTitle();
            if (mode != GameMode.Playing && mode != GameMode.Dead)
            {
                UpdateTitleSceneMotion(dt);
                return;
            }

            if (mode == GameMode.Dead)
            {
                deathTimer += dt;
                if (deathTimer > 1.25f)
                    Respawn();
                UpdatePresentation(dt);
                return;
            }

            UpdateAim();
            UpdatePlayerMovement(dt);
            UpdateWeapon(dt);
            UpdateTutorial();
            UpdateRecord(dt);
            UpdateScout(dt);
            UpdateEnemies(dt);
            UpdateProjectiles(dt);
            UpdateGhosts(dt);
            UpdatePickups(dt);
            UpdateInteractions();
            UpdatePresentation(dt);
        }

        private void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.65f;
            cam.transform.position = new Vector3(12f, 10f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(9, 31, 41, 255);
            cam.transform.position = new Vector3(StartPosition.x, StartPosition.y + 1.8f, -10f);
        }

        private void LoadExternalArt()
        {
            heroDown = LoadResourceSprite("Art/Character_Down", 480f);
            heroUp = LoadResourceSprite("Art/Character_Up", 480f);
            heroLeft = LoadResourceSprite("Art/Character_Left", 480f);
            heroRight = LoadResourceSprite("Art/Character_Right", 480f);
            heroPortrait = Resources.Load<Texture2D>("Art/Character_Portrait");
            if (heroPortrait != null)
                heroPortrait.filterMode = FilterMode.Point;
            if (heroDown == null) heroDown = PixelArtHD.Player;
            if (heroUp == null) heroUp = PixelArtHD.Player;
            if (heroLeft == null) heroLeft = PixelArtHD.Player;
            if (heroRight == null) heroRight = PixelArtHD.Player;
        }

        private static Sprite LoadResourceSprite(string path, float pixelsPerUnit)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
                return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private void SetupMusic()
        {
            musicA = gameObject.AddComponent<AudioSource>();
            musicB = gameObject.AddComponent<AudioSource>();
            foreach (var source in new[] { musicA, musicB })
            {
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
            }
        }

        private void PlayMusic(string resourceName, bool loop, float fadeDuration = 0.85f)
        {
            if (musicA == null || musicB == null)
                return;
            if (activeMusic == resourceName &&
                (musicA.isPlaying && musicA.clip != null || musicB.isPlaying && musicB.clip != null))
                return;

            var clip = Resources.Load<AudioClip>("Music/" + resourceName);
            if (clip == null)
            {
                Debug.LogWarning("Music asset missing: Music/" + resourceName);
                return;
            }

            AudioSource from = musicA.volume >= musicB.volume ? musicA : musicB;
            AudioSource to = from == musicA ? musicB : musicA;
            to.Stop();
            to.clip = clip;
            to.loop = loop;
            to.volume = 0f;
            to.Play();
            activeMusic = resourceName;
            musicVersion++;
            StartCoroutine(CrossfadeMusic(from, to, fadeDuration, musicVersion));
        }

        private IEnumerator CrossfadeMusic(AudioSource from, AudioSource to, float duration,
            int version)
        {
            const float target = 0.34f;
            float elapsed = 0f;
            float fromStart = from != null ? from.volume : 0f;
            while (elapsed < duration && musicVersion == version)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                if (from != null)
                    from.volume = Mathf.Lerp(fromStart, 0f, t);
                to.volume = Mathf.Lerp(0f, target, t);
                yield return null;
            }
            if (musicVersion != version)
                yield break;
            if (from != null)
            {
                from.Stop();
                from.volume = 0f;
            }
            to.volume = target;
        }

        private void StopMusic(float fadeDuration)
        {
            musicVersion++;
            StartCoroutine(FadeOutMusic(musicA, fadeDuration));
            StartCoroutine(FadeOutMusic(musicB, fadeDuration));
            activeMusic = "";
        }

        private IEnumerator FadeOutMusic(AudioSource source, float duration)
        {
            float start = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            source.Stop();
            source.volume = 0f;
        }

        private void BuildMap()
        {
            for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
                tiles[x, y] = TileKind.FixedWall;

            if (currentMap == 1)
                BuildIceMap();
            else if (currentMap == 2)
                BuildDesertMap();
            else
                BuildForestMap();
        }

        private void BuildForestMap()
        {
            Carve(2, 14, 7, 5);  // training room
            Carve(5, 9, 2, 5);   // teaching corridor
            Carve(1, 9, 5, 4);   // first memory room
            Carve(7, 8, 8, 6);   // main combat hall
            Carve(14, 10, 4, 2); // connecting corridor
            Carve(17, 8, 9, 6);  // dark hall
            Carve(18, 6, 8, 2);  // wall shortcut
            Carve(18, 2, 8, 5);  // boss arena
            Carve(2, 3, 7, 5);   // lower memory branch
            Carve(4, 7, 2, 2);   // lower connector
            Carve(8, 7, 2, 2);   // central lower connector

            // The two shortcuts are optional: the long route remains always available.
            tiles[21, 7] = TileKind.MovableWall;
            tiles[22, 7] = TileKind.Gap;

            // Cover in the combat rooms.
            tiles[10, 10] = TileKind.FixedWall;
            tiles[11, 12] = TileKind.FixedWall;
            tiles[20, 10] = TileKind.FixedWall;
            tiles[21, 12] = TileKind.FixedWall;
            tiles[20, 4] = TileKind.FixedWall;
            tiles[24, 4] = TileKind.FixedWall;
            tiles[5, 5] = TileKind.FixedWall;
        }

        private void BuildIceMap()
        {
            Carve(2, 14, 7, 5);
            Carve(5, 10, 2, 5);
            Carve(1, 8, 5, 3);
            Carve(7, 8, 7, 6);
            Carve(13, 10, 5, 2);
            Carve(17, 8, 9, 6);
            Carve(9, 4, 6, 4);
            Carve(4, 4, 4, 4);
            Carve(18, 5, 8, 3);
            Carve(17, 1, 9, 5);
            Carve(4, 7, 2, 2);
            Carve(10, 7, 2, 2);

            tiles[16, 9] = TileKind.MovableWall;
            tiles[16, 10] = TileKind.Gap;
            tiles[9, 9] = TileKind.FixedWall;
            tiles[11, 10] = TileKind.FixedWall;
            tiles[11, 12] = TileKind.FixedWall;
            tiles[20, 11] = TileKind.FixedWall;
            tiles[22, 9] = TileKind.FixedWall;
            tiles[20, 3] = TileKind.FixedWall;
            tiles[23, 3] = TileKind.FixedWall;
        }

        private void BuildDesertMap()
        {
            Carve(2, 14, 7, 5);
            Carve(4, 10, 4, 4);
            Carve(8, 8, 7, 6);
            Carve(14, 10, 5, 3);
            Carve(18, 7, 7, 7);
            Carve(9, 4, 6, 4);
            Carve(2, 4, 7, 4);
            Carve(4, 7, 2, 2);
            Carve(18, 5, 7, 2);
            Carve(17, 1, 9, 5);
            Carve(14, 8, 4, 2);

            tiles[10, 11] = TileKind.MovableWall;
            tiles[11, 11] = TileKind.Gap;
            tiles[9, 9] = TileKind.FixedWall;
            tiles[12, 9] = TileKind.FixedWall;
            tiles[15, 10] = TileKind.FixedWall;
            tiles[21, 9] = TileKind.FixedWall;
            tiles[20, 12] = TileKind.FixedWall;
            tiles[6, 6] = TileKind.FixedWall;
            tiles[19, 3] = TileKind.FixedWall;
            tiles[23, 3] = TileKind.FixedWall;
        }

        private void Carve(int x, int y, int width, int height)
        {
            for (int ix = x; ix < x + width; ix++)
            for (int iy = y; iy < y + height; iy++)
            {
                if (ix > 0 && ix < MapWidth - 1 && iy > 0 && iy < MapHeight - 1)
                    tiles[ix, iy] = TileKind.Floor;
            }
        }

        private void BuildWorld()
        {
            world = new GameObject("Bright Dungeon").transform;
            var background = NewSprite("DepthBackdrop", PixelArtHD.White, -1000, world);
            background.transform.position = new Vector3(MapWidth * 0.5f, MapHeight * 0.5f, 3f);
            background.transform.localScale = new Vector3(MapWidth + 12, MapHeight + 12, 1f);
            background.GetComponent<SpriteRenderer>().color = new Color32(7, 25, 34, 255);

            for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
            {
                var cell = new GameObject("Cell_" + x + "_" + y).transform;
                cell.SetParent(world);
                cell.position = new Vector3(x + 0.5f, y + 0.5f, 0f);

                int variant = (x * 17 + y * 11) % 3;
                var floorSprite = variant == 0 ? PixelArtHD.FloorA :
                    variant == 1 ? PixelArtHD.FloorB : PixelArtHD.FloorC;
                var floor = NewSprite("Floor", floorSprite, 20 - y, cell);
                floor.transform.localPosition = Vector3.zero;
                var wall = NewSprite("Wall", PixelArtHD.Wall, 4800 - y * 12, cell);
                wall.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                var shadow = NewSprite("WallShadow", PixelArtHD.Shadow, 4799 - y * 12, cell);
                shadow.transform.localPosition = new Vector3(0.07f, -0.46f, 0f);
                shadow.transform.localScale = new Vector3(1.2f, 0.62f, 1f);
                shadow.GetComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f, 0.48f);
                var trail = NewSprite("WarmTrail", PixelArtHD.White, 110 - y, cell);
                trail.transform.localScale = new Vector3(0.94f, 0.94f, 1f);
                trail.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                var inherited = NewSprite("InheritedTrail", PixelArtHD.White, 109 - y, cell);
                inherited.transform.localScale = new Vector3(0.83f, 0.83f, 1f);
                inherited.transform.localPosition = new Vector3(0f, 0f, -0.03f);
                var danger = NewSprite("Danger", PixelArtHD.White, 150 - y, cell);
                danger.transform.localScale = new Vector3(0.91f, 0.91f, 1f);
                danger.transform.localPosition = new Vector3(0f, 0f, -0.01f);

                tileViews[x, y] = new TileView
                {
                    Floor = floor.GetComponent<SpriteRenderer>(),
                    Wall = wall.GetComponent<SpriteRenderer>(),
                    Shadow = shadow.GetComponent<SpriteRenderer>(),
                    Trail = trail.GetComponent<SpriteRenderer>(),
                    Inherited = inherited.GetComponent<SpriteRenderer>(),
                    Danger = danger.GetComponent<SpriteRenderer>()
                };
                shower(false, tileViews[x, y]);
            }

            var accent = NewSprite("ExitGlow", PixelArtHD.Glow, 175, world);
            accent.transform.position = new Vector3(ExitPosition.x, ExitPosition.y, 0f);
            accent.transform.localScale = Vector3.one * 3.2f;
            accent.GetComponent<SpriteRenderer>().color = new Color(1f, 0.66f, 0.2f, 0.18f);
            bossDoor = NewSprite("ExitDoor", PixelArtHD.Door, 5200, world);
            bossDoor.transform.position = new Vector3(ExitPosition.x, ExitPosition.y - 0.55f, 0f);
            bossSeal = NewSprite("ExitSeal", PixelArtHD.Spark, 5210, world);
            bossSeal.transform.position = new Vector3(ExitPosition.x, ExitPosition.y + 0.35f, 0f);
            bossSeal.transform.localScale = Vector3.one * 1.1f;
        }

        private static void shower(bool active, TileView view)
        {
            view.Wall.enabled = active;
            view.Shadow.enabled = active;
        }

        private static GameObject NewSprite(string name, Sprite sprite, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return go;
        }

        private void CreatePlayer()
        {
            playerGlow = NewSprite("PlayerGlow", PixelArtHD.Glow, 4300, world);
            playerGlow.transform.localScale = Vector3.one * 2.8f;
            playerGlow.GetComponent<SpriteRenderer>().color = new Color(1f, 0.68f, 0.25f, 0.24f);
            playerShadow = NewSprite("PlayerShadow", PixelArtHD.Shadow, 5000, world);
            playerShadow.transform.localScale = new Vector3(0.95f, 0.72f, 1f);
            playerShadow.GetComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f, 0.65f);
            playerView = NewSprite("Player", heroDown, 6000, world);
            weaponView = NewSprite("HeldRing", PixelArtHD.Ring, 6010, playerView.transform);
            weaponView.transform.localScale = Vector3.one * 0.46f;
            reticleView = NewSprite("AimReticle", PixelArtHD.Spark, 7000, world);
            reticleView.transform.localScale = Vector3.one * 0.6f;
            damageFlash = NewSprite("DamageFlash", PixelArtHD.White, 9000, world);
            damageFlash.transform.localScale = new Vector3(80f, 50f, 1f);
            damageFlash.GetComponent<SpriteRenderer>().color = new Color(1f, 0.08f, 0.04f, 0f);

            activeGhostLineObject = new GameObject("LiveRoute");
            activeGhostLineObject.transform.SetParent(world);
            activeGhostLine = activeGhostLineObject.AddComponent<LineRenderer>();
            activeGhostLine.useWorldSpace = true;
            activeGhostLine.startWidth = 0.24f;
            activeGhostLine.endWidth = 0.24f;
            activeGhostLine.material = new Material(Shader.Find("Sprites/Default"));
            activeGhostLine.startColor = new Color(0.4f, 0.95f, 1f, 0.9f);
            activeGhostLine.endColor = new Color(0.25f, 0.72f, 0.95f, 0.25f);
            activeGhostLine.sortingOrder = 430;
            activeGhostLine.enabled = false;

            tutorialMarker = NewSprite("TutorialRing", PixelArtHD.Ring, 520, world);
            tutorialMarker.transform.localScale = Vector3.one * 1.45f;
            tutorialMarker.GetComponent<SpriteRenderer>().color = new Color(0.42f, 0.95f, 1f, 0.75f);
        }

        private void CreateInteractiveObjects()
        {
            crystals.Clear();
            crystals.Add(new MemoryNodeAction
            {
                Position = new Vector2(2.5f, 10.5f),
                Title = "裂纹里的初见"
            });
            crystals.Add(new MemoryNodeAction
            {
                Position = new Vector2(25.3f, 7.3f),
                Title = "墙缝里的旧名字"
            });
            foreach (var crystal in crystals)
            {
                var glow = NewSprite("CrystalGlow", PixelArtHD.Glow, 520, world);
                glow.transform.localScale = Vector3.one * 2.2f;
                glow.GetComponent<SpriteRenderer>().color = new Color(0.25f, 0.9f, 1f, 0.26f);
                var view = NewSprite("Crystal", PixelArtHD.Crystal, 535, world);
                var root = new GameObject("MemoryCrystal");
                root.transform.SetParent(world);
                view.transform.SetParent(root.transform);
                glow.transform.SetParent(root.transform);
                view.transform.localPosition = Vector3.zero;
                glow.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                crystal.View = root;
            }

            lights.Clear();
            lights.Add(new LightNodeAction { Position = new Vector2(3.2f, 15.2f) });
            lights.Add(new LightNodeAction { Position = new Vector2(4.3f, 10.6f) });
            lights.Add(new LightNodeAction { Position = new Vector2(12.5f, 10.6f) });
            lights.Add(new LightNodeAction { Position = new Vector2(19.5f, 11.5f) });
            lights.Add(new LightNodeAction { Position = new Vector2(19.5f, 4.5f) });
            foreach (var light in lights)
            {
                var glow = NewSprite("LightGlow", PixelArtHD.Glow, 510, world);
                glow.transform.localScale = Vector3.one * 2.1f;
                glow.GetComponent<SpriteRenderer>().color = new Color(1f, 0.57f, 0.17f, 0.22f);
                var view = NewSprite("Light", PixelArtHD.Light, 525, world);
                var root = new GameObject("LightPickup");
                root.transform.SetParent(world);
                view.transform.SetParent(root.transform);
                glow.transform.SetParent(root.transform);
                glow.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                light.View = root;
            }

            tutorialTarget = new TutorialTarget { Position = new Vector2(7.2f, 15.65f) };
            var targetGlow = NewSprite("TargetGlow", PixelArtHD.Glow, 520, world);
            targetGlow.transform.localScale = Vector3.one * 2f;
            targetGlow.GetComponent<SpriteRenderer>().color = new Color(1f, 0.73f, 0.22f, 0.22f);
            var targetView = NewSprite("TutorialTarget", PixelArtHD.Target, 535, world);
            var targetRoot = new GameObject("TutorialTargetRoot");
            targetRoot.transform.SetParent(world);
            targetView.transform.SetParent(targetRoot.transform);
            targetGlow.transform.SetParent(targetRoot.transform);
            targetGlow.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            tutorialTarget.View = targetRoot;
            tutorialTarget.Renderer = targetView.GetComponent<SpriteRenderer>();

            tutorialRelic = NewSprite("TutorialRelic", PixelArtHD.Relic, 536, world);
            tutorialRelic.transform.position = new Vector3(4.1f, 14.7f, 0f);
            tutorialRelic.SetActive(false);
        }

        private void SpawnEnemies()
        {
            foreach (var enemy in enemies)
            {
                if (enemy.View != null)
                    Destroy(enemy.View);
            }
            enemies.Clear();
            if (currentMap == 1)
            {
                AddEnemy(EnemyKindAction.Melee, new Vector2(10.5f, 12.2f), 5);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(13.1f, 11.2f), 3);
                AddEnemy(EnemyKindAction.Melee, new Vector2(11.2f, 9.5f), 5);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(20.3f, 11.1f), 4);
                AddEnemy(EnemyKindAction.Melee, new Vector2(18.3f, 9.2f), 5);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(23.3f, 10.4f), 4);
                AddEnemy(EnemyKindAction.Melee, new Vector2(7f, 6.2f), 5);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(14.2f, 5.2f), 4);
                AddEnemy(EnemyKindAction.Melee, new Vector2(20.4f, 6.3f), 6);
                AddEnemy(EnemyKindAction.Boss, new Vector2(21.3f, 3.2f), 36);
            }
            else if (currentMap == 2)
            {
                AddEnemy(EnemyKindAction.Melee, new Vector2(10.3f, 12.3f), 5);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(14.2f, 11.2f), 4);
                AddEnemy(EnemyKindAction.Melee, new Vector2(12.5f, 8.5f), 5);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(17.2f, 8.5f), 4);
                AddEnemy(EnemyKindAction.Melee, new Vector2(20.5f, 10.5f), 6);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(22.3f, 8.5f), 4);
                AddEnemy(EnemyKindAction.Melee, new Vector2(6.3f, 6.4f), 6);
                AddEnemy(EnemyKindAction.Melee, new Vector2(10.2f, 5.2f), 6);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(14.2f, 6.3f), 5);
                AddEnemy(EnemyKindAction.Melee, new Vector2(22.4f, 5.5f), 7);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(24.2f, 2.5f), 5);
                AddEnemy(EnemyKindAction.Boss, new Vector2(21.4f, 3.4f), 46);
            }
            else
            {
                AddEnemy(EnemyKindAction.Melee, new Vector2(11.3f, 12.2f), 4);
                AddEnemy(EnemyKindAction.Melee, new Vector2(10.1f, 6.2f), 4);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(16.1f, 11.2f), 3);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(24.1f, 10.2f), 3);
                AddEnemy(EnemyKindAction.Melee, new Vector2(20.3f, 9.3f), 4);
                AddEnemy(EnemyKindAction.Melee, new Vector2(8.4f, 9.4f), 4);
                AddEnemy(EnemyKindAction.Ranged, new Vector2(13.3f, 9.2f), 3);
                AddEnemy(EnemyKindAction.Melee, new Vector2(24.2f, 6.5f), 5);
                AddEnemy(EnemyKindAction.Boss, new Vector2(22.1f, 4.1f), 30);
            }
        }

        private void AddEnemy(EnemyKindAction kind, Vector2 pos, int hp)
        {
            var enemy = new LiveEnemy
            {
                Id = enemies.Count + 1,
                Kind = kind,
                Position = pos,
                Home = pos,
                Hp = hp,
                MaxHp = hp,
                Cooldown = 0.5f,
                BaseTint = currentMap == 1
                    ? new Color(0.78f, 0.96f, 1f)
                    : currentMap == 2
                        ? new Color(1f, 0.83f, 0.63f)
                        : new Color(0.82f, 1f, 0.88f)
            };
            var sprite = kind == EnemyKindAction.Melee ? PixelArtHD.Melee :
                kind == EnemyKindAction.Ranged ? PixelArtHD.Ranged : PixelArtHD.Boss;
            var shadow = NewSprite("EnemyShadow", PixelArtHD.Shadow, 5000, world);
            var view = NewSprite("Enemy_" + enemy.Id, sprite,
                kind == EnemyKindAction.Boss ? 5990 : 5980, world);
            view.transform.localScale = kind == EnemyKindAction.Boss
                ? Vector3.one * 1.42f
                : Vector3.one;
            enemy.View = view;
            enemy.Renderer = view.GetComponent<SpriteRenderer>();
            var marker = NewSprite("Shadow", PixelArtHD.Shadow, 4999, view.transform);
            marker.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            marker.transform.localScale = new Vector3(kind == EnemyKindAction.Boss ? 1.8f : 0.9f,
                0.65f, 1f);
            marker.GetComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f, 0.6f);
            Destroy(shadow);
            enemies.Add(enemy);
        }

        private void BuildTitleScene()
        {
            playerPos = StartPosition;
            UpdateEntityVisuals(0f);
            mode = GameMode.Title;
        }

        private void StartNewRun()
        {
            mode = GameMode.Playing;
            currentMap = Mathf.Clamp(selectedMap, 0, 2);
            ApplyStageData();
            AudioListener.pause = false;
            Cursor.visible = false;
            hp = 3f;
            lives = 3;
            shadowEnergy = 12f;
            lightEnergy = 5f;
            carryingWall = false;
            bossActive = false;
            bossDefeated = false;
            exitOpen = false;
            hitResponseUsed = false;
            recording = false;
            tutorialSkipped = false;
            tutorialStep = TutorialStep.Move;
            tutorialDashed = false;
            tutorialRelicCollected = false;
            tutorialShootHits = 0;
            tutorialRecordCompleted = false;
            tutorialTarget.Alive = true;
            tutorialTarget.Hp = 3;
            tutorialTarget.View.SetActive(true);
            tutorialRelic.SetActive(false);
            memories.Clear();
            currentTrail.Clear();
            inheritedTrail.Clear();
            foreach (var crystal in crystals)
                crystal.Used = false;
            foreach (var light in lights)
                light.Available = true;
            ClearProjectiles();
            ClearGhosts();
            RecallScout(false);
            ResetMap();
            Array.Clear(explored, 0, explored.Length);
            Array.Clear(tileLight, 0, tileLight.Length);
            playerPos = StartPosition;
            playerVelocity = Vector2.zero;
            aimDirection = Vector2.right;
            SpawnEnemies();
            SetToast("先活动一下：WASD 或方向键移动。错过也没关系，所有路都能变成资源。",
                new Color32(115, 230, 240, 255));
            UpdateExplorationMusic();
            UpdatePresentation(0f);
        }

        private void ApplyStageData()
        {
            currentMap = Mathf.Clamp(currentMap, 0, 2);
            cam.backgroundColor = StageBackgrounds[currentMap];
            for (int i = 0; i < crystals.Count && i < StageCrystals[currentMap].Length; i++)
            {
                crystals[i].Position = StageCrystals[currentMap][i];
                crystals[i].View.transform.position = new Vector3(crystals[i].Position.x,
                    crystals[i].Position.y, 0f);
            }
            for (int i = 0; i < lights.Count && i < StageLights[currentMap].Length; i++)
            {
                lights[i].Position = StageLights[currentMap][i];
                lights[i].View.transform.position = new Vector3(lights[i].Position.x,
                    lights[i].Position.y, 0f);
            }
        }

        private void UpdateTitle()
        {
            if (mode == GameMode.Title)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                    SelectStageForTitle(0);
                if (Input.GetKeyDown(KeyCode.Alpha2))
                    SelectStageForTitle(1);
                if (Input.GetKeyDown(KeyCode.Alpha3))
                    SelectStageForTitle(2);
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                    StartNewRun();
                return;
            }
            if (mode == GameMode.MemoryChoice)
            {
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    memoryChoiceIndex = 0;
                    audio?.PlayClip("ui");
                }
                if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    memoryChoiceIndex = 1;
                    audio?.PlayClip("ui");
                }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                    ConfirmMemory(memoryChoiceIndex);
                if (Input.GetKeyDown(KeyCode.Escape))
                    CancelMemory();
                return;
            }
            if (mode == GameMode.Paused)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    mode = GameMode.Playing;
                    AudioListener.pause = false;
                    Cursor.visible = false;
                }
                if (Input.GetKeyDown(KeyCode.R))
                    StartNewRun();
                return;
            }
            if (mode == GameMode.Won || mode == GameMode.Lost)
            {
                if (Input.GetKeyDown(KeyCode.R))
                    StartNewRun();
                if (Input.GetKeyDown(KeyCode.N) && mode == GameMode.Won && selectedMap < 2)
                {
                    selectedMap++;
                    StartNewRun();
                }
                if (Input.GetKeyDown(KeyCode.M))
                    ReturnToMapSelect();
            }
        }

        private void ReturnToMapSelect()
        {
            mode = GameMode.Title;
            AudioListener.pause = false;
            Cursor.visible = true;
            SelectStageForTitle(selectedMap);
            PlayMusic("Exploration_Mist", true, 0.6f);
        }

        private void UpdateTitleSceneMotion(float dt)
        {
            if (mode != GameMode.Title)
                return;
            float x = 12f + Mathf.Sin(actionTime * 0.23f) * 1.4f;
            float y = 10f + Mathf.Cos(actionTime * 0.18f) * 0.7f;
            cam.transform.position = Vector3.Lerp(cam.transform.position,
                new Vector3(x, y, -10f), 1f - Mathf.Exp(-dt * 1.8f));
            for (int y1 = 0; y1 < MapHeight; y1++)
            for (int x1 = 0; x1 < MapWidth; x1++)
                highlightedBrightness(x1, y1, 0.38f + 0.08f * Mathf.Sin(actionTime + x1 * 0.2f + y1 * 0.17f));
            UpdateEntityVisuals(dt);
        }

        private void highlightedBrightness(int x, int y, float brightness)
        {
            var view = tileViews[x, y];
            bool floor = tiles[x, y] == TileKind.Floor || tiles[x, y] == TileKind.Gap;
            view.Floor.enabled = floor;
            if (floor)
            {
                view.Floor.color = new Color(0.6f + brightness * 0.4f,
                    0.6f + brightness * 0.4f, 0.6f + brightness * 0.4f, 1f);
            }
            shower(tiles[x, y] == TileKind.FixedWall || tiles[x, y] == TileKind.MovableWall, view);
            if (view.Wall.enabled)
                view.Wall.color = new Color(0.56f + brightness * 0.35f,
                    0.62f + brightness * 0.35f, 0.62f + brightness * 0.35f, 1f);
        }

        private void UpdateAim()
        {
            var screen = Input.mousePosition;
            screen.z = Mathf.Abs(cam.transform.position.z);
            var mouse = cam.ScreenToWorldPoint(screen);
            var delta = (Vector2)mouse - playerPos;
            if (delta.sqrMagnitude > 0.015f)
                aimDirection = delta.normalized;
            else if (playerVelocity.sqrMagnitude > 0.05f)
                aimDirection = playerVelocity.normalized;
            Cursor.visible = false;
        }

        private void UpdatePlayerMovement(float dt)
        {
            fireCooldown -= dt;
            dashCooldown -= dt;
            dashTime -= dt;
            invulnerable -= dt;
            hurtFlash -= dt;

            Vector2 input = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) &&
                dashCooldown <= 0f)
            {
                dashDirection = input.sqrMagnitude > 0.01f ? input.normalized : aimDirection;
                dashTime = 0.17f;
                dashCooldown = 0.82f;
                invulnerable = Mathf.Max(invulnerable, 0.36f);
                tutorialDashed = true;
                audio?.PlayClip("dash");
                StartCoroutine(DashTrail());
            }

            Vector2 targetVelocity = input * 5.35f;
            float acceleration = input.sqrMagnitude > 0.01f ? 36f : 28f;
            playerVelocity = Vector2.MoveTowards(playerVelocity, targetVelocity, acceleration * dt);
            if (dashTime > 0f)
                playerVelocity = dashDirection * 14.5f;

            MovePlayer(playerVelocity * dt);
            if (playerVelocity.magnitude > 1.1f)
            {
                stepTimer -= dt;
                if (stepTimer <= 0f)
                {
                    stepTimer = 0.23f;
                    audio?.PlayClip("step");
                }
            }

            if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space))
                TryShoot();
            if (Input.GetKeyDown(KeyCode.Q))
                ToggleScout();
            if (Input.GetKeyDown(KeyCode.F))
                Interact();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                mode = GameMode.Paused;
                Cursor.visible = true;
                AudioListener.pause = true;
            }
            if (Input.GetKeyDown(KeyCode.T))
            {
                tutorialSkipped = true;
                tutorialStep = TutorialStep.Complete;
                tutorialTarget.View.SetActive(false);
                tutorialRelic.SetActive(false);
                tutorialMarker.SetActive(false);
                SetToast("新手引导已跳过。按 Esc 可以随时查看完整操作。", Muted);
            }
        }

        private void MovePlayer(Vector2 delta)
        {
            float distance = delta.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            var step = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                var nextX = playerPos + new Vector2(step.x, 0f);
                if (!CircleBlocked(nextX, PlayerRadius))
                    playerPos.x = nextX.x;
                else
                    playerVelocity.x = 0f;

                var nextY = playerPos + new Vector2(0f, step.y);
                if (!CircleBlocked(nextY, PlayerRadius))
                    playerPos.y = nextY.y;
                else
                    playerVelocity.y = 0f;
            }
            if (playerPos.x < 0.28f || playerPos.x > MapWidth - 0.28f)
                playerPos.x = Mathf.Clamp(playerPos.x, 0.28f, MapWidth - 0.28f);
            if (playerPos.y < 0.28f || playerPos.y > MapHeight - 0.28f)
                playerPos.y = Mathf.Clamp(playerPos.y, 0.28f, MapHeight - 0.28f);
        }

        private void TryShoot()
        {
            if (fireCooldown > 0f || mode != GameMode.Playing)
                return;
            fireCooldown = 0.145f;
            var direction = GetAssistedAim();
            var spawn = playerPos + direction * 0.38f + Vector2.up * 0.20f;
            SpawnProjectile(true, spawn, direction * 17f, 0.82f, 1f, 0.10f);
            playerVelocity -= direction * 0.18f;
            audio?.PlayClip("shot");
            StartCoroutine(MuzzleFlash(spawn + direction * 0.18f, direction));
        }

        private Vector2 GetAssistedAim()
        {
            Vector2 best = aimDirection;
            float bestScore = float.MaxValue;
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive || enemy.Kind == EnemyKindAction.Boss && !bossActive)
                    continue;
                Vector2 to = enemy.Position - playerPos;
                float distance = to.magnitude;
                if (distance > 9f || distance < 0.15f)
                    continue;
                float dot = Vector2.Dot(aimDirection, to.normalized);
                if (dot < 0.92f)
                    continue;
                float score = (1f - dot) * 12f + distance * 0.025f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = to.normalized;
                }
            }
            return best;
        }

        private void SpawnProjectile(bool friendly, Vector2 pos, Vector2 velocity, float life,
            float damage, float radius)
        {
            var sprite = friendly ? PixelArtHD.Ring : PixelArtHD.EnemyShot;
            var view = NewSprite(friendly ? "RingShot" : "EnemyShot", sprite,
                friendly ? 6400 : 6350, world);
            var renderer = view.GetComponent<SpriteRenderer>();
            if (!friendly)
                renderer.color = new Color(1f, 0.72f, 0.84f, 1f);
            var projectile = new Projectile
            {
                Friendly = friendly,
                Position = pos,
                Velocity = velocity,
                Life = life,
                Damage = damage,
                Radius = radius,
                Bounces = friendly ? 1 : 0,
                View = view,
                Renderer = renderer
            };
            projectiles.Add(projectile);
        }

        private void UpdateWeapon(float dt)
        {
            var hold = playerPos + aimDirection * 0.40f + Vector2.up * 0.64f;
            weaponView.transform.position = new Vector3(hold.x, hold.y, 0f);
            weaponView.transform.Rotate(0f, 0f, 860f * dt);
            reticleView.transform.position = new Vector3(
                playerPos.x + aimDirection.x * 2.4f,
                playerPos.y + aimDirection.y * 2.4f,
                0f);
            reticleView.GetComponent<SpriteRenderer>().color =
                new Color(1f, 0.84f, 0.36f, 0.68f + 0.18f * Mathf.Sin(actionTime * 9f));
        }

        private void UpdateTutorial()
        {
            if (tutorialSkipped || tutorialStep == TutorialStep.Complete)
                return;

            switch (tutorialStep)
            {
                case TutorialStep.Move:
                    tutorialMarker.SetActive(true);
                    tutorialMarker.transform.position = new Vector3(6f, 16.5f, 0f);
                    if (Vector2.Distance(playerPos, new Vector2(6f, 16.5f)) < 0.65f)
                    {
                        tutorialStep = TutorialStep.Shoot;
                        tutorialMarker.SetActive(false);
                        audio?.PlayClip("memory");
                    }
                    break;
                case TutorialStep.Shoot:
                    tutorialMarker.SetActive(true);
                    tutorialMarker.transform.position =
                        new Vector3(tutorialTarget.Position.x, tutorialTarget.Position.y + 0.1f, 0f);
                    if (!tutorialTarget.Alive || tutorialShootHits >= 3)
                        AdvanceTutorialShoot();
                    break;
                case TutorialStep.Dash:
                    tutorialMarker.SetActive(false);
                    if (tutorialDashed)
                    {
                        tutorialStep = TutorialStep.Interact;
                        tutorialRelic.SetActive(true);
                        SetToast("闪避带有短暂无敌。现在靠近青色遗物，按 F 拾取。", Cyan);
                    }
                    break;
                case TutorialStep.Interact:
                    if (tutorialRelicCollected)
                    {
                        tutorialStep = TutorialStep.Record;
                        SetToast("按住 E 并移动，留下青色战斗轨迹；松开 E 封存。", Gold);
                    }
                    break;
                case TutorialStep.Record:
                    if (tutorialRecordCompleted)
                    {
                        tutorialStep = TutorialStep.Complete;
                        SetToast("引导完成。残影、探路光与冰晶记忆会陪伴你走到出口。", Mint);
                        audio?.PlayClip("victory");
                    }
                    break;
            }
        }

        private void AdvanceTutorialShoot()
        {
            tutorialStep = TutorialStep.Dash;
            tutorialTarget.View.SetActive(false);
            tutorialMarker.SetActive(false);
            SetToast("目标击破。按 Shift 短距闪避；闪避期间不会受伤。", Cyan);
            audio?.PlayClip("memory");
        }

        private void UpdateRecord(float dt)
        {
            if (Input.GetKeyDown(KeyCode.E) && !recording)
            {
                if (shadowEnergy < 0.5f)
                {
                    SetToast("影能不足，暂时无法记录路线。", Coral);
                    return;
                }
                recording = true;
                recordPoints.Clear();
                recordCells.Clear();
                recordPoints.Add(playerPos);
                recordCells.Add(WorldCell(playerPos));
                activeGhostLine.enabled = true;
                activeGhostLine.positionCount = 1;
                activeGhostLine.SetPosition(0, playerPos);
            }

            if (Input.GetKeyUp(KeyCode.E) && recording)
                FinishRecord();

            if (!recording)
                return;

            recordSampleTimer -= dt;
            if (recordSampleTimer <= 0f)
            {
                recordSampleTimer = 0.055f;
                var cell = WorldCell(playerPos);
                if (recordCells.Count == 0 || recordCells[recordCells.Count - 1] != cell)
                {
                    recordCells.Add(cell);
                    recordPoints.Add(playerPos);
                    shadowEnergy = Mathf.Max(0f, shadowEnergy - 0.38f);
                    activeGhostLine.positionCount = recordPoints.Count;
                    activeGhostLine.SetPositions(recordPoints.Select(p =>
                        new Vector3(p.x, p.y, 0f)).ToArray());
                }
            }
            if (shadowEnergy <= 0.01f)
                FinishRecord();
        }

        private void FinishRecord()
        {
            if (!recording)
                return;
            recording = false;
            activeGhostLine.enabled = false;
            if (recordCells.Count < 2)
            {
                SetToast("轨迹太短，没有形成战斗残影。", Muted);
                return;
            }

            var ghost = new GhostTrail
            {
                Life = HasMemory("M1B") ? 9f : 6f,
                Damage = HasMemory("M1A") ? 2f : 1f
            };
            ghost.Cells.AddRange(recordCells);
            ghost.Points.AddRange(recordPoints);
            var lineObject = new GameObject("BattleGhost");
            lineObject.transform.SetParent(world);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = ghost.Points.Count;
            line.startWidth = 0.48f;
            line.endWidth = 0.48f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.20f, 0.94f, 1f, 0.55f);
            line.endColor = new Color(0.42f, 0.55f, 1f, 0.18f);
            line.sortingOrder = 260;
            line.SetPositions(ghost.Points.Select(p => new Vector3(p.x, p.y, 0f)).ToArray());
            ghost.View = lineObject;
            ghost.Line = line;
            ghosts.Add(ghost);
            if (HasMemory("M2B"))
                shadowEnergy = Mathf.Min(12f, shadowEnergy + 3f);
            tutorialRecordCompleted = true;
            SetToast("战斗残影封存：让追兵走进青色旧路。", Cyan);
            audio?.PlayClip("ghost");
            StartCoroutine(GhostBurst(ghost.Points[ghost.Points.Count - 1]));
        }

        private void UpdateGhosts(float dt)
        {
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var ghost = ghosts[i];
                ghost.Life -= dt;
                ghost.Tick -= dt;
                if (ghost.Life <= 0f)
                {
                    Destroy(ghost.View);
                    ghosts.RemoveAt(i);
                    continue;
                }
                float pulse = 0.20f + 0.14f * Mathf.Sin(actionTime * 6f + i);
                ghost.Line.startColor = new Color(0.25f, 0.95f, 1f, pulse);
                ghost.Line.endColor = new Color(0.36f, 0.52f, 1f, pulse * 0.45f);
                if (ghost.Tick > 0f)
                    continue;
                ghost.Tick = 0.48f;
                foreach (var enemy in enemies)
                {
                    if (!enemy.Alive || !ghost.Cells.Contains(WorldCell(enemy.Position)))
                        continue;
                    DamageEnemy(enemy, ghost.Damage);
                    StartCoroutine(Burst(enemy.Position, new Color(0.34f, 0.94f, 1f), 6));
                }
            }
        }

        private void ToggleScout()
        {
            if (scout != null)
            {
                RecallScout(true);
                return;
            }
            if (shadowEnergy < 1f || lightEnergy < 1f)
            {
                SetToast("探路光需要各 1 点影能与光。", Coral);
                return;
            }
            shadowEnergy -= 1f;
            lightEnergy -= 1f;
            var view = NewSprite("Scout", PixelArtHD.Scout, 6400, world);
            var glow = NewSprite("ScoutGlow", PixelArtHD.Glow, 530, view.transform);
            glow.transform.localScale = Vector3.one * 2.4f;
            glow.GetComponent<SpriteRenderer>().color = new Color(0.35f, 0.95f, 1f, 0.25f);
            var tailObject = new GameObject("ScoutTrail");
            tailObject.transform.SetParent(world);
            var tail = tailObject.AddComponent<LineRenderer>();
            tail.useWorldSpace = true;
            tail.startWidth = 0.08f;
            tail.endWidth = 0.18f;
            tail.material = new Material(Shader.Find("Sprites/Default"));
            tail.startColor = new Color(0.28f, 0.95f, 1f, 0.08f);
            tail.endColor = new Color(0.68f, 1f, 1f, 0.7f);
            tail.sortingOrder = 250;
            scout = new ScoutOrb
            {
                Position = playerPos + aimDirection * 0.35f,
                Velocity = aimDirection * 3.5f,
                Life = 3.2f,
                View = view,
                Tail = tail
            };
            scout.Trail.Add(scout.Position);
            audio?.PlayClip("scout");
            SetToast("光照幽灵已放出。它会穿透黑暗，留下探路光。", Cyan);
        }

        private void UpdateScout(float dt)
        {
            if (scout == null)
                return;
            scout.Life -= dt;
            scout.Position += scout.Velocity * dt;
            scout.Position = new Vector2(Mathf.Clamp(scout.Position.x, 0.2f, MapWidth - 0.2f),
                Mathf.Clamp(scout.Position.y, 0.2f, MapHeight - 0.2f));
            var cell = WorldCell(scout.Position);
            if (InBounds(cell) && (tiles[cell.x, cell.y] == TileKind.Floor ||
                                   tiles[cell.x, cell.y] == TileKind.Gap))
            {
                currentTrail.Add(cell);
                explored[cell.x, cell.y] = true;
            }
            if (scout.Trail.Count == 0 ||
                Vector2.Distance(scout.Trail[scout.Trail.Count - 1], scout.Position) > 0.08f)
            {
                scout.Trail.Add(scout.Position);
                if (scout.Trail.Count > 48)
                    scout.Trail.RemoveAt(0);
                scout.Tail.positionCount = scout.Trail.Count;
                scout.Tail.SetPositions(scout.Trail.Select(p => new Vector3(p.x, p.y, 0f)).ToArray());
            }
            shadowEnergy = Mathf.Max(0f, shadowEnergy - dt * 0.22f);
            lightEnergy = Mathf.Max(0f, lightEnergy - dt * 0.22f);
            if (scout.Life <= 0f || shadowEnergy <= 0f || lightEnergy <= 0f)
                RecallScout(true);
        }

        private void RecallScout(bool showToast)
        {
            if (scout == null)
                return;
            Destroy(scout.View);
            Destroy(scout.Tail.gameObject);
            scout = null;
            if (showToast)
                SetToast("光照幽灵收回，已照见的路仍会保留。", Muted);
        }

        private void Interact()
        {
            if (tutorialStep == TutorialStep.Interact &&
                Vector2.Distance(playerPos, tutorialRelic.transform.position) < 1.25f)
            {
                tutorialRelicCollected = true;
                tutorialRelic.SetActive(false);
                lightEnergy = Mathf.Min(10f, lightEnergy + 3f);
                SetToast("拾得微光，光 +3。", Gold);
                audio?.PlayClip("pickup");
                return;
            }

            foreach (var crystal in crystals)
            {
                if (crystal.Used || Vector2.Distance(playerPos, crystal.Position) > 1.2f)
                    continue;
                openMemory = crystal;
                mode = GameMode.MemoryChoice;
                memoryChoiceIndex = 0;
                Cursor.visible = true;
                BuildChoices(crystal);
                return;
            }

            var cell = FacingCell();
            if (InBounds(cell) && Vector2.Distance(playerPos, CellCenter(cell)) < 1.35f)
            {
                if (tiles[cell.x, cell.y] == TileKind.MovableWall)
                {
                    if (carryingWall)
                    {
                        SetToast("手里已经有一块墙材。先放进缺口。", Coral);
                        return;
                    }
                    tiles[cell.x, cell.y] = TileKind.Floor;
                    carryingWall = true;
                    SetToast("墙材拆下。沿捷径寻找缺口，按 F 放下。", Gold);
                    audio?.PlayClip("hit");
                    return;
                }
                if (tiles[cell.x, cell.y] == TileKind.Gap)
                {
                    if (!carryingWall)
                    {
                        SetToast("缺口需要一块可搬墙。", Coral);
                        return;
                    }
                    tiles[cell.x, cell.y] = TileKind.Floor;
                    carryingWall = false;
                    SetToast("墙材落位，捷径接通。", Mint);
                    audio?.PlayClip("pickup");
                    return;
                }
            }

            if (Vector2.Distance(playerPos, ExitPosition) < 1.25f && !exitOpen)
                SetToast("出口被守门者的印记封着。先击败 Boss。", Coral);
            else if (Vector2.Distance(playerPos, tutorialTarget.Position) < 1.4f &&
                     tutorialStep == TutorialStep.Shoot)
                SetToast("按住鼠标左键或空格射击训练靶。", Cyan);
        }

        private void BuildChoices(MemoryNodeAction crystal)
        {
            activeChoices.Clear();
            int crystalIndex = crystals.IndexOf(crystal);
            foreach (var packed in MemoryText[Mathf.Clamp(crystalIndex, 0, 1)])
            {
                var parts = packed.Split('|');
                activeChoices.Add(new MemoryOption
                {
                    Title = parts[0],
                    Tag = parts[1],
                    Description = parts[2],
                    Id = parts[3],
                    Color = crystalIndex == 0
                        ? new Color32(91, 224, 239, 255)
                        : new Color32(250, 177, 72, 255)
                });
            }
        }

        private void ConfirmMemory(int index)
        {
            if (mode != GameMode.MemoryChoice || activeChoices.Count < 2)
                return;
            var choice = activeChoices[Mathf.Clamp(index, 0, 1)];
            memories.Add(choice.Id);
            if (openMemory != null)
                openMemory.Used = true;
            openMemory = null;
            mode = GameMode.Playing;
            Cursor.visible = false;
            SetToast("记住：" + choice.Title + "。", choice.Color);
            audio?.PlayClip("memory");
            StartCoroutine(Burst(playerPos, choice.Color, 18));
        }

        private void CancelMemory()
        {
            openMemory = null;
            activeChoices.Clear();
            mode = GameMode.Playing;
            Cursor.visible = false;
        }

        private void UpdateEnemies(float dt)
        {
            if (!bossActive && playerPos.x > 17.6f && playerPos.y < 7.2f)
            {
                bossActive = true;
                bossChoice = 0;
                SetToast(BossNames[currentMap] + "苏醒！观察红色落点，再用闪避穿过缝隙。", Coral);
                audio?.PlayClip("boss");
                PlayMusic("Boss_Castle", true, 0.8f);
                StartCoroutine(Shake(0.45f, 0.16f));
            }

            foreach (var enemy in enemies)
            {
                if (!enemy.Alive)
                    continue;
                enemy.Cooldown -= dt;
                enemy.Flash = Mathf.Max(0f, enemy.Flash - dt);
                enemy.Invulnerable = Mathf.Max(0f, enemy.Invulnerable - dt);
                if (enemy.Kind == EnemyKindAction.Boss && !bossActive)
                    continue;

                if (enemy.State == EnemyStateAction.Telegraph)
                {
                    enemy.Timer -= dt;
                    if (enemy.Timer <= 0f)
                    {
                        enemy.State = EnemyStateAction.Attack;
                        enemy.Timer = enemy.Kind == EnemyKindAction.Boss ? 0.36f : 0.08f;
                        ExecuteEnemyAttack(enemy);
                    }
                    continue;
                }

                if (enemy.State == EnemyStateAction.Attack)
                {
                    enemy.Timer -= dt;
                    if (enemy.Kind == EnemyKindAction.Boss)
                        UpdateBossAttack(enemy, dt);
                    if (enemy.Timer <= 0f)
                    {
                        enemy.State = EnemyStateAction.Recover;
                        enemy.Timer = enemy.Kind == EnemyKindAction.Boss ? 0.75f : 0.28f;
                    }
                    continue;
                }

                if (enemy.State == EnemyStateAction.Recover)
                {
                    enemy.Timer -= dt;
                    if (enemy.Timer <= 0f)
                    {
                        enemy.State = EnemyStateAction.Idle;
                        enemy.Cooldown = enemy.Kind == EnemyKindAction.Boss ? 0.35f : 0.55f;
                    }
                    continue;
                }

                if (enemy.Cooldown > 0f)
                    continue;

                switch (enemy.Kind)
                {
                    case EnemyKindAction.Melee:
                        UpdateMelee(enemy, dt);
                        break;
                    case EnemyKindAction.Ranged:
                        UpdateRanged(enemy, dt);
                        break;
                    case EnemyKindAction.Boss:
                        UpdateBoss(enemy, dt);
                        break;
                }
            }
        }

        private void UpdateMelee(LiveEnemy enemy, float dt)
        {
            float distance = Vector2.Distance(enemy.Position, playerPos);
            if (distance < 1.05f)
            {
                enemy.State = EnemyStateAction.Telegraph;
                enemy.Timer = 0.32f;
                enemy.LockedTarget = playerPos;
                return;
            }
            if (distance > 12f || !HasLineOfSight(enemy.Position, playerPos, 0.2f))
                return;
            var dir = (playerPos - enemy.Position).normalized;
            MoveEnemy(enemy, dir * 2.15f * dt, 0.34f);
        }

        private void UpdateRanged(LiveEnemy enemy, float dt)
        {
            float distance = Vector2.Distance(enemy.Position, playerPos);
            if (distance < 9.5f && HasLineOfSight(enemy.Position, playerPos, 0.18f))
            {
                enemy.State = EnemyStateAction.Telegraph;
                enemy.Timer = 0.48f;
                var to = playerPos - enemy.Position;
                enemy.LockedDirection = to.sqrMagnitude < 0.01f ? Vector2.right : to.normalized;
                enemy.LockedTarget = playerPos;
                audio?.PlayClip("aim");
                return;
            }
            if (distance < 3.2f)
                MoveEnemy(enemy, (enemy.Position - playerPos).normalized * 1.35f * dt, 0.31f);
        }

        private void UpdateBoss(LiveEnemy boss, float dt)
        {
            float distance = Vector2.Distance(boss.Position, playerPos);
            if (distance < 2.35f)
            {
                boss.State = EnemyStateAction.Telegraph;
                boss.Timer = 0.58f;
                boss.LockedTarget = boss.Position;
                bossChoice = 1;
                audio?.PlayClip("aim");
                return;
            }
            var difference = playerPos - boss.Position;
            if (Mathf.Abs(difference.x) < 4.5f || Mathf.Abs(difference.y) < 4.5f)
            {
                boss.State = EnemyStateAction.Telegraph;
                boss.Timer = 0.68f;
                boss.LockedDirection = Mathf.Abs(difference.x) >= Mathf.Abs(difference.y)
                    ? new Vector2(Mathf.Sign(difference.x), 0f)
                    : new Vector2(0f, Mathf.Sign(difference.y));
                boss.LockedTarget = boss.Position;
                boss.ChargeHit = false;
                bossChoice = 0;
                audio?.PlayClip("aim");
                return;
            }
            var direction = difference.normalized;
            MoveEnemy(boss, direction * 1.65f * dt, 0.62f);
            boss.Cooldown = Mathf.Max(boss.Cooldown, 0.22f);
        }

        private void ExecuteEnemyAttack(LiveEnemy enemy)
        {
            if (enemy.Kind == EnemyKindAction.Melee)
            {
                if (Vector2.Distance(playerPos, enemy.Position) < 1.25f)
                    DamagePlayer(enemy.LockedTarget, 1f);
                StartCoroutine(Burst(enemy.Position, Coral, 8));
                audio?.PlayClip("slash");
                return;
            }
            if (enemy.Kind == EnemyKindAction.Ranged)
            {
                SpawnProjectile(false, enemy.Position + enemy.LockedDirection * 0.38f,
                    enemy.LockedDirection * 7.2f, 1.8f, 1f, 0.12f);
                audio?.PlayClip("enemy-shot");
                return;
            }

            if (bossChoice == 1)
            {
                if (Vector2.Distance(playerPos, enemy.Position) <= 2.35f)
                    DamagePlayer(playerPos, 1f);
                StartCoroutine(Shockwave(enemy.Position, 2.35f));
                audio?.PlayClip("boss-hit");
            }
        }

        private void UpdateBossAttack(LiveEnemy boss, float dt)
        {
            if (bossChoice != 0)
                return;
            var next = boss.Position;
            int steps = 5;
            for (int i = 0; i < steps; i++)
            {
                var candidate = next + boss.LockedDirection * (dt * 10.2f / steps);
                if (CircleBlocked(candidate, 0.62f))
                {
                    boss.Timer = 0.01f;
                    boss.Cooldown = 1.05f;
                    boss.State = EnemyStateAction.Recover;
                    boss.Timer = 1.1f;
                    StartCoroutine(Shake(0.35f, 0.24f));
                    audio?.PlayClip("boss-hit");
                    StartCoroutine(Burst(candidate, Gold, 18));
                    SetToast("守门者撞墙失衡，抓住空档输出！", Gold);
                    return;
                }
                next = candidate;
                if (!boss.ChargeHit && Vector2.Distance(next, playerPos) < 0.75f)
                {
                    boss.ChargeHit = true;
                    DamagePlayer(playerPos, 1f);
                }
            }
            boss.Position = next;
        }

        private void DamageEnemy(LiveEnemy enemy, float amount)
        {
            if (!enemy.Alive || enemy.Invulnerable > 0f)
                return;
            enemy.Hp -= Mathf.Max(1, Mathf.RoundToInt(amount));
            enemy.Flash = 0.1f;
            enemy.Invulnerable = 0.05f;
            if (enemy.Kind == EnemyKindAction.Boss && enemy.Hp > 0 &&
                enemy.Hp <= enemy.MaxHp / 2)
                PlayMusic("Boss_VictoryPath", true, 0.7f);
            var knockback = enemy.Position - playerPos;
            if (knockback.sqrMagnitude > 0.01f && enemy.Kind != EnemyKindAction.Boss)
                MoveEnemy(enemy, knockback.normalized * 0.09f, 0.32f);
            StartCoroutine(Burst(enemy.Position, Gold, 5));
            if (enemy.Hp > 0)
                return;
            enemy.Alive = false;
            enemy.View.SetActive(false);
            audio?.PlayClip("hit");
            StartCoroutine(Burst(enemy.Position, Coral, 18));
            if (enemy.Kind == EnemyKindAction.Boss)
            {
                bossDefeated = true;
                bossActive = false;
                exitOpen = true;
                SetToast("守门者倒下。出口已经打开！", Gold);
                audio?.PlayClip("victory");
                StartCoroutine(Shake(0.7f, 0.2f));
            }
            else
            {
                shadowEnergy = Mathf.Min(12f, shadowEnergy + 1.5f);
            }
        }

        private void DamagePlayer(Vector2 source, float amount)
        {
            if (invulnerable > 0f || mode != GameMode.Playing)
                return;
            invulnerable = 1.05f;
            hp -= amount;
            hurtFlash = 1f;
            StartCoroutine(Shake(0.24f, 0.14f));
            StartCoroutine(Burst(playerPos, Coral, 14));
            audio?.PlayClip("hurt");
            if (HasMemory("M2A") && !hitResponseUsed)
            {
                hitResponseUsed = true;
                shadowEnergy = Mathf.Min(12f, shadowEnergy + 2f);
            }
            if (hp <= 0f)
                EnterDeath();
        }

        private void EnterDeath()
        {
            lives--;
            if (lives <= 0)
            {
                mode = GameMode.Lost;
                Cursor.visible = true;
                StopMusic(0.8f);
                SetToast("最后一次灯火熄灭。按 R 重新进入迷宫。", Coral);
                return;
            }
            mode = GameMode.Dead;
            deathTimer = 0f;
            Cursor.visible = true;
            recording = false;
            activeGhostLine.enabled = false;
            RecallScout(false);
            ClearProjectiles();
            ClearGhosts();
            if (currentTrail.Count > 0)
            {
                inheritedTrail.Clear();
                foreach (var cell in currentTrail)
                    inheritedTrail.Add(cell);
            }
            currentTrail.Clear();
            SetToast("身体倒下，本世光路将成为下一世的照明。", Coral);
        }

        private void Respawn()
        {
            hp = 3f;
            shadowEnergy = 12f;
            lightEnergy = 5f;
            hitResponseUsed = false;
            invulnerable = 1f;
            playerPos = StartPosition;
            playerVelocity = Vector2.zero;
            aimDirection = Vector2.right;
            carryingWall = false;
            bossActive = false;
            bossDefeated = false;
            exitOpen = false;
            ResetMap();
            SpawnEnemies();
            foreach (var light in lights)
                light.Available = true;
            mode = GameMode.Playing;
            Cursor.visible = false;
            AudioListener.pause = false;
            UpdateExplorationMusic();
            SetToast(inheritedTrail.Count > 0
                ? "上一世的路仍在发光。敌人已复位，记忆不会丢失。"
                : "旧路继续照着迷宫。敌人已复位，记忆不会丢失。", Gold);
            audio?.PlayClip("respawn");
        }

        private void UpdateProjectiles(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    RemoveProjectile(i);
                    continue;
                }

                float distance = p.Velocity.magnitude * dt;
                int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.12f));
                bool consumed = false;
                for (int step = 0; step < steps; step++)
                {
                    p.Position += p.Velocity * (dt / steps);
                    if (CircleBlocked(p.Position, p.Radius))
                    {
                        if (p.Friendly && p.Bounces > 0)
                        {
                            p.Bounces--;
                            p.Velocity = -p.Velocity;
                            p.Position += p.Velocity.normalized * 0.12f;
                            audio?.PlayClip("ricochet");
                        }
                        else
                        {
                            StartCoroutine(Burst(p.Position, p.Friendly ? Gold : Coral, 5));
                            RemoveProjectile(i);
                            consumed = true;
                            break;
                        }
                    }

                    if (p.Friendly)
                    {
                        if (tutorialTarget.Alive &&
                            Vector2.Distance(p.Position, tutorialTarget.Position + Vector2.up * 0.15f) < 0.42f)
                        {
                            tutorialTarget.Hp--;
                            tutorialTarget.Flash = 0.12f;
                            tutorialShootHits++;
                            audio?.PlayClip("hit");
                            StartCoroutine(Burst(p.Position, Gold, 8));
                            if (tutorialTarget.Hp <= 0)
                            {
                                tutorialTarget.Alive = false;
                                tutorialShootHits = Mathf.Max(3, tutorialShootHits);
                            }
                            RemoveProjectile(i);
                            consumed = true;
                            break;
                        }
                        LiveEnemy target = null;
                        float targetDistance = 999f;
                        foreach (var enemy in enemies)
                        {
                            if (!enemy.Alive || enemy.Kind == EnemyKindAction.Boss && !bossActive)
                                continue;
                            float hitRadius = enemy.Kind == EnemyKindAction.Boss ? 0.76f : 0.39f;
                            float d = Vector2.Distance(p.Position, enemy.Position + Vector2.up * 0.2f);
                            if (d < hitRadius && d < targetDistance)
                            {
                                target = enemy;
                                targetDistance = d;
                            }
                        }
                        if (target != null)
                        {
                            DamageEnemy(target, p.Damage);
                            RemoveProjectile(i);
                            consumed = true;
                            break;
                        }
                    }
                    else if (Vector2.Distance(p.Position, playerPos) < PlayerRadius + p.Radius)
                    {
                        DamagePlayer(p.Position, p.Damage);
                        StartCoroutine(Burst(p.Position, Coral, 8));
                        RemoveProjectile(i);
                        consumed = true;
                        break;
                    }
                }
                if (consumed || i >= projectiles.Count)
                    continue;
                p.View.transform.position = new Vector3(p.Position.x, p.Position.y, 0f);
                p.View.transform.Rotate(0f, 0f, (p.Friendly ? 860f : 260f) * dt);
            }
        }

        private void RemoveProjectile(int index)
        {
            if (index < 0 || index >= projectiles.Count)
                return;
            var p = projectiles[index];
            if (p.View != null)
                Destroy(p.View);
            projectiles.RemoveAt(index);
        }

        private void ClearProjectiles()
        {
            foreach (var projectile in projectiles)
            {
                if (projectile.View != null)
                    Destroy(projectile.View);
            }
            projectiles.Clear();
        }

        private void ClearGhosts()
        {
            foreach (var ghost in ghosts)
            {
                if (ghost.View != null)
                    Destroy(ghost.View);
            }
            ghosts.Clear();
        }

        private void UpdatePickups(float dt)
        {
            foreach (var light in lights)
            {
                if (!light.Available)
                {
                    light.View.SetActive(false);
                    continue;
                }
                var lightCell = WorldCell(light.Position);
                bool lightVisible = mode == GameMode.Title ||
                                    InBounds(lightCell) && tileLight[lightCell.x, lightCell.y] > 0.07f;
                light.View.SetActive(lightVisible);
                if (!lightVisible)
                    continue;
                light.View.transform.position = new Vector3(light.Position.x,
                    light.Position.y + 0.05f * Mathf.Sin(actionTime * 4f + light.Position.x), 0f);
                if (Vector2.Distance(playerPos, light.Position) < 0.65f)
                {
                    light.Available = false;
                    lightEnergy = Mathf.Min(10f, lightEnergy + 3f);
                    SetToast("拾得微光，光 +3。", Gold);
                    audio?.PlayClip("pickup");
                    StartCoroutine(Burst(light.Position, Gold, 12));
                }
            }
            foreach (var crystal in crystals)
            {
                var crystalCell = WorldCell(crystal.Position);
                bool crystalVisible = !crystal.Used && (mode == GameMode.Title ||
                                    InBounds(crystalCell) && tileLight[crystalCell.x, crystalCell.y] > 0.07f);
                crystal.View.SetActive(crystalVisible);
                if (crystalVisible)
                {
                    crystal.View.transform.position = new Vector3(crystal.Position.x,
                        crystal.Position.y + 0.06f * Mathf.Sin(actionTime * 2.7f + crystal.Position.x),
                        0f);
                }
            }
        }

        private void UpdateInteractions()
        {
            if (exitOpen && Vector2.Distance(playerPos, ExitPosition) < 0.72f)
            {
                mode = GameMode.Won;
                Cursor.visible = true;
                SetToast("你从错误的路线里带回了答案。", Gold);
                audio?.PlayClip("victory");
                PlayMusic("Victory_Moment", false, 0.5f);
            }
            bossDoor.SetActive(true);
            bossDoor.GetComponent<SpriteRenderer>().color = exitOpen
                ? new Color(0.72f, 1f, 0.82f, 1f)
                : new Color(0.68f, 0.70f, 0.69f, 1f);
            bossSeal.SetActive(!exitOpen);
            bossSeal.transform.Rotate(0f, 0f, 55f * Time.unscaledDeltaTime);
            UpdateExplorationMusic();
        }

        private void UpdateExplorationMusic()
        {
            if (mode != GameMode.Playing || bossActive || bossDefeated)
                return;
            string track = currentMap == 1 ? "Exploration_Ice" :
                currentMap == 2 ? "Exploration_Mist" : "Exploration_Forest";
            PlayMusic(track, true, 1.1f);
        }

        private void UpdatePresentation(float dt)
        {
            UpdateCamera(dt);
            UpdateEntityVisuals(dt);
            lightingTimer -= dt;
            if (lightingTimer <= 0f)
            {
                lightingTimer = 0.06f;
                UpdateLightingGrid();
            }
            damageFlash.GetComponent<SpriteRenderer>().color =
                new Color(1f, 0.05f, 0.02f, Mathf.Clamp01(hurtFlash) * 0.11f);
            tutorialMarker.GetComponent<SpriteRenderer>().color = new Color(
                0.35f, 0.96f, 1f, 0.5f + 0.3f * Mathf.Sin(actionTime * 6f));
        }

        private void UpdateCamera(float dt)
        {
            if (mode == GameMode.Title)
                return;
            Vector2 target = playerPos + aimDirection * 0.85f;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * Mathf.Max(1.1f, cam.aspect);
            target.x = Mathf.Clamp(target.x, halfWidth - 1.8f, MapWidth - halfWidth + 1.8f);
            target.y = Mathf.Clamp(target.y, halfHeight - 1.4f, MapHeight - halfHeight + 1.4f);
            var desired = new Vector3(target.x, target.y, -10f);
            var current = Vector3.SmoothDamp(cam.transform.position, desired, ref cameraVelocity,
                0.105f, 45f, dt);
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                current += (Vector3)(UnityEngine.Random.insideUnitCircle * shakeStrength *
                                    Mathf.Clamp01(shakeTime / 0.3f));
            }
            cam.transform.position = current;
        }

        private Sprite DirectionalHeroSprite()
        {
            if (Mathf.Abs(aimDirection.x) >= Mathf.Abs(aimDirection.y))
                return aimDirection.x < 0f ? heroLeft : heroRight;
            return aimDirection.y < 0f ? heroDown : heroUp;
        }

        private void UpdateEntityVisuals(float dt)
        {
            bool visiblePlayer = mode == GameMode.Title || mode == GameMode.Playing ||
                                 mode == GameMode.Paused || mode == GameMode.MemoryChoice ||
                                 mode == GameMode.Dead;
            playerView.SetActive(visiblePlayer);
            playerGlow.SetActive(visiblePlayer);
            playerShadow.SetActive(visiblePlayer);
            weaponView.SetActive(visiblePlayer && mode != GameMode.Dead);
            reticleView.SetActive(mode == GameMode.Playing);

            var bob = mode == GameMode.Playing
                ? Mathf.Sin(actionTime * 12f) * Mathf.Clamp01(playerVelocity.magnitude / 5f) * 0.025f
                : 0f;
            playerView.transform.position = new Vector3(playerPos.x, playerPos.y - 0.37f + bob, 0f);
            playerView.GetComponent<SpriteRenderer>().sortingOrder =
                6000 - Mathf.RoundToInt(playerPos.y * 10f);
            playerView.GetComponent<SpriteRenderer>().sprite = DirectionalHeroSprite();
            playerView.GetComponent<SpriteRenderer>().flipX = false;
            float flicker = invulnerable > 0f && mode == GameMode.Playing ? 0.72f : 1f;
            if (hurtFlash > 0f)
                flicker *= 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(actionTime * 26f));
            playerView.GetComponent<SpriteRenderer>().color =
                new Color(1f, 1f, 1f, flicker);
            playerShadow.transform.position = new Vector3(playerPos.x, playerPos.y - 0.34f, 0f);
            playerShadow.transform.localScale = new Vector3(0.86f + 0.06f * Mathf.Sin(actionTime * 2f),
                0.64f, 1f);
            playerGlow.transform.position = new Vector3(playerPos.x, playerPos.y + 0.1f, 0f);
            playerGlow.GetComponent<SpriteRenderer>().color = new Color(1f, 0.65f, 0.22f,
                0.16f + 0.05f * Mathf.Sin(actionTime * 2.2f));

            foreach (var enemy in enemies)
            {
                if (!enemy.Alive)
                    continue;
                var cell = WorldCell(enemy.Position);
                bool visibleAnywhere = mode == GameMode.Title ||
                                       enemy.State == EnemyStateAction.Telegraph ||
                                       enemy.Kind == EnemyKindAction.Boss && bossActive ||
                                       InBounds(cell) && tileLight[cell.x, cell.y] > 0.08f;
                enemy.View.SetActive(visibleAnywhere);
                if (!visibleAnywhere)
                    continue;
                float enemyBob = Mathf.Sin(actionTime * 3.7f + enemy.Id) * 0.018f;
                float bottomOffset = enemy.Kind == EnemyKindAction.Boss ? 0.7f : 0.35f;
                enemy.View.transform.position = new Vector3(enemy.Position.x,
                    enemy.Position.y - bottomOffset + enemyBob, 0f);
                enemy.View.GetComponent<SpriteRenderer>().sortingOrder =
                    (enemy.Kind == EnemyKindAction.Boss ? 6100 : 5980) -
                    Mathf.RoundToInt(enemy.Position.y * 10f);
                float blink = enemy.Flash > 0f ? 0.55f + 0.45f * Mathf.Sin(actionTime * 42f) : 1f;
                Color tint = enemy.State == EnemyStateAction.Telegraph
                    ? new Color(1f, 0.56f, 0.48f, 1f)
                    : enemy.BaseTint;
                enemy.Renderer.color = new Color(tint.r * blink, tint.g * blink,
                    tint.b * blink, 1f);
            }

            if (tutorialTarget.Alive)
            {
                bool tutorialVisible = !tutorialSkipped && tutorialStep != TutorialStep.Complete &&
                                       (mode == GameMode.Title || mode == GameMode.Playing);
                tutorialTarget.View.SetActive(tutorialVisible);
                tutorialTarget.View.transform.position = new Vector3(tutorialTarget.Position.x,
                    tutorialTarget.Position.y - 0.4f +
                    Mathf.Sin(actionTime * 2.1f) * 0.025f, 0f);
                tutorialTarget.Renderer.color = tutorialTarget.Flash > 0f
                    ? new Color(1f, 0.47f, 0.38f)
                    : Color.white;
            }
            else
            {
                tutorialTarget.View.SetActive(false);
            }

            if (scout != null)
            {
                scout.View.transform.position = new Vector3(scout.Position.x,
                    scout.Position.y - 0.30f + Mathf.Sin(actionTime * 8f) * 0.03f, 0f);
            }
        }

        private void UpdateLightingGrid()
        {
            bool title = mode == GameMode.Title;
            for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
            {
                var center = new Vector2(x + 0.5f, y + 0.5f);
                float light = 0f;
                if (title)
                {
                    light = 0.82f;
                }
                else
                {
                    float playerDistance = Vector2.Distance(playerPos, center);
                    if (playerDistance < 5.2f && HasLineOfSight(playerPos, center, 0.28f))
                        light = Mathf.Max(light, Mathf.Lerp(1f, 0.28f, playerDistance / 5.2f));
                    foreach (var cell in currentTrail)
                    {
                        var source = CellCenter(cell);
                        if (Vector2.Distance(source, center) < 1.58f)
                            light = Mathf.Max(light, 0.52f);
                    }
                    foreach (var cell in inheritedTrail)
                    {
                        var source = CellCenter(cell);
                        if (Vector2.Distance(source, center) < 1.28f)
                            light = Mathf.Max(light, 0.34f);
                    }
                    foreach (var pickup in lights.Where(l => l.Available))
                    {
                        float d = Vector2.Distance(pickup.Position, center);
                        if (d < 1.55f)
                            light = Mathf.Max(light, 0.38f * (1f - d / 1.55f));
                    }
                    if (scout != null)
                    {
                        float d = Vector2.Distance(scout.Position, center);
                        if (d < 3.1f && HasLineOfSight(scout.Position, center, 0.2f))
                            light = Mathf.Max(light, Mathf.Lerp(0.95f, 0.18f, d / 3.1f));
                    }
                    if (explored[x, y])
                        light = Mathf.Max(light, 0.10f);
                    if (Vector2.Distance(playerPos, center) < 2.25f)
                        light = Mathf.Max(light, 0.23f);
                }
                tileLight[x, y] = light;
                if (light > 0.14f)
                    explored[x, y] = true;
                RenderTile(x, y, light, title);
            }
        }

        private void RenderTile(int x, int y, float light, bool title)
        {
            var view = tileViews[x, y];
            var kind = tiles[x, y];
            bool isFloor = kind == TileKind.Floor || kind == TileKind.Gap;
            bool isWall = kind == TileKind.FixedWall || kind == TileKind.MovableWall;
            view.Floor.enabled = isFloor;
            if (isFloor)
            {
                view.Floor.sprite = kind == TileKind.Gap ? PixelArtHD.Gap :
                    kind == TileKind.Floor && (x * 19 + y * 23) % 13 == 0
                        ? PixelArtHD.MovableFloor
                        : (x * 7 + y * 5) % 3 == 0 ? PixelArtHD.FloorA :
                        (x * 7 + y * 5) % 3 == 1 ? PixelArtHD.FloorB : PixelArtHD.FloorC;
                float brightness = 0.2f + light * 0.98f;
                Color zone = currentMap == 1
                    ? new Color(0.76f, 0.93f, 1.12f)
                    : currentMap == 2
                        ? new Color(1.18f, 0.87f, 0.58f)
                        : x >= 18 && y <= 6
                            ? new Color(1.02f, 0.78f, 0.68f)
                            : x >= 17 && y >= 8
                                ? new Color(0.78f, 0.76f, 0.96f)
                                : y >= 14
                                    ? new Color(0.76f, 0.94f, 0.81f)
                                    : Color.white;
                view.Floor.color = new Color(0.69f * brightness * zone.r,
                    0.78f * brightness * zone.g, 0.79f * brightness * zone.b, 1f);
            }

            view.Wall.enabled = isWall;
            view.Shadow.enabled = isWall && light > 0.03f;
            if (isWall)
            {
                view.Wall.sprite = kind == TileKind.MovableWall &&
                                   (light > 0.25f || explored[x, y])
                    ? PixelArtHD.MovableWall
                    : PixelArtHD.Wall;
                float brightness = 0.2f + light;
                Color wallZone = currentMap == 1
                    ? new Color(0.75f, 0.88f, 1.12f)
                    : currentMap == 2
                        ? new Color(1.18f, 0.82f, 0.55f)
                        : x >= 18 && y <= 7
                            ? new Color(1f, 0.82f, 0.76f)
                            : x >= 17 && y >= 8
                                ? new Color(0.82f, 0.83f, 1f)
                                : Color.white;
                view.Wall.color = new Color(0.70f * brightness * wallZone.r,
                    0.76f * brightness * wallZone.g, 0.78f * brightness * wallZone.b, 1f);
            }

            var cell = new Vector2Int(x, y);
            bool warm = currentTrail.Contains(cell);
            bool inherited = inheritedTrail.Contains(cell);
            view.Trail.enabled = warm;
            if (warm)
                view.Trail.color = new Color(1f, 0.62f, 0.21f,
                    0.22f + 0.07f * Mathf.Sin(actionTime * 4f + x));
            view.Inherited.enabled = inherited;
            if (inherited)
                view.Inherited.color = new Color(1f, 0.74f, 0.37f,
                    0.15f + 0.05f * Mathf.Sin(actionTime * 2f + y));

            bool danger = IsDangerCell(cell, out Color dangerColor);
            view.Danger.enabled = danger;
            if (danger)
            {
                float pulse = 0.22f + 0.16f * Mathf.Sin(actionTime * 12f + x + y);
                view.Danger.color = new Color(dangerColor.r, dangerColor.g, dangerColor.b, pulse);
            }
        }

        private bool IsDangerCell(Vector2Int cell, out Color color)
        {
            color = Coral;
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive || enemy.State != EnemyStateAction.Telegraph)
                    continue;
                if (enemy.Kind == EnemyKindAction.Melee)
                {
                    if (WorldCell(enemy.LockedTarget) == cell)
                    {
                        color = new Color(1f, 0.32f, 0.22f);
                        return true;
                    }
                }
                else if (enemy.Kind == EnemyKindAction.Ranged)
                {
                    for (int step = 1; step < 10; step++)
                    {
                        var pos = enemy.Position + enemy.LockedDirection * step * 0.5f;
                        if (WorldCell(pos) == cell)
                        {
                            color = new Color(1f, 0.28f, 0.23f);
                            return true;
                        }
                        if (CircleBlocked(pos, 0.03f))
                            break;
                    }
                }
                else
                {
                    if (bossChoice == 1)
                    {
                        if (Vector2.Distance(CellCenter(cell), enemy.Position) < 2.35f)
                        {
                            color = new Color(1f, 0.39f, 0.18f);
                            return true;
                        }
                    }
                    else
                    {
                        for (int step = 1; step < 6; step++)
                        {
                            if (WorldCell(enemy.Position + enemy.LockedDirection * step) == cell)
                            {
                                color = new Color(1f, 0.27f, 0.19f);
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool CircleBlocked(Vector2 position, float radius)
        {
            int minX = Mathf.FloorToInt(position.x - radius);
            int maxX = Mathf.FloorToInt(position.x + radius);
            int minY = Mathf.FloorToInt(position.y - radius);
            int maxY = Mathf.FloorToInt(position.y + radius);
            for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            {
                if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
                    return true;
                if (tiles[x, y] == TileKind.Floor)
                    continue;
                float closestX = Mathf.Clamp(position.x, x, x + 1f);
                float closestY = Mathf.Clamp(position.y, y, y + 1f);
                var delta = position - new Vector2(closestX, closestY);
                if (delta.sqrMagnitude < radius * radius)
                    return true;
            }
            return false;
        }

        private bool HasLineOfSight(Vector2 from, Vector2 to, float radius)
        {
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.CeilToInt(distance / 0.14f);
            for (int i = 1; i < steps; i++)
            {
                var point = Vector2.Lerp(from, to, i / (float)steps);
                if (CircleBlocked(point, radius))
                    return false;
            }
            return true;
        }

        private void MoveEnemy(LiveEnemy enemy, Vector2 delta, float radius)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.08f));
            var step = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                var nextX = enemy.Position + new Vector2(step.x, 0f);
                if (!CircleBlocked(nextX, radius))
                    enemy.Position.x = nextX.x;
                var nextY = enemy.Position + new Vector2(0f, step.y);
                if (!CircleBlocked(nextY, radius))
                    enemy.Position.y = nextY.y;
            }
        }

        private int CurrentMemoryIndex(MemoryNodeAction crystal)
        {
            return crystals.IndexOf(crystal);
        }

        private bool HasMemory(string id)
        {
            return memories.Contains(id);
        }

        private Vector2Int FacingCell()
        {
            return WorldCell(playerPos + aimDirection * 0.85f);
        }

        private static Vector2Int WorldCell(Vector2 pos)
        {
            return new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
        }

        private static Vector2 CellCenter(Vector2Int cell)
        {
            return new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        }

        private static bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < MapWidth && cell.y >= 0 && cell.y < MapHeight;
        }

        private void ResetMap()
        {
            BuildMap();
            for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
            {
                var view = tileViews[x, y];
                view.Trail.enabled = false;
                view.Inherited.enabled = false;
                view.Danger.enabled = false;
            }
        }

        private void SetToast(string text, Color color)
        {
            toastText = text;
            toastColor = color;
            toastTimer = 4.2f;
        }

        private void SpawnDamageNumber(Vector2 position, int amount, Color color)
        {
            // Numeric feedback is intentionally quiet; bursts and hit flashes carry the read.
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
            float ox = (Screen.width - DesignWidth * scale) * 0.5f;
            float oy = (Screen.height - DesignHeight * scale) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));

            if (mode == GameMode.Title)
            {
                DrawTitle();
                return;
            }

            DrawHud();
            DrawWorldIndicators();
            DrawToast();
            DrawCrosshair(scale, ox, oy);

            if (mode == GameMode.Paused)
                DrawPause();
            else if (mode == GameMode.MemoryChoice)
                DrawMemoryChoice();
            else if (mode == GameMode.Dead)
                DrawDeath();
            else if (mode == GameMode.Won)
                DrawEnd(true);
            else if (mode == GameMode.Lost)
                DrawEnd(false);
        }

        private void EnsureStyles()
        {
            if (styleReady && titleStyle != null)
                return;
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 24);
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Paper },
                alignment = TextAnchor.MiddleLeft
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Paper },
                alignment = TextAnchor.MiddleLeft
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 17,
                normal = { textColor = Paper },
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 13,
                normal = { textColor = Muted },
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            tinyStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 11,
                normal = { textColor = Muted },
                alignment = TextAnchor.MiddleLeft
            };
            monoStyle = new GUIStyle(tinyStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Gold }
            };
            centerStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    background = Solid(new Color32(38, 65, 73, 255)),
                    textColor = Paper
                },
                hover =
                {
                    background = Solid(new Color32(70, 104, 111, 255)),
                    textColor = Color.white
                },
                active =
                {
                    background = Solid(new Color32(183, 118, 40, 255)),
                    textColor = Color.white
                }
            };
            scanlines = PixelArtHD.ScanlineTexture;
            styleReady = true;
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Panel(Rect rect, Color fill, Color border)
        {
            Fill(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), border);
            Fill(rect, fill);
            Fill(new Rect(rect.x, rect.y, 4, rect.height), border);
        }

        private static void Meter(Rect rect, float value, float max, Color color)
        {
            Fill(rect, new Color(0.02f, 0.055f, 0.07f, 0.92f));
            float ratio = max <= 0f ? 0f : Mathf.Clamp01(value / max);
            Fill(new Rect(rect.x + 2, rect.y + 2, Mathf.Max(0f, (rect.width - 4) * ratio),
                rect.height - 4), color);
        }

        private void DrawTitle()
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0.01f, 0.07f, 0.10f, 0.72f));
            Fill(new Rect(80, 78, 3, 543), Gold);
            Fill(new Rect(95, 80, 1, 540), new Color(0.56f, 0.91f, 0.93f, 0.5f));
            GUI.Label(new Rect(127, 101, 700, 88), "走错也算数", titleStyle);
            GUI.Label(new Rect(132, 186, 620, 30), "WRONG TURN / RIGHT PATH", new GUIStyle(monoStyle)
            {
                fontSize = 19
            });
            GUI.Label(new Rect(132, 230, 590, 45), "森林 · 冰原 · 沙漠", new GUIStyle(headingStyle)
            {
                fontSize = 30,
                normal = { textColor = Paper }
            });
            GUI.Label(new Rect(132, 281, 520, 64),
                "像地牢清场一样移动、瞄准和连射。\n走错的路线不会消失，它会变成伤害，也会成为下一世的光。",
                bodyStyle);

            DrawTitleStat(new Rect(132, 369, 154, 70), "WASD", "顺滑移动");
            DrawTitleStat(new Rect(297, 369, 154, 70), "鼠标", "连射瞄准");
            DrawTitleStat(new Rect(462, 369, 154, 70), "Shift", "无敌闪避");

            GUI.Label(new Rect(132, 455, 250, 20), "选择地图  ·  数字键 1 / 2 / 3", tinyStyle);
            DrawStageCard(new Rect(132, 479, 154, 52), 0, "森林", "9 名敌人",
                new Color32(102, 219, 146, 255));
            DrawStageCard(new Rect(297, 479, 154, 52), 1, "冰原", "10 名敌人",
                new Color32(111, 211, 241, 255));
            DrawStageCard(new Rect(462, 479, 154, 52), 2, "沙漠", "12 名敌人",
                new Color32(239, 171, 82, 255));

            if (GUI.Button(new Rect(132, 546, 242, 57),
                "进入" + StageNames[selectedMap] + "  ↩", buttonStyle))
                StartNewRun();
            GUI.Label(new Rect(392, 560, 280, 24),
                "Enter 开始 · 建议打开声音", smallStyle);

            var showcase = new Rect(695, 93, 485, 522);
            Panel(showcase, new Color(0.035f, 0.105f, 0.13f, 0.9f),
                new Color(0.31f, 0.58f, 0.59f, 0.9f));
            GUI.Label(new Rect(720, 120, 435, 34), "动作手感升级", headingStyle);
            GUI.Label(new Rect(720, 158, 420, 26),
                "移动惯性  ·  瞄准吸附  ·  环戒连射  ·  无敌帧", smallStyle);
            Fill(new Rect(720, 199, 432, 1), new Color(0.45f, 0.76f, 0.75f, 0.45f));
            var portraitFrame = new Rect(864, 207, 258, 252);
            Panel(portraitFrame, new Color(0.015f, 0.065f, 0.085f, 0.97f),
                new Color(0.39f, 0.72f, 0.70f, 0.78f));
            if (heroPortrait != null)
            {
                GUI.DrawTexture(new Rect(portraitFrame.x + 9, portraitFrame.y + 9,
                    portraitFrame.width - 18, portraitFrame.height - 18),
                    heroPortrait, ScaleMode.ScaleToFit, true);
            }
            GUI.Label(new Rect(720, 220, 126, 150),
                "连续移动\n\n鼠标瞄准\n\n按住连射\n\n闪避无敌",
                new GUIStyle(bodyStyle) { fontSize = 14 });
            GUI.Label(new Rect(720, 436, 420, 25), "保留核心", new GUIStyle(monoStyle)
            {
                fontSize = 14
            });
            GUI.Label(new Rect(720, 475, 420, 112),
                "路线记录 → 输出残影\n探路幽灵 → 点亮区域\n拆墙铺路 → 打开捷径\n记忆冰晶 → 二选一强化",
                bodyStyle);
            GUI.Label(new Rect(1010, 674, 230, 22), "THREE REALMS  /  v3.0", monoStyle);
        }

        private void DrawStageCard(Rect rect, int index, string name, string count, Color accent)
        {
            bool selected = selectedMap == index;
            Panel(rect, new Color(0.025f, 0.09f, 0.115f, 0.95f),
                selected ? accent : new Color(0.25f, 0.39f, 0.42f, 0.9f));
            Fill(new Rect(rect.x + 9, rect.y + 11, 5, rect.height - 22), accent);
            GUI.Label(new Rect(rect.x + 22, rect.y + 7, 118, 24), name,
                new GUIStyle(headingStyle) { fontSize = 19 });
            GUI.Label(new Rect(rect.x + 22, rect.y + 28, 118, 18), count, tinyStyle);
            if (GUI.Button(rect, GUIContent.none, new GUIStyle
                {
                    normal = { background = null },
                    hover = { background = null },
                    active = { background = null }
                }))
                SelectStageForTitle(index);
        }

        private void SelectStageForTitle(int index)
        {
            selectedMap = Mathf.Clamp(index, 0, 2);
            currentMap = selectedMap;
            ClearProjectiles();
            ClearGhosts();
            RecallScout(false);
            currentTrail.Clear();
            inheritedTrail.Clear();
            ApplyStageData();
            ResetMap();
            Array.Clear(explored, 0, explored.Length);
            Array.Clear(tileLight, 0, tileLight.Length);
            playerPos = StartPosition;
            playerVelocity = Vector2.zero;
            aimDirection = Vector2.down;
            SpawnEnemies();
            audio?.PlayClip("ui");
            UpdateLightingGrid();
        }

        private void DrawTitleStat(Rect rect, string key, string label)
        {
            Panel(rect, new Color(0.035f, 0.12f, 0.15f, 0.92f),
                new Color(0.21f, 0.47f, 0.50f, 0.9f));
            GUI.Label(new Rect(rect.x + 12, rect.y + 7, rect.width - 24, 28),
                key, new GUIStyle(monoStyle) { fontSize = 17 });
            GUI.Label(new Rect(rect.x + 12, rect.y + 35, rect.width - 24, 24),
                label, smallStyle);
        }

        private void DrawTitleCombatDiagram(Rect rect)
        {
            Fill(rect, new Color(0.015f, 0.065f, 0.085f, 0.95f));
            for (int i = 0; i < 7; i++)
            {
                Fill(new Rect(rect.x + 10 + i * 60, rect.y + 18, 5, 5), 
                    i == 5 ? Gold : new Color(0.24f, 0.56f, 0.61f, 0.55f));
                Fill(new Rect(rect.x + 20 + i * 60, rect.y + 137, 5, 5),
                    i == 2 ? Cyan : new Color(0.24f, 0.56f, 0.61f, 0.38f));
            }
            Fill(new Rect(rect.x + 39, rect.y + 77, 32, 42), new Color(0.18f, 0.48f, 0.56f));
            Fill(new Rect(rect.x + 48, rect.y + 58, 14, 23), new Color(0.78f, 0.66f, 0.48f));
            Fill(new Rect(rect.x + 50, rect.y + 63, 3, 3), Ink);
            Fill(new Rect(rect.x + 57, rect.y + 63, 3, 3), Ink);
            Fill(new Rect(rect.x + 34, rect.y + 112, 17, 7), new Color(0.08f, 0.14f, 0.17f));
            Fill(new Rect(rect.x + 58, rect.y + 112, 17, 7), new Color(0.08f, 0.14f, 0.17f));
            Fill(new Rect(rect.x + 80, rect.y + 80, 25, 5), Gold);
            Fill(new Rect(rect.x + 111, rect.y + 77, 13, 11), new Color(1f, 0.85f, 0.42f));
            Fill(new Rect(rect.x + 208, rect.y + 30, 36, 36), new Color(0.55f, 0.23f, 0.30f));
            Fill(new Rect(rect.x + 215, rect.y + 37, 22, 21), new Color(0.82f, 0.33f, 0.35f));
            LineGui(new Vector2(rect.x + 342, rect.y + 45), new Vector2(rect.x + 342, rect.y + 126),
                new Color(1f, 0.31f, 0.25f, 0.58f), 3);
            Fill(new Rect(rect.x + 330, rect.y + 132, 24, 5), new Color(0.76f, 0.52f, 0.35f));
            Fill(new Rect(rect.x + 338, rect.y + 137, 8, 7), new Color(0.75f, 0.25f, 0.28f));
        }

        private static void LineGui(Vector2 a, Vector2 b, Color color, int thickness)
        {
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            Fill(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), color);
            GUI.matrix = old;
        }

        private void DrawHud()
        {
            var left = new Rect(22, 20, 304, 150);
            Panel(left, new Color(0.025f, 0.10f, 0.13f, 0.94f),
                new Color(0.30f, 0.63f, 0.66f, 0.88f));
            GUI.Label(new Rect(38, 29, 200, 28), "巡路者 · " + StageNames[currentMap], headingStyle);
            if (heroPortrait != null)
            {
                Fill(new Rect(250, 32, 58, 58), new Color(0.04f, 0.14f, 0.17f, 0.95f));
                GUI.DrawTexture(new Rect(254, 36, 50, 50), heroPortrait, ScaleMode.ScaleToFit, true);
            }
            GUI.Label(new Rect(38, 61, 52, 19), "生命", tinyStyle);
            for (int i = 0; i < 3; i++)
            {
                bool active = i < Mathf.CeilToInt(hp);
                Fill(new Rect(95 + i * 29, 63, 18, 16),
                    active ? Coral : new Color(0.2f, 0.25f, 0.26f));
                Fill(new Rect(99 + i * 29, 66, 10, 4),
                    active ? new Color(1f, 0.75f, 0.55f) : new Color(0.3f, 0.34f, 0.35f));
            }
            GUI.Label(new Rect(194, 59, 118, 22), "灯命 " + lives + " / 3", monoStyle);
            GUI.Label(new Rect(38, 88, 52, 18), "影能", tinyStyle);
            Meter(new Rect(95, 90, 212, 10), shadowEnergy, 12f, Cyan);
            GUI.Label(new Rect(38, 109, 52, 18), "光", tinyStyle);
            Meter(new Rect(95, 111, 212, 10), lightEnergy, 10f, Gold);
            GUI.Label(new Rect(38, 129, 70, 18), "闪避", tinyStyle);
            Meter(new Rect(95, 130, 82, 9), Mathf.Max(0f, 0.82f - dashCooldown), 0.82f,
                dashCooldown <= 0f ? Mint : new Color(0.35f, 0.44f, 0.44f));
            GUI.Label(new Rect(183, 124, 130, 22),
                dashCooldown <= 0f ? "READY  Shift" : dashCooldown.ToString("0.0") + "s",
                monoStyle);
            GUI.Label(new Rect(38, 150, 260, 18),
                carryingWall ? "携带墙材：靠近缺口按 F" :
                recording ? "记录中：移动落笔，松开 E 封存" : "路线残影：" + ghosts.Count,
                smallStyle);

            DrawTutorialCard();
            DrawMiniMap();
            DrawBottomBar();
            DrawBossBar();
        }

        private void DrawTutorialCard()
        {
            if (tutorialSkipped || tutorialStep == TutorialStep.Complete || mode != GameMode.Playing)
                return;
            var rect = new Rect(386, 26, 508, 112);
            Panel(rect, new Color(0.02f, 0.11f, 0.14f, 0.94f),
                new Color(0.30f, 0.72f, 0.74f, 0.9f));
            string title;
            string desc;
            string key;
            switch (tutorialStep)
            {
                case TutorialStep.Move:
                    title = "01  移动";
                    desc = "靠近青色标记。角色有加速和减速，连续移动会很顺。";
                    key = "WASD";
                    break;
                case TutorialStep.Shoot:
                    title = "02  瞄准与连射";
                    desc = "用鼠标瞄准训练靶，按住左键或空格投出旋转环戒。";
                    key = "左键 / 空格";
                    break;
                case TutorialStep.Dash:
                    title = "03  闪避";
                    desc = "闪避会提供短暂无敌，也能快速穿过危险落点。";
                    key = "Shift";
                    break;
                case TutorialStep.Interact:
                    title = "04  交互";
                    desc = "靠近发光遗物，按 F 拾取。拆墙、冰晶和补缺口也用 F。";
                    key = "F";
                    break;
                default:
                    title = "05  路线残影";
                    desc = "按住 E 移动，留下青色旧路；松开 E 后，敌人走进去会持续受伤。";
                    key = "按住 E";
                    break;
            }
            GUI.Label(new Rect(405, 36, 330, 28), title, headingStyle);
            GUI.Label(new Rect(405, 66, 463, 24), desc, smallStyle);
            Panel(new Rect(813, 37, 65, 24), new Color(0.14f, 0.31f, 0.34f, 0.9f),
                new Color(0.31f, 0.65f, 0.66f, 0.8f));
            GUI.Label(new Rect(816, 39, 59, 20), key, new GUIStyle(tinyStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Paper }
            });
            GUI.Label(new Rect(405, 96, 465, 18),
                "T 跳过引导   ·   Esc 查看全部操作", tinyStyle);
            int stepIndex = (int)tutorialStep;
            for (int i = 0; i < 5; i++)
                Fill(new Rect(408 + i * 44, 122, 36, 3),
                    i <= stepIndex ? Cyan : new Color(0.22f, 0.34f, 0.36f));
        }

        private void DrawMiniMap()
        {
            var rect = new Rect(956, 20, 302, 185);
            Panel(rect, new Color(0.025f, 0.09f, 0.12f, 0.94f),
                new Color(0.27f, 0.54f, 0.56f, 0.85f));
            GUI.Label(new Rect(972, 28, 220, 27), "迷宫简图", headingStyle);
            var map = new Rect(991, 59, 232, 130);
            int cw = 7;
            int ch = 6;
            for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
            {
                if (!explored[x, y])
                    continue;
                Color c = tiles[x, y] == TileKind.Floor
                    ? new Color(0.28f, 0.55f, 0.55f, 0.55f)
                    : new Color(0.16f, 0.26f, 0.28f, 0.42f);
                if (tileLight[x, y] > 0.45f)
                    c = new Color(0.68f, 0.85f, 0.73f, 0.9f);
                Fill(new Rect(map.x + x * cw, map.y + (MapHeight - 1 - y) * ch,
                    cw - 1, ch - 1), c);
            }
            var playerMap = new Vector2(map.x + playerPos.x * cw,
                map.y + (MapHeight - 1 - playerPos.y) * ch);
            Fill(new Rect(playerMap.x - 3, playerMap.y - 2, 6, 6), Gold);
            if (bossActive || bossDefeated)
            {
                Fill(new Rect(map.x + 22.1f * cw - 3, map.y + (MapHeight - 1 - 4.1f) * ch - 3,
                    6, 6), bossDefeated ? Mint : Coral);
            }
            GUI.Label(new Rect(975, 178, 265, 18),
                "青色路径=探路光   金色路径=已回看区域", tinyStyle);
        }

        private void DrawBottomBar()
        {
            var rect = new Rect(22, 669, 1236, 34);
            Panel(rect, new Color(0.02f, 0.07f, 0.09f, 0.94f),
                new Color(0.24f, 0.45f, 0.47f, 0.8f));
            GUI.Label(new Rect(37, 674, 1190, 22),
                "WASD 移动   鼠标 瞄准   左键/空格 连射   Shift 闪避   Q 光照幽灵   按住 E 记录   F 交互   Esc 暂停",
                smallStyle);
        }

        private void DrawBossBar()
        {
            if (!bossActive)
                return;
            var boss = enemies.FirstOrDefault(e => e.Kind == EnemyKindAction.Boss && e.Alive);
            if (boss == null)
                return;
            var rect = new Rect(440, 151, 400, 49);
            Panel(rect, new Color(0.12f, 0.035f, 0.045f, 0.96f),
                new Color(0.81f, 0.30f, 0.23f, 0.95f));
            GUI.Label(new Rect(456, 144, 230, 22), BossNames[currentMap], monoStyle);
            GUI.Label(new Rect(704, 144, 116, 22), boss.Hp + " / " + boss.MaxHp,
                new GUIStyle(monoStyle) { alignment = TextAnchor.MiddleRight });
            Meter(new Rect(456, 169, 368, 9), boss.Hp, boss.MaxHp, Coral);
        }

        private void DrawWorldIndicators()
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive)
                    continue;
                var cell = WorldCell(enemy.Position);
                bool visible = InBounds(cell) && tileLight[cell.x, cell.y] > 0.08f ||
                               enemy.State == EnemyStateAction.Telegraph;
                if (!visible)
                    continue;
                var guiPos = WorldToGui(enemy.Position + Vector2.up * 0.88f);
                float width = enemy.Kind == EnemyKindAction.Boss ? 92f : 46f;
                Fill(new Rect(guiPos.x - width * 0.5f - 2, guiPos.y - 4, width + 4, 7),
                    new Color(0f, 0f, 0f, 0.72f));
                Fill(new Rect(guiPos.x - width * 0.5f, guiPos.y - 3,
                    width * Mathf.Clamp01(enemy.Hp / (float)enemy.MaxHp), 5),
                    enemy.State == EnemyStateAction.Telegraph ? Coral : Gold);
                if (enemy.State == EnemyStateAction.Telegraph)
                {
                    string warn = enemy.Kind == EnemyKindAction.Ranged ? "LOCK" :
                        enemy.Kind == EnemyKindAction.Boss && bossChoice == 1 ? "SLAM" : "DANGER";
                    GUI.Label(new Rect(guiPos.x - 45, guiPos.y + 7, 90, 18), warn,
                        new GUIStyle(tinyStyle)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = Coral }
                        });
                }
            }

            if ((mode == GameMode.Playing || mode == GameMode.Paused) &&
                Vector2.Distance(playerPos, ExitPosition) < 2f)
            {
                var pos = WorldToGui(ExitPosition + Vector2.up * 1.05f);
                GUI.Label(new Rect(pos.x - 75, pos.y - 12, 150, 22),
                    exitOpen ? "出口 · 走进去" : "封印出口 · 击败 Boss", monoStyle);
            }
        }

        private Vector2 WorldToGui(Vector2 worldPos)
        {
            var viewport = cam.WorldToViewportPoint(new Vector3(worldPos.x, worldPos.y, 0f));
            return new Vector2(viewport.x * DesignWidth, (1f - viewport.y) * DesignHeight);
        }

        private void DrawToast()
        {
            if (toastTimer <= 0f || string.IsNullOrEmpty(toastText))
                return;
            toastTimer -= Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(toastTimer / 0.45f);
            var rect = new Rect(314, 614, 652, 37);
            Panel(rect, new Color(0.015f, 0.07f, 0.09f, 0.9f * alpha),
                new Color(toastColor.r, toastColor.g, toastColor.b, 0.44f * alpha));
            GUI.Label(new Rect(326, 620, 628, 24), toastText, new GUIStyle(centerStyle)
            {
                fontSize = 15,
                normal = { textColor = new Color(toastColor.r, toastColor.g, toastColor.b, alpha) }
            });
        }

        private void DrawCrosshair(float scale, float ox, float oy)
        {
            if (mode != GameMode.Playing)
                return;
            float x = (Input.mousePosition.x - ox) / scale;
            float y = (Screen.height - Input.mousePosition.y - oy) / scale;
            Color c = new Color(1f, 0.82f, 0.34f, 0.9f);
            Fill(new Rect(x - 10, y - 1, 7, 2), c);
            Fill(new Rect(x + 4, y - 1, 7, 2), c);
            Fill(new Rect(x - 1, y - 10, 2, 7), c);
            Fill(new Rect(x - 1, y + 4, 2, 7), c);
            Fill(new Rect(x - 2, y - 2, 4, 4), new Color(1f, 1f, 1f, 0.92f));
        }

        private void DrawPause()
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0f, 0.06f, 0.08f, 0.78f));
            var rect = new Rect(290, 78, 700, 560);
            Panel(rect, new Color(0.025f, 0.11f, 0.14f, 0.98f),
                new Color(0.54f, 0.77f, 0.72f, 0.92f));
            GUI.Label(new Rect(326, 104, 540, 44), "暂停 / 先看清下一步", headingStyle);
            GUI.Label(new Rect(326, 148, 620, 24), "战斗暂停，鼠标会重新显示。", smallStyle);
            Fill(new Rect(326, 184, 628, 1), new Color(0.42f, 0.68f, 0.67f, 0.48f));
            GUI.Label(new Rect(330, 211, 278, 272),
                "WASD / 方向键\n连续移动，带加减速\n\n鼠标\n自由瞄准，带轻微吸附\n\n左键 / 空格\n按住连续投环\n\nShift\n短距闪避 + 无敌帧",
                bodyStyle);
            GUI.Label(new Rect(652, 211, 300, 272),
                "Q\n放出穿透黑暗的探路幽灵\n\n按住 E\n边走边记录战斗路线\n\nF\n拾取、拆墙、补缺口\n\nT\n跳过当前新手引导",
                bodyStyle);
            if (GUI.Button(new Rect(330, 525, 184, 52), "继续", buttonStyle))
            {
                mode = GameMode.Playing;
                Cursor.visible = false;
                AudioListener.pause = false;
            }
            if (GUI.Button(new Rect(538, 525, 184, 52), "重新开始", buttonStyle))
                StartNewRun();
            if (GUI.Button(new Rect(746, 525, 184, 52), "退出游戏", buttonStyle))
                Application.Quit();
        }

        private void DrawMemoryChoice()
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0f, 0.05f, 0.08f, 0.86f));
            GUI.Label(new Rect(0, 58, DesignWidth, 44), "选择一种留下来的解释", new GUIStyle(headingStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 31
            });
            GUI.Label(new Rect(0, 101, DesignWidth, 24),
                openMemory == null ? "" : openMemory.Title + "  ·  本局永久生效",
                new GUIStyle(centerStyle) { normal = { textColor = Muted } });
            if (activeChoices.Count == 2)
            {
                DrawMemoryCard(new Rect(160, 158, 460, 410), activeChoices[0], 0,
                    memoryChoiceIndex == 0);
                DrawMemoryCard(new Rect(660, 158, 460, 410), activeChoices[1], 1,
                    memoryChoiceIndex == 1);
            }
            GUI.Label(new Rect(0, 591, DesignWidth, 25),
                "A / D 切换     Enter / 空格 确认     Esc 暂不选择",
                new GUIStyle(centerStyle) { normal = { textColor = Muted } });
        }

        private void DrawMemoryCard(Rect rect, MemoryOption option, int index, bool selected)
        {
            Color accent = option.Color;
            Panel(rect, new Color(0.025f, 0.10f, 0.13f, 0.98f),
                selected ? accent : new Color(0.26f, 0.40f, 0.42f, 0.9f));
            if (selected)
                Fill(new Rect(rect.x, rect.y, rect.width, 5), accent);
            GUI.Label(new Rect(rect.x + 25, rect.y + 25, 45, 24), index == 0 ? "A" : "B",
                new GUIStyle(monoStyle) { fontSize = 22 });
            GUI.Label(new Rect(rect.x + 74, rect.y + 23, rect.width - 100, 30),
                option.Title, headingStyle);

            var art = new Rect(rect.x + 25, rect.y + 70, rect.width - 50, 170);
            Fill(art, new Color(0.012f, 0.06f, 0.08f, 0.96f));
            for (int i = 1; i < 5; i++)
                Fill(new Rect(art.x + 12, art.y + i * 31, art.width - 24, 1),
                    new Color(accent.r, accent.g, accent.b, 0.12f));
            float cx = art.x + art.width * 0.5f;
            float cy = art.y + art.height * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                float r = 28f + i * 20f;
                Fill(new Rect(cx - r, cy - 1, r * 2f, 2f),
                    new Color(accent.r, accent.g, accent.b, 0.24f - i * 0.055f));
                Fill(new Rect(cx - 1, cy - r, 2f, r * 2f),
                    new Color(accent.r, accent.g, accent.b, 0.24f - i * 0.055f));
            }
            Fill(new Rect(cx - 23, cy - 23, 46, 46), new Color(accent.r * 0.38f,
                accent.g * 0.38f, accent.b * 0.38f, 1f));
            Fill(new Rect(cx - 10, cy - 10, 20, 20), accent);
            Fill(new Rect(cx - 4, cy - 4, 8, 8), Paper);

            GUI.Label(new Rect(rect.x + 25, rect.y + 256, rect.width - 50, 28),
                option.Tag, new GUIStyle(headingStyle) { fontSize = 20 });
            GUI.Label(new Rect(rect.x + 25, rect.y + 292, rect.width - 50, 75),
                option.Description, bodyStyle);
            GUI.Label(new Rect(rect.x + 25, rect.y + 369, rect.width - 50, 20),
                selected ? "ENTER 确认" : (index == 0 ? "A 切换" : "D 切换"), monoStyle);
            if (GUI.Button(rect, GUIContent.none, new GUIStyle
            {
                normal = { background = null },
                hover = { background = null },
                active = { background = null }
            }))
                ConfirmMemory(index);
        }

        private void DrawDeath()
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0.05f, 0.01f, 0.02f, 0.67f));
            GUI.Label(new Rect(0, 282, DesignWidth, 66), "灯灭了", new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 58
            });
            GUI.Label(new Rect(0, 359, DesignWidth, 34),
                currentTrail.Count > 0 ? "本世走过的光正在沉入下一世。" : "旧路仍在，下一世继续照你。",
                centerStyle);
            Meter(new Rect(470, 426, 340, 8), Mathf.Clamp01(deathTimer / 1.25f), 1f, Gold);
            GUI.Label(new Rect(0, 447, DesignWidth, 24),
                "敌人与地形复位 · 冰晶记忆保留", smallStyle);
        }

        private void DrawEnd(bool won)
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0f, 0.05f, 0.08f, 0.86f));
            var rect = new Rect(300, 78, 680, 548);
            Panel(rect, new Color(0.025f, 0.10f, 0.13f, 0.98f),
                won ? Gold : Coral);
            GUI.Label(new Rect(335, 108, 600, 76), won ? "出口在身后合上" : "这里没有新的黎明",
                new GUIStyle(titleStyle) { fontSize = 47 });
            GUI.Label(new Rect(339, 179, 600, 34),
                won ? "你从错误的路线里带回了答案。" : "灯火耗尽，但迷宫仍留下了一条可以重走的路。",
                headingStyle);
            GUI.Label(new Rect(340, 215, 590, 24),
                "当前地图：" + StageNames[selectedMap] + "  ·  " + BossNames[selectedMap],
                smallStyle);
            GUI.Label(new Rect(340, 247, 270, 160),
                "使用生命\n" + Mathf.Clamp(3 - lives, 0, 3) + " / 3\n\n距离出口\n" +
                Vector2.Distance(playerPos, ExitPosition).ToString("0.0") + " 格\n\n保留记忆\n" +
                memories.Count + " 项",
                bodyStyle);
            GUI.Label(new Rect(640, 247, 300, 180),
                "关键提示\n\n连射时保持移动\n危险区域先闪避再输出\n战斗中也能按住 E 记录路线",
                bodyStyle);
            var endButton = new GUIStyle(buttonStyle) { fontSize = 15 };
            if (GUI.Button(new Rect(337, 520, 143, 52), "重玩  R", endButton))
                StartNewRun();
            if (won && selectedMap < 2)
            {
                if (GUI.Button(new Rect(492, 520, 143, 52), "下一关  N", endButton))
                {
                    selectedMap++;
                    StartNewRun();
                }
            }
            else
            {
                GUI.Button(new Rect(492, 520, 143, 52), selectedMap >= 2 && won ? "三关完成" :
                    "未通关", new GUIStyle(endButton)
                    {
                        normal = { textColor = Muted, background = Solid(new Color32(25, 39, 43, 255)) },
                        hover = { textColor = Muted, background = Solid(new Color32(25, 39, 43, 255)) }
                    });
            }
            if (GUI.Button(new Rect(647, 520, 143, 52), "选择地图  M", endButton))
                ReturnToMapSelect();
            if (GUI.Button(new Rect(802, 520, 143, 52), "退出", endButton))
                Application.Quit();
        }

        private IEnumerator Shake(float duration, float strength)
        {
            shakeTime = Mathf.Max(shakeTime, duration);
            shakeStrength = Mathf.Max(shakeStrength, strength);
            yield break;
        }

        private IEnumerator Burst(Vector2 position, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var spark = NewSprite("Spark", PixelArtHD.Spark, 6500, world);
                spark.transform.position = position;
                spark.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.16f, 0.34f);
                var renderer = spark.GetComponent<SpriteRenderer>();
                renderer.color = color;
                Vector2 velocity = UnityEngine.Random.insideUnitCircle.normalized *
                                   UnityEngine.Random.Range(1f, 3.6f);
                float duration = UnityEngine.Random.Range(0.22f, 0.46f);
                StartCoroutine(AnimateParticle(spark, velocity, color, duration));
                yield return new WaitForSecondsRealtime(0.008f);
            }
        }

        private IEnumerator AnimateParticle(GameObject particle, Vector2 velocity, Color color,
            float duration)
        {
            float elapsed = 0f;
            var renderer = particle.GetComponent<SpriteRenderer>();
            while (elapsed < duration && particle != null)
            {
                elapsed += Time.unscaledDeltaTime;
                particle.transform.position += (Vector3)(velocity * Time.unscaledDeltaTime);
                velocity *= 0.95f;
                renderer.color = new Color(color.r, color.g, color.b,
                    color.a * (1f - elapsed / duration));
                particle.transform.localScale *= 0.984f;
                yield return null;
            }
            if (particle != null)
                Destroy(particle);
        }

        private IEnumerator MuzzleFlash(Vector2 position, Vector2 direction)
        {
            var flash = NewSprite("Muzzle", PixelArtHD.Spark, 6600, world);
            flash.transform.position = position;
            flash.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            flash.transform.localScale = Vector3.one * 0.75f;
            var renderer = flash.GetComponent<SpriteRenderer>();
            renderer.color = new Color(1f, 0.86f, 0.36f, 1f);
            yield return new WaitForSecondsRealtime(0.06f);
            if (flash != null)
                Destroy(flash);
        }

        private IEnumerator DashTrail()
        {
            float elapsed = 0f;
            while (elapsed < 0.28f)
            {
                elapsed += 0.035f;
                var trail = NewSprite("DashAfterimage", PixelArtHD.Player, 5950, world);
                trail.transform.position = new Vector3(playerPos.x, playerPos.y - 0.37f, 0f);
                trail.GetComponent<SpriteRenderer>().color = new Color(0.32f, 0.93f, 1f, 0.28f);
                StartCoroutine(FadeObject(trail, 0.25f));
                yield return new WaitForSecondsRealtime(0.035f);
            }
        }

        private IEnumerator FadeObject(GameObject obj, float duration)
        {
            float elapsed = 0f;
            var renderer = obj.GetComponent<SpriteRenderer>();
            Color start = renderer.color;
            while (elapsed < duration && obj != null)
            {
                elapsed += Time.unscaledDeltaTime;
                renderer.color = new Color(start.r, start.g, start.b,
                    start.a * (1f - elapsed / duration));
                yield return null;
            }
            if (obj != null)
                Destroy(obj);
        }

        private IEnumerator Shockwave(Vector2 position, float radius)
        {
            var view = NewSprite("Shockwave", PixelArtHD.Ring, 6500, world);
            view.transform.position = position;
            var renderer = view.GetComponent<SpriteRenderer>();
            float elapsed = 0f;
            while (elapsed < 0.38f && view != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / 0.38f;
                view.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, radius * 2.2f, t);
                renderer.color = new Color(1f, 0.35f, 0.22f, 1f - t);
                yield return null;
            }
            if (view != null)
                Destroy(view);
        }

        private IEnumerator GhostBurst(Vector2 position)
        {
            yield return StartCoroutine(Burst(position, Cyan, 12));
            for (int i = 0; i < 6; i++)
            {
                var orb = NewSprite("GhostThread", PixelArtHD.Spark, 270, world);
                orb.transform.position = position + UnityEngine.Random.insideUnitCircle * 0.9f;
                orb.transform.localScale = Vector3.one * 0.25f;
                orb.GetComponent<SpriteRenderer>().color = new Color(0.35f, 0.94f, 1f, 0.6f);
                StartCoroutine(FadeObject(orb, 0.5f));
            }
        }

        private IEnumerator ScreenshotAndQuit(string path)
        {
            yield return new WaitForSecondsRealtime(1.4f);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(0.6f);
            Application.Quit();
        }

        private IEnumerator SelfTestAndQuit(string resultPath)
        {
            yield return null;
            var failures = new List<string>();
            if (mode != GameMode.Playing)
                StartNewRun();
            Check(heroDown != null && heroUp != null && heroLeft != null && heroRight != null &&
                  heroPortrait != null, "Imported protagonist artwork is missing.", failures);
            foreach (var track in new[]
                     {
                         "Exploration_Forest", "Exploration_Ice", "Exploration_Mist",
                         "Boss_Castle", "Boss_VictoryPath", "Victory_Moment"
                     })
                Check(Resources.Load<AudioClip>("Music/" + track) != null,
                    "Music asset missing: " + track, failures);
            int[] expectedEnemyCounts = { 9, 10, 12 };
            for (int stageIndex = 0; stageIndex < 3; stageIndex++)
            {
                selectedMap = stageIndex;
                currentMap = stageIndex;
                ApplyStageData();
                ResetMap();
                SpawnEnemies();
                Check(enemies.Count == expectedEnemyCounts[stageIndex],
                    StageNames[stageIndex] + " enemy count mismatch: " + enemies.Count, failures);
                Check(HasPath(WorldCell(StartPosition), WorldCell(StageCrystals[stageIndex][0])),
                    StageNames[stageIndex] + " first crystal unreachable.", failures);
                Check(HasPath(WorldCell(StartPosition), WorldCell(StageCrystals[stageIndex][1])),
                    StageNames[stageIndex] + " second crystal unreachable.", failures);
                var stageBoss = enemies.FirstOrDefault(e => e.Kind == EnemyKindAction.Boss);
                Check(stageBoss != null &&
                      HasPath(WorldCell(StartPosition), WorldCell(stageBoss.Position)),
                    StageNames[stageIndex] + " boss unreachable.", failures);
                Check(HasPath(WorldCell(StartPosition), WorldCell(ExitPosition)),
                    StageNames[stageIndex] + " exit unreachable.", failures);
            }
            selectedMap = 0;
            currentMap = 0;
            ApplyStageData();
            ResetMap();
            Array.Clear(explored, 0, explored.Length);
            SpawnEnemies();
            Check(Mathf.Approximately(hp, 3f) && lives == 3, "Initial life state invalid.", failures);

            tutorialStep = TutorialStep.Move;
            playerPos = new Vector2(6f, 16.5f);
            UpdateTutorial();
            Check(tutorialStep == TutorialStep.Shoot, "Tutorial movement step did not advance.", failures);
            tutorialShootHits = 3;
            tutorialTarget.Alive = false;
            UpdateTutorial();
            Check(tutorialStep == TutorialStep.Dash, "Tutorial shooting step did not advance.", failures);
            tutorialDashed = true;
            UpdateTutorial();
            Check(tutorialStep == TutorialStep.Interact, "Tutorial dash step did not advance.", failures);
            tutorialRelicCollected = true;
            UpdateTutorial();
            Check(tutorialStep == TutorialStep.Record, "Tutorial interaction step did not advance.", failures);
            tutorialRecordCompleted = true;
            UpdateTutorial();
            Check(tutorialStep == TutorialStep.Complete, "Tutorial recording step did not advance.", failures);

            var boss = enemies.FirstOrDefault(e => e.Kind == EnemyKindAction.Boss);
            Check(boss != null, "Boss was not spawned.", failures);
            if (boss != null)
            {
                bossActive = true;
                boss.Hp = 1;
                DamageEnemy(boss, 5f);
                Check(bossDefeated && exitOpen, "Defeating boss did not open exit.", failures);
            }

            currentTrail.Add(new Vector2Int(5, 15));
            hp = 1f;
            invulnerable = 0f;
            DamagePlayer(playerPos, 1f);
            Check(mode == GameMode.Dead && lives == 2, "Player death did not consume one life.", failures);
            Respawn();
            Check(mode == GameMode.Playing && hp == 3f &&
                  inheritedTrail.Contains(new Vector2Int(5, 15)),
                "Respawn did not restore HP or inherited light.", failures);

            string result = failures.Count == 0
                ? "PASS\nCharacter artwork, six music tracks, three connected maps with " +
                  "9/10/12 enemies, tutorial chain, boss exit, death and light inheritance.\n"
                : "FAIL\n" + string.Join("\n", failures) + "\n";
            File.WriteAllText(resultPath, result);
            Debug.Log("ACTION SELF TEST " + result);
            yield return new WaitForSecondsRealtime(0.4f);
            Application.Quit();
        }

        private bool HasPath(Vector2Int start, Vector2Int target)
        {
            if (!InBounds(start) || !InBounds(target))
                return false;
            var open = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            open.Enqueue(start);
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (open.Count > 0)
            {
                var cell = open.Dequeue();
                if (cell == target)
                    return true;
                foreach (var dir in directions)
                {
                    var next = cell + dir;
                    if (!InBounds(next) || visited.Contains(next) || tiles[next.x, next.y] != TileKind.Floor)
                        continue;
                    visited.Add(next);
                    open.Enqueue(next);
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
