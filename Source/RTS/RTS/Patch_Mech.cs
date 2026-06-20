using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RTS
{
    [HarmonyPatch(typeof(CompOverseerSubject), nameof(CompOverseerSubject.State),MethodType.Getter)]
    public static class Patch_Mech
    {
        [HarmonyPostfix]
        public static void Postfix(ref OverseerSubjectState __result, CompOverseerSubject __instance)
        {
            if (__instance.Parent.TryGetComp<UnitComp>() is UnitComp comp
                && comp.root != null) 
            {
                __result = OverseerSubjectState.Overseen;
            }
        }
    }
    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.ShowDraftGizmo),MethodType.Getter)]
    public static class Patch_Draft
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, Pawn_DraftController __instance)
        {
            if (__instance.pawn.TryGetComp<UnitComp>() is UnitComp comp
                && comp.root != null)
            {
                __result = true;
            }
        }
    }
    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.CanDraftMech))]
    public static class Patch_MechDraft
    {
        [HarmonyPostfix]
        public static void Postfix(ref AcceptanceReport __result, Pawn mech)
        {
            if (mech.TryGetComp<UnitComp>() is UnitComp comp
                && comp.root != null)
            {
                __result = true;
            }
        }
    }
    [HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
    public static class Patch_MechMove
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, Pawn mech)
        {
            if (mech.TryGetComp<UnitComp>() is UnitComp comp
                && comp.root != null)
            {
                __result = true;
            }
        }
    }
}
