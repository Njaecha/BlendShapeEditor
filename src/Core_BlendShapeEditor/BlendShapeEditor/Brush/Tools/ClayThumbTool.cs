using UnityEngine;

namespace BlendShapeEditor
{
    public class ClayThumbTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public Vector3 StrokePlaneOrigin { get; set; }
        public Vector3 StrokePlaneNormal { get; set; }
        public Vector3 StrokeDirection { get; set; }
        public bool StrokeStarted { get; set; }
        public Vector2 MouseDelta { get; set; }
        public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals, Camera camera)
        {
            if (layer == null || brushResult == null || vertices == null)
                return;

            Vector3[] deltas = layer.Deltas;
            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            Vector3 planeNormal = StrokeStarted ? StrokePlaneNormal : brushResult.HitNormal.normalized;
            float targetOffset = 0.0005f * dir;

            Vector3 pullDir = StrokeDirection.normalized;
            pullDir = (pullDir - Vector3.Dot(pullDir, planeNormal) * planeNormal).normalized;
            if (pullDir.sqrMagnitude < 0.001f)
                pullDir = Vector3.zero;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (vertexIndex < 0 || vertexIndex >= deltas.Length)
                    continue;

                Vector3 currentPos = vertices[vertexIndex] + deltas[vertexIndex];
                Vector3 planeOrigin = StrokeStarted ? StrokePlaneOrigin : brushResult.HitPoint;
                float signedDist = Vector3.Dot(currentPos - planeOrigin, planeNormal);
                float desiredDist = targetOffset;

                float clayDisp = Mathf.Clamp(desiredDist - signedDist, -0.003f, 0.003f) * falloff;
                Vector3 displacement = planeNormal * clayDisp;

                if (pullDir != Vector3.zero)
                {
                    float pullStrength = 0.002f * falloff * falloff;
                    displacement += pullDir * pullStrength;
                }

                deltas[vertexIndex] += displacement;
            }
            layer.Dirty = true;
        }
    }
}