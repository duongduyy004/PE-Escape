using System.Collections.Generic;
using UnityEngine;

namespace KMA.Gameplay.UI
{
    // Small vector-like icons generated once, independent of font glyph coverage.
    public static class HomeMenuIcons
    {
        public enum Shape { Play, Continue, Gear, Power, Chevron, Star, Runner, Volleyball }
        static readonly Dictionary<Shape, Sprite> Cache = new Dictionary<Shape, Sprite>();

        public static Sprite Get(Shape shape)
        {
            if (Cache.TryGetValue(shape, out Sprite sprite)) return sprite;
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var point = new Vector2(x - 31.5f, y - 31.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Coverage(shape, point)));
                }
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f), new Vector2(.5f, .5f));
            Cache.Add(shape, sprite);
            return sprite;
        }

        static float Coverage(Shape shape, Vector2 p)
        {
            float radius = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            switch (shape)
            {
                case Shape.Play:
                    return p.x >= -18f && p.x <= 13f && Mathf.Abs(p.y) < (13f - p.x) * .78f ? 1f : 0f;
                case Shape.Continue:
                    return (radius > 14f && radius < 20f && angle > -55f && angle < 250f) ||
                           (p.x > 8f && p.x < 23f && p.y > 0f && p.y < 16f && p.y < p.x - 4f) ? 1f : 0f;
                case Shape.Gear:
                    float spoke = Mathf.Abs(Mathf.Repeat(angle + 22.5f, 45f) - 22.5f);
                    return (radius > 9f && radius < 18f) ||
                           (radius >= 18f && radius < 24f && spoke < 11f) ? 1f : 0f;
                case Shape.Power:
                    return (radius > 15f && radius < 21f && (angle < 55f || angle > 125f)) ||
                           (Mathf.Abs(p.x) < 3.5f && p.y > 4f && p.y < 26f) ? 1f : 0f;
                case Shape.Chevron:
                    return Mathf.Abs(p.x - Mathf.Abs(p.y) * .65f + 5f) < 3.5f && Mathf.Abs(p.y) < 22f ? 1f : 0f;
                case Shape.Star:
                    float phase = Mathf.Repeat(angle + 90f, 72f);
                    float edge = Mathf.Lerp(24f, 10f, 1f - Mathf.Abs(phase - 36f) / 36f);
                    return radius < edge ? 1f : 0f;
                case Shape.Runner:
                    return ((new Vector2(p.x - 12f, p.y - 18f).sqrMagnitude < 28f) ||
                           Segment(p, new Vector2(8f, 12f), new Vector2(-2f, 1f), 3.5f) ||
                           Segment(p, new Vector2(5f, 8f), new Vector2(20f, 4f), 3f) ||
                           Segment(p, new Vector2(2f, 7f), new Vector2(-10f, 16f), 3f) ||
                           Segment(p, new Vector2(-2f, 1f), new Vector2(-15f, -9f), 3.5f) ||
                           Segment(p, new Vector2(-2f, 1f), new Vector2(10f, -10f), 3.5f) ||
                           Segment(p, new Vector2(-15f, -9f), new Vector2(-25f, -14f), 3f) ||
                           Segment(p, new Vector2(10f, -10f), new Vector2(22f, -13f), 3f)) ? 1f : 0f;
                case Shape.Volleyball:
                    return ((radius > 21f && radius < 25f) ||
                           (radius < 22f && (Mathf.Abs(p.x * .72f + p.y * .69f) < 2f ||
                            Mathf.Abs(p.x * .72f - p.y * .69f) < 2f || Mathf.Abs(p.x) < 2f))) ? 1f : 0f;
                default:
                    return 0f;
            }
        }

        static bool Segment(Vector2 point, Vector2 start, Vector2 end, float width)
        {
            Vector2 delta = end - start;
            float lengthSquared = delta.sqrMagnitude;
            float t = lengthSquared <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(point - start, delta) / lengthSquared);
            return (point - (start + delta * t)).sqrMagnitude <= width * width;
        }
    }
}
