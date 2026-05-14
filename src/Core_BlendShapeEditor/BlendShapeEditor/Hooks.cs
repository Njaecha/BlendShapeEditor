using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using MaterialEditorAPI;
using UnityEngine;
using BSE = BlendShapeEditor.BlendShapeEditorPlugin;

namespace BlendShapeEditor
{
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
            MaterialEditorBridge.OnPopulate(renderers);
        }
        
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MaterialEditorAPI.MaterialEditorUI), nameof(MaterialEditorAPI.MaterialEditorUI.PopulateList))]
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
                    AccessTools.Method(typeof(Hooks), nameof(Hooks.MaterialEditorPopulateListTranspilerContinuer)))
            );

            return cm.Instructions();
        }
    }
}