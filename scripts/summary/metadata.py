"""导出层：把每份回测报告的配置写成 metadata.json。

用途：CSV 便于人看，但不便于机器入库查询。metadata.json 以「生成的交易 CSV 文件名」为 key，
value 里放这条回测的配置（recordId / 品种 / 周期 / 全部参数），方便日后原样导入数据库按条件检索；
同时在 raw 里保留未加工的原始字段（参数原值与报告 JSON 的指标），做到既可查询又不丢信息。

配置字段来自批量回测留下的条件清单 conditions.json（见 conditions.py），本模块不额外读盘。
"""

import json

from .conditions import UNKNOWN_CONDITION

METADATA_NAME = "metadata.json"


def write_metadata_json(frame, json_path, conditions):
    """把汇总 DataFrame 写成 metadata.json：{ csv 文件名: {配置..., raw:{原始..}} }。"""
    metadata = {}
    for row in frame.itertuples(index=False):
        csv_file_name = f"{row.report}.csv"
        metadata[csv_file_name] = _build_entry(row, conditions.get(row.report, UNKNOWN_CONDITION))

    with open(json_path, "w", encoding="utf-8") as json_file:
        json.dump(metadata, json_file, ensure_ascii=False, indent=2)


def _build_entry(row, condition):
    """一份报告 -> 一条 metadata：可入库的配置字段 + raw 原始字段。"""
    return {
        "recordId": condition.record_id,
        "种类": condition.symbol,
        "周期": condition.period,
        # 参数按后台配的中文 label 展开，枚举已还原成成员名，直接就能看/入库。
        "参数": condition.display_values(),
        "raw": {
            "report": row.report,
            "recordId": condition.record_id,
            "parameters": condition.raw_values(),
            "net_profit": row.net_profit,
            "win_rate": row.win_rate,
            "total_trades": row.total_trades,
            "starting_capital": row.starting_capital,
        },
    }
