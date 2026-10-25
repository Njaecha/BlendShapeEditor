using System;

namespace BlendShapeEditor
{
	/// A change of the mirror plane's pose (gizmo drag, snap, center pick or reset).
	public class MirrorPlaneUndoEntry : IUndoEntry
	{
		private readonly MirrorPlane _plane;
		private readonly MirrorPlane.State _before;
		private readonly MirrorPlane.State _after;
		private readonly Action _onChanged;

		/// `onChanged` runs after undo/redo so dependents (gizmo mirror targets) refresh.
		public MirrorPlaneUndoEntry(MirrorPlane plane, MirrorPlane.State before, MirrorPlane.State after, Action onChanged)
		{
			_plane = plane;
			_before = before;
			_after = after;
			_onChanged = onChanged;
		}

		public void Undo(UndoContext ctx)
		{
			_plane.SetState(_before);
			_onChanged?.Invoke();
		}

		public void Redo(UndoContext ctx)
		{
			_plane.SetState(_after);
			_onChanged?.Invoke();
		}
	}
}
