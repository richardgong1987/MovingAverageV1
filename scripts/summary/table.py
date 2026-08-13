"""导出层：把汇总 DataFrame 写成 final_summary_report.csv。

固定列是「哪份报告 + 跑得怎么样」（recordId / 品种 / 周期 / 胜率 / 盈利），指标来自报告 JSON；
后面跟着这批回测用到的全部参数列，来自条件清单 conditions.json（见 conditions.py）。
参数列随后端记录表变，这里不写死任何参数名——不同策略模板的字段本来就不一样。
"""

import csv

from .conditions import UNKNOWN_CONDITION

FIXED_COLUMNS = [
    "文件名",
    "recordId",
    "种类",
    "周期",
    "胜率%",
    "盈利金额",
    "盈利率%",
]


def write_summary_csv(frame, csv_path, conditions):
    """把汇总表写成 CSV。用 utf-8-sig（带 BOM），中文表头在 Excel 里能正确识别。"""
    parameter_columns = _collect_parameter_columns(frame, conditions)
    with open(csv_path, "w", encoding="utf-8-sig", newline="") as csv_file:
        writer = csv.writer(csv_file)
        writer.writerow(FIXED_COLUMNS + parameter_columns)
        for row in frame.itertuples(index=False):
            condition = conditions.get(row.report, UNKNOWN_CONDITION)
            writer.writerow(_build_row(row, condition, parameter_columns))


def _collect_parameter_columns(frame, conditions):
    """本批出现过的参数列，按记录里的字段顺序、首次出现排。

    一批里混了不同模板时取并集：某份报告没有的参数在它那行留空，而不是整列消失。
    """
    columns = {}
    for row in frame.itertuples(index=False):
        condition = conditions.get(row.report)
        if condition is None:
            continue
        for column in condition.display_values():
            columns.setdefault(column, None)
    return list(columns)


def _build_row(row, condition, parameter_columns):
    """一份报告 -> 一行 CSV。row 是 DataFrame 的一行（含 report / win_rate / net_profit / starting_capital）。"""
    values = condition.display_values()
    # 盈利率 = 盈利金额 / 初始资金 x 100
    roi = (row.net_profit / row.starting_capital * 100.0) if row.starting_capital else 0.0
    return [
        row.report,
        condition.record_id,
        condition.symbol,
        condition.period,
        f"{row.win_rate:.0f}%",
        f"{row.net_profit}$",
        f"{roi:.2f}%",
    ] + [values.get(column, "") for column in parameter_columns]
