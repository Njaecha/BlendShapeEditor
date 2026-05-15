using UnityEngine;

namespace BlendShapeEditor
{
    public class BlobTool : IDeformTool
    {
        public float Direction { get; set; } = 1f;

        public void Apply(DeformLayer layer, BrushResult brushResult, Vector3[] vertices, Vector3[] normals,
            Camera camera)
        {
            if (layer == null || brushResult == null || normals == null)
                return;

            Vector3[] deltas = layer.Deltas;
            float dir = Direction;
            if (Mathf.Approximately(dir, 0f))
                return;

            foreach (var vertex in brushResult.AffectedVertices)
            {
                int vertexIndex = vertex.Key;
                float falloff = vertex.Value;
                if (vertexIndex < 0 || vertexIndex >= deltas.Length || vertexIndex >= normals.Length)
                    continue;

                float blobFalloff = falloff * (1f - Mathf.Pow(1f - falloff, 3f));
                deltas[vertexIndex] += normals[vertexIndex].normalized * (dir * blobFalloff * 0.003f);
            }

            layer.Dirty = true;
        }
    }
}