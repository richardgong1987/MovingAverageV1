# MovingAverageV1

A cTrader/cAlgo cBot (C#, .NET 6) that trades candlestick reversal patterns filtered by a
dual-RMA trend direction.

- **Entry** — "HanJin 26" candlestick patterns (pinbar, engulfing, fractal, harami) on the last
  closed bar, allowed only when the fast/slow RMA agree with the trade direction.
- **Stop** — the pattern's own invalidation level, pushed out by a configurable tick offset.
- **Size** — a fixed percentage of account equity, converted through the symbol's pip value.
- **Take profit** — a fixed R multiple, written onto the order at entry and executed broker-side.
- **Breakeven** — once profit reaches a configurable R multiple, the stop moves to the entry
  price plus a small offset.

Every trade is written to a CSV under `~/Documents/`, in a folder that depends on the run mode
(`trading_reports` for backtests, `simulate_trading_reports` / `release_trading_reports`
otherwise), so backtest output never overwrites live records.

## Build and test

```bash
dotnet build MovingAverageV1.sln -c Release   # build the .algo
./scripts/test.sh                             # build + run the unit tests
```

The unit tests cover the platform-independent logic (risk guard, order planner, candlestick
patterns, CSV migration). Anything that touches `cAlgo.API` is verified in cTrader's backtester
instead — see `AGENTS.md`.

## Batch backtests

`scripts/` drives the cTrader CLI over a list of parameter sets and summarizes the results into
a chart and a CSV. It needs a configured `scripts/.env`, a built `.algo`, and CLI access — see
`scripts/README.md`.

## Layout

`AGENTS.md` has the full repository map, the strategy rules, and the conventions to follow when
changing this code. Note that most types still carry a `Pdhpdl` prefix inherited from the
sibling strategy this project was copied from; it carries no meaning here.

Pine Script originals for the ported logic live in `pine-script/lib/`, and the candlestick
signal specification (images + PDF) in `docs/signals/`.
