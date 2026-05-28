using UnityEngine;

namespace BlendShapeEditor
{
    public class ClayTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public Vector3 StrokePlaneOrigin { get; set; }
        public Vector3 StrokePlaneNormal { get; set; }
        public bool StrokeStarted { get; set; }

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

            float targetOffset = 0.001f * dir;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (vertexIndex < 0 || vertexIndex >= deltas.Length)
                    continue;

                Vector3 currentPos = vertices[vertexIndex] + deltas[vertexIndex];
                float signedDist = Vector3.Dot(currentPos - planeOrigin, planeNormal);
                float desiredDist = targetOffset;
                float displacement = Mathf.Clamp(desiredDist - signedDist, -0.003f, 0.003f) * falloff;
                deltas[vertexIndex] += planeNormal * displacement;
            }

            layer.Dirty = true;
        }
    }
}