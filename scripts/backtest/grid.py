"""把一份参数网格说明书展开成一批回测任务（本地版的「优化」）。

cTrader CLI 没有优化子命令 —— 它只有 accounts / symbols / run / backtest 四个动词，优化是桌面端
GUI 独有的功能。所以这里的「优化」就是：本地把参数组合枚举出来，每个组合跑一条 backtest，
最后按指标排名（见 summary/ranking.py）。

展开出来的任务复用 plan.ConditionRow，与接口下发的批量回测走同一条命令行/报告/汇总链路，
下游（command / runner / summary）不需要知道任务是接口来的还是网格来的。

网格说明书（JSON）：

    {
      "symbol": "XAUUSD",
      "period": "m15",
      "start": "2025-01-01",
      "end":   "2025-12-31",
      "parameters": {
        "RMABluePeriod":   {"type": "int",    "values": [11, 13, 15]},
        "RMAPurplePeriod": {"type": "int",    "from": 45, "to": 65, "step": 10},
        "GapExpansionX":   {"type": "double", "values": [0.05, 0.10, 0.20]},
        "TakeProfitR":     {"type": "double", "value": 2.0}
      }
    }

parameters 里每一项的 name 就是 cBot 的 C# 属性名，type 的取值与接口那条链路一致
（double / int / bool / date / 其余按字符串原样传，见 parameters.py）。取值三选一：

    value          固定值，每条回测都传同一个，不参与组合
    values         显式取值列表
    from/to/step   等差取值列表（闭区间，step 省略为 1）
"""

import itertools
import json
import random
from decimal import Decimal

from .plan import ConditionRow

# 说明书里除 parameters 外的必填项：前两个是 CLI 自己的选项，后两个会变成 --start / --end。
REQUIRED_SPEC_KEYS = ("symbol", "period", "start", "end")

# 组合数超过这个值时提醒一句：一条 m1 回测按分钟级也要几十秒，上千条就是过夜的量级。
LARGE_GRID_WARNING = 200


class GridSpec:
    """一份参数网格说明书。"""

    def __init__(self, spec):
        if not isinstance(spec, dict):
            raise ValueError("网格说明书的顶层必须是一个 JSON 对象。")

        missing = [key for key in REQUIRED_SPEC_KEYS if not str(spec.get(key) or "").strip()]
        if missing:
            raise ValueError("网格说明书缺少必填项：" + "、".join(missing))

        self.symbol = str(spec["symbol"]).strip()
        self.period = str(spec["period"]).strip()
        self.start = str(spec["start"]).strip()
        self.end = str(spec["end"]).strip()

        parameters = spec.get("parameters")
        if not isinstance(parameters, dict) or not parameters:
            raise ValueError("网格说明书的 parameters 必须是一个非空 JSON 对象。")

        # 固定值和扫描值分开存：扫描的那几个才进组合、才成为排名表的列，
        # 固定的那些每条回测都原样传一份。
        self._fixed = {}
        self._swept = {}
        for name, field in parameters.items():
            parameter_type, values = _read_parameter(name, field)
            if len(values) == 1:
                self._fixed[name] = (parameter_type, values[0])
            else:
                self._swept[name] = (parameter_type, values)

        if not self._swept:
            raise ValueError("网格说明书里没有任何需要扫描的参数（每一项都只给了一个取值）。")

    @property
    def swept_names(self):
        """参与组合的参数名，顺序与说明书里一致 —— 排名表的列顺序也用它。"""
        return list(self._swept)

    @property
    def total_combinations(self):
        count = 1
        for _, values in self._swept.values():
            count *= len(values)
        return count

    def combinations(self):
        """按说明书顺序展开所有组合，每个组合是 {参数名: 取值}。"""
        value_lists = [values for _, values in self._swept.values()]
        return [dict(zip(self.swept_names, combination)) for combination in itertools.product(*value_lists)]

    def parameter_type(self, name):
        if name in self._swept:
            return self._swept[name][0]
        return self._fixed[name][0]

    def fixed_fields(self):
        """每条回测都要带上的固定参数字段。"""
        return [
            {"name": name, "type": parameter_type, "value": value}
            for name, (parameter_type, value) in self._fixed.items()
        ]


class GridConditionRow(ConditionRow):
    """网格展开出来的一条回测任务。除了 ConditionRow 该有的，还记着它是哪个组合。

    排名表要按参数列展开每一条，所以这份组合值必须跟着任务走 —— 从命令行片段反解是自找麻烦。
    """

    def __init__(self, record, swept_values):
        super().__init__(record)
        self.swept_values = dict(swept_values)


def load_grid(spec_path):
    """读网格说明书 JSON，返回 GridSpec。"""
    try:
        with open(spec_path, encoding="utf-8") as spec_file:
            spec = json.load(spec_file)
    except FileNotFoundError:
        raise FileNotFoundError(
            f"找不到网格说明书：{spec_path}\n"
            "可以从 scripts/optimisation.example.json 复制一份再改。"
        ) from None
    except json.JSONDecodeError as error:
        raise ValueError(f"网格说明书不是合法的 JSON：{spec_path}（{error}）") from None
    return GridSpec(spec)


def build_tasks(spec, max_passes=None, seed=0):
    """把网格展开成回测任务列表。

    组合数超过 max_passes 时随机抽样到这个数量：全网格常常是几千条、跑不完，随机子集比
    「按顺序砍掉后一半」有代表性得多（后者会把某个参数的大取值整段丢掉）。抽样用固定
    种子，同一份说明书 + 同一个上限每次抽到的是同一批，便于复跑对照。
    """
    combinations = spec.combinations()

    if max_passes is not None and 0 < max_passes < len(combinations):
        combinations = random.Random(seed).sample(combinations, max_passes)

    return [
        GridConditionRow(_build_record(pass_number, spec, combination), combination)
        for pass_number, combination in enumerate(combinations, start=1)
    ]


def _build_record(pass_number, spec, combination):
    """一个组合 -> 一条参数记录，形状与后端参数接口返回的记录一致（plan.ConditionRow 读它）。

    recordId 用补零的流水号：它同时是报告文件名的前缀（p0007-XAUUSD-m15），补零后按文件名
    排序就等于按 pass 顺序排。
    """
    fields = [
        {"name": "start", "type": "date", "value": spec.start},
        {"name": "end", "type": "date", "value": spec.end},
    ]
    fields.extend(spec.fixed_fields())
    fields.extend(
        {"name": name, "type": spec.parameter_type(name), "value": value}
        for name, value in combination.items()
    )
    return {
        "recordId": f"p{pass_number:04d}",
        "symbol": spec.symbol,
        "period": spec.period,
        "parameterFields": fields,
    }


def _read_parameter(name, field):
    """一项参数说明 -> (type, 取值列表)。取值只有一个时它就是固定参数。"""
    if not isinstance(field, dict):
        raise ValueError(f"参数「{name}」的说明必须是一个 JSON 对象。")

    parameter_type = str(field.get("type") or "").strip()

    if "values" in field:
        values = field["values"]
        if not isinstance(values, list) or not values:
            raise ValueError(f"参数「{name}」的 values 必须是非空数组。")
        return parameter_type or _infer_type(values[0]), list(values)

    if "from" in field or "to" in field:
        values = _stepped_values(name, field)
        return parameter_type or _infer_type(values[0]), values

    if "value" in field:
        return parameter_type or _infer_type(field["value"]), [field["value"]]

    raise ValueError(f"参数「{name}」要给 value、values 或 from/to/step 三者之一。")


def _stepped_values(name, field):
    """from/to/step 展开成闭区间上的等差取值。

    用 Decimal 而不是 float 累加：0.05 步长用 float 走三步就会变成 0.15000000000000002，
    传给 CLI 的数字和说明书里写的对不上，排名表也没法按取值分组。
    """
    try:
        start = Decimal(str(field["from"]))
        stop = Decimal(str(field["to"]))
        step = Decimal(str(field.get("step", 1)))
    except (KeyError, ArithmeticError, ValueError):
        raise ValueError(f"参数「{name}」的 from/to/step 必须都是数字。") from None

    if step <= 0:
        raise ValueError(f"参数「{name}」的 step 必须大于 0。")
    if stop < start:
        raise ValueError(f"参数「{name}」的 to 不能小于 from。")

    values = []
    current = start
    while current <= stop:
        values.append(int(current) if current == current.to_integral_value() else float(current))
        current += step
    return values


def _infer_type(value):
    """说明书没写 type 时按取值猜：bool -> bool，整数 -> int，小数 -> double，其余当字符串。"""
    if isinstance(value, bool):
        return "bool"
    if isinstance(value, int):
        return "int"
    if isinstance(value, float):
        return "double"
    return ""
