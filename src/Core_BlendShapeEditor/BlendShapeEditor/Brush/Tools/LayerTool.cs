using UnityEngine;

namespace BlendShapeEditor
{
    public class LayerTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public float LayerDepth { get; set; } = 0.01f;

        public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals, Camera camera)
        {
            if (layer == null || brushResult == null || vertices == null)
                return;

            Vector3[] deltas = layer.Deltas;
            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            Vector3 planeNormal = brushResult.HitNormal.normalized;
            Vector3 planeOrigin = brushResult.HitPoint;
            float maxLayerDist = LayerDepth * dir;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (vertexIndex < 0 || vertexIndex >= deltas.Length)
                    continue;

                Vector3 currentPos = vertices[vertexIndex] + deltas[vertexIndex];
                float signedDist = Vector3.Dot(currentPos - planeOrigin, planeNormal);
                float targetDist = maxLayerDist;

                if ((dir > 0f && signedDist >= targetDist) || (dir < 0f && signedDist <= targetDist))
                    continue;

                float displacement = Mathf.Clamp(targetDist - signedDist, -0.003f, 0.003f) * falloff;
                deltas[vertexIndex] += planeNormal * displacement;
            }
            layer.Dirty = true;
        }
    }
}