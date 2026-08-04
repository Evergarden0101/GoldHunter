using System;
using System.Collections.Generic;
using GoldHunter.Core.Ai;
using GoldHunter.Core.Config;
using GoldHunter.Core.Input;
using GoldHunter.Core.Math;
using GoldHunter.Core.Simulation;

namespace GoldHunter.CoreTests
{
    /// <summary>
    /// Headless test + balance harness for the engine-independent core.
    ///
    /// This is the whole point of keeping the simulation free of UnityEngine: a
    /// full 150-second match runs here in milliseconds, so invariants (gold
    /// conservation, matches reaching a result, bots actually navigating) and
    /// balance (winner spread, goal share) can be checked without an editor.
    ///
    ///   dotnet run --project tools/CoreTests -- --matches 12 --verbose
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            int matches = ArgInt(args, "--matches", 6);
            string difficulty = Arg(args, "--difficulty", "normal");
            bool verbose = Array.IndexOf(args, "--verbose") >= 0;

            var failures = new List<string>(UnitChecks.RunAll());

            var reports = new List<MatchReport>();
            for (int i = 0; i < matches; i++)
            {
                reports.Add(PlayMatch(1337 + i * 991, difficulty, failures));
            }

            Report(reports, difficulty, verbose, failures);

            if (failures.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("FAILED:");
                foreach (string f in failures) Console.WriteLine("  - " + f);
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("all checks passed");
            return 0;
        }

        /* ------------------------------------------------------------ a match */

        private sealed class MatchReport
        {
            public MatchPhase Phase;
            public readonly float[] DistanceMoved = new float[4];
            public readonly Dictionary<GoalKind, int> GoalTicks = new Dictionary<GoalKind, int>();
            public int ShopVisits;
            public float Spent;
            public int PathFailures;
            public IReadOnlyList<MatchResultRow> Rows;
        }

        private static MatchReport PlayMatch(int seed, string difficultyName, List<string> failures)
        {
            DifficultySettings difficulty =
                difficultyName == "hard" ? DifficultySettings.Hard() :
                difficultyName == "easy" ? DifficultySettings.Easy() :
                DifficultySettings.Normal();

            var profiles = new[]
            {
                NpcProfile.Bruiser(), NpcProfile.Banker(), NpcProfile.Thief(), NpcProfile.AllRound(),
            };

            var setup = new MatchSetup
            {
                Config = GameConfig.Default(),
                Difficulty = difficulty,
                Seed = seed,
            };
            for (int i = 0; i < 4; i++)
            {
                setup.Slots.Add(new PlayerSlot
                {
                    Kind = PlayerSlotKind.Npc,
                    DisplayName = profiles[i].DisplayName,
                    Profile = profiles[i],
                    Controller = new VirtualController(profiles[i].DisplayName),
                });
            }

            var sim = new MatchSimulation(setup);
            var report = new MatchReport();
            var last = new Vec2[4];
            var inShop = new bool[4];
            for (int i = 0; i < 4; i++) last[i] = sim.Players[i].Position;

            // Gold enters only from poppers. It leaves in exactly two ways:
            // spent at a shop, or evaporated on the floor uncollected. So
            // (world + spent + expired) must never fall.
            float previousLedger = sim.TotalGoldInPlay();
            bool reportedLeak = false;
            const float dt = 1f / 60f;

            for (int step = 0; step < 60 * 200 && sim.Phase != MatchPhase.Ended; step++)
            {
                for (int i = 0; i < sim.Players.Count; i++) sim.Players[i].Controller.Poll(dt);
                sim.Tick(dt);

                float spent = 0f;
                for (int i = 0; i < sim.Players.Count; i++)
                {
                    PlayerState p = sim.Players[i];
                    spent += p.Stats.Spent;
                    report.DistanceMoved[i] += Vec2.Distance(p.Position, last[i]);
                    last[i] = p.Position;

                    bool nowInShop = p.CurrentShop != null;
                    if (nowInShop && !inShop[i]) report.ShopVisits++;
                    inShop[i] = nowInShop;
                }

                float ledger = sim.TotalGoldInPlay() + spent + sim.GoldExpired;
                if (ledger < previousLedger - 0.05f && !reportedLeak)
                {
                    reportedLeak = true;
                    failures.Add($"gold vanished at t={sim.ElapsedTime:0.00}s " +
                                 $"({previousLedger:0.00} -> {ledger:0.00})");
                }
                previousLedger = ledger;

                for (int i = 0; i < sim.Brains.Count; i++)
                {
                    GoalKind goal = sim.Brains[i].Goal;
                    report.GoalTicks.TryGetValue(goal, out int count);
                    report.GoalTicks[goal] = count + 1;
                }
            }

            report.Phase = sim.Phase;
            report.Rows = sim.Results;
            for (int i = 0; i < sim.Players.Count; i++) report.Spent += sim.Players[i].Stats.Spent;
            for (int i = 0; i < sim.Brains.Count; i++) if (sim.Brains[i].PathFailed) report.PathFailures++;

            if (sim.Phase != MatchPhase.Ended) failures.Add("a match never reached the end state");
            if (report.Rows == null || report.Rows.Count != 4) failures.Add("results were not produced");
            if (report.PathFailures > 0) failures.Add($"{report.PathFailures} bots ended with an unreachable path");
            for (int i = 0; i < 4; i++)
            {
                if (report.DistanceMoved[i] < 60f)
                {
                    failures.Add($"bot {i} barely moved ({report.DistanceMoved[i]:0}m)");
                }
            }
            return report;
        }

        /* ---------------------------------------------------------- reporting */

        private static void Report(List<MatchReport> reports, string difficulty,
                                   bool verbose, List<string> failures)
        {
            var winners = new Dictionary<string, int>();
            var goals = new Dictionary<GoalKind, int>();
            var totals = new List<float>();
            int hits = 0, raids = 0, buys = 0, shopVisits = 0;
            float mined = 0f, robbed = 0f, raided = 0f, lost = 0f, spent = 0f;

            foreach (MatchReport r in reports)
            {
                if (r.Rows == null) continue;

                string winnerTag = r.Rows[0].Profile != null ? r.Rows[0].Profile.Archetype : "human";
                winners.TryGetValue(winnerTag, out int w);
                winners[winnerTag] = w + 1;

                shopVisits += r.ShopVisits;
                spent += r.Spent;
                foreach (var kv in r.GoalTicks)
                {
                    goals.TryGetValue(kv.Key, out int g);
                    goals[kv.Key] = g + kv.Value;
                }

                foreach (MatchResultRow row in r.Rows)
                {
                    totals.Add(row.Total);
                    hits += row.Stats.PunchesLanded;
                    raids += row.Stats.VaultRaids;
                    mined += row.Stats.Mined;
                    robbed += row.Stats.Robbed;
                    raided += row.Stats.RaidedFor;
                    lost += row.Stats.Lost;
                    buys += row.Purchases != null ? row.Purchases.Count : 0;
                }
            }

            if (verbose && reports.Count > 0 && reports[0].Rows != null)
            {
                Console.WriteLine("--- match 1 ---");
                foreach (MatchResultRow row in reports[0].Rows)
                {
                    Console.WriteLine(
                        $"  {row.Place}. {row.Name,-11} {row.Total,4:0}g  " +
                        $"mined {row.Stats.Mined,4:0}  robbed {row.Stats.Robbed,4:0}  " +
                        $"raided {row.Stats.RaidedFor,3:0}  lost {row.Stats.Lost,4:0}  " +
                        $"hits {row.Stats.PunchesLanded,3}  {DescribePurchases(row)}");
                }
                Console.WriteLine();
            }

            if (totals.Count == 0)
            {
                failures.Add("no match produced any results");
                return;
            }

            int n = System.Math.Max(1, reports.Count);
            totals.Sort((a, b) => b.CompareTo(a));
            int goalTotal = 0;
            foreach (int v in goals.Values) goalTotal += v;
            if (goalTotal == 0) goalTotal = 1;

            Console.WriteLine($"GoldHunter core balance — {n} matches · difficulty={difficulty}");
            Console.WriteLine("  winners            " + JoinCounts(winners));
            Console.WriteLine($"  scores             top {totals[0]:0}g · median {totals[totals.Count / 2]:0}g " +
                              $"· low {totals[totals.Count - 1]:0}g");
            Console.WriteLine($"  per match avg      mined {mined / n:0}g · robbed {robbed / n:0}g " +
                              $"· vault-raided {raided / n:0}g · lost {lost / n:0}g");
            Console.WriteLine($"  fighting           {(float)hits / n:0} punches landed/match " +
                              $"· {(float)raids / n:0.0} vault raids/match");
            Console.WriteLine($"  economy            {(float)buys / n:0} upgrades bought/match " +
                              $"· {spent / n:0}g spent · {(float)shopVisits / n:0} shop visits");

            var goalParts = new List<KeyValuePair<GoalKind, int>>(goals);
            goalParts.Sort((a, b) => b.Value.CompareTo(a.Value));
            var rendered = new List<string>();
            foreach (var kv in goalParts) rendered.Add($"{kv.Key} {(kv.Value * 100f / goalTotal):0}%");
            Console.WriteLine("  AI goal share      " + string.Join("  ", rendered));

            // Behaviour coverage: every headline system must show up in the sample.
            if (hits < n * 4) failures.Add($"almost no punches landed ({hits} over {n} matches)");
            if (buys < n) failures.Add($"shops barely used ({buys} upgrades over {n} matches)");
            if (mined < n * 400f) failures.Add($"too little mining ({mined:0}g over {n} matches)");
            if (raids == 0) failures.Add("no vault raid in any match — the Steal chain may be broken");
            if (totals[totals.Count - 1] <= 0f) failures.Add("a player finished with nothing banked");
        }

        private static string DescribePurchases(MatchResultRow row)
        {
            if (row.Purchases == null || row.Purchases.Count == 0) return "-";
            var counts = new Dictionary<ItemId, int>();
            foreach (ItemId id in row.Purchases)
            {
                counts.TryGetValue(id, out int c);
                counts[id] = c + 1;
            }
            var parts = new List<string>();
            foreach (var kv in counts) parts.Add(kv.Value > 1 ? $"{kv.Key}x{kv.Value}" : kv.Key.ToString());
            return string.Join(" ", parts);
        }

        private static string JoinCounts(Dictionary<string, int> map)
        {
            var parts = new List<KeyValuePair<string, int>>(map);
            parts.Sort((a, b) => b.Value.CompareTo(a.Value));
            var rendered = new List<string>();
            foreach (var kv in parts) rendered.Add($"{kv.Key}:{kv.Value}");
            return string.Join("  ", rendered);
        }

        private static string Arg(string[] args, string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        private static int ArgInt(string[] args, string name, int fallback)
        {
            return int.TryParse(Arg(args, name, fallback.ToString()), out int v) ? v : fallback;
        }
    }
}
