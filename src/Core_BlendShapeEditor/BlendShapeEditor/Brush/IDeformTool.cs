using System.Collections.Generic;
using UnityEngine;

namespace BlendShapeEditor
{
	public interface IDeformTool
	{
		float Direction { get; set; }

		/// Adds a world-space displacement for each vertex the tool wants to move into `displacements`.
		/// Tools never write layer deltas directly; DeformSurface.ApplyDisplacements converts to bind space.
		void Apply(DeformSurface surface, BrushResult brushResult, Camera camera, Dictionary<int, Vector3> displacements);
	}

	public static class DisplacementExtensions
	{
		public static void AddDisplacement(this Dictionary<int, Vector3> displacements, int vertexIndex, Vector3 displacement)
		{
			displacements.TryGetValue(vertexIndex, out Vector3 existing);
			displacements[vertexIndex] = existing + displacement;
		}
	}
}
