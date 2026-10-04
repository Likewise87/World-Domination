using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Outposts;
using TSA_WorldDomination.Settings;

namespace TSA_WorldDomination
{
    public class WITab_Outpost_Logistics : WITab
    {
        private Vector2 scrollPosition;
        private float scrollViewHeight;
        public WITab_Outpost_Logistics() { this.size = new Vector2(500f, 600f); this.labelKey = "TSA_LogisticsTab"; }

        public override bool IsVisible =>
            WorldDominationMod.settings.foodLogisticsActive &&
            SelOutpost != null &&
            SelOutpost.Faction == Faction.OfPlayer;
        public Outpost SelOutpost => base.SelObject as Outpost;
        public CompOutpostLogistics Logi => SelOutpost?.GetComponent<CompOutpostLogistics>();

        protected override void FillTab()
        {
            if (Logi == null) return;
            var manager = Find.World.GetComponent<WorldComponent_LogisticsManager>();
            var s = WorldDominationMod.settings;
            bool isProducer = SelOutpost.def.defName.Contains("Farming") || SelOutpost.def.defName.Contains("Hunting");

            // Fixed Y-offset to prevent cropping at the top
            float yOffset = 30f;
            Rect rect = new Rect(0f, yOffset, this.size.x, this.size.y - yOffset).ContractedBy(10f);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            Text.Font = GameFont.Medium;
            listing.Label(isProducer ? "TSA_DistroHub".Translate() : "TSA_ResourceReceiving".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine();

            // NEW: Gate the logistics logic behind the toggle for producers
            if (isProducer && !Logi.produceForLogistics)
            {
                GUI.color = Color.yellow;
                listing.Label("TSA_LogisticsDisabledWarning".Translate());
                GUI.color = Color.white;
                listing.Gap(10f);
                listing.Label("TSA_LogisticsDisabledExplanation".Translate());
                listing.End();
                return;
            }

            // --- Header Section ---
            float dailyProd = manager.GetDailyProduction(SelOutpost);
            float demand = manager.GetDailyDemand(SelOutpost);
            float netBase = dailyProd - demand;

            listing.Label("TSA_FoodProdConsumTitle".Translate());

            // Mouseover tooltip for math breakdown using settings-driven values
            Rect mathRect = listing.GetRect(Text.LineHeight);
            string mathLabel = "TSA_FoodProdConsumMath".Translate(dailyProd.ToString("F1"), demand.ToString("F1"), netBase.ToString("F1"));
            Widgets.Label(mathRect, mathLabel);
            TooltipHandler.TipRegion(mathRect, GetProductionBreakdown(manager));

            // Local Storage with Dynamic Color Coding
            Color storageCol = Logi.currentFood <= 20f ? Color.red : (Logi.currentFood <= 50f ? Color.yellow : Color.green);
            listing.LabelDouble("TSA_LocalStorage".Translate() + ":", $"{Logi.currentFood:F1} / 100".Colorize(storageCol));

            // Local change per Day
            float outgoing = manager.manualLinks.Where(l => l.sourceTile == SelOutpost.Tile).Sum(l => l.manualAssignment);
            float incoming = manager.manualLinks.Where(l => l.destTile == SelOutpost.Tile).Sum(l => l.manualAssignment * manager.GetEfficiency(l.sourceTile, l.destTile));
            float realTimeNet = netBase + incoming - outgoing;
            Color balCol = realTimeNet > 0.1f ? Color.green : (realTimeNet < -0.1f ? Color.red : Color.yellow);
            listing.LabelDouble("TSA_LocalChangePerDay".Translate() + ":", "TSA_UnitsCount".Translate(realTimeNet.ToString("F1")).Colorize(balCol));

            listing.GapLine();

            if (isProducer)
            {
                DrawSmartToggles(listing, manager);
                // Real-time status label
                listing.Label("TSA_AllocatedStatus".Translate(outgoing.ToString("F1"), Mathf.Max(0, netBase).ToString("F1")));
                listing.Gap(10f);
                listing.Label("TSA_OutpostsInRange".Translate());
                DrawProducerInterface(rect, listing, manager, Mathf.Max(0, netBase), outgoing);
            }
            else
            {
                listing.Label("TSA_IncomingSupplyLines".Translate());
                DrawConsumerInterface(rect, listing, manager);
            }
            listing.End();
        }

        // Helper to generate the math breakdown string using settings
        private string GetProductionBreakdown(WorldComponent_LogisticsManager manager)
        {
            StringBuilder sb = new StringBuilder();
            var s = WorldDominationMod.settings;
            bool isFarming = SelOutpost.def.defName.Contains("Farming");
            bool isHunting = SelOutpost.def.defName.Contains("Hunting");

            sb.AppendLine("TSA_Breakdown_Title".Translate()); // Production Breakdown
            sb.AppendLine($" - {"TSA_Breakdown_Base".Translate()}: {(s.foodConsumptionPerPawn * 2f):F1}");

            if (isFarming || isHunting)
            {
                float totalSkill = 0f;
                string skillName = isFarming ? SkillDefOf.Plants.label : $"{SkillDefOf.Shooting.label}/{SkillDefOf.Animals.label}";

                foreach (Pawn pawn in SelOutpost.AllPawns)
                {
                    if (pawn.skills == null) continue;
                    if (isFarming) totalSkill += pawn.skills.GetSkill(SkillDefOf.Plants).Level;
                    else totalSkill += Mathf.Max(pawn.skills.GetSkill(SkillDefOf.Shooting).Level, pawn.skills.GetSkill(SkillDefOf.Animals).Level);
                }

                float skillBonus = totalSkill * s.foodProductionPerSkill;
                bool capped = skillBonus > 35f;

                sb.AppendLine($" - {"TSA_Breakdown_SkillBonus".Translate()}: {s.foodProductionPerSkill:F1} * {totalSkill} ({skillName}) = {Mathf.Min(skillBonus, 35f):F1}" + (capped ? " (Capped)" : ""));
            }

            sb.AppendLine("------------------");
            sb.AppendLine($"{"TSA_Breakdown_TotalProd".Translate()}: {manager.GetDailyProduction(SelOutpost):F1}");
            sb.AppendLine($"{"TSA_Breakdown_Consumption".Translate()}: -{manager.GetDailyDemand(SelOutpost):F1} ({SelOutpost.AllPawns.Count()} {"Pawns".Translate()} @ {s.foodConsumptionPerPawn:F1})");

            return sb.ToString();
        }

        private void DrawSmartToggles(Listing_Standard listing, WorldComponent_LogisticsManager manager)
        {
            Rect toggleRect = listing.GetRect(30f);
            float btnSize = 24f;
            float spacing = 115f;

            string autoUpdateNote = "TSA_AutoUpdateNote".Translate();

            // Equal
            Rect equalRect = new Rect(toggleRect.x, toggleRect.y, spacing, btnSize);
            if (Widgets.RadioButton(equalRect.x, equalRect.y, Logi.mode == LogisticsMode.Equal))
            { Logi.mode = LogisticsMode.Equal; manager.ExecuteSmartLogic(SelOutpost, Logi); }
            Widgets.Label(new Rect(equalRect.x + btnSize + 4f, equalRect.y, 80f, btnSize), "TSA_LogisticsMode_Equal".Translate());
            TooltipHandler.TipRegion(equalRect, "TSA_EqualTooltip".Translate() + autoUpdateNote);

            // Feed
            Rect feedRect = new Rect(toggleRect.x + spacing, toggleRect.y, spacing, btnSize);
            if (Widgets.RadioButton(feedRect.x, feedRect.y, Logi.mode == LogisticsMode.FullyFeed))
            { Logi.mode = LogisticsMode.FullyFeed; manager.ExecuteSmartLogic(SelOutpost, Logi); }
            Widgets.Label(new Rect(feedRect.x + btnSize + 4f, feedRect.y, 80f, btnSize), "TSA_LogisticsMode_FullyFeed".Translate());
            TooltipHandler.TipRegion(feedRect, "TSA_FeedTooltip".Translate() + autoUpdateNote);

            // Deficit
            Rect deficitRect = new Rect(toggleRect.x + spacing * 2, toggleRect.y, spacing, btnSize);
            if (Widgets.RadioButton(deficitRect.x, deficitRect.y, Logi.mode == LogisticsMode.BiggestDeficit))
            { Logi.mode = LogisticsMode.BiggestDeficit; manager.ExecuteSmartLogic(SelOutpost, Logi); }
            Widgets.Label(new Rect(deficitRect.x + btnSize + 4f, deficitRect.y, 80f, btnSize), "TSA_LogisticsMode_BiggestDeficit".Translate());
            TooltipHandler.TipRegion(deficitRect, "TSA_DeficitTooltip".Translate() + autoUpdateNote);

            // Manual
            Rect manualRect = new Rect(toggleRect.x + spacing * 3, toggleRect.y, spacing, btnSize);
            if (Widgets.RadioButton(manualRect.x, manualRect.y, Logi.mode == LogisticsMode.Manual))
            { Logi.mode = LogisticsMode.Manual; }
            Widgets.Label(new Rect(manualRect.x + btnSize + 4f, manualRect.y, 80f, btnSize), "TSA_LogisticsMode_Manual".Translate());
            TooltipHandler.TipRegion(manualRect, "TSA_ManualTooltip".Translate());
        }

        private void DrawProducerInterface(Rect rect, Listing_Standard listing, WorldComponent_LogisticsManager manager, float budget, float totalAssigned)
        {
            var s = WorldDominationMod.settings;
            float curY = listing.CurHeight + 10f;
            Rect scrollRect = new Rect(0f, curY, rect.width, rect.height - curY);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, scrollViewHeight);
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
            float innerY = 0f;

            var others = Find.WorldObjects.AllWorldObjects.OfType<WorldObject>()
                .Where(o => o.Faction == Faction.OfPlayer && o != SelOutpost && o.GetComponent<CompOutpostLogistics>() != null
                       && !o.def.defName.Contains("Farming") && !o.def.defName.Contains("Hunting")
                       && Find.WorldGrid.ApproxDistanceInTiles(SelOutpost.Tile, o.Tile) <= s.maxLogisticsRange)
                .OrderBy(o => Find.WorldGrid.ApproxDistanceInTiles(SelOutpost.Tile, o.Tile)).ToList();

            foreach (var target in others)
            {
                var targetLogi = target.GetComponent<CompOutpostLogistics>();
                var link = manager.manualLinks.FirstOrDefault(l => l.sourceTile == SelOutpost.Tile && l.destTile == target.Tile);
                float efficiency = manager.GetEfficiency(SelOutpost.Tile, target.Tile);

                Rect row = new Rect(0f, innerY, viewRect.width, 60f);
                Widgets.DrawHighlightIfMouseover(row);

                Rect jumpRect = new Rect(row.x, row.y, 180f, row.height);
                if (Widgets.ButtonInvisible(jumpRect))
                {
                    CameraJumper.TryJump(target);
                    Find.WorldSelector.ClearSelection();
                    Find.WorldSelector.Select(target);
                }

                float contentY = innerY + (row.height / 2f);
                string nameLabel = $"{target.LabelCap} ({target.def.label})";
                Widgets.Label(new Rect(0f, contentY - 22f, 180f, 25f), nameLabel.Truncate(175f));

                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Rect infoRect = new Rect(0f, contentY + 2f, 180f, 20f);
                Widgets.Label(infoRect, "TSA_OutpostInfoRow".Translate(targetLogi.currentFood.ToString("F1"), CompOutpostLogistics.MaxFood.ToString("F0"), efficiency.ToString("P0")));
                TooltipHandler.TipRegion(infoRect, "TSA_EfficiencyTooltip".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                float tNet = manager.GetDailyProduction(target) - manager.GetDailyDemand(target)
                           + manager.manualLinks.Where(l => l.destTile == target.Tile).Sum(l => l.manualAssignment * manager.GetEfficiency(l.sourceTile, l.destTile))
                           - manager.manualLinks.Where(l => l.sourceTile == target.Tile).Sum(l => l.manualAssignment);

                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(190f, innerY, 80f, row.height), "TSA_NetValue".Translate(tNet.ToString("F1")).Colorize(tNet > 0.1f ? Color.green : (tNet < -0.1f ? Color.red : Color.yellow)));

                float btnY = innerY + (row.height / 2f) - 12f;
                float val = link?.manualAssignment ?? 0f;

                if (Widgets.ButtonText(new Rect(280f, btnY, 25f, 25f), "-"))
                    AdjustLink(manager, target.Tile, -1f, budget, true);

                Widgets.Label(new Rect(310f, innerY, 40f, row.height), val.ToString("F1"));

                bool canAdd = (totalAssigned + 0.05f) < budget;
                if (!canAdd) GUI.color = Color.gray;
                if (Widgets.ButtonText(new Rect(355f, btnY, 25f, 25f), "+") && canAdd)
                    AdjustLink(manager, target.Tile, 1f, budget, false);

                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                innerY += 65f;
            }
            if (Event.current.type == EventType.Layout) scrollViewHeight = innerY;
            Widgets.EndScrollView();
        }

        private void DrawConsumerInterface(Rect rect, Listing_Standard listing, WorldComponent_LogisticsManager manager)
        {
            var sources = manager.manualLinks.Where(l => l.destTile == SelOutpost.Tile && l.manualAssignment > 0.01f);
            if (!sources.Any()) { listing.Label("TSA_Isolated".Translate()); return; }

            foreach (var link in sources)
            {
                var srcOp = Find.WorldObjects.WorldObjectAt<WorldObject>(link.sourceTile);
                if (srcOp == null) continue;

                float received = link.manualAssignment * manager.GetEfficiency(link.sourceTile, link.destTile);
                Rect sourceRect = listing.GetRect(25f);

                if (Widgets.ButtonInvisible(sourceRect))
                {
                    CameraJumper.TryJump(srcOp);
                    Find.WorldSelector.ClearSelection();
                    Find.WorldSelector.Select(srcOp);
                }
                Widgets.DrawHighlightIfMouseover(sourceRect);
                Widgets.Label(sourceRect, "TSA_ReceivingFrom".Translate(received.ToString("F1"), srcOp.LabelCap, srcOp.def.label));
            }
        }

        private void AdjustLink(WorldComponent_LogisticsManager manager, int targetTile, float change, float budget, bool isMinus)
        {
            Logi.mode = LogisticsMode.Manual;
            var link = manager.manualLinks.FirstOrDefault(l => l.sourceTile == SelOutpost.Tile && l.destTile == targetTile);
            if (link == null) { link = new WorldComponent_LogisticsManager.LogisticsLink { sourceTile = SelOutpost.Tile, destTile = targetTile }; manager.manualLinks.Add(link); }

            float currentTotal = manager.manualLinks.Where(l => l.sourceTile == SelOutpost.Tile).Sum(l => l.manualAssignment);
            float remaining = budget - currentTotal;

            if (isMinus)
            {
                if (link.manualAssignment <= 0) return;
                float dec = link.manualAssignment - (float)Math.Floor(link.manualAssignment);
                if (dec > 0.01f) link.manualAssignment -= dec; else link.manualAssignment -= 1.0f;
            }
            else
            {
                float toAdd = (remaining < 1.0f) ? remaining : 1.0f;
                if (toAdd <= 0) return; link.manualAssignment += toAdd;
            }
            link.manualAssignment = (float)Math.Round(Mathf.Max(0, link.manualAssignment), 1);
        }
    }
}