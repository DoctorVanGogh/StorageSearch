using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace RSA {
    public class ExtendedCrafting {
        public static bool TryDetourExtendedCrafting(Harmony harmony) {
            var ecAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "AcEnhancedCrafting");
            if (ecAssembly != null) {
                MethodInfo mi = ecAssembly.GetType("AlcoholV.Overriding.Dialog_BillConfig").GetMethod(nameof(RimWorld.Dialog_BillConfig.DoWindowContents));
                harmony.Patch(mi, new HarmonyMethod(typeof(Dialog_BillConfig_DoWindowContents), nameof(Dialog_BillConfig_DoWindowContents.Before_DoWindowContents)), null, null);
                return true;
            }
            return false;
        }
    }
}
