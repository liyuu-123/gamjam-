using System;
using System.Collections.Generic;
using UnityEngine;

namespace WrongTurn
{
    public static class PixelArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Floor
        {
            get { return Get("floor", () => BuildFloor(false)); }
        }

        public static Sprite MovableFloor
        {
            get { return Get("movable-floor", () => BuildFloor(true)); }
        }

        public static Sprite Wall
        {
            get { return Get("wall", BuildWall); }
        }

        public static Sprite MovableWall
        {
            get { return Get("movable-wall", BuildMovableWall); }
        }

        public static Sprite Gap
        {
            get { return Get("gap", BuildGap); }
        }

        public static Sprite Player
        {
            get { return Get("player", BuildPlayer); }
        }

        public static Sprite Scout
        {
            get { return Get("scout", BuildScout); }
        }

        public static Sprite Melee
        {
            get { return Get("melee", () => BuildEnemy(EnemyKind.Melee)); }
        }

        public static Sprite Ranged
        {
            get { return Get("ranged", () => BuildEnemy(EnemyKind.Ranged)); }
        }

        public static Sprite Boss
        {
            get { return Get("boss", () => BuildEnemy(EnemyKind.Boss)); }
        }

        public static Sprite Crystal
        {
            get { return Get("crystal", BuildCrystal); }
        }

        public static Sprite Light
        {
            get { return Get("light", BuildLight); }
        }

        public static Sprite Glow
        {
            get { return Get("glow", BuildGlow); }
        }

        public static Sprite Shadow
        {
            get { return Get("shadow", BuildShadow); }
        }

        public static Sprite Arrow
        {
            get { return Get("arrow", BuildArrow); }
        }

        public static Sprite Ring
        {
            get { return Get("ring", BuildRing); }
        }

        public static Sprite Orb
        {
            get { return Get("orb", BuildOrb); }
        }

        public static Sprite Door
        {
            get { return Get("door", BuildDoor); }
        }

        public static Sprite Seal
        {
            get { return Get("seal", BuildSeal); }
        }

        public static Sprite White
        {
            get
            {
                return Get("white", () =>
                {
                    var tex = NewTexture(2, 2);
                    Fill(tex, Color.white);
                    tex.Apply();
                    return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                });
            }
        }

        public static Texture2D VignetteTexture
        {
            get
            {
                var key = "vignette-tex";
                if (Cache.ContainsKey(key))
                    return null;
                var tex = NewTexture(128, 72);
                for (int y = 0; y < 72; y++)
                for (int x = 0; x < 128; x++)
                {
                    float nx = Mathf.Abs((x / 127f) * 2f - 1f);
                    float ny = Mathf.Abs((y / 71f) * 2f - 1f);
                    float edge = Mathf.Clamp01((Mathf.Max(nx * 1.08f, ny * 1.13f) - 0.44f) / 0.56f);
                    float a = Mathf.SmoothStep(0f, 1f, edge) * 0.72f;
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
                tex.Apply();
                tex.name = "Vignette";
                return tex;
            }
        }

        public static Texture2D TextureGrid
        {
            get
            {
                var tex = NewTexture(16, 16);
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool line = x == 0 || y == 0;
                    tex.SetPixel(x, y, line ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0f));
                }
                tex.Apply();
                tex.name = "Grid";
                return tex;
            }
        }

        private static Sprite Get(string key, Func<Sprite> builder)
        {
            Sprite sprite;
            if (!Cache.TryGetValue(key, out sprite) || sprite == null)
            {
                sprite = builder();
                sprite.name = key;
                Cache[key] = sprite;
            }
            return sprite;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            return texture;
        }

        private static Sprite Finish(Texture2D texture, Vector2 pivot, float ppu = 32f)
        {
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, ppu);
        }

        private static void Fill(Texture2D texture, Color color, int x0 = -999, int y0 = -999,
            int width = -1, int height = -1)
        {
            if (x0 < -900)
            {
                x0 = 0;
                y0 = 0;
                width = texture.width;
                height = texture.height;
            }
            if (width < 0)
                width = texture.width - x0;
            if (height < 0)
                height = texture.height - y0;
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(texture.height, y0 + height); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(texture.width, x0 + width); x++)
                texture.SetPixel(x, y, color);
        }

        private static void Rect(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            Fill(texture, color, x, y, width, height);
        }

        private static void Pixel(Texture2D texture, int x, int y, Color color)
        {
            if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                texture.SetPixel(x, y, color);
        }

        private static void Circle(Texture2D texture, float cx, float cy, float radius, Color color,
            bool hollow = false, float innerRadius = 0f)
        {
            int minX = Mathf.FloorToInt(cx - radius);
            int maxX = Mathf.CeilToInt(cx + radius);
            int minY = Mathf.FloorToInt(cy - radius);
            int maxY = Mathf.CeilToInt(cy + radius);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= radius && (!hollow || d >= innerRadius))
                    Pixel(texture, x, y, color);
            }
        }

        private static void Line(Texture2D texture, int x0, int y0, int x1, int y1, int width, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx - dy;
            while (true)
            {
                Rect(texture, x0 - width / 2, y0 - width / 2, width, width, color);
                if (x0 == x1 && y0 == y1)
                    break;
                int e2 = error * 2;
                if (e2 > -dy)
                {
                    error -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static Sprite BuildFloor(bool seamVariant)
        {
            var t = NewTexture(32, 32);
            Fill(t, new Color(0.33f, 0.34f, 0.37f, 1f));
            Rect(t, 0, 0, 32, 1, new Color(0.15f, 0.16f, 0.19f, 1f));
            Rect(t, 0, 31, 32, 1, new Color(0.18f, 0.19f, 0.22f, 1f));
            Rect(t, 0, 0, 1, 32, new Color(0.18f, 0.19f, 0.22f, 1f));
            Rect(t, 31, 0, 1, 32, new Color(0.17f, 0.18f, 0.20f, 1f));
            Rect(t, 2, 2, 28, 1, new Color(0.38f, 0.39f, 0.43f, 1f));
            Rect(t, 2, 2, 1, 28, new Color(0.39f, 0.40f, 0.44f, 1f));
            Rect(t, 15, 2, 1, 28, new Color(0.24f, 0.25f, 0.28f, 1f));
            Rect(t, 2, 15, 28, 1, new Color(0.24f, 0.25f, 0.28f, 1f));
            int seed = seamVariant ? 71 : 19;
            for (int i = 0; i < 18; i++)
            {
                seed = (seed * 31 + 17) & 0x7fffffff;
                int x = 3 + seed % 26;
                seed = (seed * 47 + 9) & 0x7fffffff;
                int y = 3 + seed % 26;
                Pixel(t, x, y, i % 3 == 0
                    ? new Color(0.5f, 0.51f, 0.53f, 1f)
                    : new Color(0.22f, 0.23f, 0.26f, 1f));
            }
            if (seamVariant)
            {
                Pixel(t, 15, 22, new Color(0.85f, 0.63f, 0.26f, 1f));
                Pixel(t, 16, 21, new Color(0.55f, 0.4f, 0.2f, 1f));
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildWall()
        {
            var t = NewTexture(32, 48);
            Fill(t, new Color(0.12f, 0.13f, 0.16f, 1f));
            Rect(t, 1, 5, 30, 38, new Color(0.31f, 0.32f, 0.35f, 1f));
            Rect(t, 2, 37, 28, 6, new Color(0.43f, 0.43f, 0.45f, 1f));
            Rect(t, 3, 4, 26, 4, new Color(0.08f, 0.09f, 0.11f, 1f));
            Rect(t, 3, 11, 26, 1, new Color(0.19f, 0.20f, 0.23f, 1f));
            Rect(t, 3, 23, 26, 1, new Color(0.19f, 0.20f, 0.23f, 1f));
            Rect(t, 3, 10, 1, 28, new Color(0.20f, 0.21f, 0.24f, 1f));
            for (int i = 0; i < 9; i++)
            {
                int x = 5 + (i * 7) % 22;
                int y = 9 + (i * 11) % 23;
                Rect(t, x, y, 2, 1, new Color(0.24f, 0.25f, 0.27f, 1f));
            }
            Rect(t, 12, 18, 3, 12, new Color(0.18f, 0.19f, 0.22f, 1f));
            Rect(t, 13, 16, 1, 16, new Color(0.13f, 0.14f, 0.16f, 1f));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildMovableWall()
        {
            var t = NewTexture(32, 48);
            var wall = Wall.texture;
            for (int y = 0; y < 48; y++)
            for (int x = 0; x < 32; x++)
                t.SetPixel(x, y, wall.GetPixel(x, y));
            Rect(t, 14, 7, 2, 31, new Color(0.05f, 0.06f, 0.07f, 1f));
            Rect(t, 15, 12, 1, 24, new Color(0.76f, 0.55f, 0.24f, 0.85f));
            Rect(t, 9, 17, 5, 1, new Color(0.05f, 0.06f, 0.07f, 1f));
            Rect(t, 20, 25, 4, 1, new Color(0.05f, 0.06f, 0.07f, 1f));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildGap()
        {
            var t = NewTexture(32, 32);
            Fill(t, new Color(0.15f, 0.16f, 0.18f, 1f));
            Rect(t, 2, 2, 28, 28, new Color(0.04f, 0.05f, 0.07f, 1f));
            Rect(t, 4, 5, 24, 20, new Color(0.01f, 0.015f, 0.025f, 1f));
            Rect(t, 5, 25, 22, 2, new Color(0.28f, 0.27f, 0.26f, 1f));
            Pixel(t, 7, 8, new Color(0.38f, 0.53f, 0.58f, 0.9f));
            Pixel(t, 8, 8, new Color(0.24f, 0.38f, 0.42f, 0.8f));
            Pixel(t, 21, 14, new Color(0.37f, 0.54f, 0.59f, 0.65f));
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildPlayer()
        {
            var t = NewTexture(32, 42);
            // Shadow and coat tail.
            Circle(t, 16, 4, 11, new Color(0f, 0f, 0f, 0.35f));
            Rect(t, 10, 9, 12, 16, new Color(0.11f, 0.14f, 0.19f, 1f));
            Rect(t, 7, 9, 5, 9, new Color(0.09f, 0.11f, 0.16f, 1f));
            Rect(t, 20, 9, 5, 9, new Color(0.08f, 0.10f, 0.14f, 1f));
            Rect(t, 9, 16, 14, 14, new Color(0.19f, 0.24f, 0.31f, 1f));
            Rect(t, 11, 20, 10, 10, new Color(0.34f, 0.40f, 0.46f, 1f));
            Rect(t, 11, 22, 10, 3, new Color(0.92f, 0.58f, 0.23f, 1f));
            Rect(t, 13, 29, 7, 7, new Color(0.76f, 0.63f, 0.51f, 1f));
            Rect(t, 12, 34, 9, 3, new Color(0.12f, 0.14f, 0.17f, 1f));
            Rect(t, 14, 31, 1, 2, new Color(0.08f, 0.1f, 0.11f, 1f));
            Rect(t, 18, 31, 1, 2, new Color(0.08f, 0.1f, 0.11f, 1f));
            Rect(t, 15, 13, 3, 3, new Color(0.92f, 0.72f, 0.28f, 1f));
            Rect(t, 11, 7, 4, 2, new Color(0.08f, 0.09f, 0.11f, 1f));
            Rect(t, 18, 7, 4, 2, new Color(0.08f, 0.09f, 0.11f, 1f));
            Pixel(t, 14, 37, new Color(0.98f, 0.76f, 0.35f, 1f));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildScout()
        {
            var t = NewTexture(28, 34);
            Circle(t, 14, 17, 11, new Color(0.14f, 0.64f, 0.73f, 0.2f));
            Circle(t, 14, 17, 8, new Color(0.30f, 0.86f, 0.92f, 0.55f));
            Circle(t, 14, 17, 6, new Color(0.52f, 0.97f, 1f, 0.85f));
            Rect(t, 10, 11, 8, 12, new Color(0.71f, 0.99f, 1f, 0.9f));
            Rect(t, 8, 10, 12, 5, new Color(0.27f, 0.76f, 0.87f, 0.55f));
            Rect(t, 12, 19, 1, 2, new Color(0.04f, 0.23f, 0.3f, 0.8f));
            Rect(t, 16, 19, 1, 2, new Color(0.04f, 0.23f, 0.3f, 0.8f));
            Rect(t, 12, 28, 2, 2, new Color(0.7f, 1f, 1f, 0.8f));
            Rect(t, 19, 7, 1, 2, new Color(0.4f, 0.9f, 1f, 0.7f));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildEnemy(EnemyKind kind)
        {
            if (kind == EnemyKind.Boss)
                return BuildBoss();

            bool ranged = kind == EnemyKind.Ranged;
            var t = NewTexture(ranged ? 34 : 32, ranged ? 38 : 34);
            int cx = ranged ? 17 : 16;
            Color dark = ranged
                ? new Color(0.20f, 0.13f, 0.22f, 1f)
                : new Color(0.28f, 0.09f, 0.11f, 1f);
            Color mid = ranged
                ? new Color(0.48f, 0.24f, 0.48f, 1f)
                : new Color(0.61f, 0.18f, 0.21f, 1f);
            Circle(t, cx, 4, 12, new Color(0f, 0f, 0f, 0.4f));
            if (ranged)
            {
                Rect(t, cx - 9, 6, 18, 17, dark);
                Rect(t, cx - 7, 11, 14, 13, mid);
                Rect(t, cx - 5, 20, 10, 8, new Color(0.35f, 0.23f, 0.38f, 1f));
                Rect(t, cx - 6, 27, 12, 3, new Color(0.17f, 0.1f, 0.2f, 1f));
                Rect(t, cx - 4, 22, 2, 2, new Color(0.98f, 0.37f, 0.29f, 1f));
                Rect(t, cx + 2, 22, 2, 2, new Color(0.98f, 0.37f, 0.29f, 1f));
                Rect(t, cx + 5, 8, 12, 3, new Color(0.14f, 0.16f, 0.2f, 1f));
                Rect(t, cx + 14, 8, 4, 4, new Color(0.68f, 0.6f, 0.46f, 1f));
                Pixel(t, cx + 17, 10, new Color(0.96f, 0.7f, 0.28f, 1f));
            }
            else
            {
                Rect(t, cx - 10, 5, 20, 16, dark);
                Rect(t, cx - 8, 10, 16, 14, mid);
                Rect(t, cx - 6, 22, 12, 7, new Color(0.48f, 0.15f, 0.17f, 1f));
                Rect(t, cx - 7, 26, 3, 4, new Color(0.13f, 0.06f, 0.08f, 1f));
                Rect(t, cx + 4, 26, 3, 4, new Color(0.13f, 0.06f, 0.08f, 1f));
                Rect(t, cx - 4, 19, 2, 2, new Color(0.97f, 0.74f, 0.27f, 1f));
                Rect(t, cx + 2, 19, 2, 2, new Color(0.97f, 0.74f, 0.27f, 1f));
                Rect(t, cx - 10, 9, 3, 7, new Color(0.78f, 0.62f, 0.47f, 1f));
                Rect(t, cx + 7, 9, 3, 7, new Color(0.78f, 0.62f, 0.47f, 1f));
                Rect(t, cx - 7, 4, 5, 3, new Color(0.1f, 0.07f, 0.08f, 1f));
                Rect(t, cx + 2, 4, 5, 3, new Color(0.1f, 0.07f, 0.08f, 1f));
            }
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildBoss()
        {
            var t = NewTexture(56, 66);
            Circle(t, 28, 5, 21, new Color(0f, 0f, 0f, 0.5f));
            Rect(t, 8, 9, 40, 30, new Color(0.16f, 0.10f, 0.12f, 1f));
            Rect(t, 12, 17, 32, 30, new Color(0.33f, 0.12f, 0.15f, 1f));
            Rect(t, 16, 33, 24, 21, new Color(0.45f, 0.17f, 0.18f, 1f));
            Rect(t, 19, 47, 18, 8, new Color(0.30f, 0.11f, 0.13f, 1f));
            Rect(t, 17, 53, 5, 8, new Color(0.2f, 0.08f, 0.1f, 1f));
            Rect(t, 34, 53, 5, 8, new Color(0.2f, 0.08f, 0.1f, 1f));
            Rect(t, 20, 42, 4, 3, new Color(1f, 0.74f, 0.24f, 1f));
            Rect(t, 32, 42, 4, 3, new Color(1f, 0.74f, 0.24f, 1f));
            Rect(t, 5, 16, 9, 25, new Color(0.54f, 0.24f, 0.16f, 1f));
            Rect(t, 42, 16, 9, 25, new Color(0.54f, 0.24f, 0.16f, 1f));
            Rect(t, 6, 12, 9, 5, new Color(0.20f, 0.08f, 0.09f, 1f));
            Rect(t, 41, 12, 9, 5, new Color(0.20f, 0.08f, 0.09f, 1f));
            Rect(t, 18, 9, 7, 5, new Color(0.13f, 0.07f, 0.08f, 1f));
            Rect(t, 31, 9, 7, 5, new Color(0.13f, 0.07f, 0.08f, 1f));
            Rect(t, 20, 23, 16, 3, new Color(0.95f, 0.47f, 0.18f, 1f));
            Rect(t, 24, 18, 8, 5, new Color(0.18f, 0.07f, 0.08f, 1f));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildCrystal()
        {
            var t = NewTexture(28, 36);
            Circle(t, 14, 5, 9, new Color(0.25f, 0.66f, 0.9f, 0.24f));
            Line(t, 14, 7, 7, 22, 11, new Color(0.38f, 0.56f, 0.94f, 0.8f));
            Line(t, 14, 7, 21, 22, 11, new Color(0.42f, 0.76f, 1f, 0.9f));
            Line(t, 7, 22, 21, 22, 4, new Color(0.22f, 0.35f, 0.65f, 1f));
            Rect(t, 12, 12, 4, 21, new Color(0.62f, 0.91f, 1f, 1f));
            Rect(t, 9, 18, 7, 12, new Color(0.41f, 0.67f, 0.98f, 1f));
            Rect(t, 16, 20, 5, 9, new Color(0.35f, 0.53f, 0.87f, 1f));
            Pixel(t, 13, 27, Color.white);
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildLight()
        {
            var t = NewTexture(24, 28);
            Circle(t, 12, 15, 10, new Color(1f, 0.68f, 0.22f, 0.2f));
            Circle(t, 12, 16, 6, new Color(1f, 0.8f, 0.32f, 0.75f));
            Circle(t, 12, 17, 3, new Color(1f, 0.97f, 0.72f, 1f));
            Rect(t, 9, 4, 6, 9, new Color(0.43f, 0.3f, 0.17f, 1f));
            Rect(t, 8, 3, 8, 2, new Color(0.2f, 0.14f, 0.1f, 1f));
            Pixel(t, 12, 24, new Color(1f, 0.9f, 0.5f, 0.8f));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildGlow()
        {
            var t = NewTexture(64, 64);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = (x - 31.5f) / 31.5f;
                float dy = (y - 31.5f) / 31.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildShadow()
        {
            var t = NewTexture(32, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - 15.5f) / 15.5f;
                float dy = (y - 7.5f) / 7.5f;
                float d = dx * dx + dy * dy;
                if (d < 1f)
                    t.SetPixel(x, y, new Color(0f, 0f, 0f, 0.35f * (1f - d)));
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildArrow()
        {
            var t = NewTexture(12, 12);
            for (int y = 2; y < 10; y++)
            {
                int half = 5 - Mathf.Abs(y - 6);
                for (int x = 6 - half; x <= 6 + half; x++)
                    Pixel(t, x, y, new Color(1f, 0.8f, 0.35f, 0.95f));
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildRing()
        {
            var t = NewTexture(20, 20);
            Circle(t, 10, 10, 8, new Color(0.93f, 0.67f, 0.28f, 0.8f), true, 5.5f);
            Circle(t, 10, 10, 6, new Color(1f, 0.82f, 0.43f, 0.6f), true, 5f);
            Pixel(t, 10, 18, new Color(1f, 0.98f, 0.72f, 1f));
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildOrb()
        {
            var t = NewTexture(16, 16);
            Circle(t, 8, 8, 7, new Color(0.18f, 0.7f, 0.82f, 0.3f));
            Circle(t, 8, 8, 4, new Color(0.43f, 0.91f, 1f, 0.8f));
            Circle(t, 8, 8, 2, new Color(0.9f, 1f, 1f, 1f));
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildDoor()
        {
            var t = NewTexture(32, 48);
            Rect(t, 2, 2, 28, 42, new Color(0.12f, 0.13f, 0.16f, 1f));
            Rect(t, 5, 5, 22, 38, new Color(0.3f, 0.29f, 0.27f, 1f));
            Rect(t, 7, 7, 18, 34, new Color(0.18f, 0.17f, 0.17f, 1f));
            Rect(t, 9, 16, 2, 25, new Color(0.42f, 0.4f, 0.34f, 1f));
            Rect(t, 21, 9, 2, 32, new Color(0.4f, 0.38f, 0.32f, 1f));
            Rect(t, 15, 5, 2, 38, new Color(0.05f, 0.06f, 0.07f, 1f));
            Rect(t, 12, 22, 8, 2, new Color(0.7f, 0.54f, 0.23f, 1f));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildSeal()
        {
            var t = NewTexture(20, 20);
            Circle(t, 10, 10, 8, new Color(0.85f, 0.35f, 0.27f, 0.9f), true, 6f);
            Circle(t, 10, 10, 3, new Color(0.85f, 0.35f, 0.27f, 0.9f));
            return Finish(t, new Vector2(0.5f, 0.5f));
        }
    }
}
