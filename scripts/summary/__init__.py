"""汇总 cTrader 回测报告（--report-json 的 JSON），产出两个文件：

- final_report.png          胜率 / 净利润的柱状图
- final_summary_report.csv  每份报告一行（文件名/起止/周期/止盈/胜率%/盈利金额）

按职责拆成：

- metrics ：把报告 JSON 读成一张 DataFrame（胜率 / 净利润）——数据层。
- conditions：读批量回测留下的 conditions.json，还原每份报告对应的参数记录——数据层。
- chart   ：把 DataFrame 画成柱状图 PNG——展示层。
- table   ：把 DataFrame 导出成 CSV——导出层。
- report  ：扫描目录 -> 汇总 -> 出图 + 出表的编排（组合根用的公开入口）。

命令行入口在上一层的 report_summary.py。
"""

from .report import CSV_NAME, IMAGE_NAME, SummaryOutputs, update_final_report

__all__ = ["update_final_report", "SummaryOutputs", "IMAGE_NAME", "CSV_NAME"]
