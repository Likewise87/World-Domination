using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.Settings;

namespace WorldDomination
{
    public class WorldThreatManager : WorldComponent
    {
        private int lastUpdateTick = -9999;
        private ChoiceLetter activeLetter;
        private int lastThreatCategory = -1; // -1: None, 0: Low, 1: Medium, 2: High, 3: Critical

        public WorldThreatManager(World world) : base(world) { }

        public void Notify_ThreatChanged() => lastUpdateTick = -9999;

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (Find.TickManager.TicksGame >= lastUpdateTick + 2500)
            {
                UpdateThreatLevel();
                lastUpdateTick = Find.TickManager.TicksGame;
            }
        }

        private void UpdateThreatLevel()
        {
            if (!WorldDominationMod.settings.notifyThreatLevel)
            {
                if (activeLetter != null) { Find.LetterStack.RemoveLetter(activeLetter); activeLetter = null; }
                lastThreatCategory = -1;
                return;
            }

            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
            if (manager == null) return;

            float currentMaxThreat = 0f;
            Faction scariestFaction = null;
            List<Pair<string, float>> factionThreats = new List<Pair<string, float>>();

            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f.IsPlayer || f.def.hidden || f.defeated || !f.HostileTo(Faction.OfPlayer)) continue;
                float score = manager.GetThreatScoreFor(f);
                factionThreats.Add(new Pair<string, float>(f.Name, score));
                if (score > currentMaxThreat) { currentMaxThreat = score; scariestFaction = f; }
            }

            int currentCategory = GetCategory(currentMaxThreat);
            factionThreats.Sort((x, y) => y.Second.CompareTo(x.Second));

            if (currentCategory != lastThreatCategory)
            {
                lastThreatCategory = currentCategory;
                CreateNewLetter(currentMaxThreat, currentCategory, scariestFaction, factionThreats);
            }
            else if (activeLetter != null && Find.LetterStack.LettersListForReading.Contains(activeLetter))
            {
                activeLetter.Text = ConstructDescription(currentMaxThreat, currentCategory, scariestFaction, factionThreats);
            }
        }

        private int GetCategory(float threat)
        {
            if (threat >= 2.5f) return 3;
            if (threat >= 1.75f) return 2;
            if (threat >= 1.25f) return 1;
            return 0;
        }

        private void CreateNewLetter(float threat, int category, Faction scariest, List<Pair<string, float>> allThreats)
        {
            if (activeLetter != null) Find.LetterStack.RemoveLetter(activeLetter);

            string label = GetLabelForCategory(category);
            LetterDef def = (category >= 3) ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent;
            string text = ConstructDescription(threat, category, scariest, allThreats);

            activeLetter = LetterMaker.MakeLetter(label, text, def);
            Find.LetterStack.ReceiveLetter(activeLetter, playSound: true);
        }

        private string ConstructDescription(float threat, int category, Faction scariest, List<Pair<string, float>> allThreats)
        {
            string baseDesc = GetBaseDescForCategory(category, threat, scariest);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(baseDesc);
            sb.AppendLine();
            sb.AppendLine("TSA_WD_ThreatBreakdownHeader".Translate());
            sb.AppendLine("---------------------------------------------");

            if (allThreats.Count > 0)
            {
                foreach (var pair in allThreats)
                    sb.AppendLine($"{pair.First}: x{pair.Second:F2}");
            }
            else
            {
                sb.AppendLine("TSA_WD_NoHostileFactions".Translate());
            }

            return sb.ToString().Trim();
        }

        private string GetLabelForCategory(int category)
        {
            return $"TSA_WD_ThreatLabel_Cat{category}".Translate();
        }

        private string GetBaseDescForCategory(int category, float threat, Faction scariest)
        {
            string name = scariest?.Name ?? "TSA_WD_HostileForcesFallback".Translate();
            return $"TSA_WD_ThreatDesc_Cat{category}".Translate(name, threat.ToString("F2"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastThreatCategory, "lastThreatCategory", -1);
        }
    }
}