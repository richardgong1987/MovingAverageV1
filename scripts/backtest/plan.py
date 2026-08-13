"""把参数接口返回的每条记录变成一个回测任务 ConditionRow。

一条 record = 一次回测：symbol / period 决定跑哪个品种周期，parameterFields 里的每一项直接
翻译成一个 cTrader CLI 参数（见 parameters.ParameterField）。参数的增删由后端那张记录表决定，
脚本这边不再维护列名或白名单。

报告文件名就是 <recordId>-<symbol>-<period>：recordId 已经唯一，文件名不再承担「记录参数」的
职责，所以模板换一套参数（Dragon / pdhpdl 字段并不相同）也不用改这里。这条回测跑了什么参数由
command.write_conditions 把接口记录原样留档到报告目录，汇总层从那里读（见 summary/conditions.py）。
"""

from .parameters import ParameterField, to_cli_date
from .records import fetch_parameter_records

# 起止日期只用来在日志里显示回测区间；它们和其他参数一样原样传给 CLI，缺了也不拦。
FIELD_START_DATE = "start"
FIELD_END_DATE = "end"

# 交易 CSV 的落盘路径由 command.py 统一给（绝对路径，与 report-json 同目录、同名）；
# 接口里的 FileName 只是后台展示用的默认值，一起传下去会和它冲突，所以在这里丢掉。
IGNORED_FIELD_NAMES = {"FileName"}


class ConditionRow:
    """一条回测任务，来自参数接口的一条 record。"""

    def __init__(self, record):
        # 原始记录留着：批量开跑前会原样写进报告目录的 conditions.json，供汇总层还原参数。
        self.record = record
        self.record_id = _required_text(record.get("recordId"), "缺少 recordId。")
        self.symbol = _required_text(record.get("symbol"), "缺少 symbol。")
        self.period = _required_text(record.get("period"), "缺少 period。")
        self._fields = _read_fields(record)
        # 构造时就把命令行片段算出来：日期格式、非数字取值这类问题要在批量开跑之前报错，
        # 而不是跑到一半才炸。
        self._cli_args = [field.cli_arg() for field in self._fields.values() if not field.is_blank]
        self.start_date = self._date_text(FIELD_START_DATE)
        self.end_date = self._date_text(FIELD_END_DATE)

    @property
    def report_stem(self):
        """这条回测的产物文件名（不含扩展名）：CSV / 报告 JSON / conditions.json 的键都用它。"""
        return f"{self.record_id}-{self.symbol}-{self.period}"

    @property
    def file_name(self):
        """交易明细 CSV 的文件名。"""
        return f"{self.report_stem}.csv"

    @property
    def report_file_name(self):
        """回测报告文件名：与 CSV 同名，只把 .csv 换成 .json。"""
        return f"{self.report_stem}.json"

    def cli_args(self):
        """这条任务的 cBot 参数命令行片段，顺序与接口返回的字段顺序一致。"""
        return list(self._cli_args)

    def as_condition_entry(self):
        """留档进 conditions.json 的记录：接口原文（含 label / options，汇总层要靠它出列名），
        但去掉不参与回测的字段——否则汇总表里会出现一个 FileName=Dragon-trades.csv 的假文件名。
        """
        entry = dict(self.record)
        entry["parameterFields"] = [
            raw_field
            for raw_field in self.record.get("parameterFields") or []
            if str(raw_field.get("name") or "").strip() not in IGNORED_FIELD_NAMES
        ]
        return entry

    def _date_text(self, field_name):
        """日志里显示的回测区间端点；接口没给这个字段时返回空串，由调用方决定怎么显示。"""
        field = self._fields.get(field_name)
        if field is None or field.is_blank:
            return ""
        try:
            return to_cli_date(field.value)
        except ValueError:
            # 字段类型不是 date 时原样显示：这只是日志好不好看，不该拦住整批回测。
            return str(field.value)


def read_condition_rows(records_url):
    """拉取参数接口，返回回测任务列表（顺序与接口返回一致）。"""
    return [_to_condition_row(record) for record in fetch_parameter_records(records_url)]


def _to_condition_row(record):
    """接口数据有问题时补上 recordId 再抛出，让人一眼看出该改后端的哪条记录。"""
    try:
        return ConditionRow(record)
    except ValueError as error:
        raise ValueError(f"参数记录 recordId={record.get('recordId')}：{error}") from error


def _read_fields(record):
    fields = {}
    for raw_field in record.get("parameterFields") or []:
        field = ParameterField(raw_field)
        if field.name in IGNORED_FIELD_NAMES:
            continue
        fields[field.name] = field
    return fields


def _required_text(value, message):
    text = "" if value is None else str(value).strip()
    if not text:
        raise ValueError(message)
    return text
