using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RSA.HaulingHysterisis;
using Verse;
using Verse.AI;

namespace RSA
{
    [HarmonyPatch(typeof(StoreUtility), "NoStorageBlockersIn")]
    internal class StoreUtility_NoStorageBlockersIn
    {
        /// <summary>
        /// Allows injecting arbitrary custom logic into the <see cref="FilledEnough"/> method.
        /// </summary>
        /// <remarks>
        /// If the function returns anything but <see langword="null" /> that return value will be mapped to the result
        /// of <see cref="FilledEnough"/>, otherwise the default logic is used.
        /// </remarks>
        public volatile static Func<IntVec3, Map, Thing, float, bool?> CustomFilledEnough = (_, __, ___, ____) => null;


        [HarmonyPostfix]
        public static void FilledEnough(ref bool __result, IntVec3 c, Map map, Thing thing) {
            // if base implementation waves of, then don't need to care
            if (__result) {
                float fillPercent = 100f;
                SlotGroup slotGroup=c.GetSlotGroup(map);

                if (slotGroup?.Settings != null) {
                    fillPercent = StorageSettings_Mapping.Get(slotGroup.Settings).FillPercent;
                }

                var customResult = CustomFilledEnough?.Invoke(c, map, thing, fillPercent);
                if (customResult != null)
                    __result = customResult.Value;
                else
                    __result &= !map.thingGrid.ThingsListAt(c).Any(t => t.def.EverStorable(false) && t.stackCount >= thing.def.stackLimit*(fillPercent/100f));
            }
        }
    }
}
