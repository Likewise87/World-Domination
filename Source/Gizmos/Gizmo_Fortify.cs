using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using TSA_WorldDomination.Settings;
using UnityEngine;
using Verse;
using WorldDomination;

namespace TSA_WorldDomination.Gizmos
{
    [HarmonyPatch(typeof(Caravan), "GetGizmos")]
    public static class Patch_FortifyGizmo
    {
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Caravan __instance)
        {
            foreach (var g in __result) yield return g;

            // 1. Identify Best Crafter
            Pawn bestCrafter = __instance.PawnsListForReading
                .Where(p => p.RaceProps.Humanlike && !p.Downed && !p.Dead && !p.WorkTagIsDisabled(WorkTags.ManualSkilled))
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Crafting).Level)
                .FirstOrDefault();

            if (bestCrafter == null) yield break;
            int skill = bestCrafter.skills.GetSkill(SkillDefOf.Crafting).Level;

            // 2. Identify Neighboring Settlements
            List<PlanetTile> neighborTiles = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(__instance.Tile, neighborTiles);
            HashSet<int> neighborIDs = neighborTiles.Select(pt => pt.tileId).ToHashSet();

            var targets = Find.WorldObjects.Settlements
                .Where(s => neighborIDs.Contains(s.Tile) &&
                            s.Faction != null &&
                            !s.Faction.IsPlayer &&
                            !s.Faction.HostileTo(Faction.OfPlayer) &&
                            WorldActions_Utils.IsWdSurfaceWorldObject(s))
                .Select(s => new { Settlement = s, Comp = s.GetComponent<CompViralSpread>() })
                .Where(x => x.Comp != null)
                .ToList();

            if (!targets.Any()) yield break;

            bool anyAvailable = targets.Any(t => !t.Comp.IsAidOnCooldown);

            // 3. Define Gizmo
            Command_Action fortify = new Command_Action
            {
                defaultLabel = "TSA_WD_Fort_GizmoLabel".Translate(),
                icon = anyAvailable
                    ? ContentFinder<Texture2D>.Get("UI/Commands/Fortify")
                    : ContentFinder<Texture2D>.Get("UI/Commands/Fortify_Cooldown"),
                action = () =>
                {
                    if (targets.Count == 1)
                    {
                        ExecuteFortify(targets[0].Comp, __instance, bestCrafter);
                    }
                    else
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        foreach (var target in targets)
                        {
                            float chance = GetFortifySuccessChance(skill);
                            string label = "TSA_WD_Fort_MenuOption".Translate(target.Settlement.LabelCap, chance.ToString("P0"));

                            if (target.Comp.IsAidOnCooldown)
                            {
                                options.Add(new FloatMenuOption(label + " " + "TSA_WD_OnCooldown".Translate(), null));
                            }
                            else
                            {
                                options.Add(new FloatMenuOption(label, () => ExecuteFortify(target.Comp, __instance, bestCrafter)));
                            }
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                }
            };

            if (!anyAvailable) fortify.Disable("TSA_WD_Fort_DisabledAlert".Translate());

            float totalChance = GetFortifySuccessChance(skill);
            fortify.defaultDesc = "TSA_WD_Fort_GizmoDesc".Translate(bestCrafter.LabelShort, skill, totalChance.ToString("P0"));
            yield return fortify;
        }

        private static float GetFortifySuccessChance(int skill) => Mathf.Lerp(0.05f, 0.60f, skill / 20f);
        private static float GetFortifyStrengthGain(int skill) => Mathf.Lerp(60f, 250f, skill / 20f);

        private static void ExecuteFortify(CompViralSpread comp, Caravan caravan, Pawn pawn)
        {
            if (comp.IsAidOnCooldown) return;

            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
            comp.aidCooldownUntilTick = Find.TickManager.TicksGame + 300000;

            int skill = pawn.skills.GetSkill(SkillDefOf.Crafting).Level;
            float roll = Rand.Value;
            float chance = GetFortifySuccessChance(skill);

            if (roll <= chance)
            {
                float gain = GetFortifyStrengthGain(skill);
                float oldStr = comp.strength;
                comp.strength += gain;
                comp.CheckTierUpdate();

                Find.LetterStack.ReceiveLetter("TSA_WD_Fort_LetterSuccessLabel".Translate(),
                    "TSA_WD_Fort_LetterSuccessText".Translate(pawn.LabelShort, comp.parent.Label, gain.ToString("F0")),
                    LetterDefOf.PositiveEvent, comp.parent);

                manager?.AddLog(new SpreadLogEntry("TSA_WD_Fort_LogSuccess".Translate(oldStr.ToString("F0"), comp.strength.ToString("F0")), caravan, comp.parent as Settlement));
            }
            else
            {
                Find.LetterStack.ReceiveLetter("TSA_WD_Fort_LetterFailedLabel".Translate(),
                    "TSA_WD_Fort_LetterFailedText".Translate(pawn.LabelShort, comp.parent.Label),
                    LetterDefOf.NeutralEvent, comp.parent);

                manager?.AddLog(new SpreadLogEntry("TSA_WD_Fort_LogFailed".Translate(), caravan, comp.parent as Settlement));
            }

            WorldActions_Utils.RefreshMap();
        }
    }
}