using System;
using System.Collections.Generic;
using cAlgo.Robots;
using Xunit;

namespace MovingAverageV1.Tests.Optimisation {
    // The optimisation filter: a pass survives only if every calendar year of the window was
    // profitable, and survivors keep cTrader's built-in score.
    public class AnnualFitnessTests {
        private static readonly DateTime WindowStart = new DateTime(2023, 1, 1);
        private static readonly DateTime WindowEnd = new DateTime(2025, 12, 31);

        [Fact]
        public void a_profitable_year_on_year_pass_keeps_the_platform_score() {
            var fitness = new AnnualFitness(WindowStart, WindowEnd);
            List<ClosedTradeModel> trades = Trades((2023, 100), (2024, 50), (2025, 30));
            var stats = new FitnessStatsModel { NetProfit = 180, WinningTrades = 3, MaxEquityDrawdownPercent = 20 };

            // 180 * 3 / 1.2
            Assert.Equal(450, fitness.Calculate(trades, stats), 6);
        }

        [Fact]
        public void one_losing_year_sinks_a_pass_below_every_survivor() {
            var fitness = new AnnualFitness(WindowStart, WindowEnd);
            List<ClosedTradeModel> trades = Trades((2023, 5000), (2024, -400), (2025, 30));
            var stats = new FitnessStatsModel { NetProfit = 4630, WinningTrades = 2, MaxEquityDrawdownPercent = 10 };

            Assert.True(fitness.Calculate(trades, stats) < 0);
        }

        // A year is judged on its own net, not on the individual trades inside it.
        [Fact]
        public void a_year_that_nets_positive_after_losses_still_passes() {
            var fitness = new AnnualFitness(WindowStart, WindowEnd);
            var trades = new List<ClosedTradeModel> {
                new ClosedTradeModel(new DateTime(2023, 3, 1), -100),
                new ClosedTradeModel(new DateTime(2023, 9, 1), 250),
                new ClosedTradeModel(new DateTime(2024, 6, 1), 10),
                new ClosedTradeModel(new DateTime(2025, 6, 1), 10)
            };
            var stats = new FitnessStatsModel { NetProfit = 170, WinningTrades = 3, MaxEquityDrawdownPercent = 0 };

            Assert.Equal(510, fitness.Calculate(trades, stats), 6);
        }

        // A year with no trades earned nothing, so it fails like any other unprofitable year —
        // this is the case the trade history alone cannot reveal.
        [Fact]
        public void a_year_inside_the_window_with_no_trades_fails() {
            var fitness = new AnnualFitness(WindowStart, WindowEnd);
            List<ClosedTradeModel> trades = Trades((2023, 100), (2025, 100));
            var stats = new FitnessStatsModel { NetProfit = 200, WinningTrades = 2, MaxEquityDrawdownPercent = 0 };

            Assert.True(fitness.Calculate(trades, stats) < 0);
        }

        // A year that broke even exactly is not "greater than zero" either.
        [Fact]
        public void a_break_even_year_fails() {
            var fitness = new AnnualFitness(WindowStart, WindowEnd);
            var trades = new List<ClosedTradeModel> {
                new ClosedTradeModel(new DateTime(2023, 6, 1), 100),
                new ClosedTradeModel(new DateTime(2024, 6, 1), 40),
                new ClosedTradeModel(new DateTime(2024, 7, 1), -40),
                new ClosedTradeModel(new DateTime(2025, 6, 1), 100)
            };
            var stats = new FitnessStatsModel { NetProfit = 200, WinningTrades = 3, MaxEquityDrawdownPercent = 0 };

            Assert.True(fitness.Calculate(trades, stats) < 0);
        }

        [Fact]
        public void fewer_losing_years_scores_higher_than_more_of_them() {
            var fitness = new AnnualFitness(WindowStart, WindowEnd);
            var stats = new FitnessStatsModel { NetProfit = 0, WinningTrades = 0, MaxEquityDrawdownPercent = 0 };

            double oneBadYear = fitness.Calculate(Trades((2023, 100), (2024, -100), (2025, 100)), stats);
            double twoBadYears = fitness.Calculate(Trades((2023, 100), (2024, -100), (2025, -100)), stats);

            Assert.True(oneBadYear > twoBadYears);
        }

        [Fact]
        public void a_pass_that_never_traded_scores_below_every_scored_rejection() {
            var fitness = new AnnualFitness(WindowStart, WindowEnd);
            var stats = new FitnessStatsModel { NetProfit = 0, WinningTrades = 0, MaxEquityDrawdownPercent = 0 };

            double noTrades = fitness.Calculate(new List<ClosedTradeModel>(), stats);
            double heavyLoss = fitness.Calculate(Trades((2023, -1_000_000), (2024, -1_000_000), (2025, -1_000_000)), stats);

            Assert.True(noTrades < heavyLoss);
        }

        // A single-year window is the common case for this project's grids.
        [Fact]
        public void a_single_year_window_only_checks_that_year() {
            var fitness = new AnnualFitness(new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
            var stats = new FitnessStatsModel { NetProfit = 100, WinningTrades = 4, MaxEquityDrawdownPercent = 0 };

            Assert.Equal(400, fitness.Calculate(Trades((2025, 100)), stats), 6);
        }

        // The window is the robot's start/stop time, so a trade closing just outside it still has
        // to answer for its year rather than being silently dropped.
        [Fact]
        public void a_trade_closing_outside_the_window_still_counts_as_its_own_year() {
            var fitness = new AnnualFitness(new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
            List<ClosedTradeModel> trades = Trades((2024, -500), (2025, 100));
            var stats = new FitnessStatsModel { NetProfit = -400, WinningTrades = 1, MaxEquityDrawdownPercent = 0 };

            Assert.True(fitness.Calculate(trades, stats) < 0);
        }

        private static List<ClosedTradeModel> Trades(params (int Year, double NetProfit)[] yearlyProfits) {
            var trades = new List<ClosedTradeModel>();
            foreach ((int year, double netProfit) in yearlyProfits) {
                trades.Add(new ClosedTradeModel(new DateTime(year, 6, 1), netProfit));
            }

            return trades;
        }
    }
}
