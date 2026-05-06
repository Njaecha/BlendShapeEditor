using Studio;
using System.Collections.Generic;
using KKAPI.Studio;
using UnityEngine;

namespace BlendShapeEditor
{
    /// <summary>
    /// This class provides a method to connect Studio's UndoRedoManager with the plugin's UndoStack
    /// </summary>
    public static class StudioUndoBridge
    {
        public static void PushDummy(ShapePaintOverlay overlay)
        {
            if (!StudioAPI.InsideStudio) return;
            Singleton<UndoRedoManager>.Instance.Push(new BlendShapeEditorCommand(overlay));
        }

        public static void ClearBSECommands()
        {
            if (!StudioAPI.InsideStudio) return;
            UndoRedoManager mgr = Singleton<UndoRedoManager>.Instance;
            RemoveBSEEntries(mgr.undo);
            RemoveBSEEntries(mgr.redo);
        }

        private static void RemoveBSEEntries(Stack<ICommand> stack)
        {
            // Stack<T> enumerates top-to-bottom; new List converts top→bottom as index 0→last.
            // To restore original order after filtering, push from last to 0.
            var temp = new List<ICommand>(stack);
            stack.Clear();
            for (int i = temp.Count - 1; i >= 0; i--)
            {
                if (!(temp[i] is BlendShapeEditorCommand))
                    stack.Push(temp[i]);
            }
        }
    }

    public class BlendShapeEditorCommand : ICommand
    {
        private readonly ShapePaintOverlay _overlay;

        public BlendShapeEditorCommand(ShapePaintOverlay overlay)
        {
            _overlay = overlay;
        }

        public void Do() { }

        public void Undo() => _overlay.UndoOneStep();

        public void Redo() => _overlay.RedoOneStep();
    }
}
