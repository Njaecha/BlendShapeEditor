using UnityEngine;

namespace BlendShapeEditor
{
    public class ClayStripsTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public Vector3 StrokePlaneOrigin { get; set; }
        public Vector3 StrokePlaneNormal { get; set; }
        public Vector3 StrokeDirection { get; set; }
        public bool StrokeStarted { get; set; }
        public float StripWidth { get; set; } = 1f;

        public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals,
            Camera camera)
        {
            if (layer == null || brushResult == null || vertices == null)
                return;

            Vector3[] deltas = layer.Deltas;
            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            Vector3 planeOrigin = StrokeStarted ? StrokePlaneOrigin : brushResult.HitPoint;
            Vector3 planeNormal = StrokeStarted ? StrokePlaneNormal : brushResult.HitNormal.normalized;
            Vector3 strokeDir = StrokeDirection.normalized;
            if (strokeDir.sqrMagnitude < 0.001f)
                strokeDir = planeNormal;

            Vector3 stripAxis = Vector3.Cross(planeNormal, strokeDir).normalized;
            if (stripAxis.sqrMagnitude < 0.001f)
                stripAxis = Vector3.Cross(planeNormal, Vector3.up).normalized;

            float targetOffset = 0.001f * dir;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (vertexIndex < 0 || vertexIndex >= deltas.Length)
                    continue;

                Vector3 currentPos = vertices[vertexIndex] + deltas[vertexIndex];
                float signedDist = Vector3.Dot(currentPos - planeOrigin, planeNormal);

                Vector3 toVertex = currentPos - planeOrigin;
                float alongStrip = Vector3.Dot(toVertex, stripAxis);
                float stripWeight = Mathf.Clamp01(1f - Mathf.Abs(alongStrip) / StripWidth);
                if (stripWeight <= 0f) continue;

                float desiredDist = targetOffset;
                float displacement = Mathf.Clamp(desiredDist - signedDist, -0.003f, 0.003f) * falloff * stripWeight;
                deltas[vertexIndex] += planeNormal * displacement;
            }

            layer.Dirty = true;
        }
    }
}