using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WrongTurn.TurnBased
{
    public sealed class GridTurnSession
    {
        public const int PlayerMaxHp = 3;
        public const int MaxLives = 3;
        public const int MaxShadow = 12;
        public const int MaxLight = 10;

        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        private static readonly Vector2Int[] AimDirections =
        {
            Vector2Int.up, new Vector2Int(1, 1), Vector2Int.right, new Vector2Int(1, -1),
            Vector2Int.down, new Vector2Int(-1, -1), Vector2Int.left, new Vector2Int(-1, 1)
        };

        private static readonly GridMemoryDefinition[][] MemoryDefinitions =
        {
            new[]
            {
                new GridMemoryDefinition
                {
                    Id = "M1A", Title = "尖刻回声", Short = "刃痕加深",
                    Description = "战斗残影伤害由 2 提升至 3。"
                },
                new GridMemoryDefinition
                {
                    Id = "M1B", Title = "长夜残响", Short = "余光延长",
                    Description = "战斗残影寿命由 6 回合提升至 9 回合。"
                }
            },
            new[]
            {
                new GridMemoryDefinition
                {
                    Id = "M2A", Title = "惊险回身", Short = "痛感蓄能",
                    Description = "每条生命首次受伤时获得 2 格影子。"
                },
                new GridMemoryDefinition
                {
                    Id = "M2B", Title = "灯下留步", Short = "路线返能",
                    Description = "生成战斗残影时返还 3 格影子。"
                }
            }
        };

        private readonly GridMapData map;
        private readonly GridTileKind[,] tiles;
        private readonly bool[,] explored;
        private readonly bool[,] visible;
        private readonly bool[,] trapKnown;
        private readonly HashSet<Vector2Int> currentTrail = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> inheritedTrail = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> soulTrail = new HashSet<Vector2Int>();
        private readonly HashSet<string> memories = new HashSet<string>();
        private readonly HashSet<Vector2Int> shadowDrops = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> collectedLights = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> usedCrystals = new HashSet<Vector2Int>();
        private readonly List<GridEnemyState> enemies = new List<GridEnemyState>();
        private readonly List<GridProjectileState> projectiles = new List<GridProjectileState>();
        private readonly List<GridTurnEvent> events = new List<GridTurnEvent>();
        private readonly List<Vector2Int> recordedPath = new List<Vector2Int>();
        private readonly List<Vector2Int> recordedOrder = new List<Vector2Int>();
        private readonly List<GridMemoryDefinition> activeMemoryChoices =
            new List<GridMemoryDefinition>();

        private int openMemoryIndex = -1;
        private int nextProjectileId = 1;
        private bool damagedThisWorldTurn;
        private bool hitResponseUsed;

        public GridTurnPhase Phase { get; private set; } = GridTurnPhase.Title;
        public Vector2Int PlayerCell { get; private set; }
        public Vector2Int SoulCell { get; private set; }
        public Vector2Int Facing { get; private set; } = Vector2Int.right;
        public int PlayerHp { get; private set; } = PlayerMaxHp;
        public int Lives { get; private set; } = MaxLives;
        public int Shadow { get; private set; } = MaxShadow;
        public int Light { get; private set; } = 5;
        public int TurnNumber { get; private set; }
        public bool SoulActive { get; private set; }
        public bool Recording { get; private set; }
        public bool CarryingWall { get; private set; }
        public bool BossActive { get; private set; }
        public bool BossDefeated { get; private set; }
        public bool ExitOpen { get; private set; }
        public GridBattleGhostState BattleGhost { get; private set; }
        public IReadOnlyList<GridEnemyState> Enemies => enemies;
        public IReadOnlyList<GridProjectileState> Projectiles => projectiles;
        public IReadOnlyList<Vector2Int> SoulTrail => soulTrail.ToList();
        public IReadOnlyList<GridMemoryDefinition> ActiveMemoryChoices => activeMemoryChoices;
        public int Width => map.Width;
        public int Height => map.Height;
        public Vector2Int ExitCell => map.Exit;
        public bool IsMemoryChoiceOpen => Phase == GridTurnPhase.MemoryChoice;
        public bool IsSoulBlockedByResource => SoulActive && Shadow <= 0;

        public GridTurnSession(GridMapData map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            this.map = map;
            tiles = new GridTileKind[map.Width, map.Height];
            explored = new bool[map.Width, map.Height];
            visible = new bool[map.Width, map.Height];
            trapKnown = new bool[map.Width, map.Height];
        }

        public GridEnemyState Boss =>
            enemies.FirstOrDefault(enemy => enemy.Kind == GridActorKind.Boss);

        public void StartNewRun()
        {
            Phase = GridTurnPhase.PlayerTurn;
            PlayerHp = PlayerMaxHp;
            Lives = MaxLives;
            Shadow = MaxShadow;
            Light = 5;
            TurnNumber = 0;
            SoulActive = false;
            Recording = false;
            CarryingWall = false;
            BossActive = false;
            BossDefeated = false;
            ExitOpen = false;
            BattleGhost = null;
            openMemoryIndex = -1;
            hitResponseUsed = false;
            memories.Clear();
            currentTrail.Clear();
            inheritedTrail.Clear();
            soulTrail.Clear();
            shadowDrops.Clear();
            collectedLights.Clear();
            usedCrystals.Clear();
            recordedPath.Clear();
            recordedOrder.Clear();
            activeMemoryChoices.Clear();
            projectiles.Clear();
            events.Clear();
            ResetMap();
            SpawnEnemies();
            PlayerCell = map.PlayerSpawn;
            SoulCell = PlayerCell;
            Facing = Vector2Int.right;
            AddTrail(PlayerCell);
            RefreshVision();
            AddEvent(GridTurnEventKind.Message, "玩家回合：选择移动、方向攻击、交互或等待。");
        }

        public void RestartRun()
        {
            StartNewRun();
        }

        public void TogglePause()
        {
            if (Phase == GridTurnPhase.PlayerTurn)
            {
                Phase = GridTurnPhase.Paused;
                AddEvent(GridTurnEventKind.Message, "游戏暂停。");
            }
            else if (Phase == GridTurnPhase.Paused)
            {
                Phase = GridTurnPhase.PlayerTurn;
                AddEvent(GridTurnEventKind.Message, "继续行动。");
            }
        }

        public bool TryPlayerMove(Vector2Int direction)
        {
            if (Phase != GridTurnPhase.PlayerTurn || !IsCardinal(direction))
                return false;
            if (SoulActive)
                return TrySoulMove(direction);

            Facing = direction;
            var target = PlayerCell + direction;
            if (!InBounds(target))
            {
                AddMessage("地图边缘被黑雾封住。");
                return false;
            }

            var tile = tiles[target.x, target.y];
            if (tile == GridTileKind.Wall || tile == GridTileKind.MovableWall)
            {
                explored[target.x, target.y] = true;
                AddMessage(tile == GridTileKind.MovableWall
                    ? "可搬墙挡住了路，按 F 可以尝试拆下。"
                    : "墙挡住了脚步。");
                RefreshVision();
                return false;
            }

            if (tile == GridTileKind.Gap)
            {
                explored[target.x, target.y] = true;
                AddMessage("前方是缺口，需要先搬运一块墙材。");
                RefreshVision();
                return false;
            }

            if (EnemyAt(target) != null)
            {
                AddMessage("敌人占据了目标格。先选择方向攻击或改变路线。");
                return false;
            }

            var from = PlayerCell;
            PlayerCell = target;
            AddTrail(PlayerCell);
            explored[target.x, target.y] = true;
            if (tile == GridTileKind.DamageTrap)
            {
                trapKnown[target.x, target.y] = true;
                AddEvent(GridTurnEventKind.Message, "踩中伤害陷阱。",
                    from: from, to: target);
                ApplyPlayerDamage(1, "陷阱割伤了身体。");
                if (Phase != GridTurnPhase.PlayerTurn)
                    return true;
            }

            AddEvent(GridTurnEventKind.PlayerMoved, "", from: from, to: target);
            ApplyRecordingStep();
            PickupAt(PlayerCell);
            ActivateBossIfNeeded();

            if (ExitOpen && PlayerCell == map.Exit)
            {
                Win();
                return true;
            }

            EndPlayerTurn();
            return true;
        }

        public bool TryAttack(Vector2Int direction)
        {
            if (Phase != GridTurnPhase.PlayerTurn || SoulActive || !IsAimDirection(direction))
                return false;

            Facing = direction;
            var end = PlayerCell;
            GridEnemyState target = null;
            for (var distance = 1; distance <= 3; distance++)
            {
                var cell = PlayerCell + direction * distance;
                if (!InBounds(cell))
                    break;
                var tile = tiles[cell.x, cell.y];
                if (tile == GridTileKind.Wall || tile == GridTileKind.MovableWall)
                    break;
                end = cell;
                target = EnemyAt(cell);
                if (target != null)
                    break;
            }

            if (target != null)
            {
                DamageEnemy(target, 1);
                AddEvent(GridTurnEventKind.PlayerAttack, "方向斩命中。", target.Id,
                    PlayerCell, end, 1);
            }
            else
            {
                AddEvent(GridTurnEventKind.PlayerAttack, "方向斩落空。", 0, PlayerCell,
                    end);
            }
            EndPlayerTurn();
            return true;
        }

        public bool TryInteract()
        {
            if (Phase != GridTurnPhase.PlayerTurn || SoulActive)
                return false;
            if (!IsCardinal(Facing))
                Facing = Cardinalize(Facing);
            var target = PlayerCell + Facing;
            if (!InBounds(target))
                return false;

            var crystalIndex = map.Crystals.IndexOf(target);
            if (crystalIndex >= 0 && !memories.Contains(MemoryDefinitions[
                    Mathf.Clamp(crystalIndex, 0, 1)][0].Id) &&
                !memories.Contains(MemoryDefinitions[Mathf.Clamp(crystalIndex, 0, 1)][1].Id))
            {
                OpenMemory(crystalIndex);
                return true;
            }

            var tile = tiles[target.x, target.y];
            if (tile == GridTileKind.MovableWall)
            {
                if (CarryingWall)
                {
                    AddMessage("手里已经有一块墙材。");
                    return false;
                }
                tiles[target.x, target.y] = GridTileKind.Floor;
                trapKnown[target.x, target.y] = false;
                CarryingWall = true;
                explored[target.x, target.y] = true;
                RefreshVision();
                AddMessage("拆下墙材，寻找缺口铺路。");
                EndPlayerTurn();
                return true;
            }

            if (tile == GridTileKind.Gap)
            {
                if (!CarryingWall)
                {
                    AddMessage("缺口需要一块可搬墙。");
                    return false;
                }
                tiles[target.x, target.y] = GridTileKind.Floor;
                CarryingWall = false;
                explored[target.x, target.y] = true;
                RefreshVision();
                AddMessage("墙材落位，缺口接通。");
                EndPlayerTurn();
                return true;
            }

            if (target == map.Exit && !ExitOpen)
            {
                AddMessage("出口被守门者封住。先击败 Boss。");
                return false;
            }
            AddMessage("面朝方向没有可交互物。");
            return false;
        }

        public bool WaitTurn()
        {
            if (Phase != GridTurnPhase.PlayerTurn)
                return false;
            AddMessage(SoulActive ? "虚影停在原地观察。" : "你屏住呼吸等待世界回合。");
            EndPlayerTurn();
            return true;
        }

        public bool ToggleSoul()
        {
            if (Phase != GridTurnPhase.PlayerTurn)
                return false;
            if (Recording)
            {
                AddMessage("先结束路线记录，再让灵魂出窍。");
                return false;
            }

            if (SoulActive)
            {
                SoulActive = false;
                AddEvent(GridTurnEventKind.SoulToggled, "虚影收回身体。", from: SoulCell,
                    to: PlayerCell);
            }
            else
            {
                if (Shadow <= 0)
                {
                    AddMessage("影子格数不足，无法出窍。");
                    return false;
                }
                SoulActive = true;
                SoulCell = PlayerCell;
                soulTrail.Add(SoulCell);
                currentTrail.Add(SoulCell);
                AddEvent(GridTurnEventKind.SoulToggled, "灵魂出窍。虚影可以穿墙探路。",
                    from: PlayerCell, to: SoulCell);
            }
            RefreshVision();
            return true;
        }

        public bool TrySoulMove(Vector2Int direction)
        {
            if (Phase != GridTurnPhase.PlayerTurn || !SoulActive || !IsCardinal(direction))
                return false;
            var target = SoulCell + direction;
            if (!InBounds(target))
            {
                AddMessage("虚影不能离开地图边界。");
                return false;
            }
            if (Shadow <= 0)
            {
                RecallSoul("影子格数耗尽，虚影自动回体。");
                return false;
            }

            var from = SoulCell;
            Shadow--;
            SoulCell = target;
            soulTrail.Add(target);
            currentTrail.Add(target);
            explored[target.x, target.y] = true;
            MarkTrapKnownAroundTarget(target);
            AddEvent(GridTurnEventKind.SoulMoved, "虚影前进一格。", from: from, to: target);
            RefreshVision();
            EndPlayerTurn();
            if (Shadow <= 0 && SoulActive)
                RecallSoul("最后一点影子留在路上，虚影自动回体。");
            return true;
        }

        public bool ToggleRecording()
        {
            if (Phase != GridTurnPhase.PlayerTurn || SoulActive)
                return false;
            if (Recording)
            {
                FinishRecording("路线封存为战斗残影。");
                return true;
            }
            if (Shadow <= 0)
            {
                AddMessage("影子格数不足，无法记录。");
                return false;
            }

            Recording = true;
            recordedPath.Clear();
            recordedOrder.Clear();
            recordedPath.Add(PlayerCell);
            recordedOrder.Add(PlayerCell);
            AddMessage("开始记录。每移动一格消耗 1 格影子。");
            return true;
        }

        public void SelectMemory(int index)
        {
            if (Phase != GridTurnPhase.MemoryChoice)
                return;
            index = Mathf.Clamp(index, 0, activeMemoryChoices.Count - 1);
            var choice = activeMemoryChoices[index];
            memories.Add(choice.Id);
            if (openMemoryIndex >= 0 && openMemoryIndex < map.Crystals.Count)
                usedCrystals.Add(map.Crystals[openMemoryIndex]);
            Phase = GridTurnPhase.PlayerTurn;
            activeMemoryChoices.Clear();
            openMemoryIndex = -1;
            AddEvent(GridTurnEventKind.MemoryChosen, "记住：" + choice.Title + "。");
        }

        public void CancelMemory()
        {
            if (Phase != GridTurnPhase.MemoryChoice)
                return;
            Phase = GridTurnPhase.PlayerTurn;
            activeMemoryChoices.Clear();
            openMemoryIndex = -1;
        }

        public void Respawn()
        {
            if (Phase != GridTurnPhase.Dead)
                return;
            var oldBoss = Boss;
            var preservedBossHp = oldBoss != null && oldBoss.Alive ? oldBoss.Hp : 0;
            var bossWasDefeated = BossDefeated;
            inheritedTrail.Clear();
            foreach (var cell in currentTrail)
                inheritedTrail.Add(cell);
            currentTrail.Clear();
            shadowDrops.Clear();
            collectedLights.Clear();
            projectiles.Clear();
            BattleGhost = null;
            Recording = false;
            SoulActive = false;
            PlayerHp = PlayerMaxHp;
            Shadow = MaxShadow;
            Light = 5;
            CarryingWall = false;
            BossActive = false;
            BossDefeated = bossWasDefeated;
            ExitOpen = bossWasDefeated;
            hitResponseUsed = false;
            PlayerCell = map.PlayerSpawn;
            SoulCell = PlayerCell;
            Facing = Vector2Int.right;
            ResetMapPreservingKnowledge();
            SpawnEnemies();
            var respawnedBoss = Boss;
            if (respawnedBoss != null)
            {
                if (bossWasDefeated)
                {
                    respawnedBoss.Alive = false;
                    respawnedBoss.Hp = 0;
                }
                else if (preservedBossHp > 0)
                {
                    respawnedBoss.Hp = Mathf.Clamp(preservedBossHp, 1,
                        respawnedBoss.MaxHp);
                }
                respawnedBoss.Phase = GridEnemyPhase.Idle;
                respawnedBoss.Action = GridEnemyAction.None;
                respawnedBoss.DangerCells.Clear();
            }
            AddTrail(PlayerCell);
            RefreshVision();
            Phase = GridTurnPhase.PlayerTurn;
            AddMessage("身体在起点重塑。旧路仍留在地图上。");
        }

        public GridTileKind TileAt(Vector2Int cell)
        {
            return InBounds(cell) ? tiles[cell.x, cell.y] : GridTileKind.Wall;
        }

        public bool IsExplored(Vector2Int cell)
        {
            return InBounds(cell) && explored[cell.x, cell.y];
        }

        public bool IsVisible(Vector2Int cell)
        {
            return InBounds(cell) && visible[cell.x, cell.y];
        }

        public bool IsTrapKnown(Vector2Int cell)
        {
            return InBounds(cell) && trapKnown[cell.x, cell.y];
        }

        public bool IsTrail(Vector2Int cell)
        {
            return currentTrail.Contains(cell) || inheritedTrail.Contains(cell) ||
                   soulTrail.Contains(cell);
        }

        public bool IsShadowDrop(Vector2Int cell)
        {
            return shadowDrops.Contains(cell);
        }

        public bool IsLightAvailable(Vector2Int cell)
        {
            return map.Lights.Contains(cell) && !collectedLights.Contains(cell);
        }

        public bool IsCrystalAvailable(Vector2Int cell)
        {
            return map.Crystals.Contains(cell) && !usedCrystals.Contains(cell);
        }

        public bool IsDangerCell(Vector2Int cell, out GridEnemyState source)
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.Alive || enemy.Phase != GridEnemyPhase.Telegraph)
                    continue;
                if (enemy.DangerCells.Contains(cell))
                {
                    source = enemy;
                    return true;
                }
            }
            source = null;
            return false;
        }

        public bool IsEnemyDetected(Vector2Int cell)
        {
            if (IsVisible(cell))
                return true;
            return Manhattan(PlayerCell, cell) <= 2 ||
                   SoulActive && Manhattan(SoulCell, cell) <= 2;
        }

        public GridEnemyState EnemyAt(Vector2Int cell)
        {
            return enemies.FirstOrDefault(enemy => enemy.Alive && enemy.Cell == cell);
        }

        public bool TryGetEnemy(int id, out GridEnemyState result)
        {
            result = enemies.FirstOrDefault(enemy => enemy.Id == id);
            return result != null;
        }

        public bool HasMemory(string id)
        {
            return memories.Contains(id);
        }

        public void DebugRevealAll()
        {
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                explored[x, y] = true;
                visible[x, y] = true;
                if (tiles[x, y] == GridTileKind.DamageTrap)
                    trapKnown[x, y] = true;
            }
        }

        public bool DrainEvents(List<GridTurnEvent> destination)
        {
            if (events.Count == 0)
                return false;
            destination.AddRange(events);
            events.Clear();
            return true;
        }

        private void EndPlayerTurn()
        {
            if (Phase != GridTurnPhase.PlayerTurn)
                return;
            TurnNumber++;
            Phase = GridTurnPhase.WorldTurn;
            damagedThisWorldTurn = false;
            AddEvent(GridTurnEventKind.Message, "世界回合。");

            ResolveBattleGhost();
            StepProjectiles();
            if (Phase != GridTurnPhase.WorldTurn)
                return;
            ResolveEnemyTelegraphs();
            if (Phase != GridTurnPhase.WorldTurn)
                return;
            ThinkIdleEnemies();
            TickRecoveries();
            DecayBattleGhost();
            RefreshVision();

            if (Phase == GridTurnPhase.WorldTurn)
                Phase = GridTurnPhase.PlayerTurn;
            if (Phase == GridTurnPhase.PlayerTurn)
                AddEvent(GridTurnEventKind.Message, "玩家回合。");
        }

        private void ResolveBattleGhost()
        {
            if (BattleGhost == null)
                return;
            foreach (var enemy in enemies.Where(enemy => enemy.Alive).ToArray())
            {
                if (!BattleGhost.DamageCells.Contains(enemy.Cell) ||
                    BattleGhost.HitEnemies.Contains(enemy.Id))
                    continue;
                BattleGhost.HitEnemies.Add(enemy.Id);
                DamageEnemy(enemy, BattleGhost.Damage);
                AddEvent(GridTurnEventKind.GhostHit, "旧路线截住敌人。", enemy.Id,
                    enemy.Cell, enemy.Cell, BattleGhost.Damage);
            }
        }

        private void StepProjectiles()
        {
            foreach (var projectile in projectiles.ToArray())
            {
                var from = projectile.Cell;
                projectile.Cell += projectile.Direction;
                projectile.Life--;
                if (projectile.Life <= 0 || !InBounds(projectile.Cell) ||
                    !CanProjectileEnter(projectile.Cell))
                {
                    projectiles.Remove(projectile);
                    AddEvent(GridTurnEventKind.ProjectileHit, "弹体撞上墙体。",
                        projectile.Id, from, projectile.Cell);
                    continue;
                }
                if (!projectile.Friendly && projectile.Cell == PlayerCell)
                {
                    ApplyPlayerDamage(projectile.Damage, "远程弹体命中了身体。");
                    projectiles.Remove(projectile);
                    AddEvent(GridTurnEventKind.ProjectileHit, "远程弹体命中。",
                        projectile.Id, from, projectile.Cell, projectile.Damage);
                    if (Phase != GridTurnPhase.WorldTurn)
                        return;
                    continue;
                }
                AddEvent(GridTurnEventKind.ProjectileMoved, "", projectile.Id, from,
                    projectile.Cell);
            }
        }

        private bool CanProjectileEnter(Vector2Int cell)
        {
            var tile = tiles[cell.x, cell.y];
            return tile == GridTileKind.Floor || tile == GridTileKind.DamageTrap;
        }

        private void ResolveEnemyTelegraphs()
        {
            foreach (var enemy in enemies.Where(enemy => enemy.Alive &&
                                                          enemy.Phase ==
                                                          GridEnemyPhase.Telegraph).ToArray())
            {
                switch (enemy.Action)
                {
                    case GridEnemyAction.MeleeStrike:
                        if (enemy.DangerCells.Contains(PlayerCell))
                            ApplyPlayerDamage(1, "近战怪扑中了身体。");
                        AddEvent(GridTurnEventKind.EnemyAttack, "近战怪挥出利爪。",
                            enemy.Id, enemy.Cell, PlayerCell);
                        enemy.Phase = GridEnemyPhase.Recover;
                        enemy.RecoverTurns = 2;
                        break;
                    case GridEnemyAction.RangedStrike:
                        SpawnEnemyProjectile(enemy);
                        AddEvent(GridTurnEventKind.EnemyAttack,
                            "远程敌人开火。", enemy.Id, enemy.Cell,
                            enemy.DangerCells.Count > 0
                                ? enemy.DangerCells[enemy.DangerCells.Count - 1]
                                : enemy.Cell);
                        enemy.Phase = GridEnemyPhase.Recover;
                        enemy.RecoverTurns = 3;
                        break;
                    case GridEnemyAction.BossSlam:
                        if (enemy.DangerCells.Contains(PlayerCell))
                            ApplyPlayerDamage(1, "Boss 重击砸中了身体。");
                        AddEvent(GridTurnEventKind.EnemyAttack, "Boss 重击落地。",
                            enemy.Id, enemy.Cell, PlayerCell);
                        enemy.Phase = GridEnemyPhase.Recover;
                        enemy.RecoverTurns = 3;
                        break;
                    case GridEnemyAction.BossCharge:
                        ResolveBossCharge(enemy);
                        break;
                }
                enemy.Action = GridEnemyAction.None;
                enemy.DangerCells.Clear();
                if (enemy.Phase == GridEnemyPhase.Telegraph)
                    enemy.Phase = GridEnemyPhase.Recover;
                if (Phase != GridTurnPhase.WorldTurn)
                    return;
            }
        }

        private void ResolveBossCharge(GridEnemyState boss)
        {
            var direction = boss.LockedDirection;
            var hitWall = false;
            for (var step = 0; step < 4 && Phase == GridTurnPhase.WorldTurn; step++)
            {
                var target = boss.Cell + direction;
                if (!InBounds(target) || !IsEnemyWalkable(target) ||
                    EnemyAt(target) != null && EnemyAt(target) != boss)
                {
                    hitWall = true;
                    break;
                }
                var from = boss.Cell;
                boss.Cell = target;
                AddEvent(GridTurnEventKind.EnemyMoved, "", boss.Id, from, boss.Cell);
                if (boss.Cell == PlayerCell)
                {
                    ApplyPlayerDamage(1, "Boss 冲锋撞中了身体。");
                    break;
                }
                ResolveGhostTouch(boss);
            }
            boss.Phase = GridEnemyPhase.Recover;
            boss.RecoverTurns = hitWall ? 4 : 3;
            AddEvent(GridTurnEventKind.EnemyAttack,
                hitWall ? "Boss 撞墙失衡。" : "Boss 冲锋结束。", boss.Id,
                boss.Cell, boss.Cell);
        }

        private void ThinkIdleEnemies()
        {
            foreach (var enemy in enemies.Where(enemy => enemy.Alive &&
                                                          enemy.Phase == GridEnemyPhase.Idle)
                         .ToArray())
            {
                if (Phase != GridTurnPhase.WorldTurn)
                    return;
                if (enemy.Cooldown > 0)
                    continue;
                switch (enemy.Kind)
                {
                    case GridActorKind.Melee:
                        ThinkMelee(enemy);
                        break;
                    case GridActorKind.Ranged:
                        ThinkRanged(enemy);
                        break;
                    case GridActorKind.Boss:
                        ThinkBoss(enemy);
                        break;
                }
            }
        }

        private void ThinkMelee(GridEnemyState enemy)
        {
            var distance = Manhattan(enemy.Cell, PlayerCell);
            if (distance <= 4 && HasClearMeleeLine(enemy.Cell, PlayerCell))
            {
                SetTelegraph(enemy, GridEnemyAction.MeleeStrike,
                    new List<Vector2Int> { PlayerCell }, Vector2Int.zero, PlayerCell);
                AddEvent(GridTurnEventKind.EnemyTelegraph,
                    "近战怪锁定脚下的红色地板，移动离开即可躲避。",
                    enemy.Id, enemy.Cell, PlayerCell);
                return;
            }
            if (distance > 6)
                return;
            var path = FindPath(enemy.Cell, PlayerCell, enemy);
            if (path.Count == 0)
                return;
            var next = path[0];
            if (EnemyAt(next) != null)
                return;
            var from = enemy.Cell;
            enemy.Cell = next;
            enemy.Cooldown = 1;
            AddEvent(GridTurnEventKind.EnemyMoved, "近战怪逼近一格。", enemy.Id, from,
                enemy.Cell);
            ResolveGhostTouch(enemy);
        }

        private bool HasClearMeleeLine(Vector2Int from, Vector2Int to)
        {
            var delta = to - from;
            var steps = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            if (steps <= 1)
                return true;
            for (var i = 1; i < steps; i++)
            {
                var x = from.x + Mathf.RoundToInt(delta.x * i / (float)steps);
                var y = from.y + Mathf.RoundToInt(delta.y * i / (float)steps);
                var cell = new Vector2Int(x, y);
                if (!InBounds(cell))
                    return false;
                var tile = tiles[cell.x, cell.y];
                if (tile == GridTileKind.Wall || tile == GridTileKind.MovableWall)
                    return false;
            }
            return true;
        }

        private void ThinkRanged(GridEnemyState enemy)
        {
            if (!TryCardinalLine(enemy.Cell, PlayerCell, 7, out var direction))
                return;
            var cells = RayCells(enemy.Cell, direction, 6);
            if (cells.Count == 0)
                return;
            SetTelegraph(enemy, GridEnemyAction.RangedStrike, cells, direction,
                cells[cells.Count - 1]);
            AddEvent(GridTurnEventKind.EnemyTelegraph, "远程怪锁定了一条直线。", enemy.Id,
                enemy.Cell, cells[cells.Count - 1]);
        }

        private void ThinkBoss(GridEnemyState boss)
        {
            if (!BossActive || boss.RecoverTurns > 0)
                return;
            if (Manhattan(boss.Cell, PlayerCell) == 1)
            {
                var cells = new List<Vector2Int>();
                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    var cell = boss.Cell + new Vector2Int(dx, dy);
                    if (InBounds(cell))
                        cells.Add(cell);
                }
                SetTelegraph(boss, GridEnemyAction.BossSlam, cells, Vector2Int.zero,
                    PlayerCell);
                AddEvent(GridTurnEventKind.EnemyTelegraph, "Boss 重击范围亮起。", boss.Id,
                    boss.Cell, PlayerCell);
                return;
            }
            if (TryCardinalLine(boss.Cell, PlayerCell, 5, out var direction))
            {
                SetTelegraph(boss, GridEnemyAction.BossCharge,
                    RayCells(boss.Cell, direction, 4), direction, PlayerCell);
                AddEvent(GridTurnEventKind.EnemyTelegraph, "Boss 冲锋路线锁定。", boss.Id,
                    boss.Cell, PlayerCell);
                return;
            }
            var path = FindPath(boss.Cell, PlayerCell, boss);
            if (path.Count == 0)
                return;
            var next = path[0];
            if (EnemyAt(next) != null)
                return;
            var from = boss.Cell;
            boss.Cell = next;
            boss.Cooldown = 1;
            AddEvent(GridTurnEventKind.EnemyMoved, "Boss 逼近。", boss.Id, from, boss.Cell);
            ResolveGhostTouch(boss);
        }

        private void SetTelegraph(GridEnemyState enemy, GridEnemyAction action,
            List<Vector2Int> danger, Vector2Int direction, Vector2Int target)
        {
            enemy.Phase = GridEnemyPhase.Telegraph;
            enemy.Action = action;
            enemy.LockedDirection = direction;
            enemy.LockedTarget = target;
            enemy.DangerCells.Clear();
            enemy.DangerCells.AddRange(danger);
        }

        private void SpawnEnemyProjectile(GridEnemyState enemy)
        {
            var direction = enemy.LockedDirection;
            var cell = enemy.Cell + direction;
            if (!InBounds(cell) || !CanProjectileEnter(cell))
                return;
            var projectile = new GridProjectileState
            {
                Id = nextProjectileId++,
                Friendly = false,
                Cell = cell,
                Direction = direction,
                Damage = 1,
                Life = 12
            };
            projectiles.Add(projectile);
            AddEvent(GridTurnEventKind.ProjectileSpawned, "远程弹体出现。",
                projectile.Id, enemy.Cell, cell);
        }

        private void TickRecoveries()
        {
            foreach (var enemy in enemies.Where(enemy => enemy.Alive))
            {
                if (enemy.Cooldown > 0)
                    enemy.Cooldown--;
                if (enemy.Phase != GridEnemyPhase.Recover)
                    continue;
                enemy.RecoverTurns--;
                if (enemy.RecoverTurns <= 0)
                {
                    enemy.Phase = GridEnemyPhase.Idle;
                    enemy.RecoverTurns = 0;
                }
            }
        }

        private void DamageEnemy(GridEnemyState enemy, int amount)
        {
            if (enemy == null || !enemy.Alive)
                return;
            var wasSecondPhase = enemy.IsBossSecondPhase;
            enemy.Hp = Mathf.Max(0, enemy.Hp - amount);
            AddEvent(GridTurnEventKind.EnemyHurt, "", enemy.Id, enemy.Cell, enemy.Cell,
                amount);
            if (enemy.Kind == GridActorKind.Boss && !wasSecondPhase &&
                enemy.IsBossSecondPhase)
            {
                AddEvent(GridTurnEventKind.BossPhaseChanged, "Boss 进入二阶段。", enemy.Id,
                    enemy.Cell, enemy.Cell);
            }
            if (enemy.Hp > 0)
                return;

            enemy.Alive = false;
            enemy.Phase = GridEnemyPhase.Idle;
            enemy.DangerCells.Clear();
            AddEvent(GridTurnEventKind.EnemyDied, "敌人倒下。", enemy.Id, enemy.Cell,
                enemy.Cell);
            if (enemy.Kind == GridActorKind.Boss)
            {
                BossDefeated = true;
                BossActive = false;
                ExitOpen = true;
                AddEvent(GridTurnEventKind.BossDefeated, "守门者倒下，出口打开。",
                    enemy.Id, enemy.Cell, map.Exit);
            }
            else
            {
                shadowDrops.Add(enemy.Cell);
            }
        }

        private void ApplyPlayerDamage(int amount, string text)
        {
            if (damagedThisWorldTurn || PlayerHp <= 0 ||
                Phase == GridTurnPhase.Dead || Phase == GridTurnPhase.Lost)
                return;
            damagedThisWorldTurn = true;
            PlayerHp = Mathf.Max(0, PlayerHp - amount);
            AddEvent(GridTurnEventKind.PlayerHurt, text, amount: amount);
            if (HasMemory("M2A") && !hitResponseUsed)
            {
                hitResponseUsed = true;
                Shadow = Mathf.Min(MaxShadow, Shadow + 2);
            }
            if (PlayerHp <= 0)
                KillPlayer();
        }

        private void KillPlayer()
        {
            if (Phase == GridTurnPhase.Dead || Phase == GridTurnPhase.Lost)
                return;
            Lives = Mathf.Max(0, Lives - 1);
            Recording = false;
            SoulActive = false;
            BattleGhost = null;
            if (Lives <= 0)
            {
                Phase = GridTurnPhase.Lost;
                AddEvent(GridTurnEventKind.Defeat, "最后一次灯火熄灭。");
            }
            else
            {
                Phase = GridTurnPhase.Dead;
                AddEvent(GridTurnEventKind.Defeat, "身体倒下，准备重生。");
            }
        }

        private void OpenMemory(int crystalIndex)
        {
            openMemoryIndex = crystalIndex;
            activeMemoryChoices.Clear();
            activeMemoryChoices.AddRange(MemoryDefinitions[Mathf.Clamp(crystalIndex, 0, 1)]);
            Phase = GridTurnPhase.MemoryChoice;
            AddEvent(GridTurnEventKind.MemoryOffer, "记忆冰晶正在等待选择。");
        }

        private void ApplyRecordingStep()
        {
            if (!Recording)
                return;
            if (Shadow <= 0)
            {
                FinishRecording("影子格数耗尽，路线凝固。");
                return;
            }
            Shadow--;
            if (recordedOrder.Count == 0 || recordedOrder[recordedOrder.Count - 1] !=
                PlayerCell)
            {
                recordedOrder.Add(PlayerCell);
                recordedPath.Add(PlayerCell);
            }
            if (Shadow <= 0)
                FinishRecording("最后一点影子凝固成战斗残影。");
        }

        private void FinishRecording(string message)
        {
            Recording = false;
            if (recordedPath.Count <= 1)
            {
                AddMessage("路线太短，没有形成战斗残影。");
                return;
            }
            BattleGhost = new GridBattleGhostState
            {
                Damage = HasMemory("M1A") ? 3 : 2,
                Life = HasMemory("M1B") ? 9 : 6
            };
            BattleGhost.Path.AddRange(recordedOrder);
            foreach (var cell in recordedPath)
                BattleGhost.DamageCells.Add(cell);
            if (HasMemory("M2B"))
                Shadow = Mathf.Min(MaxShadow, Shadow + 3);
            AddEvent(GridTurnEventKind.GhostSpawned, message);
        }

        private void PickupAt(Vector2Int cell)
        {
            if (map.Lights.Contains(cell) && !collectedLights.Contains(cell) &&
                Light < MaxLight)
            {
                collectedLights.Add(cell);
                Light = Mathf.Min(MaxLight, Light + 3);
                AddEvent(GridTurnEventKind.Pickup, "拾得微光，光 +3。", from: cell,
                    to: cell, amount: 3);
            }
            if (shadowDrops.Remove(cell))
            {
                Shadow = Mathf.Min(MaxShadow, Shadow + 2);
                AddEvent(GridTurnEventKind.Pickup, "拾得敌人余烬，影子 +2。", from: cell,
                    to: cell, amount: 2);
            }
        }

        private void AddTrail(Vector2Int cell)
        {
            currentTrail.Add(cell);
            explored[cell.x, cell.y] = true;
        }

        private void RefreshVision()
        {
            Array.Clear(visible, 0, visible.Length);
            RevealCardinalVision(PlayerCell);
            if (SoulActive)
                RevealCardinalVision(SoulCell);
            foreach (var cell in currentTrail)
                explored[cell.x, cell.y] = true;
            foreach (var cell in inheritedTrail)
                explored[cell.x, cell.y] = true;
            foreach (var cell in soulTrail)
                explored[cell.x, cell.y] = true;
        }

        private void RevealCardinalVision(Vector2Int center)
        {
            RevealCell(center);
            foreach (var direction in CardinalDirections)
                RevealCell(center + direction);
        }

        private void RevealCell(Vector2Int cell)
        {
            if (!InBounds(cell))
                return;
            visible[cell.x, cell.y] = true;
            explored[cell.x, cell.y] = true;
            if (tiles[cell.x, cell.y] == GridTileKind.DamageTrap)
                trapKnown[cell.x, cell.y] = true;
        }

        private void MarkTrapKnownAroundTarget(Vector2Int cell)
        {
            if (tiles[cell.x, cell.y] == GridTileKind.DamageTrap)
                trapKnown[cell.x, cell.y] = true;
        }

        private void RecallSoul(string message)
        {
            if (!SoulActive)
                return;
            SoulActive = false;
            SoulCell = PlayerCell;
            RefreshVision();
            AddEvent(GridTurnEventKind.SoulToggled, message, from: SoulCell, to: PlayerCell);
        }

        private void ActivateBossIfNeeded()
        {
            if (BossActive || BossDefeated || !map.BossTrigger.Contains(PlayerCell))
                return;
            var boss = Boss;
            if (boss == null || !boss.Alive)
                return;
            BossActive = true;
            AddEvent(GridTurnEventKind.BossAwake, "Boss 苏醒。", boss.Id, boss.Cell,
                PlayerCell);
        }

        private void ResolveGhostTouch(GridEnemyState enemy)
        {
            if (BattleGhost == null || BattleGhost.HitEnemies.Contains(enemy.Id) ||
                !BattleGhost.DamageCells.Contains(enemy.Cell))
                return;
            BattleGhost.HitEnemies.Add(enemy.Id);
            DamageEnemy(enemy, BattleGhost.Damage);
            AddEvent(GridTurnEventKind.GhostHit, "残影击中移动中的敌人。", enemy.Id,
                enemy.Cell, enemy.Cell, BattleGhost.Damage);
        }

        private void DecayBattleGhost()
        {
            if (BattleGhost == null)
                return;
            BattleGhost.Life--;
            if (BattleGhost.Life <= 0)
            {
                BattleGhost = null;
                AddEvent(GridTurnEventKind.Message, "战斗残影散去。");
            }
        }

        private void Win()
        {
            Phase = GridTurnPhase.Won;
            AddEvent(GridTurnEventKind.Victory, "你从错误的路线里带回了答案。");
        }

        private void CheckVictory()
        {
            if (ExitOpen && PlayerCell == map.Exit)
                Win();
        }

        private void ResetMap()
        {
            Array.Copy(map.InitialTiles, tiles, map.InitialTiles.Length);
            Array.Clear(explored, 0, explored.Length);
            Array.Clear(visible, 0, visible.Length);
            Array.Clear(trapKnown, 0, trapKnown.Length);
        }

        private void ResetMapPreservingKnowledge()
        {
            var knownTraps = new HashSet<Vector2Int>();
            for (var x = 0; x < map.Width; x++)
            for (var y = 0; y < map.Height; y++)
            {
                if (trapKnown[x, y])
                    knownTraps.Add(new Vector2Int(x, y));
            }
            Array.Copy(map.InitialTiles, tiles, map.InitialTiles.Length);
            Array.Clear(visible, 0, visible.Length);
            foreach (var cell in knownTraps)
                trapKnown[cell.x, cell.y] = true;
        }

        private void SpawnEnemies()
        {
            enemies.Clear();
            foreach (var spec in map.Enemies)
            {
                enemies.Add(new GridEnemyState
                {
                    Id = spec.Id,
                    Kind = spec.Kind,
                    Spawn = spec.Cell,
                    Cell = spec.Cell,
                    MarkerIndex = spec.MarkerIndex,
                    Hp = spec.Hp,
                    MaxHp = spec.Hp
                });
            }
        }

        private List<Vector2Int> FindPath(Vector2Int start, Vector2Int target,
            GridEnemyState mover)
        {
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == target)
                    break;
                foreach (var direction in CardinalDirections)
                {
                    var next = current + direction;
                    if (!InBounds(next) || visited.Contains(next) ||
                        !IsEnemyWalkable(next))
                        continue;
                    var occupied = EnemyAt(next);
                    if (occupied != null && occupied != mover && next != target)
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

        private bool TryCardinalLine(Vector2Int from, Vector2Int to, int maxDistance,
            out Vector2Int direction)
        {
            direction = Vector2Int.zero;
            var delta = to - from;
            if (delta.x == 0 && delta.y != 0 && Mathf.Abs(delta.y) <= maxDistance)
                direction = new Vector2Int(0, Math.Sign(delta.y));
            else if (delta.y == 0 && delta.x != 0 && Mathf.Abs(delta.x) <= maxDistance)
                direction = new Vector2Int(Math.Sign(delta.x), 0);
            if (direction == Vector2Int.zero)
                return false;
            return RayCells(from, direction, maxDistance).Contains(to);
        }

        private List<Vector2Int> RayCells(Vector2Int from, Vector2Int direction, int max)
        {
            var cells = new List<Vector2Int>();
            for (var distance = 1; distance <= max; distance++)
            {
                var cell = from + direction * distance;
                if (!InBounds(cell))
                    break;
                var tile = tiles[cell.x, cell.y];
                if (tile == GridTileKind.Wall || tile == GridTileKind.MovableWall)
                    break;
                cells.Add(cell);
            }
            return cells;
        }

        private bool IsEnemyWalkable(Vector2Int cell)
        {
            var tile = tiles[cell.x, cell.y];
            return tile == GridTileKind.Floor || tile == GridTileKind.DamageTrap;
        }

        private bool InBounds(Vector2Int cell)
        {
            return map.InBounds(cell);
        }

        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static bool IsCardinal(Vector2Int direction)
        {
            return Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1;
        }

        private static bool IsAimDirection(Vector2Int direction)
        {
            return AimDirections.Contains(direction);
        }

        private static Vector2Int Cardinalize(Vector2Int direction)
        {
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
                return direction.x >= 0 ? Vector2Int.right : Vector2Int.left;
            return direction.y >= 0 ? Vector2Int.up : Vector2Int.down;
        }

        private void AddMessage(string text)
        {
            AddEvent(GridTurnEventKind.Message, text);
        }

        private void AddEvent(GridTurnEventKind kind, string text, int id = 0,
            Vector2Int from = default, Vector2Int to = default, int amount = 0)
        {
            events.Add(new GridTurnEvent
            {
                Kind = kind,
                Text = text,
                Id = id,
                From = from,
                To = to,
                Amount = amount
            });
        }
    }
}
