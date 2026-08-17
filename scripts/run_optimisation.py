#!/usr/bin/env python3
"""按本地参数网格批量跑 cTrader 历史回测，再按指标排名（cTrader CLI 没有优化子命令的替代方案）。

cTrader.Mac 的命令行只有 accounts / symbols / run / backtest 四个动词，Optimisation 是桌面端
GUI 独有的功能。所以这里把优化拆成两步：本地枚举参数组合 -> 每个组合跑一条 backtest ->
按指标排名。回测本身完全复用 run_conditions.py 那条链路，两者只有「任务从哪来」不同：

    run_conditions.py    任务来自后端参数接口（跑既定的几套参数）
    run_optimisation.py  任务来自本地网格说明书（扫参数，找哪套参数好）

命令行入口（组合根）：解析参数、读环境配置、展开网格，交给 runner 执行，最后出名次表。

    backtest/grid     —— 网格说明书 -> 一批回测任务（本文件专用）
    backtest/command  —— 把任务翻译成 cTrader CLI 的 backtest 命令（与接口链路共用）
    backtest/runner   —— 串行/并发执行任务（与接口链路共用）
    summary/ranking   —— 报告 JSON + 每条任务的参数组合 -> 名次表

用法：

    python3 run_optimisation.py --spec optimisation.json              # 跑完整网格
    python3 run_optimisation.py --spec optimisation.json --dry-run    # 只看有多少组合、跑多久
    python3 run_optimisation.py --spec optimisation.json --max-passes 100 --jobs 4
    python3 run_optimisation.py --spec optimisation.json --sort-by win_rate --min-trades 30

注意：优化跑的是本次网格的回测，会清空并重建报告目录 ~/Documents/trading_reports —— 上一批
run_conditions.py 的报告会被清掉（zip 存档在上一层，不受影响）。名次表与报告一起留在那里，
不打包也不上传：这是给你自己挑参数用的中间产物，不是要归档的批次结果。
"""

import argparse
import sys
from pathlib import Path

from backtest.command import CBOT_OUTPUT_DIR
from backtest.config import load_config
from backtest.grid import LARGE_GRID_WARNING, build_tasks, load_grid
from backtest.runner import DEFAULT_JOBS, find_missing_reports, print_batch_summary, run_tasks
from summary.ranking import (
    RANKING_CSV_NAME,
    SORTABLE_METRICS,
    build_ranking,
    format_ranking_table,
    write_ranking_csv,
)

SCRIPTS_DIR = Path(__file__).resolve().parent
DEFAULT_ENV_FILE = SCRIPTS_DIR / ".env"
DEFAULT_SPEC_FILE = SCRIPTS_DIR / "optimisation.json"

# 名次表在控制台上只打前几名，完整的看 CSV。
CONSOLE_TOP_N = 10


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description="按本地参数网格批量跑 cTrader 历史回测，并按指标排名。"
    )
    parser.add_argument(
        "--spec",
        default=str(DEFAULT_SPEC_FILE),
        help=f"参数网格说明书 JSON（默认 {DEFAULT_SPEC_FILE.name}；"
        "格式见 scripts/optimisation.example.json）",
    )
    parser.add_argument(
        "--env-file",
        default=str(DEFAULT_ENV_FILE),
        help="环境配置文件路径（默认 scripts/.env），与 run_conditions.py 同一套",
    )
    parser.add_argument(
        "--jobs",
        type=int,
        default=DEFAULT_JOBS,
        help=f"并发回测的任务数（默认 {DEFAULT_JOBS} = 逐条串行）；>1 才开启并发。",
    )
    parser.add_argument(
        "--max-passes",
        type=int,
        default=None,
        help="最多跑多少个组合；网格比它大时随机抽样（同一份说明书每次抽到的是同一批）。",
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=0,
        help="随机抽样的种子（默认 0）；换一个种子换一批组合。",
    )
    parser.add_argument(
        "--sort-by",
        choices=SORTABLE_METRICS,
        default="net_profit",
        help="按哪个指标排名（默认 net_profit）。",
    )
    parser.add_argument(
        "--min-trades",
        type=int,
        default=0,
        help="交易笔数少于这个数的组合不进名次表（默认 0 = 不过滤）；用来挡掉样本太少的噪音。",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="只展开网格并打印规模，不真的跑回测。",
    )
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)
    spec = load_grid(Path(args.spec))
    tasks = build_tasks(spec, max_passes=args.max_passes, seed=args.seed)

    print_plan(spec, tasks, args)

    if args.dry_run:
        return 0

    # 配置在展开网格之后才读：说明书写错时不必先去连账户、找 .algo。
    config = load_config(Path(args.env_file))

    results = run_tasks(tasks, config, max(1, args.jobs))
    missing_reports = find_missing_reports(tasks)
    print_batch_summary(tasks, results, missing_reports)

    print_ranking(tasks, spec, args)

    # 有任何一条没跑成就以非零码退出：名次表照常出，但它少了几个组合，别当成完整结果。
    return 1 if missing_reports or any(not result.is_successful for result in results) else 0


def print_plan(spec, tasks, args):
    """开跑前先把这次要跑什么说清楚：组合从哪来、跑几条、跑多久量级。"""
    print(f"网格说明书：{args.spec}")
    print(f"回测区间：{spec.start} -> {spec.end}，品种 {spec.symbol}，周期 {spec.period}")
    print(f"扫描参数：{'、'.join(spec.swept_names)}")
    print(f"完整网格 {spec.total_combinations} 个组合，本次执行 {len(tasks)} 条。")

    if len(tasks) < spec.total_combinations:
        print(f"（超过 --max-passes {args.max_passes}，已按种子 {args.seed} 随机抽样。）")
    elif len(tasks) >= LARGE_GRID_WARNING:
        print(
            f"（{len(tasks)} 条是过夜的量级：一条 ticks 回测按分钟计，先用 --max-passes 试小批，"
            "或把 .env 的 DATA_MODE 换成 m1。）"
        )


def print_ranking(tasks, spec, args):
    """出名次表：CSV 留在报告目录，前几名直接打在控制台。"""
    ranked_passes = build_ranking(
        tasks, CBOT_OUTPUT_DIR, sort_by=args.sort_by, min_trades=args.min_trades
    )
    csv_path = write_ranking_csv(ranked_passes, spec.swept_names, CBOT_OUTPUT_DIR / RANKING_CSV_NAME)

    print(f"\n===== 名次表（按 {args.sort_by} 从好到差，共 {len(ranked_passes)} 个组合） =====")
    print(format_ranking_table(ranked_passes, spec.swept_names, limit=CONSOLE_TOP_N))
    print(f"\n完整名次表：{csv_path}")


if __name__ == "__main__":
    sys.exit(main())
