using System.Collections.Generic;
using UnityEngine;

namespace WrongTurn.TurnBased
{
    public sealed class GridMapView : MonoBehaviour
    {
        public int Width;
        public int Height;
        public List<GridTileView> Cells = new List<GridTileView>();

        public GridTileView CellAt(int x, int y)
        {
            var index = y * Width + x;
            return index >= 0 && index < Cells.Count ? Cells[index] : null;
        }

        public void Render(GridTurnSession session)
        {
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(x, y);
                var view = CellAt(x, y);
                if (view == null)
                    continue;
                view.Render(session.TileAt(cell), session.IsExplored(cell),
                    session.IsVisible(cell), session.IsTrapKnown(cell),
                    session.IsTrail(cell), session.IsShadowDrop(cell),
                    session.IsDangerCell(cell, out _));
            }
        }
    }
}
