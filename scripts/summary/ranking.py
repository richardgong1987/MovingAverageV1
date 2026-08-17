"""把一批优化回测的结果排成名次表：指标来自报告 JSON，参数列来自每条任务自己的组合。

与 table.py 的区别只有一个：那边按报告名排（谁先跑的一目了然），这边按指标排（谁跑得好
一目了然）。跑不出可用报告的组合不会出现在表里 —— 报告缺失的原因见批次结论行。
"""

import csv

from .metrics import load_report_frame

RANKING_CSV_NAME = "optimisation_ranking.csv"

# 可以拿来排名的指标，都来自 metrics.read_report_stats。
# 想按盈亏比 / 最大回撤排，要先在 metrics.py 里把那两个字段从报告 JSON 读出来。
SORTABLE_METRICS = ("net_profit", "win_rate", "total_trades")

FIXED_COLUMNS = ["名次", "pass", "盈利金额", "胜率%", "交易笔数"]


class RankedPass:
    """名次表里的一行：一个参数组合 + 它跑出来的指标。"""

    def __init__(self, rank, report, net_profit, win_rate, total_trades, parameters):
        self.rank = rank
        self.report = report
        self.net_profit = net_profit
        self.win_rate = win_rate
        self.total_trades = total_trades
        self.parameters = parameters


def build_ranking(tasks, output_dir, sort_by="net_profit", min_trades=0):
    """扫描报告目录，把跑出结果的组合按 sort_by 从好到差排好。

    min_trades 用来挡掉样本太少的组合：三笔交易赢两笔的 66% 胜率不是策略好，是噪音。
    被挡掉的组合和没跑出报告的组合一样，不进表。
    """
    if sort_by not in SORTABLE_METRICS:
        raise ValueError(f"不支持按 {sort_by} 排名，可选：" + "、".join(SORTABLE_METRICS))

    swept_values_by_report = {
        task.report_stem: task.swept_values for task in tasks if hasattr(task, "swept_values")
    }

    frame = load_report_frame(output_dir)
    rows = [
        row
        for row in frame.itertuples(index=False)
        if row.report in swept_values_by_report and row.total_trades >= min_trades
    ]
    rows.sort(key=lambda row: getattr(row, sort_by), reverse=True)

    return [
        RankedPass(
            rank=rank,
            report=row.report,
            net_profit=row.net_profit,
            win_rate=row.win_rate,
            total_trades=row.total_trades,
            parameters=swept_values_by_report[row.report],
        )
        for rank, row in enumerate(rows, start=1)
    ]


def write_ranking_csv(ranked_passes, parameter_names, csv_path):
    """把名次表写成 CSV。用 utf-8-sig（带 BOM），中文表头在 Excel 里能正确识别。"""
    with open(csv_path, "w", encoding="utf-8-sig", newline="") as csv_file:
        writer = csv.writer(csv_file)
        writer.writerow(FIXED_COLUMNS + list(parameter_names))
        for ranked_pass in ranked_passes:
            writer.writerow(_build_row(ranked_pass, parameter_names))
    return csv_path


def format_ranking_table(ranked_passes, parameter_names, limit=10):
    """把前 limit 名排成等宽文本，直接打在批次结论后面。"""
    if not ranked_passes:
        return "（没有任何组合跑出可用报告，无法排名）"

    header = FIXED_COLUMNS + list(parameter_names)
    rows = [[str(cell) for cell in _build_row(ranked_pass, parameter_names)] for ranked_pass in ranked_passes[:limit]]
    widths = [max(len(header[column]), *(len(row[column]) for row in rows)) for column in range(len(header))]

    lines = ["  ".join(header[column].ljust(widths[column]) for column in range(len(header)))]
    lines.append("  ".join("-" * widths[column] for column in range(len(header))))
    lines.extend("  ".join(row[column].ljust(widths[column]) for column in range(len(header))) for row in rows)
    return "\n".join(lines)


def _build_row(ranked_pass, parameter_names):
    return [
        ranked_pass.rank,
        ranked_pass.report,
        round(ranked_pass.net_profit, 2),
        round(ranked_pass.win_rate, 2),
        ranked_pass.total_trades,
        *(ranked_pass.parameters.get(name, "") for name in parameter_names),
    ]
