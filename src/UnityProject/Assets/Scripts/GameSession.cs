using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WrongTurn
{
    public enum TileKind
    {
        Floor,
        FixedWall,
        MovableWall,
        Gap
    }

    public enum ControlMode
    {
        Body,
        Record,
        Scout
    }

    public enum RunStatus
    {
        Title,
        Playing,
        Paused,
        MemoryChoice,
        Dead,
        Won,
        Lost
    }

    public enum EnemyKind
    {
        Melee,
        Ranged,
        Boss
    }

    public enum EnemyPhase
    {
        Idle,
        Telegraph,
        Recover
    }

    public enum EnemyMoveKind
    {
        None,
        RangedShot,
        BossCharge,
        BossSlam
    }

    public enum GameEventKind
    {
        Move,
        Wall,
        Record,
        Ghost,
        GhostHit,
        Scout,
        AttackHit,
        AttackMiss,
        Pickup,
        Hurt,
        Death,
        Respawn,
        Memory,
        Boss,
        Victory,
        ExitLocked,
        Invalid
    }

    public sealed class GameEvent
    {
        public GameEventKind Kind;
        public string Text;

        public GameEvent(GameEventKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }

    public sealed class TileState
    {
        public TileKind Kind;
        public bool Explored;
        public bool SeamKnown;

        public bool Walkable => Kind == TileKind.Floor;
        public bool BlocksSight => Kind == TileKind.FixedWall || Kind == TileKind.MovableWall;
    }

    public sealed class EnemyState
    {
        public int Id;
        public EnemyKind Kind;
        public Vector2Int Spawn;
        public Vector2Int Cell;
        public int Hp;
        public int MaxHp;
        public bool Alive = true;
        public EnemyPhase Phase = EnemyPhase.Idle;
        public EnemyMoveKind MoveKind = EnemyMoveKind.None;
        public Vector2Int LockedCell;
        public Vector2Int LockedDir;
        public int Cooldown;
        public int RecoverTurns;
        public int TelegraphSerial;
        public readonly List<Vector2Int> DangerCells = new List<Vector2Int>();

        public bool IsTelegraphing => Alive && Phase == EnemyPhase.Telegraph;
    }

    public sealed class BattleGhost
    {
        public readonly List<Vector2Int> Path = new List<Vector2Int>();
        public readonly HashSet<Vector2Int> DamageCells = new HashSet<Vector2Int>();
        public readonly HashSet<int> HitEnemies = new HashSet<int>();
        public int Life;
        public int Damage;
    }

    public sealed class LightNode
    {
        public Vector2Int Cell;
        public bool Available = true;
    }

    public sealed class MemoryNode
    {
        public Vector2Int Cell;
        public string Title;
        public bool Used;
    }

    public sealed class MemoryDefinition
    {
        public string Id;
        public string Title;
        public string Short;
        public string Description;
        public Color Accent;
    }

    public sealed class BodyAttackTrace
    {
        public Vector2Int From;
        public Vector2Int To;
        public bool Hit;
    }

    public sealed class GameSession
    {
        public const int Width = 16;
        public const int Height = 14;
        public const int BodyMaxHp = 3;
        public const int MaxLives = 3;
        public const int MaxShadow = 12;
        public const int MaxLight = 10;

        public readonly TileState[,] Tiles = new TileState[Width, Height];
        public readonly List<EnemyState> Enemies = new List<EnemyState>();
        public readonly List<LightNode> LightNodes = new List<LightNode>();
        public readonly List<MemoryNode> MemoryNodes = new List<MemoryNode>();
        public readonly HashSet<Vector2Int> CurrentTrail = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> InheritedTrail = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> PreviousInheritedTrail = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> RecordedPath = new HashSet<Vector2Int>();
        public readonly List<Vector2Int> RecordedOrder = new List<Vector2Int>();
        public readonly HashSet<Vector2Int> EnergyDrops = new HashSet<Vector2Int>();
        public readonly HashSet<string> Memories = new HashSet<string>();
        public readonly Queue<GameEvent> Events = new Queue<GameEvent>();

        public Vector2Int Body;
        public Vector2Int Scout;
        public Vector2Int Facing = Vector2Int.right;
        public Vector2Int Spawn = new Vector2Int(4, 10);

        public ControlMode Mode = ControlMode.Body;
        public RunStatus Status = RunStatus.Title;
        public int BodyHp = BodyMaxHp;
        public int Lives = MaxLives;
        public int Shadow = MaxShadow;
        public int Light = 5;
        public int Actions;
        public int TurnSerial;
        public int DeathCount;
        public bool CarryingWall;
        public bool BossStarted;
        public bool BossDefeated;
        public bool ExitOpen;
        public bool Recording;
        public bool RecordingSpawnedThisTurn;
        public bool HitResponseUsed;
        public bool BossRoomVisited;
        public int MemoryChoiceSelection;
        public int DangerSerial;
        public int LastTelegraphSerial;
        public GameEventKind LastEventKind;
        public BodyAttackTrace LastAttack;

        public BattleGhost BattleGhost;

        private MemoryNode openMemory;
        private bool damageThisPhase;

        public MemoryNode OpenMemory => openMemory;
        public bool IsBossActive => BossStarted && !BossDefeated;
        public EnemyState Boss => Enemies.FirstOrDefault(e => e.Kind == EnemyKind.Boss);

        public static readonly MemoryDefinition[][] MemoryDefinitions =
        {
            new[]
            {
                new MemoryDefinition
                {
                    Id = "M1A", Title = "尖刻回声", Short = "刃痕加深",
                    Description = "战斗残影伤害由 2 提升至 3。让一次预判足以终结普通敌人。",
                    Accent = new Color(0.33f, 0.88f, 0.94f, 1f)
                },
                new MemoryDefinition
                {
                    Id = "M1B", Title = "长夜残响", Short = "余温延长",
                    Description = "战斗残影寿命由 6 回合提升至 9 回合。适合把危险路线留得更久。",
                    Accent = new Color(1f, 0.63f, 0.27f, 1f)
                }
            },
            new[]
            {
                new MemoryDefinition
                {
                    Id = "M2A", Title = "惊险回身", Short = "痛感蓄能",
                    Description = "每条新生命首次受伤时，立即获得 2 点影能。",
                    Accent = new Color(0.94f, 0.4f, 0.55f, 1f)
                },
                new MemoryDefinition
                {
                    Id = "M2B", Title = "灯下留步", Short = "路线返能",
                    Description = "每次生成战斗残影时返还 3 点影能，原路仍按步消耗。",
                    Accent = new Color(0.95f, 0.78f, 0.3f, 1f)
                }
            }
        };

        public GameSession()
        {
            InitializeMap();
            Status = RunStatus.Title;
        }

        public void StartNewRun()
        {
            BodyHp = BodyMaxHp;
            Lives = MaxLives;
            Shadow = MaxShadow;
            Light = 5;
            Actions = 0;
            TurnSerial = 0;
            DeathCount = 0;
            HitResponseUsed = false;
            Memories.Clear();
            CurrentTrail.Clear();
            InheritedTrail.Clear();
            PreviousInheritedTrail.Clear();
            EnergyDrops.Clear();
            BossRoomVisited = false;
            foreach (var tile in Tiles)
            {
                tile.Explored = false;
                tile.SeamKnown = false;
            }
            ResetAfterDeath();

            foreach (var memory in MemoryNodes)
                memory.Used = false;

            Status = RunStatus.Playing;
            Emit(GameEventKind.Respawn, "灯重新点亮。先用脚尖试探黑暗。");
            RefreshVision();
        }

        public void BeginPause()
        {
            if (Status == RunStatus.Playing)
                Status = RunStatus.Paused;
        }

        public void Resume()
        {
            if (Status == RunStatus.Paused)
                Status = RunStatus.Playing;
        }

        public void TryDirection(Vector2Int dir)
        {
            if (Status != RunStatus.Playing || dir == Vector2Int.zero)
                return;

            Facing = dir;
            if (Mode == ControlMode.Scout)
                TryScoutMove(dir);
            else
                TryBodyMove(dir);
        }

        private void TryBodyMove(Vector2Int dir)
        {
            var target = Body + dir;
            RefreshVision();
            if (!InBounds(target))
            {
                Invalid("黑暗之外没有可确认的路。");
                return;
            }

            var tile = Tiles[target.x, target.y];
            if (tile.Kind == TileKind.FixedWall || tile.Kind == TileKind.MovableWall)
            {
                tile.Explored = true;
                if (tile.Kind == TileKind.MovableWall && IsCellLit(target))
                    tile.SeamKnown = true;
                Invalid("墙挡住了脚。方向已经改变，投环不会偏。");
                RefreshVision();
                return;
            }

            if (tile.Kind == TileKind.Gap)
            {
                tile.Explored = true;
                Invalid("前面是缺口。找一块可搬墙才能铺过去。");
                RefreshVision();
                return;
            }

            if (EnemyAt(target) != null)
            {
                Invalid("敌人占住了下一格。用环戒或路线残影开路。");
                return;
            }

            if (target == new Vector2Int(14, 1) && !ExitOpen)
            {
                Tiles[target.x, target.y].Explored = true;
                Emit(GameEventKind.ExitLocked, "出口仍被守门者的印记封着。先击败 Boss。");
                return;
            }

            Body = target;
            Tiles[Body.x, Body.y].Explored = true;
            EnergyDrops.Remove(Body);

            if (Recording)
            {
                if (Shadow > 0)
                {
                    Shadow--;
                    if (RecordedOrder.Count == 0 || RecordedOrder[RecordedOrder.Count - 1] != Body)
                    {
                        RecordedOrder.Add(Body);
                        RecordedPath.Add(Body);
                    }
                }

                if (Shadow <= 0)
                    FinishRecording("影能见底，路线自行凝固。");
            }

            PickupAt(Body);
            EnterBossRoomIfNeeded();
            if (ExitOpen && Body == new Vector2Int(14, 1))
            {
                Status = RunStatus.Won;
                Emit(GameEventKind.Victory, "你从错误的路线里带回了答案。");
                return;
            }
            Emit(GameEventKind.Move, "");
            EndPlayerTurn();
        }

        private void TryScoutMove(Vector2Int dir)
        {
            var target = Scout + dir;
            if (!InBounds(target))
            {
                Invalid("探路影子无法越过黑暗边界。");
                return;
            }

            var tile = Tiles[target.x, target.y];
            if (!tile.Walkable)
            {
                tile.Explored = true;
                if (tile.Kind == TileKind.MovableWall && IsCellLit(target))
                    tile.SeamKnown = true;
                Invalid(tile.Kind == TileKind.Gap ? "影子不能跨过缺口。" : "影子撞上了墙。");
                RefreshVision();
                return;
            }

            if (Shadow < 1 || Light < 1)
            {
                ReturnScout("资源不足，影子自动收回。");
                return;
            }

            Shadow--;
            Light--;
            Scout = target;
            CurrentTrail.Add(Scout);
            Tiles[Scout.x, Scout.y].Explored = true;
            RefreshVision();
            Emit(GameEventKind.Scout, "");
            EndPlayerTurn();

            if (Status == RunStatus.Playing && (Shadow < 1 || Light < 1))
                ReturnScout("最后一点光留在路上，影子自动收回。");
        }

        public void ToggleRecord()
        {
            if (Status != RunStatus.Playing)
                return;
            if (Mode == ControlMode.Scout)
            {
                Invalid("收回探路影子后才能开始记录。");
                return;
            }

            if (Recording)
            {
                FinishRecording("路线封存为战斗残影。");
                return;
            }

            if (Mode != ControlMode.Body)
                return;
            if (Shadow < 1)
            {
                Invalid("影能不足，无法记录新路线。");
                return;
            }

            Mode = ControlMode.Record;
            Recording = true;
            RecordingSpawnedThisTurn = false;
            RecordedOrder.Clear();
            RecordedPath.Clear();
            RecordedOrder.Add(Body);
            RecordedPath.Add(Body);
            Emit(GameEventKind.Record, "记录开始：每一步消耗 1 影能。");
        }

        private void FinishRecording(string message)
        {
            Recording = false;
            Mode = ControlMode.Body;
            if (RecordedPath.Count <= 1)
            {
                Invalid("没有走出有效路线，残影没有形成。");
                return;
            }

            BattleGhost = new BattleGhost();
            BattleGhost.Path.AddRange(RecordedOrder);
            foreach (var cell in RecordedPath)
                BattleGhost.DamageCells.Add(cell);
            BattleGhost.Damage = HasMemory("M1A") ? 3 : 2;
            BattleGhost.Life = HasMemory("M1B") ? 9 : 6;
            RecordingSpawnedThisTurn = true;
            if (HasMemory("M2B"))
                Shadow = Mathf.Min(MaxShadow, Shadow + 3);
            Emit(GameEventKind.Ghost, message + "  伤害 " + BattleGhost.Damage +
                " · 寿命 " + BattleGhost.Life + " 回合");
        }

        public void ToggleScout()
        {
            if (Status != RunStatus.Playing)
                return;
            if (Mode == ControlMode.Scout)
            {
                ReturnScout("影子主动收回。已经照见的路仍会发亮。");
                return;
            }

            if (Mode != ControlMode.Body)
            {
                Invalid("先结束路线记录。");
                return;
            }

            if (Shadow < 1 || Light < 1)
            {
                Invalid("放出探路影子需要各 1 点影能与光。");
                return;
            }

            Shadow--;
            Light--;
            Mode = ControlMode.Scout;
            Scout = Body;
            CurrentTrail.Add(Body);
            RefreshVision();
            Emit(GameEventKind.Scout, "持环影子已放出。每一步各消耗 1 影能与光。");
            EndPlayerTurn();

            if (Status == RunStatus.Playing && (Shadow < 1 || Light < 1))
                ReturnScout("资源见底，影子自动收回。");
        }

        private void ReturnScout(string message)
        {
            if (Mode != ControlMode.Scout)
                return;
            Mode = ControlMode.Body;
            RefreshVision();
            Emit(GameEventKind.Scout, message);
        }

        public void WaitTurn()
        {
            if (Status != RunStatus.Playing)
                return;
            Emit(GameEventKind.Move, "你屏住呼吸，等敌人先动。");
            EndPlayerTurn();
        }

        public void Attack()
        {
            if (Status != RunStatus.Playing)
                return;
            if (Mode == ControlMode.Scout)
            {
                Invalid("影子只负责看路，环戒仍在身体手里。");
                return;
            }

            bool validLength = false;
            bool hit = false;
            Vector2Int end = Body;
            EnemyState target = null;

            for (int distance = 1; distance <= 3; distance++)
            {
                var cell = Body + Facing * distance;
                if (!InBounds(cell))
                    break;
                var tile = Tiles[cell.x, cell.y];
                if (tile.Explored)
                    validLength = true;
                if (tile.Kind == TileKind.FixedWall || tile.Kind == TileKind.MovableWall ||
                    tile.Kind == TileKind.Gap)
                    break;

                end = cell;
                var enemy = EnemyAt(cell);
                if (enemy != null && enemy.Alive)
                {
                    target = enemy;
                    hit = true;
                    break;
                }
            }

            if (!validLength)
            {
                Invalid("前方没有已确认的飞行段。先移动一步或让影子探路。");
                return;
            }

            LastAttack = new BodyAttackTrace { From = Body, To = end, Hit = hit };
            if (hit)
            {
                DamageEnemy(target, 1);
                if (target.Kind == EnemyKind.Boss && !BossDefeated)
                    StartBossFight();
                Emit(GameEventKind.AttackHit, target.Kind == EnemyKind.Boss ? "环戒擦过巨型身影。" : "环戒命中。");
            }
            else
            {
                Emit(GameEventKind.AttackMiss, "环戒敲在黑暗里，又转回手中。");
            }

            EndPlayerTurn();
        }

        public void Interact()
        {
            if (Status != RunStatus.Playing)
                return;
            if (Mode == ControlMode.Scout)
            {
                Invalid("影子无法搬墙、拾取或触碰冰晶。");
                return;
            }

            var target = Body + Facing;
            if (!InBounds(target))
                return;
            var tile = Tiles[target.x, target.y];

            var memory = MemoryNodes.FirstOrDefault(m => m.Cell == target && !m.Used);
            if (memory != null)
            {
                openMemory = memory;
                MemoryChoiceSelection = 0;
                Status = RunStatus.MemoryChoice;
                Emit(GameEventKind.Memory, "记忆冰晶在掌心发热。选择一条会留下来的解释。");
                return;
            }

            if (tile.Kind == TileKind.MovableWall)
            {
                if (CarryingWall)
                {
                    Invalid("手里已经有墙块。先把它放进缺口。");
                    return;
                }

                tile.Kind = TileKind.Floor;
                tile.SeamKnown = true;
                CarryingWall = true;
                RefreshVision();
                Emit(GameEventKind.Record, "可搬墙已经拆下。携墙时仍可移动和投环。");
                EndPlayerTurn();
                return;
            }

            if (tile.Kind == TileKind.Gap)
            {
                if (!CarryingWall)
                {
                    Invalid("缺口很深。需要先带着一块可搬墙。");
                    return;
                }

                tile.Kind = TileKind.Floor;
                tile.Explored = true;
                CarryingWall = false;
                RefreshVision();
                Emit(GameEventKind.Record, "墙块落进缺口，捷径接通。");
                EndPlayerTurn();
                return;
            }

            Invalid("面朝方向没有可交互物。先用 F 面对它，或靠近一步。");
        }

        public void SelectMemoryOption(int direction)
        {
            if (Status != RunStatus.MemoryChoice)
                return;
            MemoryChoiceSelection = Mathf.Clamp(MemoryChoiceSelection + direction, 0, 1);
        }

        public void ConfirmMemory(int index)
        {
            if (Status != RunStatus.MemoryChoice || openMemory == null)
                return;
            index = Mathf.Clamp(index, 0, 1);
            var definition = MemoryDefinitions[MemoryChoiceIndex(openMemory)][index];
            Memories.Add(definition.Id);
            openMemory.Used = true;
            openMemory = null;
            Status = RunStatus.Playing;
            Emit(GameEventKind.Memory, "记住：" + definition.Title + "。效果将在本局持续。");
        }

        public void CancelMemory()
        {
            if (Status == RunStatus.MemoryChoice)
            {
                openMemory = null;
                Status = RunStatus.Playing;
            }
        }

        public int MemoryChoiceIndex(MemoryNode node)
        {
            return MemoryNodes.IndexOf(node);
        }

        public void Respawn()
        {
            if (Status != RunStatus.Dead)
                return;
            if (CurrentTrail.Count > 0)
            {
                InheritedTrail.Clear();
                foreach (var cell in CurrentTrail)
                    InheritedTrail.Add(cell);
            }

            CurrentTrail.Clear();
            EnergyDrops.Clear();
            HitResponseUsed = false;
            ResetAfterDeath();
            Status = RunStatus.Playing;
            Emit(GameEventKind.Respawn, InheritedTrail.Count > 0
                ? "上一世的路仍在发热。敌人恢复，冰晶记忆保留。"
                : "上一世没有留下光路。旧路继续照着迷宫。");
            RefreshVision();
        }

        private void ResetAfterDeath()
        {
            var knownSeams = new HashSet<Vector2Int>();
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                if (Tiles[x, y].SeamKnown)
                    knownSeams.Add(new Vector2Int(x, y));
            }

            Body = Spawn;
            Scout = Body;
            Facing = Vector2Int.right;
            BodyHp = BodyMaxHp;
            Shadow = MaxShadow;
            Light = 5;
            Mode = ControlMode.Body;
            Recording = false;
            RecordedPath.Clear();
            RecordedOrder.Clear();
            BattleGhost = null;
            CarryingWall = false;
            BossStarted = false;
            BossDefeated = false;
            ExitOpen = false;
            BossRoomVisited = false;
            LastAttack = null;
            ResetMap();
            ResetEnemies();
            foreach (var light in LightNodes)
                light.Available = true;
            foreach (var cell in knownSeams)
            {
                var tile = Tiles[cell.x, cell.y];
                if (tile.Kind == TileKind.MovableWall || tile.Kind == TileKind.Gap)
                    tile.SeamKnown = true;
            }
            RefreshVision();
        }

        public void RestartRun()
        {
            StartNewRun();
        }

        private void StartBossFight()
        {
            if (BossStarted || BossDefeated)
                return;
            BossStarted = true;
            Emit(GameEventKind.Boss, "巨型守门者醒了。它第一次不会立刻攻击。");
        }

        private void EnterBossRoomIfNeeded()
        {
            if (BossRoomVisited)
                return;
            if (Body.x >= 9 && Body.y <= 3)
            {
                BossRoomVisited = true;
                StartBossFight();
            }
        }

        private void EndPlayerTurn()
        {
            if (Status != RunStatus.Playing)
                return;
            Actions++;
            TurnSerial++;
            RefreshVision();
            ResolveEnemyPhase();
            if (Status != RunStatus.Playing)
                return;
            DecayGhost();
            RefreshVision();
            CheckVictory();
        }

        private void ResolveEnemyPhase()
        {
            damageThisPhase = false;
            int telegraphBefore = DangerSerial;

            if (BattleGhost != null)
            {
                foreach (var enemy in Enemies.Where(e => e.Alive).ToArray())
                {
                    if (BattleGhost.DamageCells.Contains(enemy.Cell))
                        DamageFromGhost(enemy);
                }
            }

            foreach (var enemy in Enemies.Where(e => e.Alive).ToArray())
            {
                if (enemy.Phase == EnemyPhase.Telegraph)
                    ResolveTelegraph(enemy);
                if (Status != RunStatus.Playing)
                    return;
            }

            foreach (var enemy in Enemies.Where(e => e.Alive).ToArray())
            {
                if (enemy.Phase == EnemyPhase.Idle)
                    ThinkWhileIdle(enemy);
            }

            foreach (var enemy in Enemies)
            {
                if (!enemy.Alive)
                    continue;
                if (enemy.Phase == EnemyPhase.Recover)
                {
                    enemy.RecoverTurns--;
                    if (enemy.RecoverTurns <= 0)
                    {
                        enemy.Phase = EnemyPhase.Idle;
                        enemy.RecoverTurns = 0;
                    }
                }
                else if (enemy.Cooldown > 0)
                {
                    enemy.Cooldown--;
                }
            }

            if (DangerSerial != telegraphBefore)
                LastTelegraphSerial = DangerSerial;

            if (BossDefeated && !ExitOpen)
                OpenExit();
        }

        private void ResolveTelegraph(EnemyState enemy)
        {
            switch (enemy.MoveKind)
            {
                case EnemyMoveKind.RangedShot:
                    foreach (var cell in enemy.DangerCells)
                    {
                        if (cell == Body)
                        {
                            ApplyBodyDamage(1, "远程弹命中了身体。");
                            break;
                        }
                    }
                    enemy.Phase = EnemyPhase.Recover;
                    enemy.RecoverTurns = 3;
                    break;

                case EnemyMoveKind.BossCharge:
                    ResolveBossCharge(enemy);
                    break;

                case EnemyMoveKind.BossSlam:
                    if (enemy.DangerCells.Contains(Body))
                        ApplyBodyDamage(1, "重击砸中了身体。");
                    enemy.Phase = EnemyPhase.Recover;
                    enemy.RecoverTurns = 3;
                    break;
            }

            enemy.MoveKind = EnemyMoveKind.None;
            enemy.DangerCells.Clear();
            if (enemy.Phase == EnemyPhase.Telegraph)
                enemy.Phase = EnemyPhase.Idle;
        }

        private void ResolveBossCharge(EnemyState boss)
        {
            var dir = boss.LockedDir;
            bool hitWall = false;
            for (int step = 0; step < 4; step++)
            {
                if (!CanBossEnter(boss, boss.Cell + dir))
                {
                    hitWall = true;
                    break;
                }
                boss.Cell += dir;
                if (BattleGhost != null && BattleGhost.DamageCells.Contains(boss.Cell))
                    DamageFromGhost(boss);
                if (boss.Cell == Body)
                {
                    ApplyBodyDamage(1, "冲锋撞中了身体。");
                    break;
                }
            }

            if (Status != RunStatus.Playing)
                return;
            boss.Phase = EnemyPhase.Recover;
            boss.RecoverTurns = hitWall ? 4 : 3;
            Emit(GameEventKind.Boss, hitWall ? "冲锋撞上石墙，守门者露出更长的空档。" : "冲锋掠过地面。");
        }

        private void ThinkWhileIdle(EnemyState enemy)
        {
            if (enemy.Cooldown > 0 || enemy.Phase != EnemyPhase.Idle)
                return;

            switch (enemy.Kind)
            {
                case EnemyKind.Melee:
                    ThinkMelee(enemy);
                    break;
                case EnemyKind.Ranged:
                    ThinkRanged(enemy);
                    break;
                case EnemyKind.Boss:
                    ThinkBoss(enemy);
                    break;
            }
        }

        private void ThinkMelee(EnemyState enemy)
        {
            int distance = Manhattan(enemy.Cell, Body);
            if (distance == 1)
            {
                SetTelegraph(enemy, EnemyMoveKind.None, new List<Vector2Int> { Body }, Vector2Int.zero, Body);
                Emit(GameEventKind.Invalid, "近战怪抬起利爪：下一回合砸向红格。");
                return;
            }

            if (enemy.Cooldown > 0 || distance > 4)
                return;
            var path = FindPath(enemy, Body);
            if (path.Count == 0)
                return;
            var next = path[0];
            if (EnemyAt(next) != null)
                return;
            enemy.Cell = next;
            enemy.Cooldown = 1;
            TryGhostOnMovement(enemy);
        }

        private void ThinkRanged(EnemyState enemy)
        {
            Vector2Int dir;
            if (!TryGetRangedDirection(enemy.Cell, Body, out dir))
                return;
            var cells = RayCells(enemy.Cell, dir, 5);
            SetTelegraph(enemy, EnemyMoveKind.RangedShot, cells, dir, cells.Count > 0 ? cells[0] : Body);
            Emit(GameEventKind.Invalid, "黑暗里亮起枪口：方向已经锁定。");
        }

        private void ThinkBoss(EnemyState boss)
        {
            if (!BossStarted)
                return;
            if (boss.RecoverTurns > 0 || boss.Cooldown > 0)
                return;

            if (Manhattan(boss.Cell, Body) == 1)
            {
                var cells = new List<Vector2Int>();
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    var cell = boss.Cell + new Vector2Int(dx, dy);
                    if (InBounds(cell))
                        cells.Add(cell);
                }
                SetTelegraph(boss, EnemyMoveKind.BossSlam, cells, Vector2Int.zero, Body);
                Emit(GameEventKind.Boss, "重击范围亮起。离开一圈红格，或提前退开。");
                return;
            }

            Vector2Int dir;
            if (TryAlignmentDirection(boss.Cell, Body, 4, out dir))
            {
                SetTelegraph(boss, EnemyMoveKind.BossCharge, RayCells(boss.Cell, dir, 4), dir, Body);
                Emit(GameEventKind.Boss, "冲锋路线锁定。撞墙会让它失衡。");
                return;
            }

            var path = FindPath(boss, Body);
            if (path.Count > 0)
            {
                var next = path[0];
                if (EnemyAt(next) == null)
                {
                    boss.Cell = next;
                    TryGhostOnMovement(boss);
                }
            }
        }

        private void SetTelegraph(
            EnemyState enemy,
            EnemyMoveKind kind,
            List<Vector2Int> danger,
            Vector2Int dir,
            Vector2Int locked)
        {
            enemy.Phase = EnemyPhase.Telegraph;
            enemy.MoveKind = kind;
            enemy.DangerCells.Clear();
            enemy.DangerCells.AddRange(danger);
            enemy.LockedDir = dir;
            enemy.LockedCell = locked;
            enemy.TelegraphSerial = ++DangerSerial;
        }

        private void TryGhostOnMovement(EnemyState enemy)
        {
            if (BattleGhost != null && BattleGhost.DamageCells.Contains(enemy.Cell))
                DamageFromGhost(enemy);
        }

        private void DamageFromGhost(EnemyState enemy)
        {
            if (BattleGhost == null || BattleGhost.HitEnemies.Contains(enemy.Id))
                return;
            BattleGhost.HitEnemies.Add(enemy.Id);
            DamageEnemy(enemy, BattleGhost.Damage);
            Emit(GameEventKind.GhostHit, "旧路线成功截住敌人：" + BattleGhost.Damage + " 伤害。");
        }

        private void DamageEnemy(EnemyState enemy, int amount)
        {
            if (enemy == null || !enemy.Alive)
                return;
            enemy.Hp -= amount;
            if (enemy.Hp > 0)
                return;

            enemy.Alive = false;
            enemy.Hp = 0;
            enemy.Phase = EnemyPhase.Idle;
            enemy.DangerCells.Clear();
            if (enemy.Kind == EnemyKind.Boss)
            {
                BossDefeated = true;
                BossStarted = false;
                OpenExit();
                Emit(GameEventKind.Boss, "守门者倒下。出口铁门发出长长的回响。");
            }
            else
            {
                EnergyDrops.Add(enemy.Cell);
            }
        }

        private void ApplyBodyDamage(int amount, string text)
        {
            if (damageThisPhase || BodyHp <= 0 || Status != RunStatus.Playing)
                return;
            damageThisPhase = true;
            BodyHp = Mathf.Max(0, BodyHp - amount);
            if (HasMemory("M2A") && !HitResponseUsed)
            {
                HitResponseUsed = true;
                Shadow = Mathf.Min(MaxShadow, Shadow + 2);
            }
            Emit(GameEventKind.Hurt, text + "  生命 " + BodyHp + "/" + BodyMaxHp);
            if (BodyHp <= 0)
                KillBody();
        }

        private void KillBody()
        {
            if (Status == RunStatus.Dead || Status == RunStatus.Lost)
                return;
            DeathCount++;
            Lives = Mathf.Max(0, Lives - 1);
            Recording = false;
            Mode = ControlMode.Body;
            BattleGhost = null;
            foreach (var enemy in Enemies)
                enemy.DangerCells.Clear();
            if (Lives <= 0)
            {
                Status = RunStatus.Lost;
                Emit(GameEventKind.Death, "最后一次灯火熄灭。按 R 重新进入迷宫。");
            }
            else
            {
                Status = RunStatus.Dead;
                Emit(GameEventKind.Death, "身体倒在黑暗里。本世光路正准备留下。");
            }
        }

        private void DecayGhost()
        {
            if (BattleGhost == null)
                return;
            BattleGhost.Life--;
            if (BattleGhost.Life <= 0)
            {
                BattleGhost = null;
                Emit(GameEventKind.Ghost, "战斗残影散去了。");
            }
        }

        private void PickupAt(Vector2Int cell)
        {
            var light = LightNodes.FirstOrDefault(l => l.Available && l.Cell == cell);
            if (light != null)
            {
                light.Available = false;
                Light = Mathf.Min(MaxLight, Light + 3);
                Emit(GameEventKind.Pickup, "拾得微光，光 +3。");
            }

            if (EnergyDrops.Remove(cell))
            {
                Shadow = Mathf.Min(MaxShadow, Shadow + 2);
                Emit(GameEventKind.Pickup, "敌人的余烬进入环戒，影能 +2。");
            }
        }

        private void OpenExit()
        {
            ExitOpen = true;
            Tiles[14, 1].Explored = true;
            Emit(GameEventKind.Boss, "出口已经开放。身体走进去才算胜利。");
        }

        private void CheckVictory()
        {
            if (!ExitOpen || Body != new Vector2Int(14, 1))
                return;
            Status = RunStatus.Won;
            Emit(GameEventKind.Victory, "你从错误的路线里带回了答案。");
        }

        private void Invalid(string text)
        {
            Emit(GameEventKind.Invalid, text);
        }

        private void Emit(GameEventKind kind, string text)
        {
            LastEventKind = kind;
            Events.Enqueue(new GameEvent(kind, text));
        }

        public bool HasMemory(string id)
        {
            return Memories.Contains(id);
        }

        public EnemyState EnemyAt(Vector2Int cell)
        {
            return Enemies.FirstOrDefault(e => e.Alive && e.Cell == cell);
        }

        public bool IsDangerCell(Vector2Int cell)
        {
            foreach (var enemy in Enemies)
            {
                if (enemy.Alive && enemy.Phase == EnemyPhase.Telegraph &&
                    enemy.DangerCells.Contains(cell))
                    return true;
            }
            return false;
        }

        public bool IsTelegraphCell(Vector2Int cell, out Vector2Int origin)
        {
            foreach (var enemy in Enemies)
            {
                if (enemy.Alive && enemy.Phase == EnemyPhase.Telegraph &&
                    enemy.DangerCells.Contains(cell))
                {
                    origin = enemy.Cell;
                    return true;
                }
            }
            origin = Vector2Int.zero;
            return false;
        }

        public bool IsCellLit(Vector2Int cell)
        {
            if (!InBounds(cell))
                return false;
            if (Body == cell || Scout == cell)
                return true;
            if (Manhattan(Body, cell) <= 1)
                return true;
            if (Mode == ControlMode.Scout && Manhattan(Scout, cell) <= 1)
                return true;
            foreach (var source in CurrentTrail)
            {
                if (Manhattan(source, cell) <= 1)
                    return true;
            }
            foreach (var source in InheritedTrail)
            {
                if (Manhattan(source, cell) <= 1)
                    return true;
            }
            return false;
        }

        public void RefreshVision()
        {
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!IsCellLit(cell))
                    continue;
                var tile = Tiles[x, y];
                tile.Explored = true;
                if (tile.Kind == TileKind.MovableWall)
                    tile.SeamKnown = true;
            }
        }

        private bool CanBossEnter(EnemyState boss, Vector2Int cell)
        {
            if (!InBounds(cell) || !Tiles[cell.x, cell.y].Walkable)
                return false;
            return EnemyAt(cell) == boss || EnemyAt(cell) == null;
        }

        private bool TryGetRangedDirection(Vector2Int from, Vector2Int to, out Vector2Int dir)
        {
            dir = Vector2Int.zero;
            if (from.x == to.x && Mathf.Abs(from.y - to.y) <= 5)
                dir = new Vector2Int(0, Math.Sign(to.y - from.y));
            else if (from.y == to.y && Mathf.Abs(from.x - to.x) <= 5)
                dir = new Vector2Int(Math.Sign(to.x - from.x), 0);
            else
                return false;

            if (dir == Vector2Int.zero)
                return false;
            int distance = Manhattan(from, to);
            for (int d = 1; d < distance; d++)
            {
                var cell = from + dir * d;
                if (Tiles[cell.x, cell.y].BlocksSight)
                {
                    dir = Vector2Int.zero;
                    return false;
                }
            }
            return true;
        }

        private bool TryAlignmentDirection(Vector2Int from, Vector2Int to, int maxDistance,
            out Vector2Int dir)
        {
            dir = Vector2Int.zero;
            if (from.x == to.x && Mathf.Abs(from.y - to.y) <= maxDistance)
                dir = new Vector2Int(0, Math.Sign(to.y - from.y));
            else if (from.y == to.y && Mathf.Abs(from.x - to.x) <= maxDistance)
                dir = new Vector2Int(Math.Sign(to.x - from.x), 0);
            if (dir == Vector2Int.zero)
                return false;
            return CountClearCells(from, dir, maxDistance) >= Manhattan(from, to);
        }

        private int CountClearCells(Vector2Int from, Vector2Int dir, int max)
        {
            int count = 0;
            for (int i = 1; i <= max; i++)
            {
                var cell = from + dir * i;
                if (!InBounds(cell) || Tiles[cell.x, cell.y].BlocksSight)
                    break;
                count++;
            }
            return count;
        }

        private List<Vector2Int> RayCells(Vector2Int from, Vector2Int dir, int max)
        {
            var result = new List<Vector2Int>();
            for (int i = 1; i <= max; i++)
            {
                var cell = from + dir * i;
                if (!InBounds(cell) || Tiles[cell.x, cell.y].BlocksSight)
                    break;
                result.Add(cell);
            }
            return result;
        }

        private List<Vector2Int> FindPath(EnemyState enemy, Vector2Int target)
        {
            var start = enemy.Cell;
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            queue.Enqueue(start);
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == target)
                    break;
                foreach (var dir in dirs)
                {
                    var next = current + dir;
                    if (visited.Contains(next) || !InBounds(next) || !Tiles[next.x, next.y].Walkable)
                        continue;
                    if (next != target && EnemyAt(next) != null)
                        continue;
                    visited.Add(next);
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }

            var path = new List<Vector2Int>();
            if (!previous.ContainsKey(target))
                return path;
            var step = target;
            while (step != start)
            {
                path.Add(step);
                step = previous[step];
            }
            path.Reverse();
            return path;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        private void ResetEnemies()
        {
            Enemies.Clear();
            Enemies.Add(MakeEnemy(1, EnemyKind.Melee, new Vector2Int(6, 10), 2));
            Enemies.Add(MakeEnemy(2, EnemyKind.Ranged, new Vector2Int(12, 8), 2));
            Enemies.Add(MakeEnemy(3, EnemyKind.Melee, new Vector2Int(8, 6), 2));
            Enemies.Add(MakeEnemy(4, EnemyKind.Ranged, new Vector2Int(11, 5), 2));
            Enemies.Add(MakeEnemy(5, EnemyKind.Boss, new Vector2Int(12, 2), 8));
        }

        private EnemyState MakeEnemy(int id, EnemyKind kind, Vector2Int cell, int hp)
        {
            return new EnemyState
            {
                Id = id,
                Kind = kind,
                Spawn = cell,
                Cell = cell,
                Hp = hp,
                MaxHp = hp
            };
        }

        private void InitializeMap()
        {
            MemoryNodes.Clear();
            MemoryNodes.Add(new MemoryNode { Cell = new Vector2Int(2, 6), Title = "裂纹里的初见" });
            MemoryNodes.Add(new MemoryNode { Cell = new Vector2Int(13, 4), Title = "墙缝里的旧名字" });
            LightNodes.Clear();
            LightNodes.Add(new LightNode { Cell = new Vector2Int(5, 11) });
            LightNodes.Add(new LightNode { Cell = new Vector2Int(4, 4) });
            LightNodes.Add(new LightNode { Cell = new Vector2Int(14, 7) });
            LightNodes.Add(new LightNode { Cell = new Vector2Int(10, 3) });
            LightNodes.Add(new LightNode { Cell = new Vector2Int(4, 1) });
            ResetMap();
            ResetEnemies();
            Body = Spawn;
            Scout = Spawn;
            RefreshVision();
        }

        private void ResetMap()
        {
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                if (Tiles[x, y] == null)
                    Tiles[x, y] = new TileState();
                Tiles[x, y].Kind = TileKind.FixedWall;
            }

            Carve(1, 10, 6, 3);
            Carve(3, 7, 2, 3);
            Carve(1, 4, 5, 4);
            Carve(6, 5, 5, 4);
            Carve(11, 7, 4, 2);
            Carve(10, 3, 5, 3);
            Carve(9, 1, 6, 3);
            Carve(1, 1, 4, 3);
            Carve(6, 8, 1, 5);
            Carve(5, 5, 2, 1);
            Carve(13, 6, 1, 2);
            Carve(2, 3, 1, 2);

            Tiles[11, 4].Kind = TileKind.MovableWall;
            Tiles[12, 4].Kind = TileKind.Gap;
        }

        private void Carve(int x, int y, int width, int height)
        {
            for (int ix = x; ix < x + width; ix++)
            for (int iy = y; iy < y + height; iy++)
            {
                if (ix > 0 && ix < Width - 1 && iy > 0 && iy < Height - 1)
                    Tiles[ix, iy].Kind = TileKind.Floor;
            }
        }

        public IEnumerable<Vector2Int> AllDangerCells()
        {
            foreach (var enemy in Enemies)
            {
                if (!enemy.Alive || enemy.Phase != EnemyPhase.Telegraph)
                    continue;
                foreach (var cell in enemy.DangerCells)
                    yield return cell;
            }
        }

        public int RemainingLives => Lives;
        public int CurrentLifeIndex => Mathf.Clamp(Lives - 1, 0, MaxLives - 1);
    }
}
