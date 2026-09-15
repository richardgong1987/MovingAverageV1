using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.FullAccess, AddIndicators = false)]
public class MovingAverageV1 : Robot {
    [Parameter("订单标签", DefaultValue = "MovingAverageV1-label")]
    public string OrderLabel { get; set; }

    [Parameter("风险1%", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 15.0, Step = 0.1, Group = "风控配置")]
    public double RiskPct { get; set; }

    [Parameter("安全系数", DefaultValue = 0.9, MinValue = 0.1, MaxValue = 1.0, Step = 0.05, Group = "风控配置")]
    public double RiskSafetyFactor { get; set; }

    [Parameter("止损偏移点数", DefaultValue = 400, MinValue = 0, MaxValue = 2000, Group = "风控配置")]
    public int StopOffsetTicks { get; set; }

    [Parameter("最小止损点数 (Pips)", DefaultValue = 5.0, MinValue = 0.0, Step = 0.1, Group = "风控配置")]
    public double MinStopLossPips { get; set; }

    [Parameter("止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 30.0, Step = 0.1, Group = "风控配置")]
    public double TakeProfitR { get; set; }

    [Parameter("保护止损触发R (0=关闭)", DefaultValue = 1.0, MinValue = 0.0, MaxValue = 30.0, Step = 0.1, Group = "风控配置")]
    public double BreakevenTriggerR { get; set; }

    [Parameter("保护止损偏移点数", DefaultValue = 50, MinValue = 0, MaxValue = 2000, Group = "风控配置")]
    public int BreakevenOffsetTicks { get; set; }

    [Parameter("回撤开仓模式", DefaultValue = PdhpdlEntryModel.Pb50, Group = "风控配置")]
    public PdhpdlEntryModel EntryModel { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = true, Group = "开发调试")]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "MovingAverageV1-trades.csv", Group = "开发调试")]
    public string FileName { get; set; }

    // 三条线一律按颜色叫，和图上画出来的一一对应。周期都写死在 RmaLinesConfigModel 里：
    // 蓝、紫跑 60 分钟，黄跑 45 分钟；这里只放各自的均线周期。
    // 方向过滤看的是三线排列：多头 黄 > 蓝 > 紫，空头反过来。
    [Parameter("均线周期 蓝线 (60分钟)", DefaultValue = 13, MinValue = 1, Group = "均线")]
    public int RMABluePeriod { get; set; }

    [Parameter("均线周期 紫线 (60分钟)", DefaultValue = 55, MinValue = 1, Group = "均线")]
    public int RMAPurplePeriod { get; set; }

    [Parameter("均线周期 黄线 (45分钟)", DefaultValue = 13, MinValue = 1, Group = "均线")]
    public int RMAYellowPeriod { get; set; }

    // 开口扩大闸门：(现在的蓝紫开口 - N 根 15 分钟 K 线之前的开口) / 60分钟 ATR14 ≥ X 才开仓。
    // 蓝紫线跑在 60 分钟上，所以 N 会换算成 60 分钟的根数：4（1 小时前）→ 1 根，8（2 小时前）→ 2 根。
    [Parameter("开口回看N (15分钟K线)", DefaultValue = 12, MinValue = 4, Group = "均线")]
    public int GapExpansionLookbackBars { get; set; }

    [Parameter("开口扩大X (ATR倍数, 0=关闭)", DefaultValue = 0.10, MinValue = 0.0, Step = 0.05, Group = "均线")]
    public double GapExpansionX { get; set; }

    // ZigZag 突破窗口：越大结构点越少、确认越慢，结构点令牌闸门也就越紧（见 PivotEntryGate）。
    [Parameter("ZigZag 长度", DefaultValue = 16, MinValue = 1, Group = "市场结构")]
    public int ZigZagLength { get; set; }

    private RmaSeriesSet _rmaSeries;
    private RmaLines _movingAverageLines;

    private PdhpdlSignalDetector _signalDetector;
    private PdhpdlSignalMarkers _signalMarkers;
    private PdhpdlOrderExecutor _orderExecutor;

    private PdhpdlTradeCsvLogger _csvLogger;

    // 图表周期的 ATR，只服务 IsBigK（比较图表 K 线自身的振幅）。
    private Atr14Series _atr14;

    // 蓝紫线所在周期（60m）的 ATR，只服务开口扩大闸门（GapExpansion）。它和图表周期的 ATR 周期不同，不能共用一个实例。
    private Atr14Series _rmaSourceAtr14;
    private MarketStructure _marketStructure;

    // Optimisation only: GetFitness checks the whole run year by year, and GetFitnessArgs does not
    // carry the window, so the robot has to remember where it started.
    private DateTime _optimisationWindowStart;

    protected override void OnStart() {
        // A blank label would make every "_L"/"_S" label on the symbol look like this bot's order.
        if (string.IsNullOrWhiteSpace(OrderLabel)) {
            Print("*****OrderLabel must not be empty. cBot stopped.");
            Stop();
            return;
        }

        _optimisationWindowStart = Server.Time;
        LaunchDebug();
        DrawRmaLines();
        _marketStructure = new MarketStructure(Chart, Bars, ZigZagLength);
        _marketStructure.Update();
        _atr14 = new Atr14Series(Indicators, Bars);
        _rmaSourceAtr14 = new Atr14Series(Indicators, _rmaSeries.Blue.SourceBars);

        // 同一个闸门实例两边共用：detector 侧读它决定放不放行，executor 侧在仓位真的开出来时写它。
        var entryGate = new PivotEntryGate();
        _signalDetector = new PdhpdlSignalDetector(Bars, _rmaSeries, _rmaSourceAtr14, _marketStructure, entryGate,
            BuildGapExpansionConfig());
        _signalMarkers = new PdhpdlSignalMarkers(Chart, Symbol.TickSize);

        _csvLogger = new PdhpdlTradeCsvLogger(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName);
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        var riskGuard = new PdhpdlRiskGuard(BuildRiskGuardConfig());
        var symbolModel = new CAlgoSymbolModel(Symbol);
        var planner = new PdhpdlOrderPlanner(symbolModel, riskGuard, StopOffsetTicks, TakeProfitR, EntryModel, RiskPct);
        _orderExecutor = new PdhpdlOrderExecutor(this, SymbolName, Bars.TimeFrame.ToString(), OrderLabel.Trim(), planner, riskGuard,
            _csvLogger, symbolModel, entryGate, BreakevenTriggerR, BreakevenOffsetTicks);

        Print("*****MovingAverageV1 started.");
    }

    private void LaunchDebug() {
        if (IsDebug) {
            bool result = Debugger.Launch();
            if (!result) {
                Print("Debugger launch failed");
            }
        }
    }

    private RmaLinesConfigModel BuildMovingAverageConfig() {
        return new RmaLinesConfigModel {
            RMABluePeriod = RMABluePeriod, RMAPurplePeriod = RMAPurplePeriod, RMAYellowPeriod = RMAYellowPeriod
        };
    }

    private GapExpansionConfigModel BuildGapExpansionConfig() {
        return new GapExpansionConfigModel { LookbackBars = GapExpansionLookbackBars, MinExpansionAtr = GapExpansionX };
    }

    private void DrawRmaLines() {
        RmaLinesConfigModel rmaConfig = BuildMovingAverageConfig();
        _rmaSeries = new RmaSeriesSet(MarketData, Indicators, SymbolName, rmaConfig);
        _movingAverageLines = new RmaLines(Chart, Bars, _rmaSeries, rmaConfig.Thickness);
        _movingAverageLines.Draw();
    }

    // 输出目录按运行模式分开、互不覆盖：回测目录由脚本每次清空重建，模拟/实盘目录只追加、从不删除。
    // 回测经 run_conditions 传入绝对路径 FileName，此目录会被忽略（见 PdhpdlTradeCsvLogger）。
    private string ResolveReportsDirectory() {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, ResolveReportsFolderName());
    }

    private string ResolveReportsFolderName() {
        if (IsBacktesting)
            return "trading_reports";

        return Account.IsLive ? "release_trading_reports" : "simulate_trading_reports";
    }

    private PdhpdlRiskGuardConfigModel BuildRiskGuardConfig() {
        return new PdhpdlRiskGuardConfigModel { RiskSafetyFactor = RiskSafetyFactor, MinStopLossPips = MinStopLossPips };
    }

    protected override void OnBar() {
        _movingAverageLines?.Draw();
        _marketStructure?.Update();
        _orderExecutor?.ManageOpenPositions();
        // 先撤过期挂单再看新信号：让作废的挂单不再占住「本品种已有挂单」这个名额。
        _orderExecutor?.CancelExpiredPendingOrders(Bars.Count - 2);
        HandleClosedBarSignal();
    }

    protected override void OnTick() {
        _orderExecutor?.ManageOpenPositions();
    }

    private void HandleClosedBarSignal() {
        PdhpdlSignalModel signalModel = _signalDetector.DetectOnClosedBar();
        if (!signalModel.HasData)
            return;

        if (signalModel.IsLongSignal) {
            Print("*****LONG trigger | Time: {0}, Signal: {1}, Low: {2}, Close: {3}", signalModel.BarTime, signalModel.Label,
                signalModel.Low, signalModel.Close);
        }

        if (signalModel.IsShortSignal) {
            Print("*****SHORT trigger | Time: {0}, Signal: {1}, High: {2}, Close: {3}", signalModel.BarTime, signalModel.Label,
                signalModel.High, signalModel.Close);
        }

        signalModel.IsBigK = _atr14.IsBarRangeTooLarge(signalModel.BarIndex, signalModel.High, signalModel.Low, 3);
        if (_orderExecutor.ExecuteIfSignal(signalModel)) {
            _signalMarkers.Draw(signalModel);
        }
    }

    // Called once per pass by the desktop Optimisation tab only — a plain backtest, CLI or GUI,
    // never calls it. Passes with a losing (or idle) calendar year sink below every survivor;
    // survivors keep cTrader's own score. See AnnualFitness.
    protected override double GetFitness(GetFitnessArgs args) {
        List<ClosedTradeModel> closedTrades = args.History
            .Select(trade => new ClosedTradeModel(trade.ClosingTime, trade.NetProfit))
            .ToList();

        var stats = new FitnessStatsModel {
            NetProfit = args.NetProfit,
            WinningTrades = args.WinningTrades,
            MaxEquityDrawdownPercent = args.MaxEquityDrawdownPercentages
        };

        return new AnnualFitness(_optimisationWindowStart, Server.Time).Calculate(closedTrades, stats);
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
    }
}
