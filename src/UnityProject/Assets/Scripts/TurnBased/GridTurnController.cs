using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace WrongTurn.TurnBased
{
    public sealed class GridTurnController : MonoBehaviour
    {
        [SerializeField] private GridTurnHud hud;
        [SerializeField] private PlayerRig playerRig;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private SpriteRenderer aimReticle;
        [SerializeField] private LineRenderer attackFlash;
        [SerializeField] private Transform projectileRoot;

        private static readonly string[] StageNames = { "森林", "冰原", "沙漠" };
        private static readonly string[] StageMusic =
        {
            "Exploration_Forest", "Exploration_Ice", "Exploration_Mist"
        };
        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        private readonly List<MapVariant> mapVariants = new List<MapVariant>();
        private readonly List<GridTurnEvent> eventBuffer = new List<GridTurnEvent>();
        private readonly Dictionary<int, EnemySpawn> enemyMarkers =
            new Dictionary<int, EnemySpawn>();
        private readonly List<SpriteRenderer> projectileViews = new List<SpriteRenderer>();

        private Camera cam;
        private MapVariant activeVariant;
        private GridTurnSession session;
        private GridTurnSession previewSession;
        private int selectedMap;
        private bool inputLocked;
        private bool titleMode = true;
        private float actionTime;
        private float bossFrameTimer;
        private int bossFrameIndex;
        private bool revealAllForCapture;
        private Vector2Int aimDirection = Vector2Int.right;
        private Sprite meleeSprite;
        private Sprite rangedSprite;
        private Sprite enemyShotSprite;
        private Sprite[] bossPhaseOne;
        private Sprite[] bossPhaseTwo;
        private Sprite[] heroDirections;
        private ProceduralAudio audio;
        private MusicRig musicRig;
        private AudioSource musicA;
        private AudioSource musicB;
        private string activeMusic = "";

        public void SelectStageForTitle(int index)
        {
            if (!titleMode)
                return;
            selectedMap = Mathf.Clamp(index, 0, 2);
            BuildPreview();
            hud.ShowTitle(selectedMap, StageNames);
        }

        public void StartSelectedStage()
        {
            StartStage(selectedMap);
        }

        public void ResumeGame()
        {
            if (session == null || session.Phase != GridTurnPhase.Paused)
                return;
            session.TogglePause();
            hud.ShowGame();
            hud.Refresh(session, StageNames[selectedMap], selectedMap);
        }

        public void RestartStage()
        {
            StartStage(selectedMap);
        }

        public void ReturnToTitle()
        {
            titleMode = true;
            inputLocked = false;
            BuildPreview();
            PlayMusic("Exploration_Mist");
            hud.ShowTitle(selectedMap, StageNames);
        }

        public void SelectMemory(int index)
        {
            if (session == null || session.Phase != GridTurnPhase.MemoryChoice)
                return;
            session.SelectMemory(index);
            DrainEvents();
            hud.HideMemory();
            hud.Refresh(session, StageNames[selectedMap], selectedMap);
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            cam = Camera.main;
            if (cam == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cam = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 7.6f;
            cam.transform.position = new Vector3(14f, 10f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            hud = hud != null ? hud : FindFirstObjectByType<GridTurnHud>(FindObjectsInactive.Include);
            playerRig = playerRig != null
                ? playerRig
                : FindFirstObjectByType<PlayerRig>(FindObjectsInactive.Include);
            if (playerRig != null)
            {
                if (playerRig.SoulBody == null)
                {
                    var soulBody = playerRig.transform.Find("SoulRig/SoulBody");
                    if (soulBody != null)
                        playerRig.SoulBody = soulBody.gameObject;
                }
                if (playerRig.SoulGlow == null)
                {
                    var soulGlow = playerRig.transform.Find("SoulRig/SoulGlow");
                    if (soulGlow != null)
                        playerRig.SoulGlow = soulGlow.gameObject;
                }
            }
            mapVariants.AddRange(FindObjectsByType<MapVariant>(FindObjectsInactive.Include,
                FindObjectsSortMode.None));
            mapVariants.Sort((a, b) => a.MapId.CompareTo(b.MapId));

            LoadArt();
            SetupAudio();
            EnsurePresentationObjects();
            if (hud != null)
                hud.Bind(this);
            BuildPreview();
            hud.ShowTitle(selectedMap, StageNames);
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            revealAllForCapture = args.Any(value =>
                string.Equals(value, "-revealall", StringComparison.OrdinalIgnoreCase));
            var stageIndex = Array.FindIndex(args,
                value => string.Equals(value, "-stage", StringComparison.OrdinalIgnoreCase));
            if (stageIndex >= 0 && stageIndex + 1 < args.Length &&
                int.TryParse(args[stageIndex + 1], out var stage))
                selectedMap = Mathf.Clamp(stage - 1, 0, 2);

            if (args.Any(value => string.Equals(value, "-autostart",
                    StringComparison.OrdinalIgnoreCase)))
                StartStage(selectedMap);
            if (revealAllForCapture)
            {
                if (session == null)
                    StartStage(selectedMap);
                session.DebugRevealAll();
                cam.orthographicSize = 10.4f;
                FocusMapCamera();
            }

            var screenshotIndex = Array.FindIndex(args,
                value => string.Equals(value, "-screenshot",
                    StringComparison.OrdinalIgnoreCase));
            if (screenshotIndex >= 0 && screenshotIndex + 1 < args.Length)
                StartCoroutine(CaptureAndQuit(args[screenshotIndex + 1]));

            var testIndex = Array.FindIndex(args,
                value => string.Equals(value, "-gridtest",
                    StringComparison.OrdinalIgnoreCase));
            if (testIndex >= 0 && testIndex + 1 < args.Length)
                StartCoroutine(RuntimeSelfTestAndQuit(args[testIndex + 1]));
        }

        private void Update()
        {
            actionTime += Time.unscaledDeltaTime;
            UpdateBossAnimation();
            if (titleMode)
            {
                HandleTitleInput();
                return;
            }
            if (session == null)
                return;

            if (!inputLocked)
                HandleGameInput();
            UpdateAimPresentation();
            SyncViews();
            UpdateCameraFollow();
            hud.Refresh(session, StageNames[selectedMap], selectedMap);
        }

        private void HandleTitleInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                SelectStageForTitle(0);
            if (Input.GetKeyDown(KeyCode.Alpha2))
                SelectStageForTitle(1);
            if (Input.GetKeyDown(KeyCode.Alpha3))
                SelectStageForTitle(2);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                StartSelectedStage();
        }

        private void HandleGameInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (session.Phase == GridTurnPhase.PlayerTurn)
                {
                    session.TogglePause();
                    hud.ShowPause(true);
                    DrainEvents();
                }
                else if (session.Phase == GridTurnPhase.Paused)
                {
                    ResumeGame();
                }
                return;
            }

            if (session.Phase == GridTurnPhase.Paused)
            {
                if (Input.GetKeyDown(KeyCode.R))
                    RestartStage();
                if (Input.GetKeyDown(KeyCode.M))
                    ReturnToTitle();
                return;
            }

            if (session.Phase == GridTurnPhase.MemoryChoice)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                    SelectMemory(0);
                if (Input.GetKeyDown(KeyCode.Alpha2))
                    SelectMemory(1);
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    session.CancelMemory();
                    DrainEvents();
                    hud.HideMemory();
                }
                return;
            }

            if (session.Phase == GridTurnPhase.Dead)
            {
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space))
                    Respawn();
                if (Input.GetKeyDown(KeyCode.M))
                    ReturnToTitle();
                return;
            }

            if (session.Phase == GridTurnPhase.Won ||
                session.Phase == GridTurnPhase.Lost)
            {
                if (Input.GetKeyDown(KeyCode.R))
                    RestartStage();
                if (Input.GetKeyDown(KeyCode.M))
                    ReturnToTitle();
                return;
            }

            if (session.Phase != GridTurnPhase.PlayerTurn)
                return;

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                ExecuteTurn(() => session.TryPlayerMove(Vector2Int.up));
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                ExecuteTurn(() => session.TryPlayerMove(Vector2Int.down));
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                ExecuteTurn(() => session.TryPlayerMove(Vector2Int.left));
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                ExecuteTurn(() => session.TryPlayerMove(Vector2Int.right));
            else if (Input.GetMouseButtonDown(0))
                ExecuteTurn(() =>
                {
                    var acted = session.TryAttack(aimDirection);
                    if (acted)
                        audio?.PlayClip("slash");
                    return acted;
                });
            else if (Input.GetKeyDown(KeyCode.F))
                ExecuteTurn(session.TryInteract);
            else if (Input.GetKeyDown(KeyCode.Q))
                ExecuteFreeAction(session.ToggleSoul);
            else if (Input.GetKeyDown(KeyCode.E))
                ExecuteFreeAction(session.ToggleRecording);
            else if (Input.GetKeyDown(KeyCode.Space))
                ExecuteTurn(session.WaitTurn);
        }

        private void ExecuteTurn(Func<bool> action)
        {
            if (action == null || !action())
            {
                DrainEvents();
                SyncViews();
                return;
            }
            StartCoroutine(PresentAction());
        }

        private void ExecuteFreeAction(Func<bool> action)
        {
            if (action == null || !action())
            {
                DrainEvents();
                SyncViews();
                return;
            }
            DrainEvents();
            SyncViews();
        }

        private IEnumerator PresentAction()
        {
            inputLocked = true;
            DrainEvents();
            SyncViews();
            yield return new WaitForSecondsRealtime(0.12f);
            inputLocked = false;
            SyncViews();
            HandlePostActionPhase();
        }

        private void Respawn()
        {
            session.Respawn();
            DrainEvents();
            hud.ShowGame();
            SyncViews();
        }

        private void HandlePostActionPhase()
        {
            if (session.Phase == GridTurnPhase.MemoryChoice)
            {
                hud.ShowMemory(session);
                return;
            }
            if (session.Phase == GridTurnPhase.Won)
            {
                hud.ShowEnd(true);
                return;
            }
            if (session.Phase == GridTurnPhase.Lost)
            {
                hud.ShowEnd(false);
                return;
            }
            if (session.Phase == GridTurnPhase.Dead)
                hud.ShowToast("身体倒下。按 R 在新一轮重塑。", new Color(1f, 0.35f, 0.28f));
        }

        private void StartStage(int mapIndex)
        {
            selectedMap = Mathf.Clamp(mapIndex, 0, 2);
            titleMode = false;
            inputLocked = false;
            ActivateVariant(selectedMap);
            var map = BuildMapData(activeVariant);
            session = new GridTurnSession(map);
            session.StartNewRun();
            hud.ShowGame();
            DrainEvents();
            LayoutEnemyMarkers();
            SyncViews();
            FocusPlayerCamera();
            PlayMusic(StageMusic[selectedMap]);
        }

        private void BuildPreview()
        {
            titleMode = true;
            inputLocked = false;
            PlayMusic("Exploration_Mist");
            ActivateVariant(selectedMap);
            var map = BuildMapData(activeVariant);
            previewSession = new GridTurnSession(map);
            previewSession.StartNewRun();
            session = null;
            ClearPreviewEvents();
            LayoutEnemyMarkers();
            activeVariant.GridView?.Render(previewSession);
            if (playerRig != null)
            {
                playerRig.gameObject.SetActive(true);
                playerRig.transform.position = CellCenter(previewSession.PlayerCell);
            }
            if (aimLine != null) aimLine.enabled = false;
            if (aimReticle != null) aimReticle.enabled = false;
            SetEnemiesVisibleForPreview();
            FocusMapCamera();
        }

        private void ClearPreviewEvents()
        {
            if (previewSession == null)
                return;
            eventBuffer.Clear();
            previewSession.DrainEvents(eventBuffer);
            eventBuffer.Clear();
        }

        private GridMapData BuildMapData(MapVariant variant)
        {
            var width = variant != null ? Mathf.Max(4, variant.Width) : 28;
            var height = variant != null ? Mathf.Max(4, variant.Height) : 20;
            var map = new GridMapData(width, height);
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                map.InitialTiles[x, y] = GridTileKind.Wall;

            if (variant != null && variant.Layout != null)
            {
                foreach (var position in variant.Layout.cellBounds.allPositionsWithin)
                {
                    if (position.x < 0 || position.x >= width || position.y < 0 ||
                        position.y >= height)
                        continue;
                    var tile = variant.Layout.GetTile(position);
                    if (tile == null)
                        continue;
                    if (variant.MovableWallTile != null && tile == variant.MovableWallTile)
                        map.InitialTiles[position.x, position.y] = GridTileKind.MovableWall;
                    else if (variant.GapTile != null && tile == variant.GapTile)
                        map.InitialTiles[position.x, position.y] = GridTileKind.Gap;
                    else if (variant.TrapTile != null && tile == variant.TrapTile)
                        map.InitialTiles[position.x, position.y] = GridTileKind.DamageTrap;
                    else if (variant.WallTile != null && tile == variant.WallTile)
                        map.InitialTiles[position.x, position.y] = GridTileKind.Wall;
                    else
                        map.InitialTiles[position.x, position.y] = GridTileKind.Floor;
                }
            }

            map.PlayerSpawn = playerRig != null
                ? variant != null ? variant.PlayerSpawnCell
                    : WorldCell(playerRig.transform.position)
                : new Vector2Int(4, 16);
            map.Exit = variant != null && variant.ExitSpawn != null
                ? variant.ExitSpawn.Cell
                : new Vector2Int(width - 3, 3);

            if (variant == null)
                return map;

            var bossMarker = variant.GetComponentsInChildren<EnemySpawn>(true)
                .FirstOrDefault(marker => marker.Kind == EnemyKindAction.Boss);
            if (bossMarker != null)
            {
                var bossCell = WorldCell(bossMarker.transform.position);
                map.BossTrigger = new RectInt(
                    Mathf.Clamp(bossCell.x - 5, 0, width - 1),
                    Mathf.Clamp(bossCell.y - 4, 0, height - 1),
                    Mathf.Min(11, width),
                    Mathf.Min(9, height));
            }

            var markers = variant.GetComponentsInChildren<EnemySpawn>(true);
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                var kind = marker.Kind == EnemyKindAction.Boss
                    ? GridActorKind.Boss
                    : marker.Kind == EnemyKindAction.Ranged
                        ? GridActorKind.Ranged
                        : GridActorKind.Melee;
                map.Enemies.Add(new GridEnemySpec
                {
                    Id = i + 1,
                    Kind = kind,
                    Cell = WorldCell(marker.transform.position),
                    Hp = marker.Hp,
                    MarkerIndex = i
                });
            }

            foreach (var crystal in variant.GetComponentsInChildren<CrystalSpawn>(true))
                map.Crystals.Add(WorldCell(crystal.transform.position));
            foreach (var light in variant.GetComponentsInChildren<LightSpawn>(true))
                map.Lights.Add(WorldCell(light.transform.position));
            return map;
        }

        private void LayoutEnemyMarkers()
        {
            enemyMarkers.Clear();
            if (activeVariant == null)
                return;
            var markers = activeVariant.GetComponentsInChildren<EnemySpawn>(true);
            for (var i = 0; i < markers.Length; i++)
                enemyMarkers[i] = markers[i];
        }

        private void SyncViews()
        {
            var current = session ?? previewSession;
            if (current == null)
                return;
            if (activeVariant != null && activeVariant.GridView != null)
                activeVariant.GridView.Render(current);

            if (playerRig != null)
            {
                playerRig.transform.position = CellCenter(current.PlayerCell);
                if (playerRig.Body != null)
                {
                    var renderer = playerRig.Body.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        renderer.sprite = HeroSpriteForDirection(aimDirection, current);
                        renderer.color = Color.white;
                    }
                }

                var soulVisible = current.SoulActive;
                if (playerRig.SoulBody != null)
                    playerRig.SoulBody.SetActive(soulVisible);
                if (playerRig.SoulGlow != null)
                    playerRig.SoulGlow.SetActive(soulVisible);

                if (playerRig.SoulBody != null && soulVisible)
                {
                    playerRig.SoulBody.transform.position =
                        CellCenter(current.SoulCell) + new Vector3(0f, 0.18f, 0f);
                    var soulRenderer = playerRig.SoulBody.GetComponent<SpriteRenderer>();
                    if (soulRenderer != null)
                    {
                        soulRenderer.sprite = HeroSpriteForDirection(aimDirection, current);
                        soulRenderer.color = new Color(0.45f, 0.82f, 1f, 0.58f);
                    }
                }
                if (playerRig.SoulGlow != null && soulVisible)
                    playerRig.SoulGlow.transform.position = CellCenter(current.SoulCell);
            }

            if (activeVariant != null && activeVariant.ExitSpawn != null &&
                activeVariant.ExitSpawn.Renderer != null)
            {
                activeVariant.ExitSpawn.Renderer.enabled = current.IsExplored(current.ExitCell);
                activeVariant.ExitSpawn.Renderer.color = current.ExitOpen
                    ? new Color(1f, 0.72f, 0.28f, 1f)
                    : new Color(0.34f, 0.42f, 0.44f, 0.72f);
            }

            foreach (var enemy in current.Enemies)
            {
                if (!enemyMarkers.TryGetValue(enemy.MarkerIndex, out var marker) ||
                    marker == null)
                    continue;
                marker.transform.position = CellCenter(enemy.Cell);
                var shown = enemy.Alive && current.IsEnemyDetected(enemy.Cell);
                if (marker.Body != null)
                {
                    marker.Body.enabled = shown;
                    marker.Body.sprite = EnemySprite(enemy);
                    marker.Body.color = Color.white;
                }
                if (marker.Shadow != null)
                    marker.Shadow.enabled = shown;
                if (marker.HealthBar != null)
                {
                    var ratio = enemy.MaxHp <= 0 ? 0f : enemy.Hp / (float)enemy.MaxHp;
                    var color = enemy.Kind == GridActorKind.Boss
                        ? enemy.IsBossSecondPhase
                            ? new Color(1f, 0.22f, 0.12f)
                            : new Color(1f, 0.55f, 0.18f)
                        : enemy.Kind == GridActorKind.Ranged
                            ? new Color(0.24f, 0.82f, 1f)
                            : new Color(1f, 0.42f, 0.28f);
                    marker.HealthBar.SetHealth(ratio, shown, color);
                }
            }

            SyncSceneProps(current);
            SyncProjectiles(current);
        }

        private void SetEnemiesVisibleForPreview()
        {
            foreach (var marker in enemyMarkers.Values)
            {
                if (marker.Body != null) marker.Body.enabled = true;
                if (marker.Shadow != null) marker.Shadow.enabled = true;
            }
        }

        private void SyncSceneProps(GridTurnSession current)
        {
            if (activeVariant == null)
                return;
            foreach (var crystal in activeVariant.GetComponentsInChildren<CrystalSpawn>(true))
            {
                var cell = WorldCell(crystal.transform.position);
                var visible = current.IsVisible(cell) && current.IsCrystalAvailable(cell);
                foreach (var renderer in crystal.GetComponentsInChildren<SpriteRenderer>(true))
                    renderer.enabled = visible;
            }
            foreach (var light in activeVariant.GetComponentsInChildren<LightSpawn>(true))
            {
                var cell = WorldCell(light.transform.position);
                var visible = current.IsVisible(cell) && current.IsLightAvailable(cell);
                foreach (var renderer in light.GetComponentsInChildren<SpriteRenderer>(true))
                    renderer.enabled = visible;
            }
        }

        private void SyncProjectiles(GridTurnSession current)
        {
            if (projectileRoot == null)
                return;
            var views = projectileRoot.GetComponentsInChildren<SpriteRenderer>(true);
            projectileViews.Clear();
            projectileViews.AddRange(views);
            for (var i = 0; i < projectileViews.Count; i++)
                projectileViews[i].gameObject.SetActive(i < current.Projectiles.Count);
            for (var i = 0; i < current.Projectiles.Count && i < projectileViews.Count; i++)
            {
                var projectile = current.Projectiles[i];
                var view = projectileViews[i];
                view.transform.position = CellCenter(projectile.Cell);
                view.sprite = enemyShotSprite;
                view.color = new Color(1f, 0.34f, 0.62f, 1f);
                var pulse = 0.86f + Mathf.Sin(actionTime * 10f + projectile.Id) * 0.14f;
                view.transform.localScale = Vector3.one * pulse;
            }
        }

        private void UpdateAimPresentation()
        {
            if (session == null || session.Phase != GridTurnPhase.PlayerTurn ||
                session.SoulActive)
            {
                if (aimLine != null) aimLine.enabled = false;
                if (aimReticle != null) aimReticle.enabled = false;
                return;
            }

            aimDirection = ReadAimDirection();
            var origin = (Vector2)CellCenter(session.PlayerCell) + Vector2.up * 0.2f;
            var end = origin + (Vector2)aimDirection * 2.85f;
            if (aimLine != null)
            {
                aimLine.enabled = true;
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, origin);
                aimLine.SetPosition(1, end);
                aimLine.startColor = new Color(1f, 0.78f, 0.28f, 0.78f);
                aimLine.endColor = new Color(0.35f, 0.92f, 1f, 0.15f);
            }
            if (aimReticle != null)
            {
                aimReticle.enabled = true;
                aimReticle.transform.position = new Vector3(end.x, end.y, 0f);
            }
        }

        private Vector2Int ReadAimDirection()
        {
            var screen = Input.mousePosition;
            screen.z = Mathf.Abs(cam.transform.position.z);
            var world = (Vector2)cam.ScreenToWorldPoint(screen);
            var delta = world - (Vector2)CellCenter(session.PlayerCell);
            if (delta.sqrMagnitude < 0.04f)
                return aimDirection;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var sector = Mathf.RoundToInt(angle / 45f);
            sector = (sector % 8 + 8) % 8;
            switch (sector)
            {
                case 0: return Vector2Int.right;
                case 1: return new Vector2Int(1, 1);
                case 2: return Vector2Int.up;
                case 3: return new Vector2Int(-1, 1);
                case 4: return Vector2Int.left;
                case 5: return new Vector2Int(-1, -1);
                case 6: return Vector2Int.down;
                default: return new Vector2Int(1, -1);
            }
        }

        private void ActivateVariant(int index)
        {
            activeVariant = mapVariants.FirstOrDefault(variant => variant.MapId == index) ??
                            mapVariants.FirstOrDefault();
            foreach (var variant in mapVariants)
                variant.gameObject.SetActive(variant == activeVariant);
            if (activeVariant != null && activeVariant.Layout != null)
            {
                var renderer = activeVariant.Layout.GetComponent<TilemapRenderer>();
                if (renderer != null)
                    renderer.enabled = false;
                cam.backgroundColor = activeVariant.Background;
            }
        }

        private void FocusMapCamera()
        {
            if (activeVariant == null)
                return;
            cam.transform.position = new Vector3((activeVariant.Width - 1) * 0.5f,
                (activeVariant.Height - 1) * 0.5f, -10f);
        }

        private void FocusPlayerCamera()
        {
            if (session == null)
                return;
            cam.transform.position = ClampCamera(CellCenter(session.PlayerCell));
        }

        private void UpdateCameraFollow()
        {
            if (session == null || titleMode || cam == null)
                return;
            if (revealAllForCapture)
                return;
            var target = ClampCamera(CellCenter(session.PlayerCell));
            target.z = -10f;
            cam.transform.position = Vector3.Lerp(cam.transform.position, target,
                1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f));
        }

        private Vector3 ClampCamera(Vector2 target)
        {
            return new Vector3(target.x, target.y, -10f);
        }

        private void UpdateBossAnimation()
        {
            if (session == null || bossPhaseOne == null || bossPhaseOne.Length == 0)
                return;
            bossFrameTimer += Time.unscaledDeltaTime;
            if (bossFrameTimer < 0.18f)
                return;
            bossFrameTimer = 0f;
            bossFrameIndex++;
        }

        private Sprite EnemySprite(GridEnemyState enemy)
        {
            switch (enemy.Kind)
            {
                case GridActorKind.Melee:
                    return meleeSprite;
                case GridActorKind.Ranged:
                    return rangedSprite;
                case GridActorKind.Boss:
                    var frames = enemy.IsBossSecondPhase && bossPhaseTwo != null &&
                                 bossPhaseTwo.Length > 0
                        ? bossPhaseTwo
                        : bossPhaseOne;
                    return frames != null && frames.Length > 0
                        ? frames[bossFrameIndex % frames.Length]
                        : null;
                default:
                    return null;
            }
        }

        private Sprite HeroSpriteForDirection(Vector2Int direction,
            GridTurnSession current)
        {
            if (heroDirections == null || heroDirections.Length < 4)
                return null;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                return direction.x < 0 ? heroDirections[2] : heroDirections[3];
            return direction.y < 0 ? heroDirections[0] : heroDirections[1];
        }

        private void LoadArt()
        {
            heroDirections = new[]
            {
                Resources.Load<Sprite>("Art/Character_Down"),
                Resources.Load<Sprite>("Art/Character_Up"),
                Resources.Load<Sprite>("Art/Character_Left"),
                Resources.Load<Sprite>("Art/Character_Right")
            };
            meleeSprite = Resources.Load<Sprite>("Art/TurnBased/Melee");
            rangedSprite = Resources.Load<Sprite>("Art/TurnBased/Ranged");
            enemyShotSprite = PixelArtHD.EnemyShot;
            bossPhaseOne = Resources.LoadAll<Sprite>("Art/TurnBased/BossPhaseOne")
                .OrderBy(sprite => sprite.name).ToArray();
            bossPhaseTwo = Resources.LoadAll<Sprite>("Art/TurnBased/BossPhaseTwo")
                .OrderBy(sprite => sprite.name).ToArray();
        }

        private void SetupAudio()
        {
            audio = new ProceduralAudio(gameObject);
            audio.SetAmbienceVolume(0.025f);
            musicRig = FindFirstObjectByType<MusicRig>(FindObjectsInactive.Include);
            if (musicRig != null && musicRig.TrackA != null && musicRig.TrackB != null)
            {
                musicA = musicRig.TrackA;
                musicB = musicRig.TrackB;
            }
            else
            {
                musicA = gameObject.AddComponent<AudioSource>();
                musicB = gameObject.AddComponent<AudioSource>();
            }
            foreach (var source in new[] { musicA, musicB })
            {
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
            }
            PlayMusic("Exploration_Mist");
        }

        private void PlayMusic(string resourceName, bool loop = true)
        {
            if (musicA == null || musicB == null || activeMusic == resourceName)
                return;
            var clip = Resources.Load<AudioClip>("Music/" + resourceName);
            if (clip == null)
                return;
            var from = musicA.isPlaying ? musicA : musicB;
            var to = from == musicA ? musicB : musicA;
            from.Stop();
            to.Stop();
            to.clip = clip;
            to.loop = loop;
            to.volume = 0.32f;
            to.Play();
            activeMusic = resourceName;
        }

        private void EnsurePresentationObjects()
        {
            if (aimLine == null)
            {
                var go = new GameObject("AimLine");
                aimLine = go.AddComponent<LineRenderer>();
                aimLine.material = new Material(Shader.Find("Sprites/Default"));
                aimLine.startWidth = 0.04f;
                aimLine.endWidth = 0.10f;
                aimLine.sortingOrder = 7000;
            }
            if (aimReticle == null)
            {
                var go = new GameObject("AimReticle");
                aimReticle = go.AddComponent<SpriteRenderer>();
                aimReticle.sprite = PixelArtHD.Target;
                aimReticle.sortingOrder = 7010;
                aimReticle.transform.localScale = Vector3.one * 0.62f;
            }
            if (attackFlash == null)
            {
                var flashObject = new GameObject("AttackFlash");
                attackFlash = flashObject.AddComponent<LineRenderer>();
                attackFlash.material = new Material(Shader.Find("Sprites/Default"));
                attackFlash.useWorldSpace = true;
                attackFlash.startWidth = 0.12f;
                attackFlash.endWidth = 0.48f;
                attackFlash.sortingOrder = 7050;
                attackFlash.enabled = false;
            }
            if (projectileRoot == null)
            {
                var root = new GameObject("Projectiles");
                projectileRoot = root.transform;
            }
            while (projectileRoot.childCount < 32)
            {
                var bullet = new GameObject("Projectile_" + projectileRoot.childCount);
                bullet.transform.SetParent(projectileRoot, false);
                var renderer = bullet.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 6900;
                bullet.SetActive(false);
            }
        }

        private void DrainEvents()
        {
            var current = session ?? previewSession;
            if (current == null)
                return;
            eventBuffer.Clear();
            current.DrainEvents(eventBuffer);
            var message = "";
            var messageColor = new Color(0.72f, 0.88f, 0.90f);
            foreach (var evt in eventBuffer)
            {
                if (!string.IsNullOrWhiteSpace(evt.Text))
                    message = evt.Text;
                switch (evt.Kind)
                {
                    case GridTurnEventKind.PlayerHurt:
                    case GridTurnEventKind.Defeat:
                        messageColor = new Color(1f, 0.36f, 0.28f);
                        audio?.PlayClip(evt.Kind == GridTurnEventKind.Defeat
                            ? "death"
                            : "hurt");
                        break;
                    case GridTurnEventKind.Pickup:
                        messageColor = new Color(1f, 0.75f, 0.25f);
                        audio?.PlayClip("pickup");
                        break;
                    case GridTurnEventKind.BossPhaseChanged:
                        messageColor = new Color(1f, 0.75f, 0.25f);
                        audio?.PlayClip("boss");
                        PlayMusic("Boss_VictoryPath");
                        break;
                    case GridTurnEventKind.BossDefeated:
                        messageColor = new Color(1f, 0.75f, 0.25f);
                        audio?.PlayClip("victory");
                        break;
                    case GridTurnEventKind.SoulMoved:
                    case GridTurnEventKind.SoulToggled:
                        messageColor = new Color(0.36f, 0.85f, 1f);
                        audio?.PlayClip("scout");
                        break;
                    case GridTurnEventKind.EnemyTelegraph:
                        messageColor = new Color(1f, 0.48f, 0.32f);
                        audio?.PlayClip("aim");
                        break;
                    case GridTurnEventKind.BossAwake:
                        messageColor = new Color(1f, 0.48f, 0.32f);
                        audio?.PlayClip("boss");
                        PlayMusic("Boss_Castle");
                        break;
                    case GridTurnEventKind.PlayerMoved:
                    case GridTurnEventKind.EnemyMoved:
                        audio?.PlayClip("step");
                        break;
                    case GridTurnEventKind.PlayerAttack:
                        StartCoroutine(FlashAttack(evt.From, evt.To, false));
                        break;
                    case GridTurnEventKind.EnemyAttack:
                        StartCoroutine(FlashAttack(evt.From, evt.To, true));
                        audio?.PlayClip("slash");
                        break;
                    case GridTurnEventKind.GhostSpawned:
                    case GridTurnEventKind.GhostHit:
                        audio?.PlayClip("ghost");
                        break;
                    case GridTurnEventKind.EnemyDied:
                        audio?.PlayClip("hit");
                        break;
                    case GridTurnEventKind.Victory:
                        audio?.PlayClip("victory");
                        PlayMusic("Victory_Moment", false);
                        break;
                }
            }
            if (!string.IsNullOrEmpty(message))
                hud.ShowToast(message, messageColor);
            HandlePostActionPhase();
        }

        private IEnumerator FlashAttack(Vector2Int from, Vector2Int to, bool enemy)
        {
            if (attackFlash == null)
                yield break;
            attackFlash.enabled = true;
            attackFlash.positionCount = 2;
            attackFlash.SetPosition(0, CellCenter(from));
            attackFlash.SetPosition(1, CellCenter(to));
            attackFlash.startColor = enemy
                ? new Color(1f, 0.22f, 0.18f, 0.92f)
                : new Color(0.25f, 0.95f, 1f, 0.96f);
            attackFlash.endColor = enemy
                ? new Color(1f, 0.55f, 0.28f, 0.08f)
                : new Color(1f, 0.82f, 0.25f, 0.08f);
            attackFlash.startWidth = enemy ? 0.22f : 0.16f;
            attackFlash.endWidth = enemy ? 0.62f : 0.52f;
            yield return new WaitForSecondsRealtime(0.16f);
            attackFlash.enabled = false;
        }

        private IEnumerator CaptureAndQuit(string path)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(0.45f);
            Application.Quit();
        }

        private IEnumerator RuntimeSelfTestAndQuit(string path)
        {
            yield return null;
            var lines = new List<string>();
            var failures = new List<string>();
            if (titleMode)
                StartStage(selectedMap);
            yield return null;

            if (session == null)
                failures.Add("Session was not created.");
            if (hud == null)
                failures.Add("UGUI HUD missing.");
            if (mapVariants.Count != 3)
                failures.Add("Expected 3 map variants.");
            if (playerRig == null || playerRig.SoulBody == null ||
                playerRig.SoulGlow == null)
                failures.Add("Player or soul rig missing.");
            if (bossPhaseOne == null || bossPhaseOne.Length != 22)
                failures.Add("Boss phase one frames missing.");
            if (bossPhaseTwo == null || bossPhaseTwo.Length != 3)
                failures.Add("Boss phase two frames missing.");

            var expectedCounts = new[] { 9, 10, 12 };
            for (var i = 0; i < mapVariants.Count; i++)
            {
                var map = BuildMapData(mapVariants[i]);
                if (map.Enemies.Count != expectedCounts[i])
                    failures.Add("Map " + i + " enemy count " + map.Enemies.Count +
                                 " != " + expectedCounts[i] + ".");
                if (map.Crystals.Count != 2)
                    failures.Add("Map " + i + " crystal count mismatch.");
                if (map.Lights.Count != 5)
                    failures.Add("Map " + i + " light count mismatch.");
                foreach (var crystal in map.Crystals)
                {
                    if (!HasTopologyPath(map, map.PlayerSpawn, crystal))
                        failures.Add("Map " + i + " crystal is unreachable.");
                }
                var bossSpec = map.Enemies.FirstOrDefault(enemy =>
                    enemy.Kind == GridActorKind.Boss);
                if (bossSpec != null && !HasTopologyPath(map, map.PlayerSpawn,
                        bossSpec.Cell))
                    failures.Add("Map " + i + " boss is unreachable.");
                if (!HasTopologyPath(map, map.PlayerSpawn, map.Exit))
                    failures.Add("Map " + i + " exit is unreachable.");
                var variant = mapVariants[i];
                if (variant.GridView == null ||
                    variant.GridView.Cells.Count != variant.Width * variant.Height)
                    failures.Add("Map " + i + " GridView mismatch.");
            }

            lines.Add(failures.Count == 0 ? "PASS" : "FAIL");
            lines.AddRange(failures);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllLines(path, lines);
            Debug.Log("GRID TURN RUNTIME TEST " + string.Join(" | ", lines));
            yield return new WaitForSecondsRealtime(0.35f);
            Application.Quit();
        }

        private static bool HasTopologyPath(GridMapData map, Vector2Int start,
            Vector2Int target)
        {
            if (!map.InBounds(start) || !map.InBounds(target))
                return false;
            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell == target)
                    return true;
                foreach (var direction in CardinalDirections)
                {
                    var next = cell + direction;
                    if (!map.InBounds(next) || visited.Contains(next) ||
                        map.InitialTiles[next.x, next.y] == GridTileKind.Wall)
                        continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        private Vector3 CellCenter(Vector2Int cell)
        {
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        }

        private Vector2Int WorldCell(Vector3 position)
        {
            return new Vector2Int(Mathf.FloorToInt(position.x),
                Mathf.FloorToInt(position.y));
        }
    }
}
