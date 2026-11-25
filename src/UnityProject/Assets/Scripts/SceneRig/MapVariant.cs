using UnityEngine;
using UnityEngine.Tilemaps;
using WrongTurn.TurnBased;

namespace WrongTurn
{
    /// <summary>
    /// Root of one editable map (forest / ice / desert). The controller reads the
    /// Layout tilemap into its grid on run start and spawns whatever markers are
    /// placed underneath, so the map can be painted and rearranged in the editor.
    /// </summary>
    public class MapVariant : MonoBehaviour
    {
        [Header("Identity")]
        public int MapId;
        public string DisplayName = "森林";
        public int Width = 28;
        public int Height = 20;
        public Vector2Int PlayerSpawnCell = new Vector2Int(4, 16);

        [Header("Stage look")]
        public Color32 Background = new Color32(9, 31, 41, 255);
        public Color EnemyTint = new Color(0.82f, 1f, 0.88f);

        [Header("Layout data (paint with the Tilemap palette)")]
        public Tilemap Layout;
        public TileBase FloorTile;
        public TileBase MovableWallTile;
        public TileBase GapTile;
        public TileBase TrapTile;
        public TileBase WallTile;

        [Header("Turn-based presentation")]
        public GridMapView GridView;
        public GridExitSpawn ExitSpawn;
    }
}
