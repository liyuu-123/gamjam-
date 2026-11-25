using UnityEngine;
using UnityEngine.Tilemaps;

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

        [Header("Stage look")]
        public Color32 Background = new Color32(9, 31, 41, 255);
        public Color EnemyTint = new Color(0.82f, 1f, 0.88f);

        [Header("Layout data (paint with the Tilemap palette)")]
        public Tilemap Layout;
        public TileBase FloorTile;
        public TileBase MovableWallTile;
        public TileBase GapTile;
        public TileBase WallTile;
    }
}
