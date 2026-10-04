using HarmonyLib;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using TSA_WorldDomination.Settings;
using Outposts;

namespace WorldDomination
{
    [HarmonyPatch(typeof(FactionUIUtility), "DrawFactionRow")]
    public static class Patch_FactionUI_Threat
    {
        [HarmonyPostfix]
        public static void Postfix(Faction faction, float rowY, Rect fillRect)
        {
            if (faction == null || faction.def.hidden || faction.IsPlayer) return;
            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
            if (manager == null) return;

            float mult = manager.GetThreatScoreFor(faction);

            // Vertically centered: Row is 80px, Label is 24px.
            Rect threatRect = new Rect(fillRect.x + 570f, rowY + 4f, 400f, 24f);

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = (mult <= 0.41f) ? Color.gray : Color.Lerp(Color.white, Color.red, (mult - 1f) / 3f);

            Widgets.Label(threatRect, "TSA_WD_RaidPointsMult".Translate(mult.ToString("F2")));

            if (Mouse.IsOver(threatRect)) TooltipHandler.TipRegion(threatRect, manager.GetThreatBreakdown(faction));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }


    // Add Action Log Button
    [HarmonyPatch(typeof(MainTabWindow_Factions), "DoWindowContents")]
    public static class Patch_FactionsWindow_WorldDominationUI
    {
        [HarmonyPostfix]
        public static void Postfix(Rect fillRect)
        {
            // --- UI SETTINGS ---
            float startX = 0f;
            float startY = 0f;
            float btnWidth = 120f; // 25% narrower (was 160f)
            float btnHeight = 30f;
            float spacing = 4f;

            // --- 1. DRAW TITLE ---
            Text.Font = GameFont.Tiny;
            GUI.color = Color.cyan;
            Rect titleRect = new Rect(startX, startY, 400f, 20f);
            Widgets.Label(titleRect, "TSA_WD_WorldDominationWindows".Translate());
            GUI.color = Color.white;

            // Shift Y down to clear the title
            float currentY = startY + 20f;

            // --- 2. BUTTON: DIPLOMACY MATRIX ---
            Rect diploBtnRect = new Rect(startX, currentY, btnWidth, btnHeight);
            if (Widgets.ButtonText(diploBtnRect, "TSA_WD_DiplomacyMatrix".Translate()))
            {
                Find.WindowStack.Add(new Window_DiplomacyMatrix());
            }

            // --- 3. BUTTON: OUTPOST MANAGER ---
            Rect outpostBtnRect = new Rect(startX + (btnWidth + spacing), currentY, btnWidth, btnHeight);
            if (Widgets.ButtonText(outpostBtnRect, "TSA_WD_OutpostManager".Translate()))
            {
                Find.WindowStack.Add(new Window_OutpostOverview());
            }

            // --- 4. BUTTON: WORLD STATS ---
            Rect statsBtnRect = new Rect(startX + (btnWidth + spacing) * 2f, currentY, btnWidth, btnHeight);
            if (Widgets.ButtonText(statsBtnRect, "TSA_WD_WorldStats".Translate()))
            {
                Find.WindowStack.Add(new Window_WorldStats());
            }

            // --- 5. BUTTON: ACTION LOG ---
            Rect logBtnRect = new Rect(startX + (btnWidth + spacing) * 3f, currentY, btnWidth, btnHeight);
            if (Widgets.ButtonText(logBtnRect, "TSA_WD_ActionLog".Translate()))
            {
                Find.WindowStack.Add(new Window_ActionLog());
            }

            // Reset text settings for base game UI compatibility
            Text.Font = GameFont.Small;
        }
    }
}