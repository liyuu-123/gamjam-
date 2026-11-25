using System;
using System.Collections.Generic;
using UnityEngine;

namespace WrongTurn.TurnBased
{
    public enum GridTileKind
    {
        Wall,
        Floor,
        MovableWall,
        Gap,
        DamageTrap
    }

    public enum GridActorKind
    {
        Melee,
        Ranged,
        Boss
    }

    public enum GridEnemyPhase
    {
        Idle,
        Telegraph,
        Recover
    }

    public enum GridEnemyAction
    {
        None,
        MeleeStrike,
        RangedStrike,
        BossCharge,
        BossSlam
    }

    public enum GridTurnPhase
    {
        Title,
        PlayerTurn,
        WorldTurn,
        MemoryChoice,
        Paused,
        Dead,
        Won,
        Lost
    }

    public enum GridTurnEventKind
    {
        PlayerMoved,
        PlayerAttack,
        SoulMoved,
        SoulToggled,
        EnemyMoved,
        EnemyTelegraph,
        EnemyAttack,
        ProjectileSpawned,
        ProjectileMoved,
        ProjectileHit,
        PlayerHurt,
        EnemyHurt,
        EnemyDied,
        GhostSpawned,
        GhostHit,
        Pickup,
        BossAwake,
        BossPhaseChanged,
        BossDefeated,
        Victory,
        Defeat,
        MemoryOffer,
        MemoryChosen,
        Message
    }

    [Serializable]
    public sealed class GridEnemySpec
    {
        public int Id;
        public GridActorKind Kind;
        public Vector2Int Cell;
        public int Hp;
        public int MarkerIndex;
    }

    public sealed class GridMapData
    {
        public int Width;
        public int Height;
        public GridTileKind[,] InitialTiles;
        public Vector2Int PlayerSpawn;
        public Vector2Int Exit;
        public RectInt BossTrigger;
        public readonly List<GridEnemySpec> Enemies = new List<GridEnemySpec>();
        public readonly List<Vector2Int> Crystals = new List<Vector2Int>();
        public readonly List<Vector2Int> Lights = new List<Vector2Int>();

        public GridMapData(int width, int height)
        {
            Width = width;
            Height = height;
            InitialTiles = new GridTileKind[width, height];
            BossTrigger = new RectInt(0, 0, width, height);
        }

        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }
    }

    public sealed class GridEnemyState
    {
        public int Id;
        public GridActorKind Kind;
        public Vector2Int Spawn;
        public Vector2Int Cell;
        public int MarkerIndex;
        public int Hp;
        public int MaxHp;
        public bool Alive = true;
        public GridEnemyPhase Phase = GridEnemyPhase.Idle;
        public GridEnemyAction Action = GridEnemyAction.None;
        public Vector2Int LockedDirection;
        public Vector2Int LockedTarget;
        public int Cooldown;
        public int RecoverTurns;
        public readonly List<Vector2Int> DangerCells = new List<Vector2Int>();

        public bool IsBossSecondPhase => Kind == GridActorKind.Boss && Hp > 0 &&
                                         Hp <= MaxHp / 2;
    }

    public sealed class GridBattleGhostState
    {
        public readonly List<Vector2Int> Path = new List<Vector2Int>();
        public readonly HashSet<Vector2Int> DamageCells = new HashSet<Vector2Int>();
        public readonly HashSet<int> HitEnemies = new HashSet<int>();
        public int Life;
        public int Damage;
    }

    public sealed class GridProjectileState
    {
        public int Id;
        public bool Friendly;
        public Vector2Int Cell;
        public Vector2Int Direction;
        public int Damage;
        public int Life;
    }

    public sealed class GridMemoryDefinition
    {
        public string Id;
        public string Title;
        public string Short;
        public string Description;
    }

    public sealed class GridTurnEvent
    {
        public GridTurnEventKind Kind;
        public int Id;
        public int Amount;
        public Vector2Int From;
        public Vector2Int To;
        public string Text;
    }
}
