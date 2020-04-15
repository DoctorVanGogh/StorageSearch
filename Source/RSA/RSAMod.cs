using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using RimWorld;
using RSA;
using RSA.Core;
using RSA.Languages;
using UnityEngine;
using Verse;

namespace RSA
{
    public class RSAMod : Mod
    {
        public static bool EnableOutfitFilter = true;
        public static bool EnableCraftingFilter = true;

        private RSACoreMod baseFilterSearchMod;



        public RSAMod(ModContentPack content) : base(content)
        {
            Harmony harmonyInstance = new Harmony("RSA");
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());              // just use all [HarmonyPatch] decorated classes       

            bool modifiedExtendedCrafting = ExtendedCrafting.TryDetourExtendedCrafting(harmonyInstance);

            Log.Message($"RSA Main {typeof(RSAMod).Assembly.GetName().Version} loaded {(modifiedExtendedCrafting ? " - (ExtendedCrafting detected)" : null)}...");
                
            // supress base mod (mini) settings, we're replicating then in our own extended object
            baseFilterSearchMod = LoadedModManager.GetMod<RSACoreMod>();
            if (baseFilterSearchMod == null)
                Log.Warning("Base filter mod not found - wrong assembly load orders?");
            else
                baseFilterSearchMod.SupressSettings = true;

            this.GetSettings<Settings>();

            HoldMultipleThings.TryEnableIHoldMultipleThingsLogic();
        }



        public override string SettingsCategory() {
            return RSAKeys.RSA.Translate();
        }


        public override void DoSettingsWindowContents(Rect inRect) {
            Listing_Standard list = new Listing_Standard(GameFont.Small) {
                                        ColumnWidth = inRect.width
                                    };
            list.Begin(inRect);

            if (baseFilterSearchMod != null) {
                RSACoreMod.DoSettingsContents(list);
            }

            list.CheckboxLabeled(RSAKeys.RSA_ForOutfits.Translate(), ref Settings.EnableOutfitFilter, RSAKeys.RSA_ForOutfitsTip.Translate());
            list.CheckboxLabeled(RSAKeys.RSA_ForCrafting.Translate(), ref Settings.EnableCraftingFilter, RSAKeys.RSA_ForCraftingTip.Translate());

            if (RSACoreMod.Debug) {
                list.GapLine();
                list.Label(RSACoreKeys.RSACore_Debug.Translate(typeof(RSACoreMod).Assembly.GetName().Version.ToString()));
            }

            if (baseFilterSearchMod != null) {
                RSACoreMod.DoPreview(list);
            }

            list.End();
        }


        
    }
}
