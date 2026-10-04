using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Outposts;
using UnityEngine;
using WorldDomination;

namespace TSA_WorldDomination.Outposts
{
    [StaticConstructorOnStartup]
    public static class TSA_TownManager
    {
        private static readonly Material RedirectLineMat = MaterialPool.MatFrom("UI/Overlays/ThingLine", ShaderDatabase.WorldOverlayTransparent, Color.cyan, 3590);
        private static Type townType;

        // Tracks IDs of pawns present BEFORE the Produce() call
        private static HashSet<int> preProducePawnIDs = new HashSet<int>();

        static TSA_TownManager()
        {
            var harmony = new Harmony("TSA.WorldDomination.TownManager");
            townType = AccessTools.TypeByName("VOE.Outpost_Town");

            if (townType != null)
            {
                // 1. Requirements Logic
                harmony.Patch(AccessTools.Method(townType, "CanSpawnOnWith"), prefix: new HarmonyMethod(typeof(TSA_TownManager), nameof(Prefix_RequirementLogic)));
                harmony.Patch(AccessTools.Method(townType, "RequirementsString"), prefix: new HarmonyMethod(typeof(TSA_TownManager), nameof(Prefix_RequirementLogic)));

                // 2. Production Hook - Sandwich Strategy
                harmony.Patch(AccessTools.Method(townType, "Produce"),
                    prefix: new HarmonyMethod(typeof(TSA_TownManager), nameof(Prefix_SnapshotBefore)),
                    postfix: new HarmonyMethod(typeof(TSA_TownManager), nameof(Postfix_RedirectAfter)));

                // 3. UI/Gizmo/Visuals
                harmony.Patch(AccessTools.Method(typeof(Outpost), nameof(Outpost.GetGizmos)), postfix: new HarmonyMethod(typeof(TSA_TownManager), nameof(Postfix_TownGizmos)));
                harmony.Patch(AccessTools.Method(typeof(Outpost), nameof(Outpost.DrawExtraSelectionOverlays)), postfix: new HarmonyMethod(typeof(TSA_TownManager), nameof(Postfix_DrawRedirectionLine)));
            }
        }

        private static bool IsTown(Outpost outpost) => outpost != null && townType != null && townType.IsInstanceOfType(outpost);

        // --- PRODUCTION SNAPSHOTS ---

        public static void Prefix_SnapshotBefore(Outpost __instance)
        {
            preProducePawnIDs.Clear();
            if (__instance.AllPawns == null) return;
            foreach (var p in __instance.AllPawns) preProducePawnIDs.Add(p.thingIDNumber);
        }

        public static void Postfix_RedirectAfter(Outpost __instance)
        {
            if (!IsTown(__instance) || __instance.Faction != Faction.OfPlayer) return;

            var comp = __instance.GetComponent<CompViralSpread>();
            if (comp == null || comp.redirectionTargetTile == -1) return;

            // Identify recruits by comparing current roster to snapshot
            List<Pawn> newRecruits = __instance.AllPawns
                .Where(p => !preProducePawnIDs.Contains(p.thingIDNumber))
                .ToList();

            if (newRecruits.Any())
            {
                // Create a list for the caravan
                List<Pawn> caravanMembers = new List<Pawn>();

                foreach (var p in newRecruits)
                {
                    // 1. Surgical Removal using the Outpost function you provided
                    __instance.RemovePawn(p);

                    // 4. Provision
                    Thing meals = ThingMaker.MakeThing(ThingDefOf.MealSimple);
                    meals.stackCount = 5;
                    p.inventory.innerContainer.TryAdd(meals);

                    caravanMembers.Add(p);
                }

                // 5. Launch the Caravan (true cleans up internal RimWorld states)
                Caravan caravan = CaravanMaker.MakeCaravan(caravanMembers, Faction.OfPlayer, __instance.Tile, true);
                if (caravan != null)
                {
                    caravan.pather.StartPath(comp.redirectionTargetTile, null, true);
                    Messages.Message("TSA_WD_RecruitsRedirected".Translate(caravanMembers.Count, __instance.LabelCap), caravan, MessageTypeDefOf.PositiveEvent);
                }
            }
            preProducePawnIDs.Clear();
        }

        // --- GIZMO, REQUIREMENTS, VISUALS ---

        public static void Postfix_TownGizmos(Outpost __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!IsTown(__instance) || __instance.Faction != Faction.OfPlayer) return;
            var comp = __instance.GetComponent<CompViralSpread>();
            if (comp == null) return;
            List<Gizmo> list = __result.ToList();
            list.Add(new Command_Action
            {
                defaultLabel = "TSA_WD_SetRedirection".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/AutoSendPawn", false),
                action = () => {
                    Find.WorldTargeter.BeginTargeting((target) => {
                        if (target.Tile >= 0) { comp.redirectionTargetTile = target.Tile; return true; }
                        return false;
                    }, false, null, false, null, null, (target) => target.HasWorldObject);
                }
            });
            if (comp.redirectionTargetTile != -1)
            {
                list.Add(new Command_Action
                {
                    defaultLabel = "TSA_WD_ClearRedirection".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false),
                    action = () => comp.redirectionTargetTile = -1
                });
            }
            __result = list;
        }

        public static bool Prefix_RequirementLogic(PlanetTile tile, ref string __result, System.Reflection.MethodBase __originalMethod)
        {
            int count = Find.WorldObjects.AllWorldObjects.Count(o => (o is Settlement || o is Outpost) && Find.WorldGrid.TraversalDistanceBetween(o.Tile, tile.tileId) < 10);
            bool satisfied = count >= 3;
            if (__originalMethod.Name == "CanSpawnOnWith") __result = satisfied ? null : "Outposts.NearbySettlements".Translate(3, 10);
            else __result = "Outposts.NearbySettlements".Translate(3, 10).Requirement(satisfied);
            return false;
        }

        public static void Postfix_DrawRedirectionLine(Outpost __instance)
        {
            if (!IsTown(__instance)) return;
            if (Find.WorldSelector.IsSelected(__instance))
            {
                var comp = __instance.GetComponent<CompViralSpread>();
                if (comp != null && comp.redirectionTargetTile != -1)
                {
                    Vector3 startPos = Find.WorldGrid.GetTileCenter(__instance.Tile);
                    Vector3 endPos = Find.WorldGrid.GetTileCenter(comp.redirectionTargetTile);
                    GenDraw.DrawWorldLineBetween(startPos + startPos.normalized * 0.05f, endPos + endPos.normalized * 0.05f, RedirectLineMat, 1.00f);
                }
            }
        }
    }
}