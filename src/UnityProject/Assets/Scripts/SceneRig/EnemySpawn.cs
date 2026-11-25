using UnityEngine;
using WrongTurn.TurnBased;

namespace WrongTurn
{
    /// <summary>Enemy spawn marker: a live enemy is built here on every run start.</summary>
    public class EnemySpawn : MonoBehaviour
    {
        public int Id;
        public EnemyKindAction Kind = EnemyKindAction.Melee;
        public int Hp = 4;
        public SpriteRenderer Body;
        public SpriteRenderer Shadow;
        public GridEnemyHealthBar HealthBar;
    }
}
