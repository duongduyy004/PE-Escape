using UnityEngine;

namespace KMA.Gameplay
{
    /// <summary>Small, side-specific shoe silhouettes for Sprint's two thumb controls.</summary>
    public static class SprintFootIcons
    {
        const int Resolution = 96;
        static Sprite left;
        static Sprite right;

        public static Sprite Left => left != null ? left : left = Create(false);
        public static Sprite Right => right != null ? right : right = Create(true);

        static Sprite Create(bool mirror)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = mirror ? "SprintRightFootIconTexture" : "SprintLeftFootIconTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            var outline = new[]
            {
                new Vector2(.35f, .08f), new Vector2(.29f, .27f), new Vector2(.22f, .47f),
                new Vector2(.17f, .67f), new Vector2(.19f, .79f), new Vector2(.29f, .87f),
                new Vector2(.46f, .92f), new Vector2(.63f, .89f), new Vector2(.76f, .83f),
                new Vector2(.82f, .75f), new Vector2(.79f, .68f), new Vector2(.68f, .64f),
                new Vector2(.61f, .53f), new Vector2(.57f, .36f), new Vector2(.55f, .08f)
            };
            var inset = new[]
            {
                new Vector2(.39f, .15f), new Vector2(.34f, .30f), new Vector2(.27f, .49f),
                new Vector2(.23f, .68f), new Vector2(.25f, .76f), new Vector2(.33f, .82f),
                new Vector2(.47f, .86f), new Vector2(.61f, .84f), new Vector2(.72f, .79f),
                new Vector2(.75f, .74f), new Vector2(.65f, .70f), new Vector2(.57f, .61f),
                new Vector2(.54f, .52f), new Vector2(.51f, .35f), new Vector2(.50f, .15f)
            };

            var navy = new Color32(21, 43, 72, 255);
            var cream = new Color32(255, 247, 225, 255);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float u = (x + .5f) / Resolution;
                float v = (y + .5f) / Resolution;
                if (mirror) u = 1f - u;
                Color32 color = Inside(u, v, outline) ? navy : clear;
                if (Inside(u, v, inset)) color = cream;

                // Three short lace bars keep the silhouette readable as a shoe at small sizes.
                if (v > .48f && v < .69f && NearLine(u, v, .39f, .47f, .58f, .50f, .012f) ||
                    v > .42f && v < .63f && NearLine(u, v, .36f, .56f, .56f, .59f, .012f) ||
                    v > .36f && v < .57f && NearLine(u, v, .34f, .65f, .54f, .68f, .012f))
                    color = navy;
                texture.SetPixel(x, y, color);
            }

            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, Resolution, Resolution),
                new Vector2(.5f, .5f), Resolution, 0, SpriteMeshType.Tight);
            sprite.name = mirror ? "SprintRightFootIcon" : "SprintLeftFootIcon";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        static bool Inside(float x, float y, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        static bool NearLine(float x, float y, float x1, float y1, float x2, float y2, float radius)
        {
            Vector2 point = new Vector2(x, y);
            Vector2 start = new Vector2(x1, y1);
            Vector2 segment = new Vector2(x2 - x1, y2 - y1);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return (point - (start + segment * t)).sqrMagnitude <= radius * radius;
        }
    }
}
