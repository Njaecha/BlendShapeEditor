using UnityEngine;

namespace KKShapeEditor
{
	public class DeltaUndoEntry : IUndoEntry
	{
		private readonly DeformLayer _layer;
		private readonly int[] _indices;
		private readonly Vector3[] _before;
		private readonly Vector3[] _after;

		public DeltaUndoEntry(DeformLayer layer, int[] indices, Vector3[] before, Vector3[] after)
		{
			_layer = layer;
			_indices = indices;
			_before = before;
			_after = after;
		}

		public void Undo(UndoContext ctx)
		{
			Vector3[] deltas = _layer.Deltas;
			for (var i = 0; i < _indices.Length; i++)
				deltas[_indices[i]] = _before[i];
			_layer.Dirty = true;
		}

		public void Redo(UndoContext ctx)
		{
			Vector3[] deltas = _layer.Deltas;
			for (var i = 0; i < _indices.Length; i++)
				deltas[_indices[i]] = _after[i];
			_layer.Dirty = true;
		}
	}
}
