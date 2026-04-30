using System.Collections.Generic;

namespace KKShapeEditor
{
	public class UndoStack
	{
		private readonly List<IUndoEntry> _undoList;
		private readonly List<IUndoEntry> _redoList;
		private readonly int _maxSteps;

		public int UndoCount => _undoList.Count;
		public int RedoCount => _redoList.Count;
		public bool CanUndo => _undoList.Count > 0;
		public bool CanRedo => _redoList.Count > 0;

		public UndoStack(int maxSteps)
		{
			_maxSteps = maxSteps;
			_undoList = new List<IUndoEntry>(maxSteps);
			_redoList = new List<IUndoEntry>();
		}

		public void Push(IUndoEntry entry)
		{
			_redoList.Clear();
			if (_undoList.Count >= _maxSteps)
				_undoList.RemoveAt(0);
			_undoList.Add(entry);
		}

		public void Undo(UndoContext ctx)
		{
			if (_undoList.Count == 0)
				return;
			int index = _undoList.Count - 1;
			IUndoEntry entry = _undoList[index];
			_undoList.RemoveAt(index);
			entry.Undo(ctx);
			_redoList.Add(entry);
			ctx.Deformer?.InvalidateDeltaCache();
		}

		public void Redo(UndoContext ctx)
		{
			if (_redoList.Count == 0)
				return;
			int index = _redoList.Count - 1;
			IUndoEntry entry = _redoList[index];
			_redoList.RemoveAt(index);
			entry.Redo(ctx);
			_undoList.Add(entry);
			ctx.Deformer?.InvalidateDeltaCache();
		}

		public void Clear()
		{
			_undoList.Clear();
			_redoList.Clear();
		}
	}
}
