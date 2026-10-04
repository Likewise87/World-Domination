using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse.Sound;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_AllegianceLock : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string searchFilter = "";
        private List<Pair<Faction, Faction>> factionPairs = new List<Pair<Faction, Faction>>();

        public override Vector2 InitialSize => new Vector2(850f, 850f);

        public Dialog_AllegianceLock()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                Messages.Message("TSA_WD_OnlyInGame".Translate(), MessageTypeDefOf.RejectInput, false);
                this.Close();
                return;
            }

            doCloseButton = true;
            forcePause = true;
            closeOnClickedOutside = true;
            doWindowBackground = true;
            absorbInputAroundWindow = true;

            WorldDominationMod.settings.EnsureInitialLaunchDefaults();
            RefreshFactionPairs();
        }

        private void RefreshFactionPairs()
        {
            factionPairs.Clear();
            var allFactions = new List<Faction> { Faction.OfPlayer };
            var npcs = Find.FactionManager.AllFactionsListForReading
                .Where(f => !f.def.hidden && !f.defeated && !f.IsPlayer)
                .OrderBy(f => f.def.LabelCap.Resolve());
            allFactions.AddRange(npcs);

            for (int i = 0; i < allFactions.Count; i++)
            {
                for (int j = 0; j < allFactions.Count; j++)
                {
                    if (i == j) continue;
                    factionPairs.Add(new Pair<Faction, Faction>(allFactions[i], allFactions[j]));
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            var s = WorldDominationMod.settings;

            // Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width * 0.4f, 34f), "TSA_WD_AllegianceLock_Title".Translate());
            Text.Font = GameFont.Small;

            // BUTTON ROW
            float btnW = 115f;
            float btnGap = 5f;
            float curX = inRect.xMax - btnW;

            Rect btnResetRect = new Rect(curX, inRect.y, btnW, 30f);
            if (Widgets.ButtonText(btnResetRect, "TSA_WD_BtnReset".Translate()))
            {
                s.ResetAllegianceLocks(false);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(btnResetRect, "TSA_WD_BtnReset_Tooltip".Translate());

            curX -= (btnW + btnGap);
            Rect btnResetGlobalRect = new Rect(curX, inRect.y, btnW, 30f);
            if (Widgets.ButtonText(btnResetGlobalRect, "TSA_WD_BtnResetGlobal".Translate()))
            {
                s.ResetAllegianceLocks(true);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(btnResetGlobalRect, "TSA_WD_BtnResetGlobal_Tooltip".Translate());

            curX -= (btnW + btnGap);
            Rect btnAllowAllRect = new Rect(curX, inRect.y, btnW, 30f);
            if (Widgets.ButtonText(btnAllowAllRect, "TSA_WD_BtnAllowAll".Translate()))
            {
                s.lockedAllegiancePairs.Clear();
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(btnAllowAllRect, "TSA_WD_BtnAllowAll_Tooltip".Translate());

            curX -= (btnW + btnGap);
            Rect btnLockAllRect = new Rect(curX, inRect.y, btnW, 30f);
            if (Widgets.ButtonText(btnLockAllRect, "TSA_WD_BtnLockAll".Translate()))
            {
                foreach (var pair in factionPairs)
                    s.lockedAllegiancePairs.Add(s.GetFactionPairKey(pair.First, pair.Second));
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(btnLockAllRect, "TSA_WD_BtnLockAll_Tooltip".Translate());

            // Search Bar Row
            Rect searchRowRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, 30f);
            searchFilter = Widgets.TextField(searchRowRect.LeftPart(0.25f), searchFilter);

            float clearBtnWidth = 60f;
            Rect clearBtnRect = new Rect(searchRowRect.x + searchRowRect.width * 0.25f + 5f, searchRowRect.y, clearBtnWidth, 30f);
            if (Widgets.ButtonText(clearBtnRect, "TSA_WD_BtnClear".Translate())) searchFilter = "";

            Rect infoLabelRect = new Rect(clearBtnRect.xMax + 15f, searchRowRect.y, searchRowRect.width - (clearBtnRect.xMax - searchRowRect.x) - 15f, 30f);
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Tiny;
            Widgets.Label(infoLabelRect, "TSA_WD_AllegianceLock_ScopeInfo".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            // MAIN LIST AREA
            // Increase the deduction from inRect.height (was -100f, now -120f) to prevent overlap with the bottom of the window
            Rect outRect = new Rect(inRect.x, searchRowRect.yMax + 10f, inRect.width, inRect.height - 130f);
            Widgets.DrawMenuSection(outRect);

            var filteredPairs = factionPairs.Where(p =>
                searchFilter.NullOrEmpty() ||
                p.First.Name.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.First.def.LabelCap.Resolve().IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();

            // FIXED: outRect.width minus contract padding (8f) minus scrollbar width (16f) ensures zero horizontal scroll
            float viewWidth = outRect.width - 24f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, filteredPairs.Count * 36f);

            // Using the inner rect explicitly for the scroll view
            Rect scrollRect = outRect.ContractedBy(4f);
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float cy = 0f;
            foreach (var pair in filteredPairs)
            {
                Rect rowRect = new Rect(0f, cy, viewWidth, 32f);
                Widgets.DrawHighlightIfMouseover(rowRect);

                string key = s.GetFactionPairKey(pair.First, pair.Second);
                bool isLocked = s.lockedAllegiancePairs.Contains(key);

                float statusW = 180f;
                float sideW = (rowRect.width - statusW) / 2f;
                float pad = 40f;

                // Left Column
                DrawFactionColumn(new Rect(rowRect.x + pad, rowRect.y, sideW - pad, rowRect.height), pair.First);

                // Center Toggle
                Rect btnRect = new Rect(rowRect.x + sideW, rowRect.y + 2f, statusW, 28f);
                if (Widgets.ButtonText(btnRect, isLocked ? "TSA_WD_StatusLocked".Translate().Colorize(Color.red) : "TSA_WD_StatusPossible".Translate().Colorize(Color.cyan)))
                {
                    if (isLocked) s.lockedAllegiancePairs.Remove(key);
                    else s.lockedAllegiancePairs.Add(key);

                    if (isLocked) SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                    else SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                }

                // Right Column
                DrawFactionColumn(new Rect(btnRect.xMax + pad, rowRect.y, sideW - pad, rowRect.height), pair.Second);

                cy += 36f;
            }

            Widgets.EndScrollView();
        }

        private void DrawFactionColumn(Rect rect, Faction faction)
        {
            float iconSize = 22f;
            Rect iconRect = new Rect(rect.x, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
            Rect textRect = new Rect(iconRect.xMax + 6f, rect.y, rect.width - (iconSize + 6f), rect.height);

            GUI.color = faction.Color;
            Widgets.DrawTextureFitted(iconRect, faction.def.FactionIcon, 1f);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Tiny;
            Widgets.Label(textRect, (faction.IsPlayer ? $"{faction.Name} (Player)" : faction.Name).Truncate(textRect.width));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}