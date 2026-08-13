# Batch backtest script (run_conditions.py)

Runs cTrader historical backtests (`backtest`) one record at a time, following the plan the
backend parameter API hands down. Each run finishes and automatically moves on to the next, and
each writes its trade details to its own CSV.

## Installing dependencies

```bash
pip install -r scripts/requirements.txt
```

Third-party dependencies: `pandas` + `matplotlib` (summarize backtest reports and draw the bar
chart) and `python-dotenv` (parse `.env`); everything else is the Python standard library — the
parameter API is fetched with `urllib`.

## Environment config (.env)

Account, paths, credentials and other environment-specific settings live in a `.env` file
instead of being hard-coded, so you don't have to copy the script per environment — just
maintain the env file:

```bash
cp scripts/.env.example scripts/.env        # first time: create the dev config and fill it in
```

Required keys: `AUTH_TOKEN`, `CTRADER_BIN`, `CTID`, `ACCOUNT`,
`CTRADER_PARAMETER_RECORDS_URL` (the backtest plan source — without it there is nothing to run);
optional: `DATA_MODE` (default `m1`), `BALANCE` (default `10000`),
`REPORT_UPLOAD_URL` (blank = skip the report upload).

The `.algo` path is not configured: `dotnet build` publishes the package to the directory
above the repo root, named after the repo root folder, so `backtest/config.py` derives it.
Build first — the run aborts with a clear error if the `.algo` is missing.

### Inheritance: `.env` is the shared base

`scripts/.env` is loaded for **every** environment. An environment-specific file passed via
`--env-file` (e.g. `.env-prod`) is loaded on top of it and only needs the keys that differ —
same-named keys win, everything else is inherited:

```
scripts/.env         AUTH_TOKEN, CTRADER_BIN, CTID, ACCOUNT, ...   # shared, edit once
scripts/.env-prod    only the keys that differ in production
```

So the required-keys check applies to the merged result: a key that lives in `.env` does not
have to be repeated in `.env-prod`. Note the flip side — a key you *delete* from `.env-prod`
falls back to the base value rather than becoming unset; to blank one out, write `KEY=`.

`.env` and `.env-prod` contain the auth token and are ignored in `.gitignore`, so they are
never committed; only the `.env.example` template is version-controlled.

## How to use

1. Add/remove parameter records in the backend admin (they are what this script fetches).
2. Run:

   ```bash
   python3 scripts/run_conditions.py                          # reads scripts/.env by default
   python3 scripts/run_conditions.py --env-file scripts/.env-prod   # .env + prod overrides
   python3 scripts/run_conditions.py --jobs 4                 # run up to 4 at a time (opt-in)
   ```

3. Each run's result CSV is written to `~/Documents/trading_reports/`, named after its
   `recordId`.

Before running, the script validates inputs (missing env file / missing required keys, API
error responses, records missing `recordId`/`symbol`/`period`, invalid date or number values)
and fails with a clear message naming the offending `recordId`, so it never runs with a broken
config. Every parameter field is formatted up front, so a bad value stops the batch before the
first backtest rather than halfway through.

### Parallel backtests (--jobs)

`--jobs N` controls how many backtests run at once. **The default is 1 (sequential).**
Running several cTrader processes at once has been observed to make backtests fail
intermittently inside cTrader's own report-saving step
(`InvalidOperationException: Message expected`), and a failed task disappears from the summary
without stopping the batch — so parallelism is opt-in. If you do use `--jobs N`, check that the
number of report JSONs matches the number of plan rows. Backtesting is
CPU/memory intensive — going beyond the physical core count usually isn't faster and just
makes the runs contend for resources.

The first time you run a given symbol in parallel, it's best to run one with `--jobs 1` first
to warm the m1 data cache, then scale up — this avoids multiple processes downloading the
same data at once and conflicting. The summary chart `final_report.png` is generated once
after all tasks finish, so concurrency doesn't affect it.

> cTrader's official docs state the backtesting engine supports running multiple backtest
> processes in parallel, but concurrency is not explicitly endorsed at the CLI level. For a
> first parallel run, validate a small sample (2–3 rows) produces correct reports/CSVs before
> scaling up.

## Backtest plan (parameter API)

`GET $CTRADER_PARAMETER_RECORDS_URL` returns the plan; **one record = one backtest**:

```json
{"code": 200, "success": true, "data": [
  {"recordId": 1, "symbol": "XAUUSD", "period": "m5",
   "parameterFields": [
     {"name": "TakeProfitR", "type": "double", "value": 2},
     {"name": "EntryModel",  "type": "enum",   "value": 0},
     {"name": "start",       "type": "date",   "value": "2026-01-01"}
   ]}
]}
```

- `symbol` / `period` become `--symbol` / `--period`.
- Every entry in `parameterFields` becomes `--<name>=<value>` verbatim, so **`name` must match
  the cBot's C# property name exactly** (the cTrader CLI matches by property name, not by the
  Chinese display name). Adding or removing a backtest parameter is a backend-record change
  only — this script keeps no parameter whitelist.
- `value` is formatted by `type`: `double` → `2.0` becomes `2`, `1.75` kept as is; `int`/`enum`
  → integer; `bool` → `True`/`False`; `date` → the `YYYY-MM-DD` from the API is converted to the
  **`DD/MM/YYYY` (UTC)** cTrader expects.
- A field with an empty value is **not passed**, so the cBot falls back to its own default.
- `FileName` is ignored: the trade CSV path is set by `command.py` to an absolute path next to
  the report JSON.

Enums are passed through as their integer value — the script keeps no enum table, so a record
can use any template's parameter set (the Dragon and pdhpdl templates do not share fields).

## Output CSV filename

The record's `recordId` already identifies a backtest, so the filename no longer encodes
parameters — it is just the id plus the symbol/period, which keeps chart labels and logs
readable:

```
<recordId>-<symbol>-<period>.csv
```

Example: `17-XAUUSD-m5.csv`. Files are written to `~/Documents/trading_reports/`.

Each backtest also produces a **backtest report JSON** (`--report-json`), in the **same
directory with the same name** as the CSV, only with the extension changed to `.json`
(`17-XAUUSD-m5.json`), so each run's trade details and statistics sit together as a matched
pair.

### conditions.json (what each report was run with)

Because the filename no longer carries the parameters, the batch writes the plan itself to
`~/Documents/trading_reports/conditions.json` right after clearing the directory and before the
first backtest — keyed by report filename (without extension), with the API record stored
verbatim (minus the ignored `FileName`):

```json
{"17-XAUUSD-m5": {"recordId": 17, "symbol": "XAUUSD", "period": "m5",
                  "parameterFields": [{"name": "Strategy", "label": "策略模式", "value": 1, "options": [...]}]}}
```

The summary layer reads it to fill the config columns, and it travels inside the report zip, so
an uploaded batch still records exactly what produced each report. Re-running
`report_summary.py` over a directory without this file still works — the config columns are
simply left blank.

## Summary outputs (final_report.png + final_summary_report.csv)

Once **all** tasks finish, the script scans **all** backtest report JSONs in the output
directory, summarizes them with `pandas`, and writes these files to
`~/Documents/trading_reports/`:

**1. `final_report.png`** — two stacked bar charts drawn with `matplotlib`:

- **Top chart**: net profit per report (`main.netProfit`), green for profit, red for loss.
- **Bottom chart**: win rate per report (`winningTrades.all / totalTrades.all`).
- The X-axis label is each report's full filename (without extension), so you can tell at a
  glance which parameter set / date range it is.

**2. `final_summary_report.csv`** — one row per report. Fixed columns first, then one column per
parameter the batch actually used:

```
文件名, recordId, 种类, 周期, 胜率%, 盈利金额, 盈利率%, 策略模式, 第一目标R, 起始日期, ...
17-XAUUSD-m5, 17, XAUUSD, m5, 52%, 1234.5$, 12.35%, Strong, 2, 2026-01-01, ...
```

`胜率%` (win rate), `盈利金额` (net profit) and `盈利率%` (return on capital) come from the
report JSON — win rate = `winningTrades.all / totalTrades.all`, and `盈利率% = netProfit /
startingCapital × 100` (starting capital read from each report, `10000` by default). The
parameter columns come from `conditions.json`: the column header is the field's `label` from the
backend (falling back to its `name`), and an enum shows its member name instead of the raw
integer. Add or remove a backend parameter and the column follows — there is no column
whitelist here either. Written with a UTF-8 BOM so the Chinese headers open correctly in Excel.

**3. `metadata_<ts>.json`** — the machine-readable twin, keyed by trade CSV filename, holding
`recordId` / symbol / period / the labelled parameters, plus a `raw` block with the untouched
API values and report metrics. This is what the upload endpoint stores as `extra`.

Both are generated once at the end of the batch. The summary logic lives in the `summary/`
package; `report_summary.py` is a thin CLI over it that can be run standalone to (re)generate
both files manually at any time — e.g. mid-run in another terminal, or without re-running
backtests:

```bash
python3 scripts/report_summary.py                 # scans ~/Documents by default
python3 scripts/report_summary.py --dir <dir>     # specify the report directory
```

> Note: the summary covers **all** report JSONs in the directory, including leftovers from
> previous runs. To summarize only one batch, clear the old `*.json` from the directory first.

## Tunables

Environment-related (edit in `.env` / `.env-prod`):

- `AUTH_TOKEN` / `CTRADER_BIN` / `CTID` / `ACCOUNT` (account, paths, credentials)
- `CTRADER_PARAMETER_RECORDS_URL` (parameter API — the backtest plan source)
- `REPORT_UPLOAD_URL` (where the report zip is uploaded; blank = skip)
- `BALANCE` (starting capital, default `10000`)
- `DATA_MODE` (backtest data mode, default `m1`; options `open`, `m1-csv`)

Strategy parameters: **all of them come from the parameter API** — the script no longer pins
any cBot parameter. To change one, edit the backend record; to add or remove one, add or remove
a `parameterFields` entry (its `name` must be the cBot's C# property name). A parameter the API
doesn't send falls back to the cBot's own default.

> The MA parameters aren't just cosmetic: `MaSource`/`MaFastPeriod`/`MaSlowPeriod`/
> `MaTimeFrameMinutes` feed the RMA series, and the long/short signal filters direction using
> the relative position of the fast/slow RMA — so they directly affect the trades a backtest
> produces.

## Code structure

The CLI entry point `run_conditions.py` only does the "composition" (parse args + wire the
modules together); the actual logic is split by responsibility into the `backtest/` package,
each part doing one thing and decoupled from the others:

```
run_conditions.py     Backtest CLI entry point (composition root: parse_args + main)
report_summary.py     Chart CLI entry point (refresh final_report.png standalone)
backtest/             Running backtests
  config.py           read .env, produce Config (account/paths/credentials/API url/capital)
  records.py          GET the parameter API -> raw parameter records                 [HTTP]
  parameters.py       a parameterField -> a CLI argument (value formatting by type)  [data]
  plan.py             parameter record -> backtest task (ConditionRow)
  command.py          task + config -> cTrader CLI command; also writes conditions.json
  runner.py           run tasks sequentially/in parallel; generate the chart once at the end
summary/              Summarizing results
  metrics.py          read report JSONs -> DataFrame (win rate / net profit)     [data]
  conditions.py       read conditions.json -> the record behind each report      [data]
  chart.py            DataFrame -> two-panel bar chart PNG                       [presentation]
  table.py            DataFrame -> final_summary_report.csv                      [presentation]
  metadata.py         DataFrame -> metadata.json (machine-readable)              [presentation]
  report.py           scan dir -> summarize -> chart + csv (public: update_final_report)
```

Dependency direction: `run_conditions → {backtest.plan, backtest.runner}`,
`backtest.plan → {backtest.records, backtest.parameters}`,
`backtest.runner → {backtest.command, summary}`,
`backtest.command → summary.conditions` (for the shared `conditions.json` filename), and
`report_summary → summary`. Within `summary`: `report → {metrics, conditions, chart, table,
metadata}`, and `{table, metadata} → conditions`. Leaf modules (`config` / `records` /
`parameters` / `metrics` / `conditions` / `chart` / `table` / `metadata`) don't depend back on
their orchestrators, and `summary` never imports `backtest`.

## Key design notes

- Uses the CLI's **`backtest`** subcommand (historical backtest, stops when done), **not
  `run`** (`run` is live/forward execution, stays connected to the live account and never
  exits on its own).
- The command includes **`--exit-on-stop`**: after a backtest finishes the process would not
  exit on its own (it idles); this flag makes it terminate so the script can move to the next.
- The command includes **`--full-access`**: allows the cBot to write out its own trade CSV.
- Dates are always passed as **DD/MM/YYYY (UTC)** to cTrader; the script validates the format.

Official CLI docs: <https://help.ctrader.com/ctrader-algo/documentation/ctrader-cli/>
