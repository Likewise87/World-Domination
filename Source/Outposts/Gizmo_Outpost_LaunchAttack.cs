using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Outposts;
using WorldDomination;
using TSA_WorldDomination.Settings;
using TSA_WorldDomination.RaidLogic;

namespace TSA_WorldDomination.Outposts
{
    [HarmonyPatch(typeof(Outpost), "GetGizmos")]
    public static class Patch_OutpostAttackGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Outpost __instance)
        {
            if (__result != null)
                foreach (var g in __result) yield return g;

            if (__instance.Faction != Faction.OfPlayer) yield break;

            var comp = __instance.GetComponent<CompViralSpread>();
            bool onCooldown = comp != null && comp.IsIncidentOnCooldown;

            Command_Action launchExpedition = new Command_Action
            {
                defaultLabel = "TSA_WD_LaunchExpedition".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack"),
                action = () => StartRaidTargeting(__instance)
            };

            if (onCooldown)
            {
                float daysLeft = (comp.incidentCooldownUntilTick - Find.TickManager.TicksGame) / 60000f;
                launchExpedition.defaultDesc = "TSA_WD_OnCooldown".Translate(daysLeft.ToString("F1"));
                launchExpedition.Disable("TSA_WD_ReasonCooldown".Translate());
            }
            else
            {
                launchExpedition.defaultDesc = "TSA_WD_LaunchExpeditionDesc".Translate();
            }

            yield return launchExpedition;
        }

        private static void StartRaidTargeting(Outpost source)
        {
            CameraJumper.TryJump(source.Tile);

            var seth = WorldDominationMod.settings;
            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();

            // ALIGNMENT: Dynamic Range (Zeal)
            float range = seth.raidTargetRadius;
            if (source.Faction == manager?.expansionistZealFaction && Find.TickManager.TicksGame < manager.expansionistZealExpiryTick)
            {
                range *= 2f;
            }

            Find.WorldTargeter.BeginTargeting(
                (target) =>
                {
                    if (target.WorldObject is Settlement settlement && settlement.Faction != null && settlement.Faction.HostileTo(Faction.OfPlayer))
                    {
                        Find.WindowStack.Add(new Dialog_OutpostRaidMath(source, settlement));
                        return true;
                    }
                    return false;
                },
                false, null, false,
                // ALIGNMENT: Added source check to prevent nulls on draw
                () => { if (source != null) GenDraw.DrawWorldRadiusRing(source.Tile, Mathf.CeilToInt(range)); },
                null,
                (target) =>
                {
                    if (!target.IsValid || target.Tile < 0) return false;
                    float dist = WorldActions_Utils.GetDistance(source.Tile, target.Tile, manager);
                    if (dist <= range && target.HasWorldObject)
                    {
                        return target.WorldObject is Settlement s && s.Faction != null && s.Faction.HostileTo(Faction.OfPlayer);
                    }
                    return false;
                }
            );
        }
    }

    public class Dialog_OutpostRaidMath : Window
    {
        private Outpost source;
        private Settlement target;

        private float totalAtkPower;
        private float totalDefPower;

        private List<WorldObject> atkReinforcements;
        private List<WorldObject> defReinforcements;
        private List<string> atkDetails;
        private List<string> defDetails;

        private float winChance;
        private float attLossWinPct;
        private float attLossLossPct;
        private Vector2 scrollAtk;
        private Vector2 scrollDef;

        public override Vector2 InitialSize => new Vector2(650f, 650f);

        public Dialog_OutpostRaidMath(Outpost source, Settlement target)
        {
            this.source = source;
            this.target = target;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            CalculateMath();
        }

        private void CalculateMath()
        {
            var seth = WorldDominationMod.settings;
            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();

            var lookup = Find.WorldObjects.AllWorldObjects
                .Where(s => s.GetComponent<CompViralSpread>() != null)
                .ToLookup(s => s.Faction);

            // 1. Reinforcements (Gathered at Source)
            atkReinforcements = Raid_ReinforcementLogic.GetReinforcements(source, target, seth.raidAttackerAllyRadius, lookup, manager, new List<Faction> { target.Faction });
            defReinforcements = Raid_ReinforcementLogic.GetReinforcements(target, source, seth.raidDefenderAllyRadius, lookup, manager, new List<Faction> { Faction.OfPlayer });

            // 2. Force Projection Multiplier
            float dist = WorldActions_Utils.GetDistance(source.Tile, target.Tile, manager);
            float range = seth.raidTargetRadius;
            if (source.Faction == manager?.expansionistZealFaction && Find.TickManager.TicksGame < manager.expansionistZealExpiryTick) range *= 2f;

            float attMult = Raid_ReinforcementLogic.GetDistanceMultiplier(dist, range, seth);

            // ALIGNMENT: Use simplified hub-gathering power for attackers
            totalAtkPower = Raid_ReinforcementLogic.CalculateTotalReinforcementPowerSimplified(atkReinforcements, source, attMult, out atkDetails);

            // Defenders remain localized
            totalDefPower = Raid_ReinforcementLogic.CalculateTotalReinforcementPower(defReinforcements, target.Tile, seth.raidDefenderAllyRadius, seth, manager, out defDetails, target, "Defender");

            // 3. Outcomes (Your exact Interpolation Logic)
            float ratio = totalAtkPower / (totalDefPower > 0 ? totalDefPower : 1f);
            var outcomes = seth.raidOutcomes.OrderBy(o => o.threshold).ToList();

            if (outcomes.Count == 0)
            {
                winChance = 0f;
                attLossWinPct = 0.2f;
                attLossLossPct = 0.50f;
            }
            else if (ratio <= outcomes.First().threshold)
            {
                var first = outcomes.First();
                winChance = first.winChance;
                attLossWinPct = first.attLossWin;
                attLossLossPct = first.attLossLoss;
            }
            else if (ratio >= outcomes.Last().threshold)
            {
                var last = outcomes.Last();
                winChance = last.winChance;
                attLossWinPct = last.attLossWin;
                attLossLossPct = last.attLossLoss;
            }
            else
            {
                for (int i = 0; i < outcomes.Count - 1; i++)
                {
                    var lower = outcomes[i];
                    var upper = outcomes[i + 1];
                    if (ratio >= lower.threshold && ratio <= upper.threshold)
                    {
                        float t = (ratio - lower.threshold) / (upper.threshold - lower.threshold);
                        winChance = Mathf.Lerp(lower.winChance, upper.winChance, t);
                        attLossWinPct = Mathf.Lerp(lower.attLossWin, upper.attLossWin, t);
                        attLossLossPct = Mathf.Lerp(lower.attLossLoss, upper.attLossLoss, t);
                        break;
                    }
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("TSA_WD_RaidAnalysis".Translate() + ": " + target.LabelCap);
            Text.Font = GameFont.Small;
            listing.GapLine();

            Rect powers = listing.GetRect(70f);
            Rect atkRect = powers.LeftHalf().ContractedBy(5f);
            Rect defRect = powers.RightHalf().ContractedBy(5f);

            Widgets.DrawBoxSolid(atkRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Widgets.DrawBoxSolid(defRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(atkRect.TopHalf(), "TSA_WD_AttackerPower".Translate());
            Text.Font = GameFont.Medium;
            Widgets.Label(atkRect.BottomHalf(), totalAtkPower.ToString("F0"));

            Text.Font = GameFont.Small;
            Widgets.Label(defRect.TopHalf(), "TSA_WD_DefenderPower".Translate());
            Text.Font = GameFont.Medium;
            Widgets.Label(defRect.BottomHalf(), totalDefPower.ToString("F0"));

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            listing.Gap(12f);
            float displayRatio = totalAtkPower / (totalDefPower > 0 ? totalDefPower : 1f);

            listing.Label("TSA_WD_WinChance".Translate() + ": " + winChance.ToStringPercent() +
                          " (" + "TSA_WD_RelativeStrength".Translate(displayRatio.ToStringPercent()) + ")");

            Rect rectBar = listing.GetRect(22f);
            Widgets.FillableBar(rectBar, winChance, SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.6f, 0.2f)));

            listing.Gap(15f);

            listing.Label("TSA_WD_ReinforcementBreakdown".Translate());
            Rect breakdownRect = listing.GetRect(150f);

            DrawDetailScroll(breakdownRect.LeftHalf().ContractedBy(2f), ref scrollAtk, atkDetails);
            DrawDetailScroll(breakdownRect.RightHalf().ContractedBy(2f), ref scrollDef, defDetails);

            listing.Gap(15f);
            GUI.color = Color.gray;
            listing.Label("TSA_WD_Implications".Translate());
            listing.Label(" - " + "TSA_WD_StrengthLossOnVictory".Translate(attLossWinPct.ToStringPercent()));
            listing.Label(" - " + "TSA_WD_StrengthLossOnDefeat".Translate(attLossLossPct.ToStringPercent()));
            GUI.color = Color.white;

            listing.GapLine();

            if (listing.ButtonText("TSA_WD_ExecuteRaid".Translate()))
            {
                ExecuteRaid();
                Close();
            }

            if (listing.ButtonText("Cancel".Translate())) Close();

            listing.End();
        }

        private void DrawDetailScroll(Rect rect, ref Vector2 scroll, List<string> lines)
        {
            Widgets.DrawMenuSection(rect);
            Text.Font = GameFont.Tiny;
            float width = rect.width - 20f;
            float totalHeight = 0f;

            if (lines == null || lines.Count == 0) lines = new List<string> { "(" + "None".Translate() + ")" };
            foreach (var line in lines) totalHeight += Text.CalcHeight(line, width) + 4f;

            Rect viewRect = new Rect(0, 0, width, totalHeight + 10f);
            Widgets.BeginScrollView(rect, ref scroll, viewRect);

            float curY = 5f;
            Text.Anchor = TextAnchor.UpperLeft;
            foreach (var line in lines)
            {
                float height = Text.CalcHeight(line, width);
                Widgets.Label(new Rect(5f, curY, width, height), line);
                curY += height + 4f;
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.EndScrollView();
        }

        private void ExecuteRaid()
        {
            var seth = WorldDominationMod.settings;
            var sourceComp = source.GetComponent<CompViralSpread>();
            bool won = Rand.Value < winChance;

            if (sourceComp != null && seth != null)
            {
                sourceComp.incidentCooldownUntilTick = Find.TickManager.TicksGame + Mathf.RoundToInt(seth.cooldownRaidDays * 60000f);
            }

            float attLoss = won ? attLossWinPct : attLossLossPct;
            sourceComp?.ReduceStrength(attLoss);
            foreach (var wo in atkReinforcements) wo.GetComponent<CompViralSpread>()?.ReduceStrength(attLoss);

            // Defender loss remains based on win/loss outcome
            float defLoss = won ? 1.0f : 0.2f;
            foreach (var wo in defReinforcements) wo.GetComponent<CompViralSpread>()?.ReduceStrength(defLoss);

            if (won)
            {
                Messages.Message("TSA_WD_RaidSuccess".Translate(target.Label), MessageTypeDefOf.PositiveEvent);
                SettlementTier tier = target.GetComponent<CompViralSpread>()?.tier ?? SettlementTier.T1;
                int tile = target.Tile;
                string originalName = target.Name ?? target.LabelCap;
                Faction targetFaction = target.Faction;

                target.Destroy();
                DestroyedSettlement ruins = (DestroyedSettlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.DestroyedSettlement);
                ruins.Tile = tile;
                ruins.SetFaction(targetFaction);
                Find.WorldObjects.Add(ruins);

                Find.WindowStack.Add(new Dialog_OutpostSelection(tile, originalName, ruins.ID, tier, null));
            }
            else
            {
                Find.LetterStack.ReceiveLetter(
                    "TSA_WD_RaidFailed_Label".Translate(),
                    "TSA_WD_RaidFailed_Text".Translate(source.Label, target.Label, (totalAtkPower * attLossLossPct).ToString("F0")),
                    LetterDefOf.NegativeEvent,
                    new GlobalTargetInfo(source.Tile)
                );
            }
        }
    }
}