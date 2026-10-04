using HarmonyLib;
using Outposts;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace WorldDomination.Patches
{
    [HarmonyPatch(typeof(Dialog_CreateCamp), "DoOutpostDisplay")]
    public static class Patch_OutpostCreationSkills
    {
        // Change: Pass outpostDef directly to the helper
        public static void Prefix(ref Rect inRect, WorldObjectDef outpostDef, Caravan ___creator)
        {
            List<Pawn> pawns = ___creator.PawnsListForReading?.Where(p => p.RaceProps.Humanlike).ToList();
            if (pawns.NullOrEmpty()) return;

            // Now passing the whole def
            List<SkillDef> relevantSkills = GetRelevantSkills(outpostDef);

            if (!relevantSkills.NullOrEmpty())
            {
                Rect skillRect = new Rect(inRect.xMax - 180f, inRect.y + 2f, 170f, 60f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperRight;
                GUI.color = Color.cyan;

                string fullDisplayText = "";
                foreach (var skill in relevantSkills)
                {
                    float total = pawns.Sum(p => (float)p.skills.GetSkill(skill).Level);
                    float avg = total / pawns.Count;
                    string row = "TSA_SkillCombined".Translate(skill.LabelCap, total, avg.ToString("0.0"));
                    fullDisplayText += row + "\n";
                }

                Widgets.Label(skillRect, fullDisplayText.TrimEnd());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
        }

        private static List<SkillDef> GetRelevantSkills(WorldObjectDef def)
        {
            // Use defName as it's guaranteed to match your XML
            string name = def.defName;
            var skills = new List<SkillDef>();

            if (name.Contains("Farming") || name.Contains("Logging")) skills.Add(SkillDefOf.Plants);
            if (name.Contains("Drilling")) skills.Add(SkillDefOf.Construction);
            if (name.Contains("Defensive")) skills.Add(SkillDefOf.Shooting);
            if (name.Contains("Mining")) skills.Add(SkillDefOf.Mining);
            if (name.Contains("Production")) skills.Add(SkillDefOf.Crafting);
            if (name.Contains("Science")) skills.Add(SkillDefOf.Intellectual);
            if (name.Contains("Town") || name.Contains("Trading")) skills.Add(SkillDefOf.Social);

            if (name.Contains("Hunting") || name.Contains("Artillery"))
            {
                skills.Add(SkillDefOf.Shooting);
                skills.Add(SkillDefOf.Animals);
            }

            return skills;
        }
    }
}