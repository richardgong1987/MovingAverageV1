# AGENTS.md

## Project

`MovingAverageV1` is a cTrader/cAlgo robot written in C#. The cBot targets
`.NET 6` and combines candlestick reversal signals with Bollinger-band flatness detection,
risk-based order planning, chart markers, and CSV trade logging.

The repository also contains Pine Script references and Python utilities for running and
summarizing cTrader CLI backtests.

## Repository Map

- `MovingAverageV1/MovingAverageV1.cs`: cBot composition root and
  lifecycle callbacks.
- `MovingAverageV1/Signals/`: closed-bar signal and candlestick-pattern detection.
- `MovingAverageV1/Indicators/`: indicator series used by strategy filters.
- `MovingAverageV1/Orders/`: pure order planning and cAlgo order execution.
- `MovingAverageV1/Risk/`: risk limits, trading-window rules, and position sizing.
- `MovingAverageV1/OrderLogger/`: trade CSV writing and migration.
- `MovingAverageV1/LineDrawer/`: chart-only signal markers.
- `MovingAverageV1/Models/`: strategy enums and data-transfer models.
- `MovingAverageV1/Biz/`: strategy coordination logic.
- `docs/design/`: design decisions and behavioral specifications.
- `pine-script/`: TradingView reference implementation and supporting documentation.
- `scripts/`: cTrader batch-backtest and report-generation utilities.
- `tests/Pdhpdl.Tests/`: xUnit files copied from the related PDH/PDL strategy; see Known
  Limitations before using them.

## Development Rules

- Read the relevant file in `docs/design/` before changing strategy behavior. Keep C#, Pine
  Script, tests, and design documentation aligned when they describe the same rule.
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

Build the current cBot from the repository root:

```bash
dotnet build MovingAverageV1.sln -c Release
```

For changes to pure logic, add or update focused xUnit coverage after correcting the test
project linkage described below. For cAlgo-dependent behavior, compile the cBot and validate it
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

- `tests/Pdhpdl.Tests/Pdhpdl.Tests.csproj` currently links source files from the sibling
  `PDHPDL Break and Reverse v1` repository rather than this repository. Its results do not
  validate `MovingAverageV1` until those links are corrected.
- `scripts/test.sh` currently names the sibling solution and test project, so do not use it as
  this repository's build/test entry point until it is repaired.
- The production project uses `cTrader.Automate` with a wildcard version. Be alert to package
  resolution or API changes when builds differ between machines.

## Change Checklist

1. Confirm the intended candle index, time zone, and strategy mode for signal changes.
2. Verify risk math with realistic symbol metadata and broker volume constraints.
3. Keep order labels and CSV schema compatibility in mind when modifying execution or logging.
4. Run the Release build and the narrowest meaningful test or backtest.
5. Report any verification that could not be run, especially cTrader backtests.
