// Stiletto Bridge required to fix a bug that apparently doesn't need fixing
// because the root cause of the bug (disabling animator on p_cf_body_bones) is not necessary in the first place.
// Keeping this for now in case something other bug related to Stiletto arises which could use this.
/*
using System.Collections.Generic;
using Stiletto;

namespace BlendShapeEditor
{
    public static class StilettoBridge
    {
        private static readonly List<ChaControl> StilettoHalted = new List<ChaControl>();
        
        public static bool StilettoIsHalt(ChaControl chaControl) => StilettoHalted.Contains(chaControl);
        public static void StilettoHalt(ChaControl chaControl)
        {
            HeelInfo heelInfo = chaControl.GetComponent<Stiletto.HeelInfo>();
            if (!heelInfo) return;
            heelInfo.flags.ACTIVE = false;
            StilettoHalted.Add(chaControl);
        }

        public static void StilettoResume(ChaControl chaControl)
        {
            HeelInfo heelInfo = chaControl.GetComponent<HeelInfo>();
            if (!heelInfo) return;
            heelInfo.flags.ACTIVE = true;
            StilettoHalted.Remove(chaControl);
        }
    }
}
*/