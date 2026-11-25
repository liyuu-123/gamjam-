using UnityEngine;

namespace WrongTurn.TurnBased
{
    public sealed class GridEnemyHealthBar : MonoBehaviour
    {
        public Transform Fill;
        public SpriteRenderer FillRenderer;
        public SpriteRenderer Background;
        public float Width = 0.95f;
        public float Height = 0.08f;

        public void SetHealth(float ratio, bool visible, Color color)
        {
            gameObject.SetActive(visible);
            if (!visible || Fill == null)
                return;
            if (Background != null)
                Background.enabled = true;
            if (FillRenderer != null)
                FillRenderer.enabled = true;
            ratio = Mathf.Clamp01(ratio);
            Fill.localScale = new Vector3(Mathf.Max(0.001f, Width * ratio), Height, 1f);
            Fill.localPosition = new Vector3(-Width * (1f - ratio) * 0.5f, 0f, 0f);
            if (FillRenderer != null)
                FillRenderer.color = color;
        }
    }
}
