#!/usr/bin/env python3
"""把桌面端 Optimisation 跑出来的 pass 目录按 fitness 排名，取前 N 名复制出来并重新编号。

桌面端优化把每个 pass 的产物写在

    ~/cAlgo/Data/cBots/<cBot>/<实例GUID>-Default/Optimization/<pass 号>/

一个 pass 一个目录（report.html / events.json / log.txt / parameters.cbotset），目录名是 pass 号，
跟名次没有关系。**fitness 本身不落盘**——它只活在界面的那张表里，重启就没了。所以这里从每个
pass 的 report.html 里嵌的那份回测报告 JSON 重新算一遍，算法与 cBot 里的 AnnualFitness
（MovingAverageV1/Optimisation/AnnualFitness.cs）逐条对应：

    回测区间里每个自然年都赚钱  ->  fitness = 净利润 × 盈利笔数 ÷ (1 + 最大净值回撤% / 100)
    有任何一年没赚钱            ->  fitness = Σ(那些年的净利 − 1)，必为负，排在所有幸存者之后
    一笔都没成交                ->  fitness = -1e12

上面那条乘除式是 cTrader 内置公式（cBot 没重写 GetFitness 时用的那个），已用界面上 22 个 pass 的
fitness 逐个对过，误差在 1e-11（浮点舍入）以内。cBot 现在重写了 GetFitness，界面上的 fitness 就是
这里算的分数——两边改一边就得改另一边，否则名次对不上。

取自 report.html 里的报告 JSON：main.netProfit、tradeStatistics.winningTrades.all、
equity.maxEquityDrawdownPercent，年度净利来自 history.items 的 closeTime / net，
区间来自 main.testingPeriod。

注意：报告里的 closeTime 是 UTC，cBot 那边按机器人时区（东京，UTC+9）分年。跨年夜前 9 小时
平掉的那一笔，两边会分进不同的年份——只有这种擦边的单子会让名次出现差异。

用法：

    python3 export_optimisation_passes.py                     # 前 50 名 -> ~/Downloads/tmp
    python3 export_optimisation_passes.py --top 20 --dry-run  # 只看排名，不复制
    python3 export_optimisation_passes.py --source <Optimization 目录> --dest <目标目录>

复制出来的目录按名次重新编号（1 = 第一名），原 pass 号写进 ranking.csv 备查：

    ~/Downloads/tmp/1/   <- pass 726
    ~/Downloads/tmp/2/   <- pass 574
    ~/Downloads/tmp/ranking.csv
"""

import argparse
import csv
import json
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

# report.html 把整份回测报告塞在这个 <script> 里，与 CLI 的 --report-json 是同一套结构。
REPORT_JSON_START = '<script type="application/json" id="backtesting-report">'
REPORT_JSON_END = "</script>"

CBOT_DATA_DIR = Path.home() / "cAlgo" / "Data" / "cBots"
DEFAULT_CBOT_NAME = "MovingAverageV1"
DEFAULT_DEST = Path.home() / "Downloads" / "tmp"
DEFAULT_TOP = 50

RANKING_CSV_NAME = "ranking.csv"
RANKING_COLUMNS = ["名次", "原pass", "fitness", "净利润", "盈利笔数", "总笔数", "最大净值回撤%", "未盈利年份"]

# 与 AnnualFitness 里的两个常数一一对应，改一边就得改另一边。
NO_TRADES_FITNESS = -1e12
FAILED_YEAR_PENALTY = 1.0


class PassResult:
    """一个 pass 目录 + 从它的报告里算出来的 fitness。"""

    def __init__(self, path, net_profit, winning_trades, total_trades, max_equity_drawdown_percent, profit_by_year):
        self.path = path
        self.pass_id = path.name
        self.net_profit = net_profit
        self.winning_trades = winning_trades
        self.total_trades = total_trades
        self.max_equity_drawdown_percent = max_equity_drawdown_percent
        # 年 -> 该年净利，区间里一笔没成交的年份也在，值为 0（这种年份同样算没盈利）。
        self.profit_by_year = profit_by_year

    @property
    def failed_years(self):
        return sorted(year for year, profit in self.profit_by_year.items() if profit <= 0)

    @property
    def fitness(self):
        if not self.profit_by_year:
            return NO_TRADES_FITNESS
        if not self.failed_years:
            return self.net_profit * self.winning_trades / (1 + self.max_equity_drawdown_percent / 100)
        return sum(self.profit_by_year[year] - FAILED_YEAR_PENALTY for year in self.failed_years)

    @property
    def sort_key(self):
        """fitness 高的在前；完全同分的（重复参数组合会跑出一模一样的结果）按 pass 号排，
        好让同一批产物每次导出的编号都一样。"""
        return (-self.fitness, _pass_number(self.pass_id))


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description="按 cTrader 内置 fitness 给优化 pass 排名，前 N 名复制出来并重新编号。"
    )
    parser.add_argument(
        "--source",
        default=None,
        help=f"Optimization 目录（默认自动找 {CBOT_DATA_DIR}/{DEFAULT_CBOT_NAME} 下唯一的实例）",
    )
    parser.add_argument("--cbot", default=DEFAULT_CBOT_NAME, help=f"自动查找时用的 cBot 名（默认 {DEFAULT_CBOT_NAME}）")
    parser.add_argument("--top", type=int, default=DEFAULT_TOP, help=f"复制前几名（默认 {DEFAULT_TOP}）")
    parser.add_argument("--dest", default=str(DEFAULT_DEST), help=f"复制到哪里（默认 {DEFAULT_DEST}）")
    parser.add_argument("--dry-run", action="store_true", help="只打印排名，不复制。")
    parser.add_argument("--force", action="store_true", help="目标目录已存在时先删掉重建。")
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)

    source = Path(args.source) if args.source else find_optimization_dir(args.cbot)
    results = read_pass_results(source)
    if not results:
        print(f"{source} 下没有找到任何可读的 pass（缺 report.html 或报告不完整）。")
        return 1

    ranked = sorted(results, key=lambda result: result.sort_key)[: max(1, args.top)]

    print(f"来源：{source}")
    print(f"读到 {len(results)} 个 pass，按 fitness 取前 {len(ranked)} 名：\n")
    print_ranking(ranked)

    if args.dry_run:
        return 0

    dest = prepare_dest(Path(args.dest), args.force)
    for rank, result in enumerate(ranked, start=1):
        shutil.copytree(result.path, dest / str(rank))

    csv_path = write_ranking_csv(ranked, dest / RANKING_CSV_NAME)
    print(f"\n已复制 {len(ranked)} 个 pass 到 {dest}（目录名 1..{len(ranked)} 即名次）")
    print(f"原 pass 号对照表：{csv_path}")
    return 0


def find_optimization_dir(cbot_name):
    """自动定位 Optimization 目录：<cBot>/<实例GUID>-Default/Optimization。

    一个 cBot 可能有多个实例目录（图表上开了几个就有几个），这时不猜，让人用 --source 指定。
    """
    cbot_dir = CBOT_DATA_DIR / cbot_name
    if not cbot_dir.is_dir():
        raise FileNotFoundError(f"找不到 cBot 数据目录：{cbot_dir}（用 --source 直接指定 Optimization 目录）")

    candidates = sorted(path for path in cbot_dir.glob("*/Optimization") if path.is_dir())
    if not candidates:
        raise FileNotFoundError(f"{cbot_dir} 下没有 Optimization 目录（先在桌面端跑一次优化）")
    if len(candidates) > 1:
        listed = "\n  ".join(str(path) for path in candidates)
        raise ValueError(f"{cbot_dir} 下有多个实例，请用 --source 指定其中一个：\n  {listed}")
    return candidates[0]


def read_pass_results(source):
    """扫描 Optimization 目录下的每个 pass，读出算 fitness 要的三项。

    读不出来的 pass（没跑完、report.html 缺失或结构不对）直接跳过：优化中断时这种目录很常见，
    不该让整次导出失败。
    """
    results = []
    for pass_dir in sorted(Path(source).iterdir(), key=lambda path: _pass_number(path.name)):
        if not pass_dir.is_dir():
            continue
        result = _read_pass(pass_dir)
        if result is not None:
            results.append(result)
    return results


def print_ranking(ranked):
    print(f"{'名次':>4} {'pass':>6} {'fitness':>20} {'净利润':>14} {'盈利/总':>10} {'回撤%':>8}  未盈利年份")
    for rank, result in enumerate(ranked, start=1):
        print(
            f"{rank:>4} {result.pass_id:>6} {result.fitness:>20,.2f} {result.net_profit:>14,.2f} "
            f"{result.winning_trades:>4}/{result.total_trades:<5} {result.max_equity_drawdown_percent:>8.2f}"
            f"  {_format_failed_years(result)}"
        )


def _format_failed_years(result):
    if not result.profit_by_year:
        return "无成交"
    return ",".join(str(year) for year in result.failed_years)


def prepare_dest(dest, force):
    """准备目标目录。已存在且非空时默认不动它——里面可能是上一次导出的结果，也可能是别的东西。"""
    if dest.exists() and any(dest.iterdir()):
        if not force:
            raise FileExistsError(f"目标目录已存在且非空：{dest}\n确认可以覆盖就加 --force。")
        shutil.rmtree(dest)
    dest.mkdir(parents=True, exist_ok=True)
    return dest


def write_ranking_csv(ranked, csv_path):
    """名次 -> 原 pass 号的对照表。用 utf-8-sig（带 BOM），中文表头在 Excel 里能正确识别。"""
    with open(csv_path, "w", encoding="utf-8-sig", newline="") as csv_file:
        writer = csv.writer(csv_file)
        writer.writerow(RANKING_COLUMNS)
        for rank, result in enumerate(ranked, start=1):
            writer.writerow(
                [
                    rank,
                    result.pass_id,
                    round(result.fitness, 2),
                    round(result.net_profit, 2),
                    result.winning_trades,
                    result.total_trades,
                    round(result.max_equity_drawdown_percent, 2),
                    _format_failed_years(result),
                ]
            )
    return csv_path


def _read_pass(pass_dir):
    report = _load_report(pass_dir / "report.html")
    if report is None:
        return None

    try:
        return PassResult(
            path=pass_dir,
            net_profit=float(report["main"]["netProfit"]),
            winning_trades=int(report["tradeStatistics"]["winningTrades"]["all"]),
            total_trades=int(report["tradeStatistics"]["totalTrades"]["all"]),
            max_equity_drawdown_percent=float(report["equity"]["maxEquityDrawdownPercent"]),
            profit_by_year=_profit_by_year(report),
        )
    except (KeyError, TypeError, ValueError):
        return None


def _profit_by_year(report):
    """按平仓年份汇总净利，并把回测区间里一笔没成交的年份补成 0。

    补零这一步就是过滤的重点：空转的一年在 history 里没有任何痕迹，不补上就会被当成「没有这一年」
    而蒙混过关。区间边界外平掉的单子也保留（cBot 那边同样保留），它照样是没赚钱的一年。
    """
    profit_by_year = {}
    history = report.get("history") or {}
    for trade in history.get("items") or []:
        year = _epoch_ms_year(trade["closeTime"])
        profit_by_year[year] = profit_by_year.get(year, 0.0) + float(trade["net"])

    if not profit_by_year:
        return {}

    testing_period = report["main"]["testingPeriod"]
    first_year = min(_epoch_ms_year(testing_period["startDate"]), min(profit_by_year))
    last_year = max(_epoch_ms_year(testing_period["endDate"]), max(profit_by_year))
    for year in range(first_year, last_year + 1):
        profit_by_year.setdefault(year, 0.0)
    return profit_by_year


def _epoch_ms_year(epoch_ms):
    return datetime.fromtimestamp(epoch_ms / 1000, tz=timezone.utc).year


def _load_report(report_path):
    """从 report.html 里把那份报告 JSON 抠出来。读不了或结构不对就返回 None。"""
    try:
        html = report_path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return None

    start = html.find(REPORT_JSON_START)
    if start < 0:
        return None
    start += len(REPORT_JSON_START)
    end = html.find(REPORT_JSON_END, start)
    if end < 0:
        return None

    try:
        report = json.loads(html[start:end])
    except json.JSONDecodeError:
        return None
    return report if isinstance(report, dict) else None


def _pass_number(name):
    """pass 目录名是数字，按数值排而不是按字符串排（否则 10 会排在 9 前面）。"""
    return int(name) if name.isdigit() else sys.maxsize


if __name__ == "__main__":
    sys.exit(main())
