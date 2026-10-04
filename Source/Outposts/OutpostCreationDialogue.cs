using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using Outposts;
using WorldDomination;
using TSA_WorldDomination.Settings;

namespace TSA_WorldDomination.Outposts
{
    public class Dialog_OutpostSelection : Window
    {
        private int tile;
        private string originalName;
        private int ruinsId;
        private SettlementTier tier;
        private ChoiceLetter parentLetter;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(650f, 600f);

        public Dialog_OutpostSelection(int tile, string name, int ruinsId, SettlementTier tier, ChoiceLetter parentLetter)
        {
            this.tile = tile;
            originalName = name;
            this.ruinsId = ruinsId;
            this.tier = tier;
            this.parentLetter = parentLetter;
            doCloseButton = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 40f), $"Select Outpost Type ({tier})");
            Text.Font = GameFont.Small;

            Rect viewRect = new Rect(0, 0, inRect.width - 16f, OutpostsMod.Outposts.Count * 110f);
            Rect scrollRect = new Rect(0, 50f, inRect.width, inRect.height - 100f);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
            float curY = 0;
            foreach (var def in OutpostsMod.Outposts)
            {
                Rect rowRect = new Rect(0, curY, viewRect.width, 100f);
                Widgets.DrawMenuSection(rowRect);

                GUI.color = Faction.OfPlayer.Color;
                Widgets.DrawTextureFitted(new Rect(5, curY + 10, 80, 80), def.ExpandingIconTexture, 1f);
                GUI.color = Color.white;

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(95, curY + 10, viewRect.width - 220f, 25f), def.LabelCap);

                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(95, curY + 35, viewRect.width - 220f, 60f), def.description);
                GUI.color = Color.white;

                if (Widgets.ButtonText(new Rect(viewRect.width - 115f, curY + 30, 100f, 40f), "Establish"))
                {
                    FinalizeOutpost(def);
                    Close();
                }
                curY += 105f;
            }
            Widgets.EndScrollView();
        }

        private void FinalizeOutpost(WorldObjectDef outpostDef)
        {
            WorldObject target = Find.WorldObjects.AllWorldObjects.FirstOrDefault(x => x.ID == ruinsId);
            if (target != null) Find.WorldObjects.Remove(target);

            Outpost outpost = (Outpost)WorldObjectMaker.MakeWorldObject(outpostDef);
            outpost.Tile = tile;
            outpost.SetFaction(Faction.OfPlayer);
            outpost.Name = originalName;
            Find.WorldObjects.Add(outpost);

            int count = tier == SettlementTier.T4 ? 20 : tier == SettlementTier.T3 ? 12 : tier == SettlementTier.T2 ? 7 : 3;
            List<SkillDef> relevantSkills = GetRequiredSkillsForOutpost(outpostDef);

            for (int i = 0; i < count; i++)
            {
                Pawn p = null;
                try
                {
                    bool foundValid = false;
                    int attempts = 0;
                    while (!foundValid && attempts < 50)
                    {
                        attempts++;
                        // FIX: Use NonPlayer context to satisfy compiler and bypass BPC's birth hooks.
                        // We pass null for faction initially to prevent early 'Player-Pawn' logic triggers.
                        p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                            PawnKindDefOf.Colonist, null, PawnGenerationContext.NonPlayer, -1,
                            forceGenerateNewPawn: true, canGeneratePawnRelations: false));

                        if (p.skills != null && !relevantSkills.Any(s => p.skills.GetSkill(s).TotallyDisabled))
                            foundValid = true;
                        else
                            Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                    }

                    if (p != null)
                    {

                        // Boost Skills
                        foreach (var skill in relevantSkills)
                        {
                            var skillRecord = p.skills.GetSkill(skill);
                            if (skillRecord != null)
                            {
                                if (skillRecord.Level < 4) skillRecord.Level = 4;
                                if (skillRecord.passion != Passion.Major && Rand.Chance(0.5f))
                                    skillRecord.passion = Passion.Minor;
                            }
                        }

                        Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                        p.SetFaction(Faction.OfPlayer);
                        outpost.AddPawn(p);
                    }
                }
                catch (Exception ex)
                {
                    // SUPPRESSION: Better Pawn Control or other UI-heavy mods often fail here.
                    Log.Warning("TSA_WorldDomination: Suppressed generation error. Pawn added anyway. Error: " + ex.Message);

                    if (p != null)
                    {
                        // Ensure the pawn is player-owned and not orphaned even on crash
                        if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
                        if (!outpost.AllPawns.Contains(p)) outpost.AddPawn(p);
                    }
                }
            }

            // Finalize Outpost Logic (Cooldowns/Strength)
            var spreadComp = outpost.GetComponent<CompViralSpread>();
            if (spreadComp != null)
            {
                spreadComp.UpdateOutpostStrengthLogically();
                var seth = WorldDominationMod.settings;
                if (seth != null)
                    spreadComp.incidentCooldownUntilTick = Find.TickManager.TicksGame + Mathf.RoundToInt(seth.cooldownRaidDays * 60000f);
            }

            if (parentLetter != null) Find.LetterStack.RemoveLetter(parentLetter);

            HarmonyLib.Traverse.Create(outpost).Method("RecachePawnTraits").GetValue();
            Find.WorldSelector.Select(outpost);
        }

        private List<SkillDef> GetRequiredSkillsForOutpost(WorldObjectDef def)
        {
            string name = def.defName.ToLower();
            var skills = new List<SkillDef>();

            if (name.Contains("mining")) skills.Add(SkillDefOf.Mining);
            if (name.Contains("drilling") || name.Contains("construction")) skills.Add(SkillDefOf.Construction);
            if (name.Contains("trading") || name.Contains("town")) skills.Add(SkillDefOf.Social);
            if (name.Contains("farming") || name.Contains("logging")) skills.Add(SkillDefOf.Plants);
            if (name.Contains("production") || name.Contains("factory")) skills.Add(SkillDefOf.Crafting);
            if (name.Contains("science") || name.Contains("research")) skills.Add(SkillDefOf.Intellectual);
            if (name.Contains("med")) skills.Add(SkillDefOf.Medicine);

            if (name.Contains("hunting") || name.Contains("artillery"))
            {
                skills.Add(SkillDefOf.Shooting);
                skills.Add(SkillDefOf.Animals);
            }
            else if (name.Contains("defensive"))
            {
                skills.Add(SkillDefOf.Shooting);
            }

            return skills;
        }
    }
}