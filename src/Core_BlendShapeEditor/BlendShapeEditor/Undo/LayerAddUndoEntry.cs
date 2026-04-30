namespace KKShapeEditor
{
	public class LayerAddUndoEntry : IUndoEntry
	{
		private readonly DeformData _data;
		private readonly DeformLayer _layer;
		private readonly int _index;

		public LayerAddUndoEntry(DeformData data, DeformLayer layer, int index)
		{
			_data = data;
			_layer = layer;
			_index = index;
		}

		public void Undo(UndoContext ctx)
		{
			_data.Layers.Remove(_layer);
			if (_data.Layers.Count == 0)
			{
				_data.ActiveLayerIndex = -1;
				return;
			}
			if (_data.ActiveLayerIndex >= _data.Layers.Count)
				_data.ActiveLayerIndex = _data.Layers.Count - 1;
		}

		public void Redo(UndoContext ctx)
		{
			int insertAt = (_index <= _data.Layers.Count) ? _index : _data.Layers.Count;
			_data.Layers.Insert(insertAt, _layer);
			_data.ActiveLayerIndex = insertAt;
		}
	}
}
