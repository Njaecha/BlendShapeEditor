using UnityEngine;

namespace BlendShapeEditor
{
    public class FlattenTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public Vector3 AveragePlaneOrigin { get; set; }
        public Vector3 AveragePlaneNormal { get; set; }
        public bool PlaneComputed { get; set; }

        public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals,
            Camera camera)
        {
            if (layer == null || brushResult == null || vertices == null)
                return;

            Vector3[] deltas = layer.Deltas;
            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            if (!PlaneComputed)
            {
                Vector3 avgPos = Vector3.zero;
                Vector3 avgNormal = Vector3.zero;
                int count = 0;
                foreach (var vertex in brushResult.AffectedVertices)
                {
                    int idx = vertex.Key;
                    if (idx >= 0 && idx < vertices.Length && idx < normals.Length)
                    {
                        avgPos += vertices[idx] + deltas[idx];
                        avgNormal += normals[idx];
                        count++;
                    }
                }

                if (count > 0)
                {
                    AveragePlaneOrigin = avgPos / count;
                    AveragePlaneNormal = (avgNormal / count).normalized;
                    PlaneComputed = true;
                }
            }

            Vector3 planeOrigin = AveragePlaneOrigin;
            Vector3 planeNormal = AveragePlaneNormal;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (vertexIndex < 0 || vertexIndex >= deltas.Length)
                    continue;

                Vector3 currentPos = vertices[vertexIndex] + deltas[vertexIndex];
                float signedDist = Vector3.Dot(currentPos - planeOrigin, planeNormal);

                float displacement = Mathf.Clamp(-signedDist * dir, -0.003f, 0.003f) * falloff;
                deltas[vertexIndex] += planeNormal * displacement;
            }

            layer.Dirty = true;
        }
    }
}