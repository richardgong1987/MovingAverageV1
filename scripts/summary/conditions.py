"""读取批量回测留下的条件清单 conditions.json：报告文件名 -> 那条参数记录。

报告文件名现在只有 <recordId>-<品种>-<周期>，回测用了哪些参数不再能从文件名反解析出来，
所以批量开跑前会把接口返回的记录原样写进报告目录（见 backtest/command.py 的 write_conditions）。
汇总表和 metadata 的配置列都从这里取，参数的增删只由后端那张记录表决定，本模块不维护列名白名单。

清单缺失或对不上（例如拿一批旧报告手动重跑汇总）时按空处理：配置列留空，不中断导出。
"""

import json
from pathlib import Path

CONDITIONS_FILE_NAME = "conditions.json"


class Condition:
    """一条回测记录：报告文件名对应的 recordId / 品种 / 周期 / 全部参数。"""

    def __init__(self, entry):
        self.record_id = entry.get("recordId", "")
        self.symbol = entry.get("symbol", "")
        self.period = entry.get("period", "")
        self._fields = [field for field in entry.get("parameterFields") or [] if isinstance(field, dict)]

    def display_values(self):
        """给人看的 {列名: 值}：列名用接口给的 label（没有就用 name），枚举显示成员名。"""
        return {_column_name(field): _display_value(field) for field in self._fields}

    def raw_values(self):
        """给机器用的 {参数名: 接口原值}，不做任何格式化，入库后仍能还原这条回测。"""
        return {field.get("name", ""): field.get("value") for field in self._fields}


# 清单里没有这份报告时用它，让调用方不必到处判空。
UNKNOWN_CONDITION = Condition({})


def load_conditions(output_dir):
    """读报告目录里的 conditions.json，返回 {报告文件名(不含扩展名): Condition}；读不到就返回空字典。"""
    conditions_path = Path(output_dir) / CONDITIONS_FILE_NAME
    try:
        with open(conditions_path, encoding="utf-8") as conditions_file:
            entries = json.load(conditions_file)
    except (OSError, json.JSONDecodeError):
        return {}
    if not isinstance(entries, dict):
        return {}
    return {stem: Condition(entry) for stem, entry in entries.items() if isinstance(entry, dict)}


def _column_name(field):
    """参数在汇总表里的列名：优先用后台配的中文 label，没配就退回 C# 属性名。"""
    return field.get("label") or field.get("name") or ""


def _display_value(field):
    """参数值的展示写法：枚举显示成员名而不是数字，bool 显示是/否，2.0 显示成 2。

    这里只管好不好读，和 parameters.py 那套「CLI 认什么格式」是两回事，各自独立地变。
    """
    value = field.get("value")
    if value is None:
        return ""
    if isinstance(value, bool):
        return "是" if value else "否"
    if field.get("options"):
        return _option_name(field["options"], value)
    if isinstance(value, (int, float)):
        return str(int(value)) if float(value).is_integer() else str(value)
    return str(value)


def _option_name(options, value):
    """枚举值 -> 后端给的成员名（label 常常是空的，name 才是 Strong / Close 这类）。"""
    for option in options:
        if isinstance(option, dict) and option.get("value") == value:
            return option.get("name") or option.get("label") or str(value)
    return str(value)
