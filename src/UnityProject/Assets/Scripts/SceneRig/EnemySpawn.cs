using UnityEngine;

namespace WrongTurn
{
    /// <summary>Enemy spawn marker: a live enemy is built here on every run start.</summary>
    public class EnemySpawn : MonoBehaviour
    {
        public EnemyKindAction Kind = EnemyKindAction.Melee;
        public int Hp = 4;
    }
}
