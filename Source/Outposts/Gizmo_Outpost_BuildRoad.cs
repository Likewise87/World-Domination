using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.WorldActions;
using HarmonyLib;
using Outposts;
using TSA_WorldDomination.Settings;
using WorldDomination;

namespace TSA_WorldDomination.Outposts
{
    [HarmonyPatch(typeof(Outpost), nameof(Outpost.GetGizmos))]
    public static class Patch_OutpostBuildRoadGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Outpost __instance)
        {
            if (__result != null)
            {
                foreach (var g in __result) yield return g;
            }

            if (__instance.Faction != Faction.OfPlayer) yield break;

            var comp = __instance.GetComponent<CompViralSpread>();
            if (comp == null) yield break;

            var buildRoad = new Command_Action
            {
                defaultLabel = "TSA_WD_BuildRoad".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/BuildRoad", false) ?? TexCommand.Replant
            };

            if (comp.roadTargetTile != -1)
            {
                // UI UPDATE: Set label to the type of road being built
                string roadTypeLabel = comp.selectedRoadTier == SettlementTier.T3 ? "TSA_WD_RoadAsphalt".Translate() :
                                       comp.selectedRoadTier == SettlementTier.T2 ? "TSA_WD_RoadStone".Translate() :
                                       "TSA_WD_RoadDirt".Translate();

                buildRoad.defaultLabel = roadTypeLabel;

                string targetName = !comp.roadTargetName.NullOrEmpty() ? comp.roadTargetName : "Tile " + comp.roadTargetTile;
                buildRoad.Disable("TSA_WD_RoadAlreadyInProgress".Translate());
                buildRoad.defaultDesc = "TSA_WD_BuildRoadStatus".Translate(targetName, (comp.roadProgress * 100f).ToString("F0"));
            }
            else
            {
                buildRoad.defaultDesc = "TSA_WD_BuildRoadDesc".Translate();
                buildRoad.action = () =>
                {
                    bool hasRotR = DefDatabase<RoadDef>.GetNamed("StoneRoadBuilt", false) != null;

                    if (!hasRotR)
                    {
                        comp.selectedRoadTier = SettlementTier.T1;
                        StartRoadTargeting(__instance, comp);
                    }
                    else
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        float totalConstruction = GetTotalConstructionSkill(__instance);

                        options.Add(new FloatMenuOption("TSA_WD_RoadDirt".Translate(), () =>
                        {
                            comp.selectedRoadTier = SettlementTier.T1;
                            StartRoadTargeting(__instance, comp);
                        }));

                        var stoneOption = new FloatMenuOption("TSA_WD_RoadStone".Translate(), () =>
                        {
                            comp.selectedRoadTier = SettlementTier.T2;
                            StartRoadTargeting(__instance, comp);
                        });
                        if (totalConstruction < 15f)
                        {
                            stoneOption.Disabled = true;
                            stoneOption.Label = "TSA_WD_RoadStone_Disabled".Translate(totalConstruction, 15);
                        }
                        options.Add(stoneOption);

                        var asphaltOption = new FloatMenuOption("TSA_WD_RoadAsphalt".Translate(), () =>
                        {
                            comp.selectedRoadTier = SettlementTier.T3;
                            StartRoadTargeting(__instance, comp);
                        });
                        if (totalConstruction < 30f)
                        {
                            asphaltOption.Disabled = true;
                            asphaltOption.Label = "TSA_WD_RoadAsphalt_Disabled".Translate(totalConstruction, 30);
                        }
                        options.Add(asphaltOption);

                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                };
            }

            yield return buildRoad;

            if (comp.roadTargetTile != -1)
            {
                yield return new Command_Action
                {
                    defaultLabel = "TSA_WD_CancelRoad".Translate(),
                    defaultDesc = "TSA_WD_CancelRoadDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                    action = delegate
                    {
                        comp.roadTargetTile = -1;
                        comp.roadTargetName = string.Empty;
                        comp.roadProgress = 0f;
                        Messages.Message("TSA_WD_RoadCancelled".Translate(__instance.LabelCap), MessageTypeDefOf.NeutralEvent);
                    }
                };
            }
        }

        private static float GetTotalConstructionSkill(Outpost outpost)
        {
            return outpost.AllPawns?.Sum(p => p.skills?.GetSkill(SkillDefOf.Construction).Level ?? 0) ?? 0f;
        }

        private static void StartRoadTargeting(Outpost source, CompViralSpread comp)
        {
            CameraJumper.TryJump(source.Tile);
            var seth = WorldDominationMod.settings;
            float range = seth.maxRoadRange;

            Find.WorldTargeter.BeginTargeting(
                (target) =>
                {
                    if (target.Tile >= 0)
                    {
                        int currentGap = WorldActions_Roads.GetCurrentWorkTile(source.Tile, target.Tile, comp.selectedRoadTier);
                        if (currentGap == -1)
                        {
                            Messages.Message("TSA_WD_RoadAlreadyExists".Translate(), MessageTypeDefOf.RejectInput);
                            return false;
                        }

                        comp.roadTargetTile = target.Tile;
                        comp.roadProgress = 0f;

                        var worldObject = Find.WorldObjects.ObjectsAt(target.Tile).FirstOrDefault(x => x is Settlement || x is Outpost);
                        comp.roadTargetName = worldObject != null ? worldObject.LabelCap : "Tile " + target.Tile;

                        Messages.Message("TSA_WD_RoadTargetSet".Translate(comp.roadTargetName), MessageTypeDefOf.PositiveEvent);
                        return true;
                    }
                    return false;
                },
                false,
                null,
                false,
                () => { GenDraw.DrawWorldRadiusRing(source.Tile, (int)Mathf.Ceil(range)); },
                null,
                (target) =>
                {
                    if (!target.IsValid || target.Tile < 0) return false;
                    return Find.WorldGrid.ApproxDistanceInTiles(source.Tile, target.Tile) <= Mathf.CeilToInt(range);
                }
            );
        }
    }

    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(Outpost), nameof(Outpost.DrawExtraSelectionOverlays))]
    public static class Patch_OutpostRoadOverlay
    {
        private static readonly Color RoadOrange = new Color(1f, 0.5f, 0.1f, 0.8f);
        private static readonly Material RoadLineMat = MaterialPool.MatFrom("UI/Overlays/ThingLine", ShaderDatabase.WorldOverlayTransparent, RoadOrange, 3590);
        private static readonly Material WorksiteHighlightMat = MaterialPool.MatFrom("UI/Overlays/RoadTarget", ShaderDatabase.WorldOverlayTransparent, RoadOrange, 3590);

        public static void Postfix(Outpost __instance)
        {
            if (__instance.Faction != Faction.OfPlayer || !Find.WorldSelector.IsSelected(__instance)) return;

            var comp = __instance.GetComponent<CompViralSpread>();
            if (comp != null && comp.roadTargetTile != -1)
            {
                Vector3 startPos = Find.WorldGrid.GetTileCenter(__instance.Tile);
                Vector3 endPos = Find.WorldGrid.GetTileCenter(comp.roadTargetTile);
                GenDraw.DrawWorldLineBetween(startPos + startPos.normalized * 0.05f, endPos + endPos.normalized * 0.05f, RoadLineMat, 0.6f);

                int workTile = WorldActions_Roads.GetCurrentWorkTile(__instance.Tile, comp.roadTargetTile, comp.selectedRoadTier);
                if (workTile != -1)
                {
                    Vector3 tileCenter = Find.WorldGrid.GetTileCenter(workTile);
                    float size = Find.WorldGrid.AverageTileSize * 0.6f;
                    WorldRendererUtility.DrawQuadTangentialToPlanet(tileCenter, size, 0.02f, WorksiteHighlightMat);
                }
            }
        }
    }
}