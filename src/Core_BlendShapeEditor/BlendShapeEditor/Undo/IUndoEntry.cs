namespace KKShapeEditor
{
	public interface IUndoEntry
	{
		void Undo(UndoContext ctx);
		void Redo(UndoContext ctx);
	}
}
