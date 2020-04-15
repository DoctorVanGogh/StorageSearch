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


        public static bool TryEnableIHoldMultipleThingsLogic() {
            if (!EnableHoldMultipleThingsLogic(AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetCustomAttribute<GuidAttribute>()?.Value?.ToLowerInvariant() == Guid_IHoldMultipleThings))) {
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                return false;
            }

            return true;
        }

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
                ilGen.Emit(OpCodes.Ldloc, cap);
                ilGen.Emit(OpCodes.Pop);
                ilGen.Emit(OpCodes.Ret);

                var accessor = dm.CreateDelegate(typeof(Func<,,,,>).MakeGenericType(typeof(IntVec3),
                                                                                    typeof(Map),
                                                                                    typeof(Thing),
                                                                                    holdMultipleThings,
                                                                                    typeof(int)));
                #endregion

                // *this* is okay to call via reflection since it is only ever invoked *once* in the total program lifetime... what do we care about a few extra 100s ms in performance loss here
                custom = (Func<IntVec3, Map, Thing, float, bool?>) typeof(StoreUtility_NoStorageBlockersIn.Util).GetMethod(nameof(StoreUtility_NoStorageBlockersIn.Util.BuildCustomCheck)).MakeGenericMethod(holdMultipleThings)
                                                                                                                .Invoke(null, new[] {accessor});

                StoreUtility_NoStorageBlockersIn.CustomFilledEnough = (c, m, t, n) => existing(c, m, t, n) ?? custom(c, m, t, n);

                return true;
            }

            Log.Warning("RSA Main: IHoldMultipleThings detected, but could not find IHoldMultipleThings type - not enabling custom logic.");
            return false;
        }
    }
}
