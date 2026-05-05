namespace BlendShapeEditor
{
	public class LayerRemoveUndoEntry : IUndoEntry
	{
		private readonly DeformData _data;
		private readonly DeformLayer _layer;
		private readonly int _index;
		private readonly int _prevActiveIndex;

		public LayerRemoveUndoEntry(DeformData data, DeformLayer layer, int index, int prevActiveIndex)
		{
			_data = data;
			_layer = layer;
			_index = index;
			_prevActiveIndex = prevActiveIndex;
		}

		public void Undo(UndoContext ctx)
		{
			int insertAt = (_index <= _data.Layers.Count) ? _index : _data.Layers.Count;
			_data.Layers.Insert(insertAt, _layer);
			_data.ActiveLayerIndex = _prevActiveIndex;
		}

		public void Redo(UndoContext ctx)
		{
			_data.RemoveLayer(_data.Layers.IndexOf(_layer));
		}
	}
}
