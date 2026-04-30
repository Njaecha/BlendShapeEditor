namespace KKShapeEditor
{
	public class LayerWeightUndoEntry : IUndoEntry
	{
		private readonly DeformLayer _layer;
		private readonly float _before;
		private readonly float _after;

		public LayerWeightUndoEntry(DeformLayer layer, float before, float after)
		{
			_layer = layer;
			_before = before;
			_after = after;
		}

		public void Undo(UndoContext ctx)
		{
			_layer.Weight = _before;
			_layer.Dirty = true;
		}

		public void Redo(UndoContext ctx)
		{
			_layer.Weight = _after;
			_layer.Dirty = true;
		}
	}
}
