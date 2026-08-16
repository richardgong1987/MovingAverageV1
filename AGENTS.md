# AGENTS.md

## Project

`MovingAverageV1` is a cTrader/cAlgo cBot written in C#, targeting `.NET 6`. It trades
candlestick reversal patterns (the "HanJin 26" signal set) filtered by a dual-RMA trend
direction, sizes every order from a fixed percentage of account equity, exits at a fixed R
multiple, and logs each trade to CSV.

The repository also contains the Pine Script originals that the C# ports follow, and Python
tooling for running and summarizing batch cTrader CLI backtests.

**Naming caveat:** this codebase was copied from a sibling PDH/PDL strategy, so most types still
carry a `Pdhpdl` prefix — `PdhpdlOrderPlanner`, `PdhpdlSignalModel`, `PdhpdlOrderExecutor` and so
on. The prefix is historical only. None of these classes has anything to do with previous-day
highs and lows; do not infer behaviour from it.

## Strategy At A Glance

| Step | Where | Rule |
| --- | --- | --- |
| Direction filter | `MainBiz`, `RmaUtils` | Three-line stack: longs need mid > fast > slow (45m RMA13 > 60m RMA13 > 60m RMA55), shorts the reverse |
| Entry signal | `HanJinSignals26` → `MainBiz` | Candlestick patterns on the last **closed** bar (pinbar, engulfing, fractal, harami) |
| Stop | `PdhpdlOrderPlanner` | The pattern's own SL price, pushed out by `StopOffsetTicks` |
| Sizing | `PdhpdlOrderPlanner` + `PdhpdlRiskGuard` | `RiskPct` of equity, converted through the symbol's pip value |
| Take profit | `PdhpdlOrderPlanner` | Fixed `TakeProfitR × R`, written onto the order at entry and executed **broker-side** |
| Breakeven | `PdhpdlOrderExecutor` | At `BreakevenTriggerR × R` of profit, the stop moves to entry ± `BreakevenOffsetTicks` |

There is no partial/two-stage exit: reaching the take profit closes the whole position.

## Repository Map

- `MovingAverageV1/MovingAverageV1.cs`: cBot composition root and lifecycle callbacks.
- `MovingAverageV1/Signals/`: closed-bar signal assembly (`PdhpdlSignalDetector`) and the
  candlestick pattern library (`HanJinSignals26`).
- `MovingAverageV1/Biz/`: `MainBiz` — turns a scanned pattern set into a long/short decision.
- `MovingAverageV1/Indicators/`: indicator series the strategy reads (`Atr14Series`,
  `RmaSeriesSet` — fast/slow pair plus the shorter-timeframe mid line, each a single `RmaSeries`)
  plus chart-only ports that feed nothing back (`MarketStructure`).
- `MovingAverageV1/Orders/`: pure order planning (`PdhpdlOrderPlanner`) and cAlgo order
  execution (`PdhpdlOrderExecutor`).
- `MovingAverageV1/Risk/`: risk limits, trading-window rules, and position sizing.
- `MovingAverageV1/OrderLogger/`: trade CSV writing and migration of older CSV layouts.
- `MovingAverageV1/LineDrawer/`: chart-only drawing (RMA lines, signal markers).
- `MovingAverageV1/Models/`: strategy enums and data-transfer models.
- `MovingAverageV1/Utils/`: small shared helpers (`RmaUtils`, `Utils`).
- `docs/signals/`: reference images and the PDF spec for the candlestick signals.
- `pine-script/lib/`: the TradingView Pine originals the C# ports follow.
- `scripts/`: cTrader batch-backtest and report-generation utilities.
- `tests/MovingAverageV1.Tests/`: xUnit coverage of the cAlgo-free logic, which it links in as
  source rather than referencing the cBot project.

## Development Rules

- Keep C#, Pine Script, tests and this document aligned when they describe the same rule. The
  files in `pine-script/lib/` are the reference for anything that was ported.
- Keep the Robot class as a composition root. Put signal detection, drawing, risk, execution,
  and persistence in their existing focused classes instead of adding more lifecycle logic.
- Evaluate trading signals on completed candles. In `OnBar()`, the last fully closed candle is
  `Bars.Count - 2`; do not accidentally use the newly opened bar.
- Keep calculation-heavy code independent of `cAlgo.API` where practical so it can be tested
  without the cTrader runtime. Isolate broker/platform calls in adapters and executors.
- Treat position sizing and price-unit conversions as high-risk code. Distinguish price,
  ticks, pips, lots, and volume-in-units, and use symbol normalization and limits.
- Preserve order-management sequencing: manage positions, cancel expired pending orders, then
  evaluate a new closed-bar signal unless a documented strategy change requires otherwise.
- The take profit is a static price set once at entry. Do not reintroduce per-tick target
  watching in the executor without a documented reason to.
- Preserve `AccessRights.FullAccess`; CSV logging requires filesystem access.
- Do not expose chart-only constants as cBot parameters. Parameters should affect real strategy
  or operational behavior.
- Never commit credentials or local environment files. Backtest secrets belong in ignored
  `scripts/.env*` files.

## C# Conventions

- Follow `.editorconfig`: four spaces, LF endings, 140-column limit, braces on the same line,
  block-scoped namespaces, explicit types, and System usings first.
- Use the existing `cAlgo.Robots` namespace for production code.
- Prefer small classes with one responsibility and straightforward control flow over new
  abstraction layers.
- Name tests by observable behavior using the repository's snake_case test-method convention.
- Add comments only for non-obvious strategy, timing, or unit-conversion constraints. Keep new
  code comments in English unless the surrounding user-facing text requires Chinese.
- Avoid drive-by formatting or unrelated cleanup, especially in strategy files under active
  modification.

## Build And Verification

Build the cBot from the repository root:

```bash
dotnet build MovingAverageV1.sln -c Release
```

Build the cBot and run the unit tests in one step:

```bash
./scripts/test.sh
```

For changes to pure logic, add or update focused xUnit coverage in
`tests/MovingAverageV1.Tests/`. For cAlgo-dependent behavior, compile the cBot and validate it
in cTrader's backtester; a successful .NET build does not verify trading behavior.

Python backtest tooling prerequisites:

```bash
python3 -m pip install -r scripts/requirements.txt
python3 scripts/run_conditions.py --help
python3 scripts/report_summary.py --help
```

Batch backtests require a configured local environment, a built `.algo`, cTrader CLI access,
and account/API credentials. Default to sequential execution; `--jobs N` is opt-in because
parallel cTrader report generation can fail intermittently. Consult `scripts/README.md` before
running or changing the batch workflow.

## Known Limitations

- Test coverage reaches only the cAlgo-free classes. Everything the test project excludes —
  the Robot itself, indicator wrappers, chart drawing, the order executor, the CSV logger and
  the signal detector — is verified only by a cTrader backtest.
- PDH/PDL-era leftovers still in the tree: the `Pdhpdl` type prefix, the `KeyLevel` CSV column
  (never populated any more, so always empty), and `SignalFamilyModel` (referenced by nothing).
  Removing the CSV column is a schema change and would need `PdhpdlTradeCsvMigrator` handling.
- The production project uses `cTrader.Automate` with a wildcard version. Be alert to package
  resolution or API changes when builds differ between machines.

## Change Checklist

1. Confirm the intended candle index, time zone, and strategy mode for signal changes.
2. Verify risk math with realistic symbol metadata and broker volume constraints.
3. Keep order labels and CSV schema compatibility in mind when modifying execution or logging.
4. Run the Release build and the narrowest meaningful test or backtest.
5. Report any verification that could not be run, especially cTrader backtests.
