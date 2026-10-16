using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using Outposts;
using TSA_WorldDomination.Settings;

namespace TSA_WorldDomination
{
    public class WITab_Outpost_Logistics : WITab
    {
        private Vector2 scrollPosition;
        private float scrollViewHeight;
        private Vector2 consumerScrollPosition;
        private float consumerScrollViewHeight;

        private const int LogisticsTabWorldScanIntervalTicks = 90;
        private int logisticsTabWorldScanTick = -999999;
        private Outpost logisticsTabScanCachedSel;
        private readonly List<WorldObject> logisticsSupportOutpostsCache = new List<WorldObject>(8);
        private int logisticsConsumerLinksCacheTick = -999999;
        private Outpost logisticsConsumerCacheSel;
        private readonly List<WorldComponent_LogisticsManager.LogisticsLink> logisticsConsumerInboundCache =
            new List<WorldComponent_LogisticsManager.LogisticsLink>(8);

        private int producerRecipientScanCachedLogisticsGen = int.MinValue;
        private int consumerInboundCachedLogisticsGen = int.MinValue;

        private static bool s_staticInit;
        private static string s_toggleLblSmart, s_toggleLblManual;
        private static string s_toggleTipSmart, s_toggleTipManual;
        private static string s_outpostInfoTip;
        private static string s_allocationStrategy, s_incomingSupplyLines, s_distributeTo;
        private static string s_keepHere, s_keepHereTip, s_totalNetTip;
        private static string s_resetAllocation, s_resetAllocationTip;
        private static string s_foodProdConsumTitle, s_localStorage, s_localChangePerDay;
        private static string s_disabledWarning, s_disabledExplanation;
        private static string s_distroHub, s_resourceReceiving;

        private struct RowStrings
        {
            public string jump, name, info;
        }
        private readonly Dictionary<int, RowStrings> rowStrCache = new Dictionary<int, RowStrings>();
        private readonly Dictionary<int, RowStrings> consumerRowStrCache = new Dictionary<int, RowStrings>();

        private const float TabHeaderConsumedHeight = 38f;
        private const float ColumnGap = 20f;
        private const float LeftColumnFraction = 0.46f;
        private const float RightColumnBoost = 80f;
        private const float RowIconSize = 28f;
        private const float ProducerRowHeight = 58f;
        private const float ProducerRowStep = 62f;
        private const float AssignmentBtnW = 24f;
        private const float AssignmentValueW = 40f;
        private const float ProducerNetW = 60f;
        private const float ProducerScrollbarPad = 10f;

        public WITab_Outpost_Logistics() { this.size = new Vector2(860f, 560f); this.labelKey = "TSA_LogisticsTab"; }

        public override bool IsVisible =>
            WorldDominationMod.settings.foodLogisticsActive &&
            SelOutpost != null &&
            SelOutpost.Faction == Faction.OfPlayer;

        public Outpost SelOutpost => base.SelObject as Outpost;
        public CompOutpostLogistics Logi => SelOutpost?.GetComponent<CompOutpostLogistics>();

        private static void EnsureStaticInit()
        {
            if (s_staticInit) return;
            s_staticInit = true;
            string note = "TSA_AutoUpdateNote".Translate();
            s_toggleLblSmart = "TSA_LogisticsMode_Smart".Translate();
            s_toggleLblManual = "TSA_LogisticsMode_Manual".Translate();
            s_toggleTipSmart = "TSA_DeficitTooltip".Translate() + note;
            s_toggleTipManual = "TSA_ManualTooltip".Translate();
            s_outpostInfoTip = "TSA_Logistics_CurrentMaxFood_Tooltip".Translate();
            s_allocationStrategy = "TSA_AllocationStrategy".Translate();
            s_incomingSupplyLines = "TSA_IncomingSupplyLines".Translate();
            s_distributeTo = "TSA_Logistics_SectionOtherOutposts".Translate();
            s_keepHere = "TSA_Logistics_KeepHere".Translate();
            s_keepHereTip = "TSA_Logistics_KeepHereTip".Translate();
            s_totalNetTip = "TSA_Logistics_TotalNetTip".Translate();
            s_resetAllocation = "TSA_Logistics_ResetAllocation".Translate();
            s_resetAllocationTip = "TSA_Logistics_ResetAllocationTip".Translate();
            s_foodProdConsumTitle = "TSA_FoodProdConsumTitle".Translate();
            s_localStorage = "TSA_LocalStorage".Translate();
            s_localChangePerDay = "TSA_LocalChangePerDay".Translate();
            s_disabledWarning = "TSA_LogisticsDisabledWarning".Translate();
            s_disabledExplanation = "TSA_LogisticsDisabledExplanation".Translate();
            s_distroHub = "TSA_DistroHub".Translate();
            s_resourceReceiving = "TSA_ResourceReceiving".Translate();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Find.World?.GetComponent<WorldComponent_LogisticsManager>()
                ?.TryReconcileSmartFromFingerprint("food tab open");
        }

        private static void LabelAnchored(Rect rect, string text, TextAnchor anchor)
        {
            TextAnchor prev = Text.Anchor;
            Text.Anchor = anchor;
            Widgets.Label(rect, text);
            Text.Anchor = prev;
        }

        private static string FormatNetColored(float net)
        {
            string netNum = net > 0.05f ? ("+" + net.ToString("F1")) : net.ToString("F1");
            return netNum.Colorize(net > 0.1f ? Color.green : (net < -0.1f ? Color.red : Color.yellow));
        }

        private static float AdjustmentStep()
        {
            if (Event.current.control && Event.current.shift) return 1000f;
            if (Event.current.control) return 100f;
            if (Event.current.shift) return 10f;
            return 1f;
        }

        private static void GetProducerRowLayout(float rowWidth, out float nameX, out float nameW, out float netX, out float ctrlX, out float ctrlW)
        {
            ctrlW = AssignmentBtnW + AssignmentValueW + AssignmentBtnW;
            ctrlX = rowWidth - ctrlW - ProducerScrollbarPad;
            netX = ctrlX - ProducerNetW - 6f;
            nameX = RowIconSize + 6f;
            nameW = netX - nameX - 6f;
        }

        private static void DrawSectionHeader(float x, float y, float w, string label)
        {
            GUI.color = Widgets.SeparatorLabelColor;
            Widgets.Label(new Rect(x, y, w, 22f), label);
            GUI.color = Color.white;
        }

        private static void DrawLabelRow(float x, ref float y, float w, string left, string right, string tip = null)
        {
            Rect row = new Rect(x, y, w, 22f);
            Widgets.Label(new Rect(x, y, w * 0.55f, 22f), left);
            LabelAnchored(new Rect(x + w * 0.55f, y, w * 0.45f, 22f), right, TextAnchor.MiddleRight);
            if (!string.IsNullOrEmpty(tip))
                TooltipHandler.TipRegion(row, tip);
            y += 24f;
        }

        protected override void FillTab()
        {
            EnsureStaticInit();
            if (Logi == null) return;
            var manager = Find.World.GetComponent<WorldComponent_LogisticsManager>();
            bool isProducer = WorldComponent_LogisticsManager.IsFoodProducerOutpost(SelOutpost);

            this.size = new Vector2(isProducer ? 940f : 860f, 560f);

            Rect body = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);

            float leftColW = Mathf.Max(200f, body.width * LeftColumnFraction - RightColumnBoost);
            float rightX = body.x + leftColW + ColumnGap;
            float rightW = body.width - leftColW - ColumnGap;

            Text.Font = GameFont.Medium;
            string headline = isProducer ? s_distroHub : s_resourceReceiving;
            LabelAnchored(new Rect(body.x, body.y, body.width, 30f),
                headline + ": " + SelOutpost.LabelCap, TextAnchor.MiddleLeft);
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(body.x, body.y + 32f, body.width);

            float contentY = body.y + TabHeaderConsumedHeight;
            float contentH = body.height - TabHeaderConsumedHeight;

            if (isProducer && !Logi.produceForLogistics)
            {
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(body.x, contentY, body.width, 28f), s_disabledWarning);
                GUI.color = Color.white;
                Widgets.Label(new Rect(body.x, contentY + 32f, body.width, contentH - 32f), s_disabledExplanation);
                return;
            }

            DrawLeftFoodSummary(new Rect(body.x, contentY, leftColW, contentH), manager);

            float dividerX = body.x + leftColW + ColumnGap * 0.5f;
            Widgets.DrawLineVertical(dividerX, contentY, contentH);

            Rect rightRect = new Rect(rightX, contentY, rightW, contentH);
            if (isProducer)
                DrawProducerColumn(rightRect, manager);
            else
                DrawReceiverColumn(rightRect, manager);
        }

        private void DrawLeftFoodSummary(Rect leftRect, WorldComponent_LogisticsManager manager)
        {
            float y = leftRect.y;
            DrawSectionHeader(leftRect.x, y, leftRect.width, s_foodProdConsumTitle);
            y += 24f;

            float dailyProd = manager.GetDailyProduction(SelOutpost);
            float demand = manager.GetDailyDemand(SelOutpost);
            float netBase = dailyProd - demand;
            string math = "TSA_FoodProdConsumMath".Translate(dailyProd.ToString("F1"), demand.ToString("F1"), netBase.ToString("F1"));
            Rect mathRect = new Rect(leftRect.x, y, leftRect.width, Text.CalcHeight(math, leftRect.width));
            Widgets.Label(mathRect, math);
            TooltipHandler.TipRegion(mathRect, GetProductionBreakdown(manager));
            y = mathRect.yMax + 8f;

            Color storageCol = Logi.currentFood <= 20f ? Color.red : (Logi.currentFood <= 50f ? Color.yellow : Color.green);
            DrawLabelRow(leftRect.x, ref y, leftRect.width, s_localStorage + ":",
                $"{Logi.currentFood:F1} / {CompOutpostLogistics.MaxFood:F0}".Colorize(storageCol),
                s_outpostInfoTip);

            float realTimeNet = manager.GetLogisticsNetDailyForOutpost(SelOutpost);
            DrawLabelRow(leftRect.x, ref y, leftRect.width, s_localChangePerDay + ":",
                FormatNetColored(realTimeNet));

            if (WorldComponent_LogisticsManager.IsFoodProducerOutpost(SelOutpost))
            {
                float budget = manager.GetDistributionBudgetForSourceTile(SelOutpost.Tile);
                float outgoing = manager.GetOutgoingManualSumForTile(SelOutpost.Tile);
                y += 6f;
                Widgets.Label(new Rect(leftRect.x, y, leftRect.width, 22f),
                    "TSA_AllocatedStatus".Translate(outgoing.ToString("F1"), budget.ToString("F1")));
            }
        }

        private string GetProductionBreakdown(WorldComponent_LogisticsManager manager)
        {
            StringBuilder sb = new StringBuilder();
            var s = WorldDominationMod.settings;
            bool isFarming = SelOutpost.def.defName.Contains("Farming");
            bool isHunting = SelOutpost.def.defName.Contains("Hunting");

            sb.AppendLine("TSA_Breakdown_Title".Translate());
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

        private void DrawProducerColumn(Rect rightRect, WorldComponent_LogisticsManager manager)
        {
            EnsureLogisticsSupportListCached();
            manager.MergeDuplicateManualLinksFromSource(SelOutpost.Tile);

            float budget = manager.GetDistributionBudgetForSourceTile(SelOutpost.Tile);
            float outgoingFromHubAll = manager.GetOutgoingManualSumForTile(SelOutpost.Tile);
            float keepHere = Mathf.Max(0f, budget - outgoingFromHubAll);

            DrawSectionHeader(rightRect.x, rightRect.y, rightRect.width, s_allocationStrategy);
            float y = rightRect.y + 24f;

            DrawSmartToggles(new Rect(rightRect.x, y, rightRect.width, 28f), manager);
            y += 32f;

            Rect keepHeaderRect = new Rect(rightRect.x, y, rightRect.width, 22f);
            GUI.color = Widgets.SeparatorLabelColor;
            Widgets.Label(keepHeaderRect, s_keepHere);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(keepHeaderRect, s_keepHereTip);
            y += 24f;

            float rowContentW = rightRect.width - 16f;
            DrawKeepHereRow(new Rect(rightRect.x, y, rowContentW, ProducerRowHeight), keepHere);
            y += ProducerRowStep;

            DrawSectionHeader(rightRect.x, y, rightRect.width, s_distributeTo);
            y += 24f;

            Rect listRect = new Rect(rightRect.x, y, rightRect.width, rightRect.yMax - y);
            DrawProducerInterface(listRect, manager, budget, logisticsSupportOutpostsCache);
        }

        private void DrawKeepHereRow(Rect row, float keepAmount)
        {
            Widgets.DrawHighlight(row);
            Widgets.DrawHighlightIfMouseover(row);

            GetProducerRowLayout(row.width, out float nameX, out float nameW, out float netX, out _, out _);

            float contentY = row.y + (row.height / 2f);
            Rect iconRect = new Rect(row.x, contentY - (RowIconSize / 2f), RowIconSize, RowIconSize);
            Texture2D iconTex = SelOutpost.def?.ExpandingIconTexture;
            if (iconTex != null)
            {
                GUI.color = SelOutpost.Faction?.Color ?? Color.white;
                GUI.DrawTexture(iconRect, iconTex, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }

            Widgets.Label(new Rect(row.x + nameX, contentY - 21f, nameW, 24f), SelOutpost.LabelCap.Truncate(nameW - 4f));

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            string info = "TSA_OutpostInfoRowFood".Translate(
                Logi.currentFood.ToString("F1"),
                CompOutpostLogistics.MaxFood.ToString("F0")).ToString();
            Rect infoRect = new Rect(row.x + nameX, contentY + 2f, nameW, 20f);
            Widgets.Label(infoRect, info);
            TooltipHandler.TipRegion(infoRect, s_outpostInfoTip);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect netRect = new Rect(row.x + netX, row.y, ProducerNetW, row.height);
            LabelAnchored(netRect, FormatNetColored(keepAmount), TextAnchor.MiddleCenter);
            TooltipHandler.TipRegion(netRect, s_keepHereTip);
        }

        private void DrawReceiverColumn(Rect rightRect, WorldComponent_LogisticsManager manager)
        {
            DrawSectionHeader(rightRect.x, rightRect.y, rightRect.width, s_incomingSupplyLines);
            float y = rightRect.y + 24f;
            EnsureLogisticsConsumerInboundCached(manager);
            Rect listRect = new Rect(rightRect.x, y, rightRect.width, rightRect.yMax - y);
            DrawConsumerInterface(listRect, manager, logisticsConsumerInboundCache);
        }

        private void DrawSmartToggles(Rect rect, WorldComponent_LogisticsManager manager)
        {
            const float btnSize = 24f;
            const float labelGap = 4f;
            const float groupGap = 16f;
            const float resetBtnW = 140f;

            float smartLabelW = Text.CalcSize(s_toggleLblSmart).x;
            float smartGroupW = btnSize + labelGap + smartLabelW;
            float smartX = rect.x;
            Rect smartHit = new Rect(smartX, rect.y, smartGroupW, btnSize);
            if (Widgets.RadioButton(smartX, rect.y, LogisticsModeUtil.IsSmartMode(Logi.mode)))
            {
                Logi.mode = LogisticsMode.Smart;
                manager.NotifySmartLogisticsDirty();
                manager.CompleteSmartLogisticsRefreshNow();
            }
            LabelAnchored(new Rect(smartX + btnSize + labelGap, rect.y, smartLabelW, btnSize), s_toggleLblSmart, TextAnchor.MiddleLeft);
            TooltipHandler.TipRegion(smartHit, s_toggleTipSmart);

            float manualLabelW = Text.CalcSize(s_toggleLblManual).x;
            float manualGroupW = btnSize + labelGap + manualLabelW;
            float manualX = smartX + smartGroupW + groupGap;
            Rect manualHit = new Rect(manualX, rect.y, manualGroupW, btnSize);
            if (Widgets.RadioButton(manualX, rect.y, Logi.mode == LogisticsMode.Manual))
                Logi.mode = LogisticsMode.Manual;
            LabelAnchored(new Rect(manualX + btnSize + labelGap, rect.y, manualLabelW, btnSize), s_toggleLblManual, TextAnchor.MiddleLeft);
            TooltipHandler.TipRegion(manualHit, s_toggleTipManual);

            float tableContentW = rect.width - 16f;
            float resetRight = rect.x + tableContentW - ProducerScrollbarPad;
            Rect resetRect = new Rect(resetRight - resetBtnW, rect.y, resetBtnW, btnSize);
            if (Widgets.ButtonText(resetRect, s_resetAllocation))
                ResetAllocationToManualZero(manager);
            TooltipHandler.TipRegion(resetRect, s_resetAllocationTip);
        }

        private void ResetAllocationToManualZero(WorldComponent_LogisticsManager manager)
        {
            if (SelOutpost == null || manager == null || Logi == null) return;
            Logi.mode = LogisticsMode.Manual;
            int hubTile = SelOutpost.Tile;
            bool changed = false;
            var links = manager.manualLinks;
            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].sourceTile != hubTile) continue;
                if (links[i].manualAssignment <= 0.001f) continue;
                links[i].manualAssignment = 0f;
                changed = true;
            }
            if (changed)
                manager.NotifyManualLinksChanged();
            logisticsTabWorldScanTick = -999999;
            logisticsTabScanCachedSel = null;
            rowStrCache.Clear();
        }

        private void EnsureLogisticsSupportListCached()
        {
            int now = Find.TickManager.TicksGame;
            var mgr = Find.World?.GetComponent<WorldComponent_LogisticsManager>();
            int logiGen = mgr?.LogisticsNetDisplayGeneration ?? 0;
            if (logisticsTabScanCachedSel == SelOutpost
                && now - logisticsTabWorldScanTick < LogisticsTabWorldScanIntervalTicks
                && logiGen == producerRecipientScanCachedLogisticsGen)
                return;
            logisticsTabWorldScanTick = now;
            logisticsTabScanCachedSel = SelOutpost;
            producerRecipientScanCachedLogisticsGen = logiGen;
            logisticsSupportOutpostsCache.Clear();
            rowStrCache.Clear();
            var s = WorldDominationMod.settings;
            int hubTile = SelOutpost.Tile;
            if (mgr != null)
            {
                var nodes = mgr.GetCachedPlayerLogisticsNodes();
                for (int i = 0; i < nodes.Count; i++)
                {
                    var obj = nodes[i].Obj;
                    if (obj == SelOutpost) continue;
                    if (WorldComponent_LogisticsManager.IsFoodProducerOutpost(obj)) continue;
                    if (Find.WorldGrid.ApproxDistanceInTiles(hubTile, obj.Tile) <= s.maxLogisticsRange)
                        logisticsSupportOutpostsCache.Add(obj);
                }
            }
            logisticsSupportOutpostsCache.Sort((a, b) =>
                Find.WorldGrid.ApproxDistanceInTiles(hubTile, a.Tile)
                    .CompareTo(Find.WorldGrid.ApproxDistanceInTiles(hubTile, b.Tile)));
        }

        private void EnsureLogisticsConsumerInboundCached(WorldComponent_LogisticsManager manager)
        {
            int now = Find.TickManager.TicksGame;
            int logiGen = manager?.LogisticsNetDisplayGeneration ?? 0;
            if (logisticsConsumerCacheSel == SelOutpost
                && now - logisticsConsumerLinksCacheTick < LogisticsTabWorldScanIntervalTicks
                && logiGen == consumerInboundCachedLogisticsGen)
                return;
            logisticsConsumerLinksCacheTick = now;
            logisticsConsumerCacheSel = SelOutpost;
            consumerInboundCachedLogisticsGen = logiGen;
            logisticsConsumerInboundCache.Clear();
            consumerRowStrCache.Clear();
            for (int i = 0; i < manager.manualLinks.Count; i++)
            {
                var l = manager.manualLinks[i];
                if (l.destTile != SelOutpost.Tile || l.manualAssignment <= 0.01f) continue;
                logisticsConsumerInboundCache.Add(l);
                var srcOp = Find.WorldObjects.WorldObjectAt<WorldObject>(l.sourceTile);
                if (srcOp == null) continue;
                consumerRowStrCache[l.sourceTile] = new RowStrings
                {
                    jump = "TSA_WD_JumpToOutpost".Translate(srcOp.LabelCap),
                    name = srcOp.LabelCap + " (" + srcOp.def.label + ")"
                };
            }
        }

        private void BuildRowStrings(WorldObject target)
        {
            var rs = new RowStrings
            {
                jump = "TSA_WD_JumpToOutpost".Translate(target.LabelCap),
                name = target.LabelCap
            };
            var tl = target.GetComponent<CompOutpostLogistics>();
            rs.info = tl != null
                ? "TSA_OutpostInfoRowFood".Translate(tl.currentFood.ToString("F1"), CompOutpostLogistics.MaxFood.ToString("F0")).ToString()
                : "";
            rowStrCache[target.Tile] = rs;
        }

        private void DrawIsolatedMessage(Rect listRect, bool producer)
        {
            if (SelOutpost == null) return;
            string typeLabel = SelOutpost.def != null
                ? SelOutpost.def.LabelCap.ToString()
                : SelOutpost.LabelCap;
            string msg = producer
                ? "TSA_Logistics_IsolatedProducer".Translate(typeLabel).ToString()
                : "TSA_Logistics_IsolatedConsumer".Translate(typeLabel).ToString();
            GUI.color = Color.gray;
            float h = Mathf.Min(listRect.height, Mathf.Max(48f, Text.CalcHeight(msg, listRect.width)));
            Widgets.Label(new Rect(listRect.x, listRect.y, listRect.width, h), msg);
            GUI.color = Color.white;
        }

        private void DrawProducerInterface(Rect listRect, WorldComponent_LogisticsManager manager, float budget, List<WorldObject> supportOutpostsInRange)
        {
            if (supportOutpostsInRange == null || supportOutpostsInRange.Count == 0)
            {
                DrawIsolatedMessage(listRect, producer: true);
                return;
            }

            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, scrollViewHeight);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

            float vw = viewRect.width;
            GetProducerRowLayout(vw, out float nameX, out float nameW, out float netX, out float ctrlX, out float ctrlW);

            float innerY = 0f;
            int rowIndex = 0;
            for (int ti = 0; ti < supportOutpostsInRange.Count; ti++)
            {
                var target = supportOutpostsInRange[ti];
                if (!rowStrCache.TryGetValue(target.Tile, out RowStrings rs))
                {
                    BuildRowStrings(target);
                    rowStrCache.TryGetValue(target.Tile, out rs);
                }

                Rect row = new Rect(0f, innerY, vw, ProducerRowHeight);
                if (rowIndex % 2 == 0) Widgets.DrawHighlight(row);
                Widgets.DrawHighlightIfMouseover(row);

                Rect jumpRect = new Rect(row.x, row.y, netX, row.height);
                if (Widgets.ButtonInvisible(jumpRect))
                {
                    CameraJumper.TryJump(target);
                    Find.WorldSelector.ClearSelection();
                    Find.WorldSelector.Select(target);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    if (Find.MainTabsRoot.OpenTab != null) Find.MainTabsRoot.EscapeCurrentTab();
                }
                TooltipHandler.TipRegion(jumpRect, rs.jump);

                float contentY = innerY + (row.height / 2f);
                Rect iconRect = new Rect(row.x, contentY - (RowIconSize / 2f), RowIconSize, RowIconSize);
                Texture2D iconTex = target.def?.ExpandingIconTexture;
                if (iconTex != null)
                {
                    GUI.color = target.Faction?.Color ?? Color.white;
                    GUI.DrawTexture(iconRect, iconTex, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }

                Widgets.Label(new Rect(nameX, contentY - 21f, nameW, 24f), rs.name.Truncate(nameW - 4f));

                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Rect infoRect = new Rect(nameX, contentY + 2f, nameW, 20f);
                Widgets.Label(infoRect, rs.info);
                TooltipHandler.TipRegion(infoRect, s_outpostInfoTip);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                float tNet = manager.GetLogisticsNetDailyForOutpost(target);
                Rect netRect = new Rect(netX, innerY, ProducerNetW, row.height);
                LabelAnchored(netRect, FormatNetColored(tNet), TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(netRect, s_totalNetTip);

                float btnY = innerY + (row.height / 2f) - 12f;
                float val = manager.GetOutgoingManualSumForDirectedLink(SelOutpost.Tile, target.Tile);

                if (Widgets.ButtonText(new Rect(ctrlX, btnY, AssignmentBtnW, 24f), "-"))
                    AdjustLink(manager, target.Tile, budget, true);

                val = manager.GetOutgoingManualSumForDirectedLink(SelOutpost.Tile, target.Tile);
                LabelAnchored(new Rect(ctrlX + AssignmentBtnW, innerY, AssignmentValueW, row.height),
                    val.ToString("F1"), TextAnchor.MiddleCenter);

                bool canAdd = (manager.GetOutgoingManualSumForTile(SelOutpost.Tile) + 0.05f) < budget;
                if (!canAdd) GUI.color = Color.gray;
                if (Widgets.ButtonText(new Rect(ctrlX + ctrlW - AssignmentBtnW, btnY, AssignmentBtnW, 24f), "+") && canAdd)
                    AdjustLink(manager, target.Tile, budget, false);
                GUI.color = Color.white;

                innerY += ProducerRowStep;
                rowIndex++;
            }

            if (Event.current.type == EventType.Layout) scrollViewHeight = innerY;
            Widgets.EndScrollView();
        }

        private void DrawConsumerInterface(Rect listRect, WorldComponent_LogisticsManager manager,
            List<WorldComponent_LogisticsManager.LogisticsLink> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                DrawIsolatedMessage(listRect, producer: false);
                return;
            }

            const float rowH = 40f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, consumerScrollViewHeight);
            Widgets.BeginScrollView(listRect, ref consumerScrollPosition, viewRect);

            float vw = viewRect.width;
            const float iconColW = 40f;
            float amountColW = 60f;
            float nameColW = vw - iconColW - amountColW - 8f;

            float curY = 0f;
            int rowIndex = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                var link = sources[i];
                var srcOp = Find.WorldObjects.WorldObjectAt<WorldObject>(link.sourceTile);
                if (srcOp == null) continue;
                if (!consumerRowStrCache.TryGetValue(link.sourceTile, out RowStrings crs))
                {
                    crs = new RowStrings
                    {
                        jump = "TSA_WD_JumpToOutpost".Translate(srcOp.LabelCap),
                        name = srcOp.LabelCap + " (" + srcOp.def.label + ")"
                    };
                    consumerRowStrCache[link.sourceTile] = crs;
                }

                float received = link.manualAssignment * manager.GetEfficiency(link.sourceTile, link.destTile);
                Rect rowRect = new Rect(0f, curY, vw, rowH);
                if (rowIndex % 2 == 0) Widgets.DrawHighlight(rowRect);
                Widgets.DrawHighlightIfMouseover(rowRect);

                if (Widgets.ButtonInvisible(rowRect))
                {
                    CameraJumper.TryJump(srcOp);
                    Find.WorldSelector.ClearSelection();
                    Find.WorldSelector.Select(srcOp);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    if (Find.MainTabsRoot.OpenTab != null) Find.MainTabsRoot.EscapeCurrentTab();
                }
                TooltipHandler.TipRegion(rowRect, crs.jump);

                float contentY = curY + rowH / 2f;
                Rect iconRect = new Rect(4f, contentY - RowIconSize / 2f, RowIconSize, RowIconSize);
                Texture2D iconTex = srcOp.def?.ExpandingIconTexture;
                if (iconTex != null)
                {
                    GUI.color = srcOp.Faction?.Color ?? Color.white;
                    GUI.DrawTexture(iconRect, iconTex, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }

                Rect nameRect = new Rect(iconColW + 4f, curY, nameColW - 8f, rowH);
                Rect amountRect = new Rect(iconColW + nameColW + 4f, curY, amountColW - 8f, rowH);
                LabelAnchored(nameRect, crs.name.Truncate(nameColW - 12f), TextAnchor.MiddleLeft);
                LabelAnchored(amountRect, received.ToString("F1").Colorize(Color.green), TextAnchor.MiddleLeft);

                curY += rowH;
                rowIndex++;
            }

            if (Event.current.type == EventType.Layout) consumerScrollViewHeight = curY;
            Widgets.EndScrollView();
        }

        private void AdjustLink(WorldComponent_LogisticsManager manager, int targetTile, float budget, bool isMinus)
        {
            manager.MergeDuplicateManualLinksFromSource(SelOutpost.Tile);
            float current = manager.GetOutgoingManualSumForDirectedLink(SelOutpost.Tile, targetTile);
            float step = AdjustmentStep();

            float next;
            if (isMinus)
            {
                if (current <= 0f) return;
                float fractional = current - (float)Math.Floor(current);
                if (step <= 1.01f && fractional > 0.01f)
                    next = current - fractional;
                else
                    next = Mathf.Max(0f, current - step);
            }
            else
            {
                float remaining = budget - manager.GetOutgoingManualSumForTile(SelOutpost.Tile);
                if (remaining <= 0.001f) return;
                next = current + Mathf.Min(step, remaining);
            }
            SetLinkAssignment(manager, targetTile, next, budget);
        }

        private void SetLinkAssignment(WorldComponent_LogisticsManager manager, int targetTile, float amount, float budget)
        {
            manager.MergeDuplicateManualLinksFromSource(SelOutpost.Tile);
            Logi.mode = LogisticsMode.Manual;
            WorldComponent_LogisticsManager.LogisticsLink link = null;
            for (int i = 0; i < manager.manualLinks.Count; i++)
            {
                var l = manager.manualLinks[i];
                if (l.sourceTile == SelOutpost.Tile && l.destTile == targetTile)
                {
                    link = l;
                    break;
                }
            }
            if (link == null)
            {
                link = new WorldComponent_LogisticsManager.LogisticsLink
                {
                    sourceTile = SelOutpost.Tile,
                    destTile = targetTile
                };
                manager.manualLinks.Add(link);
            }

            float otherOutgoing = manager.GetOutgoingManualSumForTile(SelOutpost.Tile)
                - manager.GetOutgoingManualSumForDirectedLink(SelOutpost.Tile, targetTile);
            float maxForLink = Mathf.Max(0f, budget - otherOutgoing);
            float clamped = (float)Math.Round(Mathf.Clamp(amount, 0f, maxForLink), 1);
            if (Mathf.Abs(link.manualAssignment - clamped) <= 0.0001f)
                return;

            link.manualAssignment = clamped;
            manager.ClampManualAssignmentsForSourceBudget(SelOutpost.Tile);
            manager.NotifyManualLinksChanged();
            logisticsTabWorldScanTick = -999999;
            logisticsConsumerLinksCacheTick = -999999;
            logisticsTabScanCachedSel = null;
            logisticsConsumerCacheSel = null;
        }
    }
}
