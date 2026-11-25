using UnityEngine;

namespace WrongTurn.TurnBased
{
    public sealed class GridTileView : MonoBehaviour
    {
        public SpriteRenderer Terrain;
        public SpriteRenderer Fog;
        public SpriteRenderer Trap;
        public SpriteRenderer Trail;
        public SpriteRenderer Danger;
        public SpriteRenderer Drop;

        private static readonly Color VisibleTint = new Color(0.66f, 0.66f, 0.66f, 1f);
        private static readonly Color ExploredTint = new Color(0.20f, 0.22f, 0.23f, 1f);

        public void Render(GridTileKind kind, bool explored, bool visible,
            bool trapKnown, bool trail, bool shadowDrop, bool danger)
        {
            var hasTerrain = Terrain != null && Terrain.sprite != null;
            if (hasTerrain)
            {
                Terrain.enabled = explored;
                Terrain.color = visible ? VisibleTint : ExploredTint;
            }

            if (Fog != null)
            {
                Fog.enabled = !visible;
                Fog.color = explored
                    ? new Color(0.015f, 0.018f, 0.022f, 0.78f)
                    : new Color(0.005f, 0.006f, 0.008f, 1f);
            }

            if (Trap != null)
            {
                Trap.enabled = explored && trapKnown &&
                               kind == GridTileKind.DamageTrap;
                Trap.color = visible
                    ? new Color(0.35f, 1f, 0.42f, 0.95f)
                    : new Color(0.16f, 0.55f, 0.22f, 0.62f);
            }

            if (Trail != null)
            {
                Trail.enabled = explored && trail;
                Trail.color = visible
                    ? new Color(0.25f, 0.92f, 1f, 0.34f)
                    : new Color(0.12f, 0.48f, 0.62f, 0.20f);
            }

            if (Danger != null)
            {
                Danger.enabled = danger;
                var pulse = 0.78f + Mathf.Sin(Time.unscaledTime * 7.5f) * 0.08f;
                Danger.color = visible
                    ? new Color(1f, 0.035f, 0.02f, pulse)
                    : new Color(0.88f, 0.02f, 0.02f, pulse * 0.82f);
            }

            if (Drop != null)
                Drop.enabled = explored && shadowDrop;
        }
    }
}
