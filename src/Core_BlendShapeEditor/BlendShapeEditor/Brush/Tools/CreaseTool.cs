using UnityEngine;

namespace BlendShapeEditor
{
    public class CreaseTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;
        public float PinchStrength { get; set; } = 0.5f;

        public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals,
            Camera camera)
        {
            if (layer == null || brushResult == null || normals == null || vertices == null)
                return;

            Vector3[] deltas = layer.Deltas;
            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            Vector3 brushCenter = brushResult.HitPoint;
            Vector3 brushNormal = brushResult.HitNormal.normalized;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (vertexIndex < 0 || vertexIndex >= deltas.Length || vertexIndex >= normals.Length)
                    continue;

                Vector3 normalDisp = brushNormal * (dir * falloff * 0.003f);

                Vector3 worldPos = vertices[vertexIndex] + deltas[vertexIndex];
                Vector3 towardCenter = (brushCenter - worldPos).normalized;
                float pinchAmount = PinchStrength * falloff * falloff * 0.002f;
                Vector3 pinchDisp = towardCenter * pinchAmount;

                deltas[vertexIndex] += normalDisp + pinchDisp;
            }

            layer.Dirty = true;
        }
    }
}