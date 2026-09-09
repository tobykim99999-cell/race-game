using UnityEngine;
using UnityEngine.UI;

namespace CircuitRacing
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoadRouteGraphic : MaskableGraphic
    {
        public RoadMinimap minimap;
        private void LateUpdate() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (minimap == null || minimap.ViewCamera == null) return;
            Camera camera = minimap.ViewCamera;
            Rect rect = rectTransform.rect;
            Vector2 Map(Vector3 position)
            {
                Vector3 p = camera.WorldToViewportPoint(position);
                return rect.min + Vector2.Scale(new Vector2(p.x, p.y), rect.size);
            }
            foreach (var racer in minimap.director.racers)
            {
                if (racer == minimap.director.Player) continue;
                DrawRacer(vh, racer, Map, rect, 5f, racer.racerIndex == 1 ? new Color(1f, 0.25f, 0.18f)
                    : racer.racerIndex == 2 ? new Color(0.3f, 0.65f, 1f) : new Color(1f, 0.77f, 0.15f));
            }
            DrawRacer(vh, minimap.director.Player, Map, rect, 8f, new Color(0.15f, 1f, 0.78f));
            Arrow(vh, new Vector2(rect.xMax - 14f, rect.yMax - 32f), Vector2.up, 5f, Color.white);
        }

        private static void DrawRacer(VertexHelper vh, RoadRaceProgress racer, System.Func<Vector3, Vector2> map, Rect rect, float size, Color color)
        {
            Vector2 point = map(racer.transform.position);
            if (!rect.Contains(point)) return;
            Vector2 direction = (map(racer.transform.position + racer.transform.forward * 5f) - point).normalized;
            Arrow(vh, point, direction, size + 2f, new Color(0.02f, 0.03f, 0.04f, 0.95f));
            Arrow(vh, point, direction, size + 1f, Color.white);
            Arrow(vh, point, direction, size, color);
        }

        private static void Arrow(VertexHelper vh, Vector2 centre, Vector2 direction, float size, Color color)
        {
            Vector2 side = new Vector2(-direction.y, direction.x);
            int index = vh.currentVertCount;
            vh.AddVert(centre + direction * size, color, Vector2.zero);
            vh.AddVert(centre - direction * size * 0.8f + side * size * 0.65f, color, Vector2.zero);
            vh.AddVert(centre - direction * size * 0.35f, color, Vector2.zero);
            vh.AddVert(centre - direction * size * 0.8f - side * size * 0.65f, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
