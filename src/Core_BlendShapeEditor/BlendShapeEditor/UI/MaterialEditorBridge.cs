using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using KK_Plugins.MaterialEditor;
using MaterialEditorAPI;
using BSE = BlendShapeEditor.BlendShapeEditorPlugin;
namespace BlendShapeEditor
{
    public static class MaterialEditorBridge
    {
        public static List<Renderer> CurrentlyVisibleRenderers { get; private set; } = new List<Renderer>();
        public static EventHandler<InterfacePopulateArgs> UserInterfacePopulate; 
        
        /// <summary>
        /// Retrieves the GameObject the MaterialEditor UI is currently pointing to.
        /// </summary>
        /// <returns></returns>
        public static GameObject GetCurrentMaterialEditorObject()
        {
            GameObject obj = Singleton<MaterialEditorUI>.Instance.CurrentGameObject;
            return obj;
        }

        internal static void OnPopulate(List<Renderer> renderers)
        {
            CurrentlyVisibleRenderers = renderers;
            UserInterfacePopulate?.Invoke(null, new  InterfacePopulateArgs(renderers));
        }

        public class InterfacePopulateArgs : EventArgs
        {
            public List<Renderer> Renderers { get; private set; }
            
            public InterfacePopulateArgs(List<Renderer> renderers)
            {
                Renderers = renderers;
            }
        }
    }
}