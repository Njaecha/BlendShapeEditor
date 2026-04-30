namespace KKShapeEditor
{
	public class LayerReorderUndoEntry : IUndoEntry
	{
		private readonly DeformData _data;
		private readonly bool _movedUp;

		public LayerReorderUndoEntry(DeformData data, bool movedUp)
		{
			_data = data;
			_movedUp = movedUp;
		}

		public void Undo(UndoContext ctx)
		{
			int index = _data.ActiveLayerIndex;
			if (_movedUp)
				_data.MoveLayerDown(index);
			else
				_data.MoveLayerUp(index);
		}

		public void Redo(UndoContext ctx)
		{
			int index = _data.ActiveLayerIndex;
			if (_movedUp)
				_data.MoveLayerUp(index);
			else
				_data.MoveLayerDown(index);
		}
	}
}
