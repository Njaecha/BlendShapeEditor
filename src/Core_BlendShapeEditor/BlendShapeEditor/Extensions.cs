using BepInEx.Configuration;
using UnityEngine;
using KKAPI;
using KKAPI.Utilities;

namespace BlendShapeEditor
{
    public static class Extensions
    {
        /// <summary>
        /// Gets Path from this transform to one of its children.
        /// The string returned by this extension can be used with .Find() on the parent to find the child.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="child"></param>
        /// <returns></returns>
        public static string GetPathToChild(this Transform transform, Transform child)
        {
            string pPath = transform.GetFullPath().Trim().Replace(" [Transform]", "");
            string cPath = child.GetFullPath().Trim().Replace(" [Transform]", "");
            if (pPath.IsNullOrEmpty() || cPath.IsNullOrEmpty() ) return null;
            if (pPath == cPath) return "";
            return !cPath.StartsWith(pPath) ? null : cPath.Replace(pPath, string.Empty).Remove(0,1);
        }

        /// <summary>
        /// Try to find the BlendShapeEditorCharaController or BlendShapeEditorItemController that the Component is child of.
        /// </summary>
        /// <param name="behaviour">Component on a GameObject that is child a controller</param>
        /// <param name="controller">The according controller, either BlendshapeEditorCharaController or BlendshapeEditorItemController</param>
        /// <returns>true if found, false if not</returns>
        public static bool TryGetOwningController(this Component behaviour, out Object controller)
        {
            BlendShapeEditorCharaController chaCtrl = behaviour.GetComponentInParent<BlendShapeEditorCharaController>();
            BlendShapeEditorItemController blendShapeEditorItemCtrl = behaviour.GetComponentInParent<BlendShapeEditorItemController>();

            if (chaCtrl && blendShapeEditorItemCtrl)
            {
                string chaPath = chaCtrl.transform.GetPathToChild(behaviour.transform);
                string itemPath = blendShapeEditorItemCtrl.transform.GetPathToChild(behaviour.transform);
                // return the controller that is "closer" to the behaviour
                if (chaPath.Length > itemPath.Length) controller = blendShapeEditorItemCtrl;
                else controller = chaCtrl;
                return true;
            }

            if (chaCtrl) controller = chaCtrl;
            else if (blendShapeEditorItemCtrl) controller = blendShapeEditorItemCtrl;
            else
            {
                controller = null;
                return false;
            }
            return true;
        }

        public static string S(this ConfigEntry<KeyboardShortcut> entry)
        {
            return entry.Value.ToString();
        }
    }
}