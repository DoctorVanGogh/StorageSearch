using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RSA {
    public class HoldMultipleThings {


        // taken from mehni's IHoldMultipleThings AssemblyInfo
        private const string Guid_IHoldMultipleThings = "e1536a54-d289-41fa-9d0b-8a2f6812c7fa";


        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) {
            if (EnableHoldMultipleThingsLogic(args.LoadedAssembly)) {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            }
        }


        /// <summary>
        /// Tries to find the <c>IHoldMultipleThings.dll</c> assembly from the current <see cref="AppDomain"/>,
        /// if not found sets up an event handler for assembly loads to check later loads.
        /// </summary>
        /// <see langword="true" /> if the <c>IHoldMultipleThings.dll</c> assembly was found;<see langword="false" /> otherwise.
        public static bool TryEnableIHoldMultipleThingsLogic() {
            if (!EnableHoldMultipleThingsLogic(AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetCustomAttribute<GuidAttribute>()?.Value?.ToLowerInvariant() == Guid_IHoldMultipleThings))) {
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Updates <see cref="StoreUtility_NoStorageBlockersIn.CustomFilledEnough"/> with custom logic for
        /// <c>IHoldMultipleThings</c> if <paramref name="a"/> is the actual <c>IHoldMultipleThings.dll</c>
        /// assembly (determined via <see cref="GuidAttribute"/> of the assembly).
        /// </summary>
        /// <param name="a">Assembly to check</param>
        /// <returns>
        /// <see langword="true" /> if <paramref name="a"/> was the <c>IHoldMultipleThings.dll</c>
        /// assembly; <see langword="false" /> otherwise.
        /// </returns>
        public static bool EnableHoldMultipleThingsLogic(Assembly a) {
            if (a?.GetCustomAttribute<GuidAttribute>()?.Value?.ToLowerInvariant() != Guid_IHoldMultipleThings) {
                return false;
            }


            Type holdMultipleThings = a.GetType("IHoldMultipleThings.IHoldMultipleThings");

            if (holdMultipleThings != null) {
                Log.Message("RSA Main: IHoldMultipleThings detected - enabling custom logic.");

                Func<IntVec3, Map, Thing, float, bool?> existing, custom;
                existing = StoreUtility_NoStorageBlockersIn.CustomFilledEnough;


                #region dynamic method vodoo

                /*  
                 *  build a tiny dynamic method of
                 *  <code>
                 *      int foo(IntVec3 c, Map m, Thing t, IHoldMultipleThings h) {
                 *          int cap;
                 *          h.CapacityAt(t, c, m, out cap);
                 *          return cap;
                 *      }
                 *  </code>
                 *  without ever having an actual dependency on IHoldMultipleThings 
                 *
                 *  "MAGIC" :D
                 *
                 *
                 *  *Not* using reflection because this gets called *a lot*. And reflection is **slow**.
                 *
                 *  A tiny dynamic method is _almost_ as fast as a direct call (except pushing/poping some value
                 *  onto & from the stack), but allows not hard coupling against the type/assembly.
                 */

                DynamicMethod dm = new DynamicMethod(
                    "dynamic IHoldMultipleThings capacity getter",
                    typeof(int),
                    new[]
                    {
                        typeof(IntVec3),
                        typeof(Map),
                        typeof(Thing),
                        holdMultipleThings
                    });
                ILGenerator ilGen = dm.GetILGenerator();
                LocalBuilder cap = ilGen.DeclareLocal(typeof(int));

                ilGen.Emit(OpCodes.Ldarg_3);
                ilGen.Emit(OpCodes.Ldarg_2);
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldarg_1);
                ilGen.Emit(OpCodes.Ldloca, cap);
                ilGen.Emit(OpCodes.Callvirt, holdMultipleThings.GetMethod("CapacityAt"));
                ilGen.Emit(OpCodes.Pop);
                ilGen.Emit(OpCodes.Ldloc, cap);
                ilGen.Emit(OpCodes.Ret);

                var accessor = dm.CreateDelegate(typeof(Func<,,,,>).MakeGenericType(typeof(IntVec3),
                                                                                    typeof(Map),
                                                                                    typeof(Thing),
                                                                                    holdMultipleThings,
                                                                                    typeof(int)));
                #endregion

                // *this* is okay to call via reflection since it is only ever invoked *once* in the total program lifetime... what do we care about a few extra 100s ms in performance loss here
                custom = (Func<IntVec3, Map, Thing, float, bool?>) typeof(Util).GetMethod(nameof(Util.BuildCustomCheck)).MakeGenericMethod(holdMultipleThings)
                                                                               .Invoke(null, new[] {accessor});

                StoreUtility_NoStorageBlockersIn.CustomFilledEnough = (c, m, t, n) => existing(c, m, t, n) ?? custom(c, m, t, n);

                return true;
            }

            Log.Warning("RSA Main: IHoldMultipleThings detected, but could not find IHoldMultipleThings type - not enabling custom logic.");
            return false;
        }


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
                       ? (int?)accessor(location, map, thingToStore, owner)
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
                    float var = (100f * (float)capacity / thingToStore.def.stackLimit);

                    //100 - num is necessary because capacity gives empty space not full space
                    return var > (100 - fillPercentage);
                    //      if (__result == false){
                    //          Log.Message("ITS TOO FULL stop");
                    //      }
                };

            }
        }
    }
}
