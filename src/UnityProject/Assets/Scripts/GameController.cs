using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WrongTurn
{
    public sealed class GameController : MonoBehaviour
    {
        private sealed class TileView
        {
            public SpriteRenderer Floor;
            public SpriteRenderer Wall;
            public SpriteRenderer Shadow;
            public SpriteRenderer Trail;
            public SpriteRenderer Inherited;
            public SpriteRenderer Danger;
        }

        private const float DesignWidth = 1280f;
        private const float DesignHeight = 720f;

        private GameSession session;
        private ProceduralAudio audio;
        private Camera cam;
        private Transform worldRoot;
        private readonly TileView[,] tileViews = new TileView[GameSession.Width, GameSession.Height];
        private readonly Dictionary<int, GameObject> enemyViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> enemyBars = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, LineRenderer> enemyLines = new Dictionary<int, LineRenderer>();
        private GameObject bodyView;
        private GameObject bodyGlow;
        private GameObject faceMarker;
        private GameObject scoutView;
        private GameObject scoutGlow;
        private GameObject doorView;
        private GameObject exitSeal;
        private readonly Dictionary<Vector2Int, GameObject> lightViews =
            new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, GameObject> crystalViews =
            new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, GameObject> energyViews =
            new Dictionary<Vector2Int, GameObject>();
        private float phase;
        private float toastUntil;
        private string toast = "";
        private GameEventKind toastKind;
        private float deathTimer;
        private bool holdInterrupted;
        private Vector2Int heldDirection;
        private float repeatTimer;
        private int lastDangerSerial;
        private int lastActionsSeen;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle tinyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle centerStyle;
        private GUIStyle monoStyle;
        private Texture2D vignette;
        private Texture2D scanlineTexture;
        private bool stylesReady;
        private Vector2Int lastBodyCell;

        private static readonly Color Ink = new Color32(7, 10, 15, 255);
        private static readonly Color Panel = new Color32(13, 18, 25, 235);
        private static readonly Color Panel2 = new Color32(20, 27, 36, 240);
        private static readonly Color Bone = new Color32(226, 219, 197, 255);
        private static readonly Color Muted = new Color32(139, 147, 145, 255);
        private static readonly Color Ember = new Color32(238, 161, 71, 255);
        private static readonly Color Cyan = new Color32(94, 226, 226, 255);
        private static readonly Color Blood = new Color32(229, 81, 77, 255);
        private static readonly Color Wine = new Color32(58, 19, 29, 255);
        private static readonly Color Acid = new Color32(177, 218, 101, 255);

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            session = new GameSession();
            lastBodyCell = session.Body;
            lastDangerSerial = session.DangerSerial;
            SetupCamera();
            SetupWorld();
            audio = new ProceduralAudio(gameObject);
        }

        private void Start()
        {
            var commandLine = Environment.GetCommandLineArgs();
            if (commandLine.Any(arg =>
                    string.Equals(arg, "-autostart", StringComparison.OrdinalIgnoreCase)))
            {
                session.StartNewRun();
                DrainSessionEvents();
            }
            RefreshWorld();

            int screenshotIndex = Array.FindIndex(commandLine, arg =>
                string.Equals(arg, "-screenshot", StringComparison.OrdinalIgnoreCase));
            if (screenshotIndex >= 0 && screenshotIndex + 1 < commandLine.Length)
                StartCoroutine(CaptureScreenshotAndQuit(commandLine[screenshotIndex + 1]));
        }

        private void Update()
        {
            phase += Time.unscaledDeltaTime;

            if (session.Status == RunStatus.Dead)
            {
                deathTimer += Time.unscaledDeltaTime;
                if (deathTimer >= 2.1f)
                {
                    session.Respawn();
                    deathTimer = 0f;
                    DrainSessionEvents();
                    RefreshWorld();
                }
            }

            if (session.Status == RunStatus.Won || session.Status == RunStatus.Lost)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    session.RestartRun();
                    deathTimer = 0f;
                    DrainSessionEvents();
                    RefreshWorld();
                }
                return;
            }

            if (session.Status == RunStatus.Title)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    session.StartNewRun();
                    DrainSessionEvents();
                    RefreshWorld();
                }
                return;
            }

            if (session.Status == RunStatus.MemoryChoice)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                    session.SelectMemoryOption(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                    session.SelectMemoryOption(1);
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                    ConfirmMemorySelection(session.MemoryChoiceSelection);
                if (Input.GetKeyDown(KeyCode.Escape))
                    session.CancelMemory();
                RefreshWorld();
                return;
            }

            if (session.Status == RunStatus.Paused)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    session.Resume();
                if (Input.GetKeyDown(KeyCode.R))
                {
                    session.RestartRun();
                    DrainSessionEvents();
                    RefreshWorld();
                }
                return;
            }

            if (session.Status != RunStatus.Playing)
                return;

            HandleDirectionInput();

            if (Input.GetKeyDown(KeyCode.Space))
                Execute(session.Attack);
            else if (Input.GetKeyDown(KeyCode.E))
                Execute(session.ToggleRecord);
            else if (Input.GetKeyDown(KeyCode.Q))
                Execute(session.ToggleScout);
            else if (Input.GetKeyDown(KeyCode.F))
                Execute(session.Interact);
            else if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Period))
                Execute(session.WaitTurn);
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                session.BeginPause();
                holdInterrupted = true;
            }
        }

        private void HandleDirectionInput()
        {
            var held = GetHeldDirection();
            if (held == Vector2Int.zero)
            {
                heldDirection = Vector2Int.zero;
                holdInterrupted = false;
                repeatTimer = 0f;
                return;
            }

            bool freshPress = heldDirection != held || heldDirection == Vector2Int.zero;
            if (freshPress)
            {
                heldDirection = held;
                repeatTimer = 0.25f;
                if (!holdInterrupted)
                    ExecuteDirection(held);
                return;
            }

            if (holdInterrupted)
                return;
            repeatTimer -= Time.unscaledDeltaTime;
            if (repeatTimer <= 0f)
            {
                repeatTimer = 0.145f;
                ExecuteDirection(held);
            }
        }

        private static Vector2Int GetHeldDirection()
        {
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
                return Vector2Int.up;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
                return Vector2Int.down;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
                return Vector2Int.left;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
                return Vector2Int.right;
            return Vector2Int.zero;
        }

        private void ExecuteDirection(Vector2Int direction)
        {
            int actionBefore = session.Actions;
            int dangerBefore = session.DangerSerial;
            var bodyBefore = session.Body;
            Execute(() => session.TryDirection(direction));
            if (session.Actions == actionBefore)
                holdInterrupted = true;
            if (session.DangerSerial != dangerBefore)
                holdInterrupted = true;
            if (session.Body != bodyBefore)
                lastBodyCell = bodyBefore;
        }

        private void Execute(Action action)
        {
            var oldStatus = session.Status;
            var oldMode = session.Mode;
            action();
            DrainSessionEvents();
            RefreshWorld();
            PlayActionFeedback(oldStatus);
            if (oldMode != session.Mode)
                holdInterrupted = true;
            if (session.Status == RunStatus.Dead)
            {
                deathTimer = 0f;
                StartCoroutine(DeathFade());
            }
        }

        private void PlayActionFeedback(RunStatus oldStatus)
        {
            if (session.LastEventKind == GameEventKind.AttackHit)
            {
                if (session.LastAttack != null)
                    StartAttackVisual(session.LastAttack);
                StartCoroutine(Shake(0.12f, 0.07f));
            }
            else if (session.LastEventKind == GameEventKind.AttackMiss && session.LastAttack != null)
            {
                StartAttackVisual(session.LastAttack);
            }
            else if (session.LastEventKind == GameEventKind.Hurt)
            {
                StartCoroutine(Shake(0.28f, 0.15f));
                StartCoroutine(FlashBody(Blood));
            }
            else if (session.LastEventKind == GameEventKind.GhostHit ||
                     session.LastEventKind == GameEventKind.Ghost)
            {
                StartCoroutine(BurstAt(bodyView.transform.position + Vector3.up * 0.55f, Cyan, 10));
            }
            else if (session.LastEventKind == GameEventKind.Boss)
            {
                StartCoroutine(Shake(0.35f, 0.1f));
            }
            else if (session.LastEventKind == GameEventKind.Victory)
            {
                StartCoroutine(Shake(0.7f, 0.08f));
                StartCoroutine(BurstAt(new Vector3(14f, 1.4f, 0f), Ember, 30));
            }
            if (oldStatus != session.Status)
                holdInterrupted = true;
            if (session.DangerSerial != lastDangerSerial)
            {
                holdInterrupted = true;
                lastDangerSerial = session.DangerSerial;
            }
            lastActionsSeen = session.Actions;
        }

        private void DrainSessionEvents()
        {
            while (session.Events.Count > 0)
            {
                var evt = session.Events.Dequeue();
                audio?.Play(evt.Kind);
                if (!string.IsNullOrWhiteSpace(evt.Text))
                {
                    toast = evt.Text;
                    toastKind = evt.Kind;
                    toastUntil = Time.unscaledTime + 3.4f;
                }
            }
        }

        private void ConfirmMemorySelection(int index)
        {
            session.ConfirmMemory(index);
            DrainSessionEvents();
            RefreshWorld();
            StartCoroutine(BurstAt(bodyView.transform.position + Vector3.up * 0.5f, Cyan, 16));
        }

        private void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cam = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = 7.35f;
            cam.transform.position = new Vector3(7.5f, 6.75f, -10f);
            cam.backgroundColor = new Color32(5, 8, 12, 255);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private void SetupWorld()
        {
            worldRoot = new GameObject("Dungeon).").transform;

            var backdrop = NewSpriteObject("Backdrop", PixelArt.White, -100, worldRoot);
            backdrop.transform.position = new Vector3(7.5f, 6.5f, 2f);
            backdrop.transform.localScale = new Vector3(34f, 25f, 1f);
            backdrop.GetComponent<SpriteRenderer>().color = new Color32(5, 7, 10, 255);

            for (int y = 0; y < GameSession.Height; y++)
            for (int x = 0; x < GameSession.Width; x++)
            {
                var cellRoot = new GameObject("Cell_" + x + "_" + y).transform;
                cellRoot.SetParent(worldRoot);
                cellRoot.position = new Vector3(x, y, 0f);

                var floor = NewSpriteObject("Floor", PixelArt.Floor, 10 - y, cellRoot);
                floor.transform.localPosition = Vector3.zero;
                var wall = NewSpriteObject("Wall", PixelArt.Wall, 600 - y * 2, cellRoot);
                wall.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                var shadow = NewSpriteObject("WallShadow", PixelArt.Shadow, 599 - y * 2, cellRoot);
                shadow.transform.localPosition = new Vector3(0.12f, -0.44f, 0f);
                shadow.transform.localScale = new Vector3(1.2f, 0.55f, 1f);
                shadow.GetComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f, 0.55f);

                var trail = NewSpriteObject("Trail", PixelArt.White, 90 - y, cellRoot);
                trail.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                trail.transform.localScale = new Vector3(0.94f, 0.94f, 1f);
                var inherited = NewSpriteObject("InheritedTrail", PixelArt.White, 89 - y, cellRoot);
                inherited.transform.localPosition = new Vector3(0f, 0f, -0.03f);
                inherited.transform.localScale = new Vector3(0.86f, 0.86f, 1f);
                var danger = NewSpriteObject("Danger", PixelArt.White, 240 - y, cellRoot);
                danger.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                danger.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

                tileViews[x, y] = new TileView
                {
                    Floor = floor.GetComponent<SpriteRenderer>(),
                    Wall = wall.GetComponent<SpriteRenderer>(),
                    Shadow = shadow.GetComponent<SpriteRenderer>(),
                    Trail = trail.GetComponent<SpriteRenderer>(),
                    Inherited = inherited.GetComponent<SpriteRenderer>(),
                    Danger = danger.GetComponent<SpriteRenderer>()
                };
                tileViews[x, y].Floor.sprite = PixelArt.Floor;
                tileViews[x, y].Wall.sprite = PixelArt.Wall;
                tileViews[x, y].Shadow.enabled = false;
                tileViews[x, y].Wall.enabled = false;
                tileViews[x, y].Trail.enabled = false;
                tileViews[x, y].Inherited.enabled = false;
                tileViews[x, y].Danger.enabled = false;
                DestroyHint(shadow, x, y);
            }

            bodyGlow = NewSpriteObject("BodyGlow", PixelArt.Glow, 530, worldRoot);
            bodyGlow.transform.localScale = new Vector3(3.1f, 3.1f, 1f);
            bodyGlow.GetComponent<SpriteRenderer>().color = new Color(1f, 0.57f, 0.2f, 0.15f);

            bodyView = NewSpriteObject("Player", PixelArt.Player, 1004, worldRoot);
            bodyView.GetComponent<SpriteRenderer>().color = Color.white;
            var bodyShadow = NewSpriteObject("PlayerShadow", PixelArt.Shadow, 1000, bodyView.transform);
            bodyShadow.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            bodyShadow.transform.localScale = new Vector3(0.9f, 0.75f, 1f);
            bodyShadow.GetComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f, 0.7f);
            faceMarker = NewSpriteObject("FaceMarker", PixelArt.Orb, 1010, bodyView.transform);
            faceMarker.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            faceMarker.GetComponent<SpriteRenderer>().color = Ember;

            scoutGlow = NewSpriteObject("ScoutGlow", PixelArt.Glow, 528, worldRoot);
            scoutGlow.transform.localScale = new Vector3(3.2f, 3.2f, 1f);
            scoutGlow.GetComponent<SpriteRenderer>().color = new Color(0.3f, 0.92f, 1f, 0.2f);
            scoutView = NewSpriteObject("Scout", PixelArt.Scout, 1006, worldRoot);

            foreach (var enemy in session.Enemies)
            {
                var visual = NewSpriteObject("Enemy_" + enemy.Id, SpriteFor(enemy.Kind),
                    enemy.Kind == EnemyKind.Boss ? 1600 : 1000, worldRoot);
                if (enemy.Kind == EnemyKind.Boss)
                    visual.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
                enemyViews[enemy.Id] = visual;

                var bar = new GameObject("EnemyBar_" + enemy.Id);
                bar.transform.SetParent(worldRoot);
                var bg = NewSpriteObject("BarBG", PixelArt.White, 2000, bar.transform);
                bg.transform.localPosition = Vector3.zero;
                bg.transform.localScale = new Vector3(0.64f, 0.07f, 1f);
                bg.GetComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f, 0.75f);
                var fill = NewSpriteObject("BarFill", PixelArt.White, 2001, bar.transform);
                fill.transform.localPosition = new Vector3(-0.01f, 0f, -0.01f);
                fill.transform.localScale = new Vector3(0.56f, 0.035f, 1f);
                fill.GetComponent<SpriteRenderer>().color = new Color(0.88f, 0.32f, 0.28f, 1f);
                enemyBars[enemy.Id] = bar;

                var lineObject = new GameObject("Aim_" + enemy.Id);
                lineObject.transform.SetParent(worldRoot);
                var line = lineObject.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.startWidth = 0.07f;
                line.endWidth = 0.02f;
                line.useWorldSpace = true;
                line.sortingOrder = 1800;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = new Color(1f, 0.25f, 0.18f, 0.7f);
                line.endColor = new Color(1f, 0.62f, 0.25f, 0.08f);
                line.enabled = false;
                enemyLines[enemy.Id] = line;
            }

            foreach (var light in session.LightNodes)
            {
                var glow = NewSpriteObject("LightGlow", PixelArt.Glow, 180, worldRoot);
                glow.transform.localScale = new Vector3(2.25f, 2.25f, 1f);
                glow.GetComponent<SpriteRenderer>().color = new Color(1f, 0.61f, 0.2f, 0.24f);
                var lamp = NewSpriteObject("Light", PixelArt.Light, 520, worldRoot);
                var root = new GameObject("LightPickup_" + light.Cell.x + "_" + light.Cell.y);
                root.transform.SetParent(worldRoot);
                lamp.transform.SetParent(root.transform);
                glow.transform.SetParent(root.transform);
                glow.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                lamp.transform.localPosition = Vector3.zero;
                lightViews[light.Cell] = root;
            }

            foreach (var memory in session.MemoryNodes)
            {
                var glow = NewSpriteObject("CrystalGlow", PixelArt.Glow, 205, worldRoot);
                var crystal = NewSpriteObject("Crystal", PixelArt.Crystal, 540, worldRoot);
                var root = new GameObject("Crystal_" + memory.Cell.x + "_" + memory.Cell.y);
                root.transform.SetParent(worldRoot);
                crystal.transform.SetParent(root.transform);
                glow.transform.SetParent(root.transform);
                glow.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                glow.transform.localScale = new Vector3(2.1f, 2.1f, 1f);
                glow.GetComponent<SpriteRenderer>().color = new Color(0.34f, 0.73f, 1f, 0.2f);
                crystal.transform.localPosition = Vector3.zero;
                crystalViews[memory.Cell] = root;
            }

            doorView = NewSpriteObject("ExitDoor", PixelArt.Door, 620, worldRoot);
            doorView.transform.position = new Vector3(14f, 0.5f, 0f);
            exitSeal = NewSpriteObject("ExitSeal", PixelArt.Seal, 650, worldRoot);
            exitSeal.transform.position = new Vector3(14f, 1.15f, 0f);
            exitSeal.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        }

        private static void DestroyHint(GameObject shadow, int x, int y)
        {
            // Keeps the map hierarchy readable while leaving the shadow component alive.
            shadow.name = "WallShadow_" + x + "_" + y;
        }

        private static GameObject NewSpriteObject(string name, Sprite sprite, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return go;
        }

        private static Sprite SpriteFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Melee: return PixelArt.Melee;
                case EnemyKind.Ranged: return PixelArt.Ranged;
                default: return PixelArt.Boss;
            }
        }

        private void RefreshWorld()
        {
            if (session == null)
                return;

            for (int y = 0; y < GameSession.Height; y++)
            for (int x = 0; x < GameSession.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                var state = session.Tiles[x, y];
                var view = tileViews[x, y];
                bool lit = session.IsCellLit(cell);
                bool explored = state.Explored;
                float brightness = lit ? 1f : explored ? 0.21f : 0.045f;
                Color floorColor = new Color(0.57f * brightness, 0.55f * brightness,
                    0.49f * brightness, 1f);
                Color wallColor = new Color(0.55f * brightness, 0.55f * brightness,
                    0.53f * brightness, 1f);

                view.Floor.enabled = state.Kind == TileKind.Floor || state.Kind == TileKind.Gap;
                view.Floor.sprite = state.Kind == TileKind.Gap
                    ? PixelArt.Gap
                    : (state.SeamKnown ? PixelArt.MovableFloor : PixelArt.Floor);
                view.Floor.color = state.Kind == TileKind.Gap
                    ? new Color(0.32f * brightness + 0.02f, 0.32f * brightness + 0.02f,
                        0.32f * brightness + 0.02f, 1f)
                    : floorColor;

                view.Wall.enabled = state.Kind == TileKind.FixedWall ||
                                    state.Kind == TileKind.MovableWall;
                view.Shadow.enabled = view.Wall.enabled;
                if (view.Wall.enabled)
                {
                    view.Wall.sprite = state.Kind == TileKind.MovableWall && state.SeamKnown
                        ? PixelArt.MovableWall
                        : PixelArt.Wall;
                    view.Wall.color = wallColor;
                }

                view.Trail.enabled = session.CurrentTrail.Contains(cell);
                if (view.Trail.enabled)
                {
                    float wave = 0.05f * Mathf.Sin(phase * 2.1f + x * 0.7f + y * 0.4f);
                    view.Trail.color = new Color(0.96f, 0.53f + wave, 0.18f, 0.26f);
                }

                view.Inherited.enabled = session.InheritedTrail.Contains(cell);
                if (view.Inherited.enabled)
                {
                    float wave = 0.04f * Mathf.Sin(phase * 1.2f + x * 0.3f);
                    view.Inherited.color = new Color(0.93f, 0.45f + wave, 0.15f, 0.2f);
                }

                bool danger = session.IsDangerCell(cell);
                view.Danger.enabled = danger;
                if (danger)
                {
                    float pulse = 0.3f + 0.18f * Mathf.Sin(phase * 9f + x + y);
                    view.Danger.color = new Color(0.98f, 0.16f, 0.13f, pulse);
                }
            }

            foreach (var enemy in session.Enemies)
            {
                var visual = enemyViews[enemy.Id];
                var bar = enemyBars[enemy.Id];
                var line = enemyLines[enemy.Id];
                visual.SetActive(enemy.Alive);
                bar.SetActive(enemy.Alive && (enemy.IsTelegraphing ||
                    session.IsCellLit(enemy.Cell) || enemy.Kind == EnemyKind.Boss && session.BossStarted));
                if (!enemy.Alive)
                {
                    line.enabled = false;
                    continue;
                }

                float bob = enemy.Kind == EnemyKind.Boss
                    ? 0.018f * Mathf.Sin(phase * 2f)
                    : 0.025f * Mathf.Sin(phase * 4.1f + enemy.Id);
                visual.transform.position = new Vector3(enemy.Cell.x, enemy.Cell.y - 0.43f + bob, 0f);
                var sr = visual.GetComponent<SpriteRenderer>();
                bool visible = session.IsCellLit(enemy.Cell) || enemy.IsTelegraphing;
                sr.color = visible
                    ? Color.white
                    : new Color(0.18f, 0.12f, 0.12f, enemy.IsTelegraphing ? 0.7f : 0.15f);
                if (enemy.IsTelegraphing)
                {
                    float flash = 0.86f + 0.14f * Mathf.Sin(phase * 12f);
                    sr.color = new Color(flash, 0.35f, 0.32f, 1f);
                }

                bar.transform.position = new Vector3(enemy.Cell.x, enemy.Cell.y + 0.66f, 0f);
                float hpRatio = enemy.MaxHp <= 0 ? 0f : enemy.Hp / (float)enemy.MaxHp;
                var fill = bar.transform.Find("BarFill");
                if (fill != null)
                {
                    fill.transform.localScale = new Vector3(0.56f * hpRatio, 0.035f, 1f);
                    fill.transform.localPosition = new Vector3(-0.28f + 0.28f * hpRatio, 0f, -0.01f);
                }

                line.enabled = enemy.IsTelegraphing && enemy.DangerCells.Count > 0;
                if (line.enabled)
                {
                    var end = enemy.DangerCells[enemy.DangerCells.Count - 1];
                    line.SetPosition(0, new Vector3(enemy.Cell.x, enemy.Cell.y + 0.28f, 0f));
                    line.SetPosition(1, new Vector3(end.x, end.y + 0.28f, 0f));
                }
            }

            bodyView.transform.position = new Vector3(session.Body.x, session.Body.y - 0.42f, 0f);
            bodyGlow.transform.position = new Vector3(session.Body.x, session.Body.y + 0.2f, 0f);
            float bodyPulse = session.Mode == ControlMode.Scout
                ? 0.19f + 0.08f * Mathf.Sin(phase * 8f)
                : 0.12f + 0.025f * Mathf.Sin(phase * 2f);
            bodyGlow.GetComponent<SpriteRenderer>().color = session.Mode == ControlMode.Scout
                ? new Color(1f, 0.43f, 0.19f, bodyPulse)
                : new Color(1f, 0.62f, 0.23f, bodyPulse);
            faceMarker.transform.localPosition = new Vector3(
                session.Facing.x * 0.31f, 0.64f + session.Facing.y * 0.42f, 0f);

            scoutView.SetActive(session.Mode == ControlMode.Scout);
            scoutGlow.SetActive(session.Mode == ControlMode.Scout);
            if (session.Mode == ControlMode.Scout)
            {
                scoutView.transform.position = new Vector3(session.Scout.x, session.Scout.y - 0.35f,
                    0f);
                scoutGlow.transform.position = new Vector3(session.Scout.x, session.Scout.y + 0.1f,
                    0f);
                float glow = 0.17f + 0.09f * Mathf.Sin(phase * 8f);
                scoutGlow.GetComponent<SpriteRenderer>().color = new Color(0.32f, 0.91f, 1f, glow);
            }

            foreach (var light in session.LightNodes)
            {
                var root = lightViews[light.Cell];
                bool visible = light.Available && session.Tiles[light.Cell.x, light.Cell.y].Explored;
                root.SetActive(visible);
                if (visible)
                {
                    root.transform.position = new Vector3(light.Cell.x,
                        light.Cell.y + 0.02f * Mathf.Sin(phase * 3f + light.Cell.x), 0f);
                }
            }

            foreach (var crystal in session.MemoryNodes)
            {
                var root = crystalViews[crystal.Cell];
                root.SetActive(!crystal.Used && session.Tiles[crystal.Cell.x, crystal.Cell.y].Explored);
                if (!root.activeSelf)
                    continue;
                root.transform.position = new Vector3(crystal.Cell.x,
                    crystal.Cell.y + 0.035f * Mathf.Sin(phase * 2.4f + crystal.Cell.x), 0f);
                var glow = root.transform.Find("CrystalGlow").GetComponent<SpriteRenderer>();
                glow.color = new Color(0.36f, 0.78f, 1f,
                    0.14f + 0.08f * Mathf.Sin(phase * 3.5f));
            }

            foreach (var cell in session.EnergyDrops.ToArray())
            {
                GameObject orb;
                if (!energyViews.TryGetValue(cell, out orb) || orb == null)
                {
                    orb = NewSpriteObject("Energy_" + cell.x + "_" + cell.y, PixelArt.Orb, 560,
                        worldRoot);
                    energyViews[cell] = orb;
                }
                orb.SetActive(true);
                orb.transform.position = new Vector3(cell.x, cell.y + 0.18f +
                    0.07f * Mathf.Sin(phase * 5f + cell.x), 0f);
            }
            foreach (var pair in energyViews)
            {
                if (!session.EnergyDrops.Contains(pair.Key))
                    pair.Value.SetActive(false);
            }

            doorView.SetActive(session.BossDefeated);
            exitSeal.SetActive(!session.BossDefeated);
            if (exitSeal.activeSelf)
            {
                exitSeal.transform.localScale = new Vector3(1.3f + 0.1f * Mathf.Sin(phase * 4f),
                    1.3f + 0.1f * Mathf.Sin(phase * 4f), 1f);
            }
        }

        private IEnumerator FlashBody(Color flashColor)
        {
            var renderer = bodyView.GetComponent<SpriteRenderer>();
            renderer.color = flashColor;
            yield return new WaitForSecondsRealtime(0.12f);
            renderer.color = Color.white;
        }

        private IEnumerator DeathFade()
        {
            var renderer = bodyView.GetComponent<SpriteRenderer>();
            float time = 0f;
            while (time < 0.5f)
            {
                time += Time.unscaledDeltaTime;
                renderer.color = Color.Lerp(Color.white, new Color(0.1f, 0.04f, 0.04f, 0.1f),
                    time / 0.5f);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.4f);
            renderer.color = Color.white;
        }

        private IEnumerator CaptureScreenshotAndQuit(string path)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(0.6f);
            Application.Quit();
        }

        private IEnumerator Shake(float duration, float strength)
        {
            var origin = new Vector3(7.5f, 6.75f, -10f);
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float fade = 1f - time / duration;
                cam.transform.position = origin + (Vector3)(UnityEngine.Random.insideUnitCircle *
                    strength * fade);
                yield return null;
            }
            cam.transform.position = origin;
        }

        private IEnumerator BurstAt(Vector3 position, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var spark = NewSpriteObject("Spark", PixelArt.Orb, 2200, worldRoot);
                spark.transform.position = position;
                spark.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.18f, 0.4f);
                spark.GetComponent<SpriteRenderer>().color = color;
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
                float speed = UnityEngine.Random.Range(0.8f, 2.2f);
                StartCoroutine(AnimateSpark(spark, direction * speed, color));
                yield return new WaitForSecondsRealtime(0.015f);
            }
        }

        private IEnumerator AnimateSpark(GameObject spark, Vector2 velocity, Color color)
        {
            float time = 0f;
            float duration = 0.42f;
            var renderer = spark.GetComponent<SpriteRenderer>();
            while (time < duration && spark != null)
            {
                time += Time.unscaledDeltaTime;
                spark.transform.position += (Vector3)(velocity * Time.unscaledDeltaTime);
                renderer.color = new Color(color.r, color.g, color.b,
                    color.a * Mathf.Pow(1f - time / duration, 1.5f));
                spark.transform.localScale *= 0.985f;
                yield return null;
            }
            if (spark != null)
                Destroy(spark);
        }

        private void StartAttackVisual(BodyAttackTrace trace)
        {
            StartCoroutine(AnimateAttack(trace));
        }

        private IEnumerator AnimateAttack(BodyAttackTrace trace)
        {
            var ring = NewSpriteObject("ThrownRing", PixelArt.Ring, 2300, worldRoot);
            var a = new Vector3(session.Body.x, session.Body.y + 0.18f, 0f);
            var b = new Vector3(trace.To.x, trace.To.y + 0.18f, 0f);
            float time = 0f;
            while (time < 0.18f)
            {
                time += Time.unscaledDeltaTime;
                ring.transform.position = Vector3.Lerp(a, b, Mathf.Clamp01(time / 0.18f));
                ring.transform.Rotate(0f, 0f, 620f * Time.unscaledDeltaTime);
                yield return null;
            }
            if (trace.Hit)
            {
                yield return StartCoroutine(BurstAt(b, Ember, 9));
            }
            time = 0f;
            while (time < 0.16f)
            {
                time += Time.unscaledDeltaTime;
                ring.transform.position = Vector3.Lerp(b, a, Mathf.Clamp01(time / 0.16f));
                yield return null;
            }
            Destroy(ring);
        }

        private void OnGUI()
        {
            EnsureStyles();
            vignette = vignette ? vignette : PixelArt.VignetteTexture;
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
            float ox = (Screen.width - DesignWidth * scale) * 0.5f;
            float oy = (Screen.height - DesignHeight * scale) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));

            if (vignette != null)
                GUI.DrawTexture(new Rect(0, 0, DesignWidth, DesignHeight), vignette);

            if (session.Status == RunStatus.Title)
            {
                DrawTitle();
                return;
            }

            DrawHud();
            DrawTutorialCard();
            DrawWorldLabels();
            DrawToast();

            if (session.Status == RunStatus.Paused)
                DrawPause();
            else if (session.Status == RunStatus.MemoryChoice)
                DrawMemoryChoice();
            else if (session.Status == RunStatus.Dead)
                DrawDeath();
            else if (session.Status == RunStatus.Won)
                DrawEnd(true);
            else if (session.Status == RunStatus.Lost)
                DrawEnd(false);
        }

        private void EnsureStyles()
        {
            if (stylesReady && titleStyle != null)
                return;
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 24);
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 62,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Bone },
                alignment = TextAnchor.MiddleLeft
            };
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ember },
                alignment = TextAnchor.MiddleLeft
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Bone },
                alignment = TextAnchor.MiddleLeft
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 17,
                normal = { textColor = Bone },
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 14,
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
            centerStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            monoStyle = new GUIStyle(tinyStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ember }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    background = SolidTexture(new Color32(31, 41, 51, 255)),
                    textColor = Bone
                },
                hover =
                {
                    background = SolidTexture(new Color32(54, 65, 69, 255)),
                    textColor = Color.white
                },
                active =
                {
                    background = SolidTexture(new Color32(109, 73, 38, 255)),
                    textColor = Color.white
                },
                border = new RectOffset(1, 1, 1, 1)
            };
            stylesReady = true;
        }

        private static Texture2D SolidTexture(Color color)
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

        private static void PanelBox(Rect rect, Color fill, Color border)
        {
            Fill(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), border);
            Fill(rect, fill);
            Fill(new Rect(rect.x, rect.y, 3, rect.height), border);
        }

        private static void Bar(Rect rect, float value, float max, Color color)
        {
            Fill(rect, new Color(0.02f, 0.03f, 0.045f, 0.9f));
            float ratio = max <= 0f ? 0f : Mathf.Clamp01(value / max);
            Fill(new Rect(rect.x + 2, rect.y + 2, Mathf.Max(0f, (rect.width - 4) * ratio),
                rect.height - 4), color);
        }

        private void DrawTitle()
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0.015f, 0.022f, 0.032f, 0.88f));
            Fill(new Rect(92, 86, 2, 520), Ember);
            Fill(new Rect(108, 88, 1, 520), new Color(0.70f, 0.47f, 0.22f, 0.45f));
            GUI.Label(new Rect(142, 112, 660, 94), "走错也算数", titleStyle);
            GUI.Label(new Rect(148, 203, 560, 34), "WRONG TURN / RIGHT PATH", subtitleStyle);
            GUI.Label(new Rect(148, 250, 505, 70),
                "每次走错，都在下一世变成你的兵器与灯光。", headingStyle);
            GUI.Label(new Rect(148, 329, 465, 84),
                "在黑暗中记录路线，把上一世的光带进这一局。\n回合制格子探索 · 阴影探路 · 环戒战斗 · 三次生命",
                bodyStyle);

            PanelBox(new Rect(147, 440, 440, 70), new Color(0.06f, 0.09f, 0.12f, 0.92f),
                new Color(0.47f, 0.39f, 0.25f, 0.8f));
            GUI.Label(new Rect(164, 452, 120, 20), "全新独立版本", monoStyle);
            GUI.Label(new Rect(164, 474, 390, 26), "原创程序化像素美术 · 无需外部素材", bodyStyle);

            if (GUI.Button(new Rect(148, 545, 265, 58), "进入迷宫  ↩", buttonStyle))
            {
                session.StartNewRun();
                DrainSessionEvents();
                RefreshWorld();
            }
            GUI.Label(new Rect(438, 559, 390, 32),
                "Enter 开始  ·  Esc 暂停  ·  鼠标可操作界面", smallStyle);

            DrawTitleDiagram(new Rect(758, 93, 414, 519));
            GUI.Label(new Rect(1072, 672, 170, 24), "FRESH BUILD  /  v1.0", monoStyle);
        }

        private void DrawTitleDiagram(Rect area)
        {
            PanelBox(area, new Color(0.028f, 0.044f, 0.061f, 0.8f),
                new Color(0.18f, 0.21f, 0.21f, 0.9f));
            for (int i = 0; i < 9; i++)
            {
                float y = area.y + 62 + i * 45;
                Fill(new Rect(area.x + 30, y, area.width - 60, 1),
                    new Color(0.21f, 0.29f, 0.31f, 0.32f));
            }

            var route = new[]
            {
                new Vector2(120, 420), new Vector2(120, 350), new Vector2(210, 350),
                new Vector2(210, 250), new Vector2(322, 250), new Vector2(322, 150)
            };
            for (int i = 0; i < route.Length - 1; i++)
            {
                var a = route[i];
                var b = route[i + 1];
                if (Mathf.Abs(a.x - b.x) < 1f)
                    Fill(new Rect(area.x + a.x - 2, area.y + Mathf.Min(a.y, b.y), 4,
                        Mathf.Abs(a.y - b.y)), new Color(0.92f, 0.55f, 0.22f, 0.75f));
                else
                    Fill(new Rect(area.x + Mathf.Min(a.x, b.x), area.y + a.y - 2,
                        Mathf.Abs(a.x - b.x), 4), new Color(0.92f, 0.55f, 0.22f, 0.75f));
            }
            for (int i = 0; i < route.Length; i++)
            {
                Fill(new Rect(area.x + route[i].x - 6, area.y + route[i].y - 6, 12, 12),
                    i == route.Length - 1 ? Cyan : Ember);
            }
            Fill(new Rect(area.x + 301, area.y + 128, 42, 42), new Color(0.08f, 0.12f, 0.13f, 1f));
            Fill(new Rect(area.x + 307, area.y + 134, 30, 30), new Color(0.27f, 0.74f, 0.7f, 0.45f));
            GUI.Label(new Rect(area.x + 32, area.y + 28, 340, 34), "一条错路，两种用途", headingStyle);
            GUI.Label(new Rect(area.x + 32, area.y + 476, 340, 22),
                "冷色伤害残影   /   暖色下一世光路", smallStyle);
        }

        private void DrawHud()
        {
            var topLeft = new Rect(24, 22, 300, 185);
            PanelBox(topLeft, Panel, new Color(0.31f, 0.29f, 0.24f, 0.8f));
            GUI.Label(new Rect(39, 32, 250, 28), session.ModeLabel(), headingStyle);
            GUI.Label(new Rect(40, 61, 90, 20), "身体", tinyStyle);
            Bar(new Rect(40, 81, 162, 13), session.BodyHp, GameSession.BodyMaxHp, Blood);
            GUI.Label(new Rect(210, 76, 94, 20), session.BodyHp + " / 3", monoStyle);

            GUI.Label(new Rect(40, 102, 90, 20), "剩余生命", tinyStyle);
            for (int i = 0; i < GameSession.MaxLives; i++)
            {
                bool active = i < session.RemainingLives;
                Fill(new Rect(120 + i * 24, 105, 14, 14), active ? Ember : new Color(0.2f, 0.18f, 0.16f));
                Fill(new Rect(122 + i * 24, 107, 4, 4), active ? Color.white : new Color(0.3f, 0.3f, 0.3f));
            }

            GUI.Label(new Rect(40, 124, 70, 20), "影能", tinyStyle);
            Bar(new Rect(91, 127, 202, 10), session.Shadow, GameSession.MaxShadow, Cyan);
            GUI.Label(new Rect(261, 122, 42, 18), session.Shadow.ToString(), monoStyle);
            GUI.Label(new Rect(40, 145, 70, 20), "光", tinyStyle);
            Bar(new Rect(91, 148, 202, 10), session.Light, GameSession.MaxLight, Ember);
            GUI.Label(new Rect(261, 143, 42, 18), session.Light.ToString(), monoStyle);

            GUI.Label(new Rect(40, 170, 44, 22), "记忆", tinyStyle);
            if (session.Memories.Count == 0)
                GUI.Label(new Rect(85, 168, 180, 22), "尚未选择", smallStyle);
            int index = 0;
            foreach (var memoryId in session.Memories)
            {
                var def = FindMemory(memoryId);
                Fill(new Rect(86 + index * 91, 170, 84, 22), new Color(def.Accent.r,
                    def.Accent.g, def.Accent.b, 0.17f));
                GUI.Label(new Rect(89 + index * 91, 170, 80, 22), def.Short, tinyStyle);
                index++;
            }

            var topRight = new Rect(956, 22, 300, 181);
            PanelBox(topRight, Panel, new Color(0.24f, 0.32f, 0.31f, 0.8f));
            GUI.Label(new Rect(972, 32, 250, 28), "本局状态", headingStyle);
            GUI.Label(new Rect(972, 68, 82, 20), "行动", tinyStyle);
            GUI.Label(new Rect(1060, 62, 160, 26), session.Actions.ToString(), monoStyle);
            GUI.Label(new Rect(972, 94, 82, 20), "本世光路", tinyStyle);
            GUI.Label(new Rect(1060, 88, 160, 26),
                session.CurrentTrail.Count + " 格", monoStyle);
            GUI.Label(new Rect(972, 120, 82, 20), "前世光路", tinyStyle);
            GUI.Label(new Rect(1060, 114, 160, 26),
                session.InheritedTrail.Count + " 格", monoStyle);
            GUI.Label(new Rect(972, 146, 82, 20), "残影", tinyStyle);
            GUI.Label(new Rect(1060, 140, 170, 26),
                session.BattleGhost == null ? "未布置" :
                "伤害 " + session.BattleGhost.Damage + " / " + session.BattleGhost.Life + " 回合",
                monoStyle);
            string objective = session.BossDefeated
                ? "出口已开：身体走进去"
                : session.BossStarted
                    ? "击败守门者，别被冲锋撞上"
                    : "穿过迷宫，找到关底区域";
            GUI.Label(new Rect(970, 171, 270, 22), "目标  " + objective, smallStyle);

            var modeRect = new Rect(492, 76, 296, 34);
            PanelBox(modeRect, new Color(0.03f, 0.06f, 0.08f, 0.84f),
                session.Mode == ControlMode.Scout ? new Color(0.26f, 0.72f, 0.76f) :
                session.Mode == ControlMode.Record ? new Color(0.72f, 0.47f, 0.19f) :
                new Color(0.32f, 0.37f, 0.38f));
            GUI.Label(new Rect(504, 80, 270, 26), session.ModeInstruction(), centerStyle);

            if (session.IsBossActive)
            {
                var boss = session.Boss;
                var rect = new Rect(430, 121, 420, 52);
                PanelBox(rect, new Color(0.10f, 0.025f, 0.035f, 0.94f),
                    new Color(0.7f, 0.22f, 0.18f, 0.9f));
                GUI.Label(new Rect(447, 126, 180, 20), "沉墙守门者", monoStyle);
                GUI.Label(new Rect(710, 126, 120, 20),
                    boss == null ? "DOWN" : boss.Hp + " / " + boss.MaxHp,
                    new GUIStyle(monoStyle) { alignment = TextAnchor.MiddleRight });
                Bar(new Rect(447, 149, 386, 11), boss == null ? 0f : boss.Hp, 8f, Blood);
            }

            var bottom = new Rect(24, 665, 1232, 38);
            PanelBox(bottom, new Color(0.025f, 0.035f, 0.045f, 0.92f),
                new Color(0.18f, 0.22f, 0.22f, 0.9f));
            GUI.Label(new Rect(39, 672, 1176, 24),
                "WASD / 方向键  移动     空格  投环     E  路线记录     Q  探路影子     F  交互     Z  等待     Esc  暂停",
                smallStyle);

            if (session.BossStarted)
            {
                string warning = session.IsDangerCell(session.Body) ? "危险格已锁住你" : "观察红色预告，下一回合才落招";
                Fill(new Rect(538, 34, 204, 26), new Color(0.45f, 0.07f, 0.06f, 0.85f));
                GUI.Label(new Rect(542, 38, 196, 18), warning, new GUIStyle(tinyStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.72f, 0.62f) }
                });
            }
        }

        private void DrawWorldLabels()
        {
            foreach (var enemy in session.Enemies)
            {
                if (!enemy.Alive || !enemy.IsTelegraphing)
                    continue;
                var pos = WorldToGui(new Vector3(enemy.Cell.x, enemy.Cell.y + 0.8f, 0f));
                string label = enemy.MoveKind == EnemyMoveKind.BossCharge ? "冲锋 →" :
                    enemy.MoveKind == EnemyMoveKind.BossSlam ? "重击 !" : "锁定";
                Fill(new Rect(pos.x - 27, pos.y - 9, 54, 18), new Color(0.25f, 0.04f, 0.04f, 0.82f));
                GUI.Label(new Rect(pos.x - 28, pos.y - 10, 56, 20), label,
                    new GUIStyle(tinyStyle) { alignment = TextAnchor.MiddleCenter });
            }

            foreach (var memory in session.MemoryNodes)
            {
                if (memory.Used)
                    continue;
                var target = memory.Cell;
                if (session.Body == target + Vector2Int.left || session.Body == target + Vector2Int.right ||
                    session.Body == target + Vector2Int.up || session.Body == target + Vector2Int.down ||
                    Manhattan(session.Body, target) <= 2)
                {
                    var pos = WorldToGui(new Vector3(target.x, target.y + 0.85f, 0f));
                    GUI.Label(new Rect(pos.x - 58, pos.y - 28, 116, 20), "记忆冰晶 · F", monoStyle);
                }
            }

            if (session.CarryingWall)
            {
                var pos = WorldToGui(bodyView.transform.position + Vector3.up * 1.25f);
                Fill(new Rect(pos.x - 27, pos.y - 8, 54, 18), new Color(0.18f, 0.13f, 0.08f, 0.85f));
                GUI.Label(new Rect(pos.x - 28, pos.y - 9, 56, 20), "携墙", centerStyle);
            }

            if (session.InheritedTrail.Count > 0 && session.Actions < 5)
            {
                GUI.Label(new Rect(348, 638, 584, 20),
                    "脚下仍亮着上一世走过的路。它只照明，不会替你战斗。",
                    new GUIStyle(centerStyle) { fontSize = 14 });
            }
        }

        private void DrawTutorialCard()
        {
            if (session.Status != RunStatus.Playing || session.Actions >= 8 ||
                session.Memories.Count > 0)
                return;

            string line1;
            string line2;
            if (session.Actions == 0)
            {
                line1 = "先走一步：WASD / 方向键";
                line2 = "方向撞墙也会改变投环朝向，不耗回合。";
            }
            else if (session.Mode == ControlMode.Body && session.BattleGhost == null)
            {
                line1 = "试试路线残影：E 开始记录";
                line2 = "移动 2 格后再按 E，把追兵引进伤害路线。";
            }
            else
            {
                line1 = session.BattleGhost == null ? "观察红色预告，再决定一步" : "残影已布置";
                line2 = "Q 放出影子看路；Z 只在你明确需要时等待。";
            }

            var rect = new Rect(24, 222, 300, 62);
            PanelBox(rect, new Color(0.03f, 0.05f, 0.062f, 0.88f),
                new Color(0.3f, 0.43f, 0.43f, 0.75f));
            GUI.Label(new Rect(38, 228, 272, 23), line1, new GUIStyle(monoStyle)
            {
                fontSize = 15,
                normal = { textColor = Cyan }
            });
            GUI.Label(new Rect(38, 252, 272, 28), line2, smallStyle);
        }

        private Vector2 WorldToGui(Vector3 world)
        {
            var viewport = cam.WorldToViewportPoint(world);
            return new Vector2(viewport.x * DesignWidth, (1f - viewport.y) * DesignHeight);
        }

        private void DrawToast()
        {
            if (Time.unscaledTime > toastUntil || string.IsNullOrWhiteSpace(toast))
                return;
            float remaining = toastUntil - Time.unscaledTime;
            float alpha = Mathf.Clamp01(remaining / 0.4f);
            Color color = toastKind == GameEventKind.Hurt || toastKind == GameEventKind.Death
                ? Blood
                : toastKind == GameEventKind.Boss
                    ? new Color(1f, 0.45f, 0.3f)
                    : toastKind == GameEventKind.Invalid
                        ? new Color(0.8f, 0.66f, 0.46f)
                        : Bone;
            var style = new GUIStyle(centerStyle)
            {
                fontSize = 16,
                normal = { textColor = new Color(color.r, color.g, color.b, alpha) }
            };
            var rect = new Rect(316, 622, 648, 34);
            PanelBox(rect, new Color(0.02f, 0.035f, 0.045f, 0.78f * alpha),
                new Color(color.r, color.g, color.b, 0.35f * alpha));
            GUI.Label(new Rect(rect.x + 12, rect.y + 5, rect.width - 24, rect.height - 8), toast, style);
        }

        private void DrawPause()
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0f, 0f, 0f, 0.76f));
            var panel = new Rect(294, 95, 692, 528);
            PanelBox(panel, new Color(0.035f, 0.052f, 0.069f, 0.98f),
                new Color(0.47f, 0.39f, 0.25f, 0.9f));
            GUI.Label(new Rect(330, 122, 500, 42), "暂停 / 路线仍在等你", headingStyle);
            GUI.Label(new Rect(330, 166, 600, 24),
                "敌人只在你提交有效行动后推进。", smallStyle);
            Fill(new Rect(330, 200, 620, 1), new Color(0.4f, 0.35f, 0.25f, 0.65f));

            GUI.Label(new Rect(334, 224, 270, 270),
                "WASD / 方向键\n移动一格，按住可连走\n\n空格\n向面朝方向投环\n\nE\n开始 / 结束路线记录\n\nQ\n放出 / 收回探路影子",
                bodyStyle);
            GUI.Label(new Rect(645, 224, 300, 270),
                "F\n拆墙、铺缺口、触碰冰晶\n\nZ\n原地等待一回合\n\nEsc\n暂停\n\n黑暗中的红色轮廓\n始终代表下一回合的威胁",
                bodyStyle);

            if (GUI.Button(new Rect(335, 513, 188, 52), "继续探索", buttonStyle))
                session.Resume();
            if (GUI.Button(new Rect(545, 513, 188, 52), "重新开局", buttonStyle))
            {
                session.RestartRun();
                DrainSessionEvents();
                RefreshWorld();
            }
            if (GUI.Button(new Rect(755, 513, 188, 52), "退出游戏", buttonStyle))
                Application.Quit();
        }

        private void DrawMemoryChoice()
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0.01f, 0.018f, 0.03f, 0.86f));
            GUI.Label(new Rect(0, 68, DesignWidth, 42), "她留下的，是两种解释", new GUIStyle(headingStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 31
            });
            GUI.Label(new Rect(0, 111, DesignWidth, 24),
                session.OpenMemory == null ? "" : session.OpenMemory.Title + "  ·  选择后本局保留",
                new GUIStyle(centerStyle) { normal = { textColor = Muted } });

            int memoryIndex = session.OpenMemory == null ? 0 : session.MemoryChoiceIndex(session.OpenMemory);
            var definitions = GameSession.MemoryDefinitions[memoryIndex];
            DrawMemoryCard(new Rect(165, 175, 450, 390), definitions[0], 0,
                session.MemoryChoiceSelection == 0, memoryIndex);
            DrawMemoryCard(new Rect(665, 175, 450, 390), definitions[1], 1,
                session.MemoryChoiceSelection == 1, memoryIndex);

            GUI.Label(new Rect(0, 587, DesignWidth, 24),
                "A / D 切换    空格或 Enter 确认    Esc 暂不选择",
                new GUIStyle(centerStyle) { normal = { textColor = Muted } });
        }

        private void DrawMemoryCard(Rect rect, MemoryDefinition def, int option, bool selected,
            int memoryIndex)
        {
            Color border = selected ? def.Accent : new Color(0.25f, 0.3f, 0.32f);
            PanelBox(rect, new Color(0.035f, 0.055f, 0.075f, 0.98f), border);
            if (selected)
                Fill(new Rect(rect.x, rect.y, rect.width, 5), def.Accent);
            GUI.Label(new Rect(rect.x + 24, rect.y + 31, 60, 26),
                option == 0 ? "A" : "B", new GUIStyle(monoStyle) { fontSize = 24 });
            GUI.Label(new Rect(rect.x + 75, rect.y + 28, rect.width - 100, 34), def.Title, headingStyle);

            Rect art = new Rect(rect.x + 25, rect.y + 78, rect.width - 50, 155);
            Fill(art, new Color(0.01f, 0.025f, 0.04f, 0.95f));
            for (int i = 0; i < 7; i++)
                Fill(new Rect(art.x, art.y + i * 24, art.width, 1), new Color(0.13f, 0.18f, 0.2f, 0.5f));

            float cx = art.x + art.width * 0.5f;
            float cy = art.y + art.height * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                float radius = 26f + i * 17f;
                Fill(new Rect(cx - radius, cy - 1, radius * 2f, 2f),
                    new Color(def.Accent.r, def.Accent.g, def.Accent.b, 0.28f - i * 0.06f));
                Fill(new Rect(cx - 1, cy - radius, 2f, radius * 2f),
                    new Color(def.Accent.r, def.Accent.g, def.Accent.b, 0.28f - i * 0.06f));
            }
            Fill(new Rect(cx - 20, cy - 20, 40, 40), new Color(def.Accent.r * 0.5f,
                def.Accent.g * 0.5f, def.Accent.b * 0.5f, 0.95f));
            Fill(new Rect(cx - 8, cy - 8, 16, 16), def.Accent);

            GUI.Label(new Rect(rect.x + 24, rect.y + 251, rect.width - 48, 32),
                def.Short, new GUIStyle(headingStyle) { fontSize = 20 });
            GUI.Label(new Rect(rect.x + 24, rect.y + 288, rect.width - 48, 66), def.Description,
                bodyStyle);
            string key = option == 0 ? "左键 / A" : "右键 / D";
            GUI.Label(new Rect(rect.x + 24, rect.y + 356, rect.width - 48, 20),
                selected ? "按 Enter 确认" : key + " 切换", monoStyle);

            if (GUI.Button(rect, GUIContent.none,
                    new GUIStyle { normal = { background = null }, hover = { background = null } }))
                ConfirmMemorySelection(option);
        }

        private void DrawDeath()
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0.02f, 0f, 0.01f, 0.66f));
            GUI.Label(new Rect(0, 289, DesignWidth, 64), "灯灭了", new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 58
            });
            GUI.Label(new Rect(0, 365, DesignWidth, 42),
                session.CurrentTrail.Count > 0
                    ? "本世走过的光正在沉进下一世。"
                    : "本世没有留下新光路，旧路会继续照你。",
                centerStyle);
            GUI.Label(new Rect(0, 416, DesignWidth, 26),
                "敌人与地形将复位，记忆冰晶不会重复领取。", smallStyle);
            float ratio = Mathf.Clamp01(deathTimer / 2.1f);
            Bar(new Rect(490, 486, 300, 8), ratio, 1f, Ember);
        }

        private void DrawEnd(bool won)
        {
            Fill(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0f, 0.01f, 0.015f, 0.82f));
            var panel = new Rect(270, 82, 740, 545);
            PanelBox(panel, new Color(0.03f, 0.045f, 0.06f, 0.98f),
                won ? new Color(0.82f, 0.55f, 0.21f) : new Color(0.55f, 0.15f, 0.16f));
            GUI.Label(new Rect(306, 112, 670, 60), won ? "出口在身后合上" : "这里没有新的黎明",
                titleStyle);
            GUI.Label(new Rect(310, 179, 650, 28),
                won ? "你从错误的路线里带回了答案。" : "灯命耗尽，但下一次可以从不同的决心开始。",
                headingStyle);

            GUI.Label(new Rect(318, 244, 270, 160),
                "使用生命\n" + (GameSession.MaxLives - session.RemainingLives) + " / 3\n\n实际行动\n" +
                session.Actions + "\n\n本世光路\n" + session.CurrentTrail.Count + " 格",
                bodyStyle);
            GUI.Label(new Rect(652, 244, 300, 160),
                "记忆选择\n" + (session.Memories.Count == 0
                    ? "无"
                    : string.Join(" / ", session.Memories.Select(id => FindMemory(id).Short))) +
                "\n\n死亡次数\n" + session.DeathCount + "\n\n路线原则\n走错也是资源",
                bodyStyle);

            DrawRouteSummary(new Rect(318, 397, 640, 80));
            if (GUI.Button(new Rect(410, 520, 210, 54), "再走一局  R", buttonStyle))
            {
                session.RestartRun();
                DrainSessionEvents();
                RefreshWorld();
            }
            if (GUI.Button(new Rect(640, 520, 210, 54), "退出游戏", buttonStyle))
                Application.Quit();
        }

        private void DrawRouteSummary(Rect rect)
        {
            Fill(rect, new Color(0.015f, 0.027f, 0.037f, 0.9f));
            var cells = session.InheritedTrail.Union(session.CurrentTrail).Take(26).ToList();
            if (cells.Count == 0)
            {
                GUI.Label(rect, "本局没有留下光路", centerStyle);
                return;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                float x = rect.x + 16 + (i % 13) * 47f;
                float y = rect.y + 21 + (i / 13) * 34f;
                Fill(new Rect(x, y, 34, 9), i < session.CurrentTrail.Count ?
                    new Color(0.96f, 0.55f, 0.2f, 0.9f) : new Color(0.44f, 0.6f, 0.58f, 0.65f));
            }
        }

        private static MemoryDefinition FindMemory(string id)
        {
            return GameSession.MemoryDefinitions.SelectMany(x => x).First(x => x.Id == id);
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }

    public static class SessionPresentation
    {
        public static string ModeLabel(this GameSession session)
        {
            switch (session.Mode)
            {
                case ControlMode.Record: return "路线记录 · 正在落笔";
                case ControlMode.Scout: return "发光影子 · 远看一格";
                default: return "身体状态 · 环戒在手";
            }
        }

        public static string ModeInstruction(this GameSession session)
        {
            switch (session.Mode)
            {
                case ControlMode.Record:
                    if (session.BattleGhost != null)
                        return "旧残影 " + session.BattleGhost.Life + " 回合 · E 封存";
                    return "移动写入路线 · 每步 1 影能";
                case ControlMode.Scout:
                    return "只控制影子 · 身体仍会受伤";
                default:
                    if (session.BattleGhost != null)
                        return "冷色残影 " + session.BattleGhost.Life + " 回合";
                    return "观察不会推进世界";
            }
        }
    }
}
