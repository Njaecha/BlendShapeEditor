using System;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using MaterialEditorAPI;
using BSE = BlendShapeEditor.BlendShapeEditorPlugin;
using HarmonyLib;

namespace BlendShapeEditor
{
    public static class MaterialEditorBridge
    {
        public static List<Renderer> CurrentlyVisibleRenderers { get; private set; } = new List<Renderer>();
        public static EventHandler<InterfacePopulateArgs> UserInterfacePopulate;

        public static bool BridgeAvailable { get; internal set; } = false;
        
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
        
        public class Hooks
        {
            public static void MaterialEditorPopulateListTranspilerContinuer(List<Renderer> renderers)
            {
                if (renderers.IsNullOrEmpty()) return;
                BSE.Logger.LogInfo($"Got {renderers.Count} renderers");
                for (var i = 0; i < renderers.Count; i++)
                {
                    BSE.Logger.LogDebug($"Renderer #{i}: {renderers[i].name}");
                }
                OnPopulate(renderers);
            }
        
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(MaterialEditorUI), nameof(MaterialEditorUI.PopulateList))]
            static IEnumerable<CodeInstruction> MaterialEditorPopulateListTranspiler(
                IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher cm = new CodeMatcher(instructions, generator);
                // find place just after the loop
                cm.MatchForward(false,
                    new CodeMatch(OpCodes.Blt),
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(OpCodes.Callvirt));
                cm.Advance(1);
                // insert after the loop: load renderers list (local var at position 1), call my method with it.
                BSE.Logger.LogInfo($"Patching ME - IL-Line: {cm.Instruction}");
                cm.InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(Hooks), nameof(MaterialEditorPopulateListTranspilerContinuer)))
                );

                return cm.Instructions();
            }
        }
    }
}