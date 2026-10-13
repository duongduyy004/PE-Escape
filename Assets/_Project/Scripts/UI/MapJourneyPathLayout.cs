using System.Collections.Generic;
using System.Linq;
using KMA.Gameplay;
using KMA.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    // Places the course stops on fixed fractions of the map zone and draws the
    // road between them as pooled rotated rectangles sampled from cubic Béziers.
    [DisallowMultipleComponent]
    public sealed class MapJourneyPathLayout : MonoBehaviour
    {
        const int SegmentsPerLeg = 36;
        const int DotsPerLeg = 20;

        sealed class Leg
        {
            public RectTransform Root;
            public readonly List<RectTransform> Outline = new List<RectTransform>();
            public readonly List<RectTransform> Fill = new List<RectTransform>();
            public readonly List<RectTransform> Dots = new List<RectTransform>();
        }

        readonly List<MapNodeView> nodes = new List<MapNodeView>(4);
        readonly List<Leg> legs = new List<Leg>(3);
        readonly Vector2[] centers = new Vector2[4];
        RectTransform root;
        Vector2 lastSize;

        static UITheme.LessonJourneyStyle Style => UITheme.Shared.LessonJourney;

        public void Configure(IEnumerable<MapNodeView> mapNodes)
        {
            root = (RectTransform)transform;
            nodes.Clear();
            if (mapNodes != null)
                nodes.AddRange(mapNodes.Where(node => node != null && !node.IsComingSoon)
                    .OrderBy(node => CourseOrder(node.SubjectId)));
            while (legs.Count < Mathf.Max(0, nodes.Count - 1)) legs.Add(CreateLeg(legs.Count));
            Refresh();
        }

        public void Refresh() => LayoutPath(true);

        public Vector2 StopCenter(int index) => centers[Mathf.Clamp(index, 0, centers.Length - 1)];

        void LateUpdate()
        {
            if (root == null) root = (RectTransform)transform;
            if (nodes.Count == 0) return;
            if (root.rect.size != lastSize) LayoutPath(false);
            UpdateProgress();
        }

        void LayoutPath(bool force)
        {
            if (root == null) root = (RectTransform)transform;
            if (root.rect.width <= 1f || root.rect.height <= 1f) return;
            Vector2 size = root.rect.size;
            if (!force && size == lastSize) return;
            lastSize = size;

            UITheme.LessonJourneyStyle style = Style;
            int count = Mathf.Min(nodes.Count, Mathf.Min(style.stopX.Length, style.stopY.Length), centers.Length);
            // Grow the whole stop up to stopMaxScale, shrinking it if the zone is shorter than its height,
            // or too narrow for neighbouring stops to sit side by side.
            float stopScale = Mathf.Min(style.stopMaxScale, size.y / (style.stopSize.y + 20f));
            for (int i = 1; i < count; i++)
                stopScale = Mathf.Min(stopScale, (style.stopX[i] - style.stopX[i - 1]) * size.x / style.stopSize.x);
            float badgeFromTop = style.stopTagHeight + style.stopBadgeSize * style.stopCurrentScale * .5f;
            float minCenter = (style.stopSize.y - badgeFromTop) * stopScale;
            float maxCenter = size.y - badgeFromTop * stopScale;
            float halfWidth = style.stopSize.x * stopScale * .5f;
            for (int i = 0; i < count; i++)
            {
                float cy = Mathf.Clamp(style.stopY[i] * size.y, minCenter, Mathf.Max(minCenter, maxCenter));
                float cx = Mathf.Clamp(style.stopX[i] * size.x, halfWidth, Mathf.Max(halfWidth, size.x - halfWidth));
                centers[i] = new Vector2(cx - size.x * .5f, cy - size.y * .5f);
                RectTransform stop = (RectTransform)nodes[i].transform;
                stop.anchorMin = stop.anchorMax = new Vector2(.5f, .5f);
                stop.pivot = new Vector2(.5f, 1f);
                stop.sizeDelta = style.stopSize;
                stop.localScale = Vector3.one * stopScale;
                stop.anchoredPosition = new Vector2(centers[i].x, centers[i].y + badgeFromTop * stopScale);
            }
            for (int i = 0; i < Mathf.Min(legs.Count, count - 1); i++)
                PositionLeg(legs[i], centers[i], centers[i + 1]);
            UpdateProgress();
        }

        Leg CreateLeg(int index)
        {
            string name = $"PathTrack{index + 1}";
            Transform existing = transform.Find(name);
            var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null) go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            go.transform.SetAsFirstSibling();
            var leg = new Leg { Root = rect };
            if (existing != null)
            {
                foreach (Transform child in rect)
                {
                    var childRect = (RectTransform)child;
                    if (child.name.StartsWith("Outline")) leg.Outline.Add(childRect);
                    else if (child.name.StartsWith("Fill")) leg.Fill.Add(childRect);
                    else if (child.name.StartsWith("Dot")) leg.Dots.Add(childRect);
                }
                if (leg.Outline.Count == SegmentsPerLeg && leg.Fill.Count == SegmentsPerLeg &&
                    leg.Dots.Count == DotsPerLeg)
                    return leg;
                // A road from an older scene or a different piece count: rebuild it instead of indexing past the pool.
                leg.Outline.Clear();
                leg.Fill.Clear();
                leg.Dots.Clear();
                for (int i = rect.childCount - 1; i >= 0; i--)
                {
                    GameObject stale = rect.GetChild(i).gameObject;
                    stale.name = "Stale";
                    if (Application.isPlaying) Destroy(stale);
                    else DestroyImmediate(stale);
                }
            }
            for (int i = 0; i < SegmentsPerLeg; i++)
                leg.Outline.Add(Piece(rect, "Outline" + (i + 1), ShellTheme.Ink, null));
            for (int i = 0; i < SegmentsPerLeg; i++)
                leg.Fill.Add(Piece(rect, "Fill" + (i + 1), ShellTheme.Gold, null));
            Sprite circle = UiKitAssets.Load().Circle;
            for (int i = 0; i < DotsPerLeg; i++)
                leg.Dots.Add(Piece(rect, "Dot" + (i + 1), ShellTheme.White, circle));
            return leg;
        }

        static RectTransform Piece(RectTransform parent, string name, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        void PositionLeg(Leg leg, Vector2 from, Vector2 to)
        {
            float dx = to.x - from.x;
            Vector2 c1 = from + new Vector2(dx * .40f, 0f);
            Vector2 c2 = to - new Vector2(dx * .45f, 0f);
            UITheme.LessonJourneyStyle style = Style;

            Vector2 previous = from;
            for (int i = 0; i < SegmentsPerLeg; i++)
            {
                Vector2 next = Bezier(from, c1, c2, to, (i + 1f) / SegmentsPerLeg);
                SetSegment(leg.Outline[i], previous, next, style.roadOutlineWidth);
                SetSegment(leg.Fill[i], previous, next, style.roadFillWidth);
                previous = next;
            }
            for (int i = 0; i < DotsPerLeg; i++)
            {
                RectTransform dot = leg.Dots[i];
                dot.anchoredPosition = Bezier(from, c1, c2, to, (i + .5f) / DotsPerLeg);
                dot.sizeDelta = Vector2.one * style.roadDotSize;
            }
        }

        static void SetSegment(RectTransform rect, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 delta = b - a;
            rect.anchoredPosition = (a + b) * .5f;
            // Overlap neighbours by half the thickness so the corners of a thick road stay closed.
            rect.sizeDelta = new Vector2(delta.magnitude + thickness * .5f, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        void UpdateProgress()
        {
            for (int i = 0; i < legs.Count && i + 1 < nodes.Count; i++)
            {
                bool passed = nodes[i].IsCompleted;
                foreach (RectTransform fill in legs[i].Fill) fill.gameObject.SetActive(passed);
                foreach (RectTransform dot in legs[i].Dots) dot.gameObject.SetActive(!passed);
            }
        }

        static int CourseOrder(SubjectId id) => id switch
        {
            SubjectId.Sprint => 0,
            SubjectId.Volleyball => 1,
            SubjectId.Football => 2,
            SubjectId.Chess => 3,
            _ => int.MaxValue
        };
    }
}
