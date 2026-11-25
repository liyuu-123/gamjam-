using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WrongTurn.TurnBased;

namespace WrongTurn.EditorTools
{
    public static class TurnBasedRuleTests
    {
        public static void RunAll(ICollection<string> failures)
        {
            TestCardinalVision(failures);
            TestTrapDamage(failures);
            TestDirectionalAttack(failures);
            TestRangedDirectionalStrike(failures);
            TestMeleeWarningCanBeDodged(failures);
            TestNearbyEnemyDetection(failures);
            TestSoulWalksThroughWalls(failures);
            TestBodyCanBeHitWhileSoulActive(failures);
            TestShadowCellCost(failures);
            TestLightPickupOnce(failures);
            TestCrystalConsumption(failures);
            TestBossSecondPhase(failures);
            TestRespawnKeepsBossHealthWithoutAggro(failures);
        }

        private static void TestCardinalVision(ICollection<string> failures)
        {
            var session = NewSession();
            session.StartNewRun();
            var player = session.PlayerCell;
            Check(session.IsVisible(player + Vector2Int.up), "North vision missing.", failures);
            Check(session.IsVisible(player + Vector2Int.right), "East vision missing.", failures);
            Check(!session.IsVisible(player + new Vector2Int(1, 1)),
                "Diagonal vision should remain dark.", failures);
            Check(!session.IsVisible(player + new Vector2Int(2, 1)),
                "Unwalked distant tile should remain dark.", failures);
        }

        private static void TestTrapDamage(ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.InitialTiles[2, 1] = GridTileKind.DamageTrap;
            var session = new GridTurnSession(map);
            session.StartNewRun();
            session.TryPlayerMove(Vector2Int.right);
            Check(session.PlayerHp == 2, "Trap did not remove one HP.", failures);
            Check(session.IsTrapKnown(new Vector2Int(2, 1)),
                "Stepped trap should become known.", failures);
        }

        private static void TestDirectionalAttack(ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.Enemies.Add(new GridEnemySpec
            {
                Id = 1,
                Kind = GridActorKind.Melee,
                Cell = new Vector2Int(2, 1),
                Hp = 3
            });
            var session = new GridTurnSession(map);
            session.StartNewRun();
            session.TryAttack(Vector2Int.right);
            var enemy = session.Enemies[0];
            Check(enemy.Hp == 2, "Directional attack did not hit an adjacent enemy.",
                failures);
            Check(session.Phase == GridTurnPhase.PlayerTurn,
                "Directional attack did not complete the world turn.", failures);

            var blockedMap = NewMap(8, 8);
            blockedMap.InitialTiles[2, 1] = GridTileKind.Wall;
            blockedMap.Enemies.Add(new GridEnemySpec
            {
                Id = 1,
                Kind = GridActorKind.Melee,
                Cell = new Vector2Int(3, 1),
                Hp = 3
            });
            var blockedSession = new GridTurnSession(blockedMap);
            blockedSession.StartNewRun();
            blockedSession.TryAttack(Vector2Int.right);
            Check(blockedSession.Enemies[0].Hp == 3,
                "Directional attack passed through a wall.", failures);
        }

        private static void TestSoulWalksThroughWalls(ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.InitialTiles[2, 1] = GridTileKind.Wall;
            map.InitialTiles[3, 1] = GridTileKind.Floor;
            var session = new GridTurnSession(map);
            session.StartNewRun();
            var body = session.PlayerCell;
            Check(session.ToggleSoul(), "Soul did not activate.", failures);
            var shadow = session.Shadow;
            Check(session.TrySoulMove(Vector2Int.right),
                "Soul move through a wall failed.", failures);
            Check(session.SoulCell == new Vector2Int(2, 1),
                "Soul did not enter the wall cell.", failures);
            Check(session.PlayerCell == body, "Body moved while soul was active.", failures);
            Check(session.Shadow == shadow - 1, "Soul move did not cost one shadow cell.",
                failures);
        }

        private static void TestRangedDirectionalStrike(ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.Enemies.Add(new GridEnemySpec
            {
                Id = 1,
                Kind = GridActorKind.Ranged,
                Cell = new Vector2Int(5, 1),
                Hp = 3
            });
            var session = new GridTurnSession(map);
            session.StartNewRun();
            session.WaitTurn();
            var ranged = session.Enemies[0];
            Check(ranged.Phase == GridEnemyPhase.Telegraph &&
                  ranged.Action == GridEnemyAction.RangedStrike &&
                  ranged.DangerCells.Contains(session.PlayerCell),
                "Ranged enemy did not lock a directional strike.", failures);
            session.WaitTurn();
            Check(session.Projectiles.Count == 1,
                "Ranged enemy did not fire on the second world turn.", failures);
            Check(session.PlayerHp == GridTurnSession.PlayerMaxHp,
                "Ranged bullet damaged the player before travelling.", failures);
            session.WaitTurn();
            session.WaitTurn();
            session.WaitTurn();
            Check(session.PlayerHp == GridTurnSession.PlayerMaxHp - 1,
                "Ranged bullet did not damage the player after travelling.", failures);
        }

        private static void TestMeleeWarningCanBeDodged(ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.Enemies.Add(new GridEnemySpec
            {
                Id = 1,
                Kind = GridActorKind.Melee,
                Cell = new Vector2Int(4, 1),
                Hp = 3
            });
            var dodged = new GridTurnSession(map);
            dodged.StartNewRun();
            dodged.WaitTurn();
            var enemy = dodged.Enemies[0];
            Check(enemy.Phase == GridEnemyPhase.Telegraph &&
                  enemy.DangerCells.Contains(dodged.PlayerCell),
                "Melee enemy did not mark the player tile within four cells.",
                failures);
            dodged.TryPlayerMove(Vector2Int.up);
            Check(dodged.PlayerHp == GridTurnSession.PlayerMaxHp,
                "Melee attack still hit after the player left the marked tile.",
                failures);

            var stayed = new GridTurnSession(NewMapWithMeleeAtFour());
            stayed.StartNewRun();
            stayed.WaitTurn();
            stayed.WaitTurn();
            Check(stayed.PlayerHp == GridTurnSession.PlayerMaxHp - 1,
                "Melee attack did not hit when the player stayed on the marked tile.",
                failures);
        }

        private static GridMapData NewMapWithMeleeAtFour()
        {
            var map = NewMap(8, 8);
            map.Enemies.Add(new GridEnemySpec
            {
                Id = 1,
                Kind = GridActorKind.Melee,
                Cell = new Vector2Int(4, 1),
                Hp = 3
            });
            return map;
        }

        private static void TestNearbyEnemyDetection(ICollection<string> failures)
        {
            var session = NewSession();
            session.StartNewRun();
            var cell = session.PlayerCell + new Vector2Int(2, 0);
            Check(!session.IsVisible(cell), "Test cell should remain outside light.",
                failures);
            Check(session.IsEnemyDetected(cell),
                "Enemy within two cells was not detected in darkness.", failures);
        }

        private static void TestBodyCanBeHitWhileSoulActive(ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.Enemies.Add(new GridEnemySpec
            {
                Id = 1,
                Kind = GridActorKind.Melee,
                Cell = new Vector2Int(2, 1),
                Hp = 3
            });
            var session = new GridTurnSession(map);
            session.StartNewRun();
            session.ToggleSoul();
            session.WaitTurn();
            session.WaitTurn();
            Check(session.PlayerHp < GridTurnSession.PlayerMaxHp,
                "Body was not vulnerable while the soul was active.", failures);
        }

        private static void TestShadowCellCost(ICollection<string> failures)
        {
            var session = NewSession();
            session.StartNewRun();
            var shadow = session.Shadow;
            session.ToggleRecording();
            session.TryPlayerMove(Vector2Int.right);
            Check(session.Shadow == shadow - 1,
                "Recorded step did not consume exactly one shadow cell.", failures);
        }

        private static void TestBossSecondPhase(ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.BossTrigger = new RectInt(0, 0, 8, 8);
            map.Enemies.Add(new GridEnemySpec
            {
                Id = 1,
                Kind = GridActorKind.Boss,
                Cell = new Vector2Int(5, 5),
                Hp = 10
            });
            var session = new GridTurnSession(map);
            session.StartNewRun();
            session.TryPlayerMove(Vector2Int.right);
            var boss = session.Boss;
            var damage = typeof(GridTurnSession).GetMethod("DamageEnemy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            damage.Invoke(session, new object[] { boss, 5 });
            Check(boss.IsBossSecondPhase, "Boss did not enter phase two at half HP.",
                failures);
        }

        private static void TestRespawnKeepsBossHealthWithoutAggro(
            ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.BossTrigger = new RectInt(0, 0, 8, 8);
            map.Enemies.Add(new GridEnemySpec
            {
                Id = 1,
                Kind = GridActorKind.Boss,
                Cell = new Vector2Int(5, 5),
                Hp = 10
            });
            var session = new GridTurnSession(map);
            session.StartNewRun();
            session.TryPlayerMove(Vector2Int.right);
            var boss = session.Boss;
            var damage = typeof(GridTurnSession).GetMethod("DamageEnemy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            damage.Invoke(session, new object[] { boss, 3 });
            var kill = typeof(GridTurnSession).GetMethod("KillPlayer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            kill.Invoke(session, null);
            session.Respawn();
            Check(session.PlayerCell == map.PlayerSpawn,
                "Respawn did not use the fixed player spawn cell.", failures);
            Check(session.Boss.Hp == 7,
                "Respawn did not preserve Boss health.", failures);
            Check(!session.BossActive,
                "Boss re-acquired the player immediately after respawn.", failures);
            session.WaitTurn();
            Check(session.Boss.DangerCells.Count == 0,
                "Inactive Boss still chose a target after respawn.", failures);
        }

        private static void TestLightPickupOnce(ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.Lights.Add(new Vector2Int(2, 1));
            var session = new GridTurnSession(map);
            session.StartNewRun();
            session.TryPlayerMove(Vector2Int.right);
            var light = session.Light;
            session.TryPlayerMove(Vector2Int.left);
            session.TryPlayerMove(Vector2Int.right);
            Check(light == 8 && session.Light == 8,
                "The same light tile granted light more than once.", failures);
            Check(!session.IsLightAvailable(new Vector2Int(2, 1)),
                "Collected light remained available.", failures);
        }

        private static void TestCrystalConsumption(ICollection<string> failures)
        {
            var map = NewMap(8, 8);
            map.Crystals.Add(new Vector2Int(2, 1));
            var session = new GridTurnSession(map);
            session.StartNewRun();
            Check(session.TryInteract(), "Crystal interaction did not open memory choice.",
                failures);
            Check(session.Phase == GridTurnPhase.MemoryChoice,
                "Crystal did not enter memory choice.", failures);
            session.SelectMemory(0);
            Check(!session.IsCrystalAvailable(new Vector2Int(2, 1)),
                "Used crystal remained available.", failures);
        }

        private static GridTurnSession NewSession()
        {
            return new GridTurnSession(NewMap(8, 8));
        }

        private static GridMapData NewMap(int width, int height)
        {
            var map = new GridMapData(width, height)
            {
                PlayerSpawn = new Vector2Int(1, 1),
                Exit = new Vector2Int(width - 2, height - 2),
                BossTrigger = new RectInt(0, 0, 0, 0)
            };
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                map.InitialTiles[x, y] = x == 0 || y == 0 || x == width - 1 ||
                                         y == height - 1
                    ? GridTileKind.Wall
                    : GridTileKind.Floor;
            return map;
        }

        private static void Check(bool condition, string message,
            ICollection<string> failures)
        {
            if (!condition)
                failures.Add(message);
        }
    }
}
