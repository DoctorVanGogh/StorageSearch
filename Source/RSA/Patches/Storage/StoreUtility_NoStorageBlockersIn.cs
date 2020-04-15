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

        public class Util {


            /// <summary>
            /// Builds a custom check if there is something at a place on a map, which can store a thing using either a direct or a comp based
            /// implementation of <typeparamref name="T"/> with a certain fill range. 
            /// </summary>
            /// <param name="accessor">Accessor to extract the capacity at the spot on the map for the thing.</param>
            /// <remarks>
            /// <paramref name="accessor"/> is intended to be something like <c>IHoldMultipleThings.CapacityAt(position, map, thing)</c> but
            /// declared as a generic to not hard couple to a certain type/assembly.
            /// </remarks>
            public static Func<IntVec3, Map, Thing, float, bool?> BuildCustomCheck<T>(Func<IntVec3, Map, Thing, T, int> accessor) where T : class {
                return (location, map, thingToStore, fillPercentage) => {

                    /// <summary>
                    /// Enumerate (an optional) direct implementation of T as well as any comps of T at
                    /// <paramref name="location" /> on <paramref name="map" />.
                    /// </summary>
                    IEnumerable<T> GetPotentialStorers() {

                        foreach (Thing thingAt in map.thingGrid
                                                     .ThingsListAt(location)) {
                            T direct = thingAt as T;
                            if (direct != null)
                                yield return direct;

                            if (thingAt is ThingWithComps tc) {
                                foreach (var comp in tc.AllComps.OfType<T>()) {
                                    yield return comp;
                                }
                            }
                        }

                    }

                    // just grab the first possible thing offering <typeparamref name="T" />
                    // maybe later expand this to all offerings, not just the first? 
                    T owner = GetPotentialStorers().FirstOrDefault();

                    int? capacity = owner != null
                       ? (int?) accessor(location, map, thingToStore, owner)
                       : null;

                    if (capacity == null)
                       return null;

                    // if total capacity is larger than the stackLimit (full stack available)
                    //    Allow hauling (other choices are valid)
                    // if (capacity > thing.def.stackLimit) return true;
                    // only haul if count is below threshold
                    //   which is equivalent to availability being above threshold:
                    //            Log.Message("capacity = " + capacity);
                    //            Log.Message("thing.def.stackLimit = " +thing.def.stackLimit);
                    float var = (100f * (float) capacity / thingToStore.def.stackLimit);

                    //100 - num is necessary because capacity gives empty space not full space
                    return var > (100 - fillPercentage);
                    //      if (__result == false){
                    //          Log.Message("ITS TOO FULL stop");
                    //      }
                };

            }
        }


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
