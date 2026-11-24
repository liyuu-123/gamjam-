using System;
using System.Collections.Generic;
using UnityEngine;

namespace WrongTurn
{
    /// <summary>
    /// Bright, dense pixel sprites authored procedurally at 64 pixels per world cell.
    /// No third-party or placeholder art is used.
    /// </summary>
    public static class PixelArtHD
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite FloorA => Get("floor-a", () => Floor(1));
        public static Sprite FloorB => Get("floor-b", () => Floor(2));
        public static Sprite FloorC => Get("floor-c", () => Floor(3));
        public static Sprite MovableFloor => Get("floor-seam", () => Floor(4));
        public static Sprite Wall => Get("wall", WallSprite);
        public static Sprite MovableWall => Get("wall-movable", MovableWallSprite);
        public static Sprite Gap => Get("gap", GapSprite);
        public static Sprite Player => Get("player-hd", PlayerSprite);
        public static Sprite Melee => Get("melee-hd", MeleeSprite);
        public static Sprite Ranged => Get("ranged-hd", RangedSprite);
        public static Sprite Boss => Get("boss-hd", BossSprite);
        public static Sprite Crystal => Get("crystal-hd", CrystalSprite);
        public static Sprite Light => Get("light-hd", LightSprite);
        public static Sprite Ring => Get("ring-hd", RingSprite);
        public static Sprite EnemyShot => Get("shot-hd", EnemyShotSprite);
        public static Sprite Scout => Get("scout-hd", ScoutSprite);
        public static Sprite Target => Get("target-hd", TargetSprite);
        public static Sprite Relic => Get("relic-hd", RelicSprite);
        public static Sprite Door => Get("door-hd", DoorSprite);
        public static Sprite Shadow => Get("shadow-hd", ShadowSprite);
        public static Sprite Glow => Get("glow-hd", () => GlowSprite(96));
        public static Sprite Spark => Get("spark-hd", SparkSprite);
        public static Sprite White => Get("white-hd", WhiteSprite);

        public static Texture2D ScanlineTexture
        {
            get
            {
                var texture = NewTexture(8, 8);
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    texture.SetPixel(x, y, y % 3 == 0
                        ? new Color(0f, 0f, 0f, 0.07f)
                        : new Color(0f, 0f, 0f, 0f));
                texture.Apply();
                return texture;
            }
        }

        private static Sprite Get(string key, Func<Sprite> factory)
        {
            Sprite result;
            if (!Cache.TryGetValue(key, out result) || result == null)
            {
                result = factory();
                result.name = key;
                Cache[key] = result;
            }
            return result;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var clear = new Color32[width * height];
            texture.SetPixels32(clear);
            texture.Apply(false, false);
            return texture;
        }

        private static Sprite Finish(Texture2D texture, Vector2 pivot)
        {
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, 64f);
        }

        private static void Fill(Texture2D tex, Color color)
        {
            FillRect(tex, 0, 0, tex.width, tex.height, color);
        }

        private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color color)
        {
            for (int yy = Mathf.Max(0, y); yy < Mathf.Min(tex.height, y + h); yy++)
            for (int xx = Mathf.Max(0, x); xx < Mathf.Min(tex.width, x + w); xx++)
                tex.SetPixel(xx, yy, color);
        }

        private static void Px(Texture2D tex, int x, int y, Color color)
        {
            if (x >= 0 && y >= 0 && x < tex.width && y < tex.height)
                tex.SetPixel(x, y, color);
        }

        private static void Line(Texture2D tex, int x0, int y0, int x1, int y1, int width, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx - dy;
            while (true)
            {
                FillRect(tex, x0 - width / 2, y0 - width / 2, width, width, color);
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

        private static void Disc(Texture2D tex, float cx, float cy, float radius, Color color,
            float inner = 0f)
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
                if (d <= radius && d >= inner)
                    Px(tex, x, y, color);
            }
        }

        private static Sprite Floor(int variant)
        {
            var t = NewTexture(64, 64);
            Color baseColor = variant % 2 == 0
                ? new Color32(74, 104, 112, 255)
                : new Color32(80, 111, 118, 255);
            Fill(t, baseColor);
            FillRect(t, 0, 0, 64, 3, new Color32(27, 47, 56, 255));
            FillRect(t, 0, 61, 64, 3, new Color32(126, 154, 157, 255));
            FillRect(t, 0, 0, 3, 64, new Color32(34, 58, 66, 255));
            FillRect(t, 61, 0, 3, 64, new Color32(46, 74, 82, 255));
            FillRect(t, 4, 4, 56, 2, new Color32(108, 138, 143, 255));
            FillRect(t, 4, 4, 2, 55, new Color32(111, 143, 148, 255));
            FillRect(t, 31, 5, 1, 53, new Color32(45, 70, 78, 255));
            FillRect(t, 4, 30, 56, 1, new Color32(46, 72, 79, 255));
            FillRect(t, 8, 8, 18, 2, new Color32(96, 127, 133, 255));
            FillRect(t, 38, 38, 16, 2, new Color32(97, 128, 132, 255));
            FillRect(t, 7, 51, 4, 2, new Color32(48, 74, 81, 255));
            FillRect(t, 48, 12, 6, 2, new Color32(47, 73, 80, 255));
            FillRect(t, 15, 39, 2, 9, new Color32(58, 83, 88, 255));
            FillRect(t, 44, 22, 2, 5, new Color32(58, 82, 87, 255));
            int seed = 29 + variant * 53;
            for (int i = 0; i < 18; i++)
            {
                seed = (seed * 73 + 17) & 0x7fffffff;
                int x = 6 + seed % 51;
                seed = (seed * 37 + 5) & 0x7fffffff;
                int y = 6 + seed % 51;
                Px(t, x, y, i % 3 == 0
                    ? new Color32(125, 154, 157, 255)
                    : new Color32(51, 77, 84, 255));
            }
            if (variant == 4)
            {
                FillRect(t, 31, 10, 2, 21, new Color32(28, 47, 53, 255));
                FillRect(t, 32, 10, 1, 18, new Color32(236, 176, 77, 255));
                FillRect(t, 17, 47, 18, 1, new Color32(28, 47, 53, 255));
            }
            if (variant == 2)
            {
                Line(t, 17, 17, 24, 24, 2, new Color32(53, 79, 84, 255));
                Line(t, 47, 43, 52, 48, 2, new Color32(51, 76, 82, 255));
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite WallSprite()
        {
            var t = NewTexture(64, 108);
            Fill(t, new Color(0f, 0f, 0f, 0f));
            FillRect(t, 2, 10, 60, 84, new Color32(45, 65, 74, 255));
            FillRect(t, 4, 17, 56, 68, new Color32(71, 91, 99, 255));
            FillRect(t, 5, 82, 54, 16, new Color32(125, 147, 148, 255));
            FillRect(t, 7, 87, 50, 7, new Color32(151, 174, 171, 255));
            FillRect(t, 5, 17, 54, 5, new Color32(26, 42, 50, 255));
            FillRect(t, 9, 26, 46, 1, new Color32(87, 108, 113, 255));
            FillRect(t, 9, 46, 46, 1, new Color32(48, 68, 76, 255));
            FillRect(t, 9, 65, 46, 1, new Color32(48, 68, 76, 255));
            FillRect(t, 15, 22, 34, 24, new Color32(63, 83, 91, 255));
            FillRect(t, 17, 24, 30, 20, new Color32(77, 98, 104, 255));
            FillRect(t, 12, 51, 40, 30, new Color32(57, 76, 84, 255));
            FillRect(t, 14, 53, 36, 27, new Color32(70, 91, 97, 255));
            Line(t, 31, 55, 28, 78, 2, new Color32(42, 61, 68, 255));
            Line(t, 44, 23, 39, 40, 1, new Color32(42, 61, 68, 255));
            Line(t, 18, 55, 15, 66, 1, new Color32(40, 58, 66, 255));
            FillRect(t, 3, 8, 58, 4, new Color32(24, 38, 45, 255));
            FillRect(t, 0, 0, 5, 17, new Color(0f, 0f, 0f, 0.25f));
            FillRect(t, 59, 0, 5, 17, new Color(0f, 0f, 0f, 0.25f));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite MovableWallSprite()
        {
            var t = NewTexture(64, 108);
            var source = Wall.texture;
            for (int y = 0; y < 108; y++)
            for (int x = 0; x < 64; x++)
                t.SetPixel(x, y, source.GetPixel(x, y));
            FillRect(t, 29, 16, 3, 68, new Color32(33, 44, 49, 255));
            FillRect(t, 31, 23, 1, 56, new Color32(236, 175, 73, 255));
            FillRect(t, 18, 36, 12, 2, new Color32(38, 51, 56, 255));
            FillRect(t, 39, 58, 10, 2, new Color32(38, 51, 56, 255));
            FillRect(t, 28, 21, 7, 5, new Color32(184, 119, 45, 255));
            FillRect(t, 29, 22, 5, 2, new Color32(252, 205, 99, 255));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite GapSprite()
        {
            var t = NewTexture(64, 64);
            Fill(t, new Color32(49, 74, 81, 255));
            FillRect(t, 2, 2, 60, 60, new Color32(13, 26, 34, 255));
            FillRect(t, 7, 8, 50, 45, new Color32(4, 12, 19, 255));
            FillRect(t, 7, 51, 50, 5, new Color32(75, 101, 104, 255));
            FillRect(t, 7, 50, 50, 2, new Color32(117, 143, 141, 255));
            Line(t, 12, 16, 23, 24, 1, new Color32(41, 100, 115, 255));
            Line(t, 44, 12, 53, 21, 1, new Color32(35, 79, 93, 255));
            Px(t, 16, 13, new Color32(91, 202, 213, 255));
            Px(t, 17, 13, new Color32(46, 130, 150, 255));
            Px(t, 48, 34, new Color32(92, 211, 219, 255));
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite PlayerSprite()
        {
            var t = NewTexture(64, 88);
            Disc(t, 32, 6, 19, new Color(0f, 0f, 0f, 0.35f));
            // Long coat.
            FillRect(t, 19, 20, 26, 30, new Color32(23, 48, 64, 255));
            FillRect(t, 15, 12, 10, 22, new Color32(19, 40, 55, 255));
            FillRect(t, 39, 12, 10, 22, new Color32(17, 36, 50, 255));
            FillRect(t, 19, 43, 26, 24, new Color32(36, 80, 96, 255));
            FillRect(t, 23, 51, 18, 20, new Color32(43, 94, 111, 255));
            FillRect(t, 18, 58, 28, 6, new Color32(241, 151, 54, 255));
            FillRect(t, 20, 59, 24, 2, new Color32(255, 198, 81, 255));
            // Head and scarf.
            FillRect(t, 24, 64, 17, 14, new Color32(224, 187, 146, 255));
            FillRect(t, 22, 73, 21, 6, new Color32(42, 61, 70, 255));
            FillRect(t, 25, 78, 15, 3, new Color32(30, 43, 52, 255));
            FillRect(t, 27, 67, 3, 3, new Color32(20, 37, 44, 255));
            FillRect(t, 36, 67, 3, 3, new Color32(20, 37, 44, 255));
            FillRect(t, 31, 72, 4, 1, new Color32(119, 72, 58, 255));
            FillRect(t, 18, 68, 7, 4, new Color32(210, 72, 57, 255));
            FillRect(t, 40, 68, 7, 4, new Color32(210, 72, 57, 255));
            // Boots and belt.
            FillRect(t, 19, 5, 11, 10, new Color32(24, 33, 37, 255));
            FillRect(t, 35, 5, 11, 10, new Color32(24, 33, 37, 255));
            FillRect(t, 17, 4, 14, 4, new Color32(15, 22, 26, 255));
            FillRect(t, 34, 4, 14, 4, new Color32(15, 22, 26, 255));
            FillRect(t, 28, 38, 8, 4, new Color32(235, 172, 65, 255));
            // Warm rim, cold rim and little highlights.
            FillRect(t, 17, 49, 2, 12, new Color32(102, 198, 203, 255));
            FillRect(t, 46, 45, 2, 16, new Color32(33, 112, 136, 255));
            FillRect(t, 26, 76, 4, 1, new Color32(244, 222, 184, 255));
            FillRect(t, 41, 37, 3, 3, new Color32(247, 191, 70, 255));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite MeleeSprite()
        {
            var t = NewTexture(64, 76);
            Disc(t, 32, 5, 20, new Color(0f, 0f, 0f, 0.4f));
            FillRect(t, 10, 8, 44, 36, new Color32(54, 30, 43, 255));
            FillRect(t, 16, 20, 32, 34, new Color32(117, 44, 64, 255));
            FillRect(t, 20, 32, 24, 25, new Color32(163, 61, 74, 255));
            FillRect(t, 23, 51, 18, 11, new Color32(124, 42, 61, 255));
            FillRect(t, 24, 25, 5, 10, new Color32(77, 27, 44, 255));
            FillRect(t, 36, 25, 5, 10, new Color32(77, 27, 44, 255));
            FillRect(t, 26, 43, 4, 4, new Color32(255, 217, 93, 255));
            FillRect(t, 36, 43, 4, 4, new Color32(255, 217, 93, 255));
            FillRect(t, 28, 36, 9, 2, new Color32(73, 24, 38, 255));
            FillRect(t, 8, 16, 7, 18, new Color32(184, 108, 61, 255));
            FillRect(t, 50, 16, 7, 18, new Color32(154, 85, 55, 255));
            FillRect(t, 9, 12, 8, 6, new Color32(102, 52, 64, 255));
            FillRect(t, 48, 12, 8, 6, new Color32(96, 48, 60, 255));
            FillRect(t, 16, 62, 5, 8, new Color32(81, 41, 60, 255));
            FillRect(t, 43, 62, 5, 8, new Color32(74, 36, 55, 255));
            Line(t, 24, 21, 18, 34, 1, new Color32(206, 88, 94, 255));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite RangedSprite()
        {
            var t = NewTexture(68, 76);
            Disc(t, 34, 5, 21, new Color(0f, 0f, 0f, 0.36f));
            FillRect(t, 19, 8, 30, 32, new Color32(45, 38, 73, 255));
            FillRect(t, 23, 15, 22, 38, new Color32(76, 55, 119, 255));
            FillRect(t, 26, 27, 16, 25, new Color32(111, 75, 150, 255));
            FillRect(t, 22, 54, 24, 9, new Color32(61, 43, 92, 255));
            FillRect(t, 25, 59, 18, 4, new Color32(185, 130, 179, 255));
            FillRect(t, 28, 44, 4, 4, new Color32(255, 110, 77, 255));
            FillRect(t, 36, 44, 4, 4, new Color32(255, 110, 77, 255));
            FillRect(t, 30, 37, 8, 2, new Color32(43, 31, 68, 255));
            FillRect(t, 49, 27, 13, 5, new Color32(36, 36, 54, 255));
            FillRect(t, 58, 26, 7, 7, new Color32(214, 173, 91, 255));
            FillRect(t, 63, 28, 2, 3, new Color32(255, 224, 123, 255));
            FillRect(t, 18, 16, 5, 15, new Color32(91, 63, 129, 255));
            FillRect(t, 48, 16, 4, 10, new Color32(66, 47, 94, 255));
            FillRect(t, 25, 65, 3, 5, new Color32(197, 140, 194, 255));
            FillRect(t, 40, 65, 3, 5, new Color32(178, 119, 175, 255));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite BossSprite()
        {
            var t = NewTexture(112, 128);
            Disc(t, 56, 6, 42, new Color(0f, 0f, 0f, 0.45f));
            FillRect(t, 17, 15, 78, 58, new Color32(54, 54, 69, 255));
            FillRect(t, 23, 28, 66, 60, new Color32(86, 75, 82, 255));
            FillRect(t, 30, 48, 52, 43, new Color32(119, 72, 68, 255));
            FillRect(t, 39, 72, 34, 33, new Color32(158, 82, 67, 255));
            FillRect(t, 31, 92, 50, 16, new Color32(83, 54, 64, 255));
            FillRect(t, 42, 101, 28, 14, new Color32(57, 44, 60, 255));
            FillRect(t, 31, 103, 8, 9, new Color32(200, 170, 123, 255));
            FillRect(t, 73, 103, 8, 9, new Color32(200, 170, 123, 255));
            FillRect(t, 38, 79, 9, 5, new Color32(255, 190, 65, 255));
            FillRect(t, 65, 79, 9, 5, new Color32(255, 190, 65, 255));
            FillRect(t, 41, 66, 30, 4, new Color32(83, 24, 31, 255));
            FillRect(t, 8, 33, 20, 42, new Color32(102, 72, 67, 255));
            FillRect(t, 84, 33, 20, 42, new Color32(91, 64, 62, 255));
            FillRect(t, 4, 28, 24, 14, new Color32(126, 91, 69, 255));
            FillRect(t, 84, 28, 24, 14, new Color32(113, 81, 68, 255));
            FillRect(t, 26, 22, 60, 15, new Color32(66, 56, 66, 255));
            Line(t, 56, 27, 48, 60, 2, new Color32(59, 48, 58, 255));
            Line(t, 64, 38, 74, 77, 2, new Color32(48, 45, 57, 255));
            FillRect(t, 42, 50, 5, 15, new Color32(247, 111, 55, 255));
            FillRect(t, 65, 50, 5, 15, new Color32(247, 111, 55, 255));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite CrystalSprite()
        {
            var t = NewTexture(48, 68);
            Disc(t, 24, 27, 20, new Color(51, 218, 255, 0.18f));
            Line(t, 24, 9, 12, 35, 10, new Color32(76, 176, 238, 255));
            Line(t, 24, 9, 37, 35, 10, new Color32(112, 223, 249, 255));
            Line(t, 12, 35, 37, 35, 7, new Color32(39, 104, 177, 255));
            FillRect(t, 19, 20, 10, 37, new Color32(103, 218, 244, 255));
            FillRect(t, 14, 30, 19, 22, new Color32(68, 160, 221, 255));
            FillRect(t, 29, 34, 8, 16, new Color32(56, 123, 197, 255));
            FillRect(t, 21, 45, 4, 10, new Color32(204, 252, 255, 255));
            Px(t, 21, 55, Color.white);
            Px(t, 34, 14, new Color32(202, 251, 255, 220));
            Px(t, 12, 22, new Color32(143, 232, 255, 180));
            return Finish(t, new Vector2(0.5f, 0.1f));
        }

        private static Sprite LightSprite()
        {
            var t = NewTexture(44, 54);
            Disc(t, 22, 30, 18, new Color(255, 183, 66, 0.16f));
            Disc(t, 22, 34, 10, new Color(255, 200, 72, 0.32f));
            FillRect(t, 18, 25, 9, 20, new Color32(102, 67, 33, 255));
            FillRect(t, 20, 27, 5, 16, new Color32(45, 37, 29, 255));
            FillRect(t, 17, 18, 11, 9, new Color32(247, 170, 55, 255));
            FillRect(t, 19, 20, 7, 5, new Color32(255, 228, 120, 255));
            FillRect(t, 15, 14, 15, 4, new Color32(244, 140, 47, 255));
            FillRect(t, 16, 46, 13, 3, new Color32(76, 54, 31, 255));
            Px(t, 22, 47, new Color32(255, 215, 106, 255));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite RingSprite()
        {
            var t = NewTexture(28, 28);
            Disc(t, 14, 14, 9, new Color32(255, 206, 84, 230), 5f);
            Disc(t, 14, 14, 6.5f, new Color32(255, 240, 156, 110), 5.2f);
            FillRect(t, 13, 23, 3, 4, new Color32(255, 247, 188, 255));
            Px(t, 5, 15, new Color32(255, 179, 42, 255));
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite EnemyShotSprite()
        {
            var t = NewTexture(24, 24);
            Disc(t, 12, 12, 11, new Color32(255, 75, 94, 80));
            Disc(t, 12, 12, 7, new Color32(255, 104, 99, 210));
            Disc(t, 12, 12, 4, new Color32(255, 229, 152, 255));
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite ScoutSprite()
        {
            var t = NewTexture(48, 58);
            Disc(t, 24, 28, 20, new Color32(67, 229, 244, 50));
            Disc(t, 24, 28, 13, new Color32(98, 245, 250, 135));
            FillRect(t, 18, 16, 13, 25, new Color32(195, 255, 255, 215));
            FillRect(t, 14, 14, 21, 8, new Color32(72, 218, 237, 155));
            FillRect(t, 21, 33, 2, 4, new Color32(34, 106, 128, 255));
            FillRect(t, 29, 33, 2, 4, new Color32(34, 106, 128, 255));
            FillRect(t, 21, 47, 2, 3, new Color32(210, 255, 255, 220));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite TargetSprite()
        {
            var t = NewTexture(56, 72);
            Disc(t, 28, 6, 18, new Color(0f, 0f, 0f, 0.38f));
            FillRect(t, 26, 8, 5, 39, new Color32(100, 69, 42, 255));
            FillRect(t, 27, 9, 2, 35, new Color32(190, 133, 66, 255));
            Disc(t, 28, 50, 19, new Color32(75, 47, 44, 255));
            Disc(t, 28, 50, 15, new Color32(227, 174, 73, 255));
            Disc(t, 28, 50, 9, new Color32(255, 226, 130, 255));
            Disc(t, 28, 50, 4, new Color32(226, 79, 79, 255));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite RelicSprite()
        {
            var t = NewTexture(44, 48);
            Disc(t, 22, 24, 19, new Color32(255, 169, 63, 38));
            FillRect(t, 10, 14, 24, 22, new Color32(32, 67, 78, 255));
            FillRect(t, 13, 17, 18, 16, new Color32(88, 211, 212, 255));
            FillRect(t, 18, 20, 8, 10, new Color32(255, 215, 105, 255));
            FillRect(t, 15, 10, 14, 4, new Color32(234, 151, 53, 255));
            FillRect(t, 18, 37, 8, 3, new Color32(228, 140, 47, 255));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite DoorSprite()
        {
            var t = NewTexture(64, 92);
            FillRect(t, 3, 4, 58, 80, new Color32(28, 42, 51, 255));
            FillRect(t, 8, 8, 48, 74, new Color32(115, 96, 72, 255));
            FillRect(t, 12, 12, 40, 66, new Color32(72, 57, 55, 255));
            FillRect(t, 16, 16, 4, 58, new Color32(177, 144, 91, 255));
            FillRect(t, 44, 16, 4, 58, new Color32(150, 118, 78, 255));
            FillRect(t, 30, 12, 4, 66, new Color32(36, 38, 45, 255));
            FillRect(t, 24, 42, 16, 5, new Color32(249, 190, 77, 255));
            return Finish(t, new Vector2(0.5f, 0f));
        }

        private static Sprite ShadowSprite()
        {
            var t = NewTexture(48, 24);
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 48; x++)
            {
                float dx = (x - 23.5f) / 23.5f;
                float dy = (y - 11.5f) / 11.5f;
                float d = dx * dx + dy * dy;
                if (d < 1f)
                    Px(t, x, y, new Color(0f, 0f, 0f, 0.38f * (1f - d)));
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite SparkSprite()
        {
            var t = NewTexture(20, 20);
            Disc(t, 10, 10, 8, new Color(255, 214, 101, 80));
            FillRect(t, 9, 2, 2, 16, new Color32(255, 244, 170, 255));
            FillRect(t, 2, 9, 16, 2, new Color32(255, 177, 70, 210));
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite WhiteSprite()
        {
            var t = NewTexture(2, 2);
            Fill(t, Color.white);
            return Finish(t, new Vector2(0.5f, 0.5f));
        }

        private static Sprite GlowSprite(int size)
        {
            var t = NewTexture(size, size);
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(d, 2.25f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            return Finish(t, new Vector2(0.5f, 0.5f));
        }
    }
}
