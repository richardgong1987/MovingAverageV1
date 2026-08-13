using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// Places and tracks the strategy's cTrader orders. It gates on the risk guard and open
// exposure, asks PdhpdlOrderPlanner to size the order, submits it, and keeps the CSV
// row ids so opens and closes can be reconciled. All sizing math lives in the planner.
public class PdhpdlOrderExecutor {
    private const string StrategyLabelPrefix = PdhpdlOrderPlanner.LabelPrefix + "_";
    private const string EntryComment = "ENTRY";

    // 挂单最多等 3 根收盘 K 线；等不到回撤就撤单。
    private const int PendingOrderExpiryBars = 3;

    private readonly Robot _robot;
    private readonly string _symbolName;
    private readonly string _timeFrame;

    private readonly PdhpdlOrderPlanner _planner;
    private readonly PdhpdlRiskGuard _riskGuard;
    private readonly PdhpdlTradeCsvLogger _csvLogger;
    private readonly IPdhpdlSymbolModel _symbolModel;
    private readonly ConsecutiveEntryGate _entryGate;

    // 浮盈达到这么多个 R 就把止损推到保本位。0 表示关闭，止损全程留在开仓时的位置。
    private readonly double _breakevenTriggerR;

    // Once protection triggers, the stop moves this many ticks past the entry price, in the
    // profitable direction. Same tick unit as PdhpdlOrderPlanner's stop offset.
    private readonly int _breakevenOffsetTicks;

    private readonly Dictionary<string, string> _pendingCsvIdsByLabel = new();
    private readonly Dictionary<string, double> _pendingEntryEquitiesByLabel = new();
    private readonly Dictionary<string, double> _pendingRiskPricesByLabel = new();
    private readonly Dictionary<int, int> _pendingOrderBarIndexById = new();
    // 下单时先按 label 记下方向和当时的 LL/HH 计数，等仓位真的开出来（OnPositionOpened）再交给
    // 闸门。挂单没成交就撤掉的那些，永远不会走到记账这一步。
    private readonly Dictionary<string, EntryGateSnapshot> _pendingGateSnapshotsByLabel = new();

    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, double> _positionEntryEquities = new();

    // 开仓时的 R（入场价到初始止损的距离）。止损被推到保护位之后就没法从 position.StopLoss
    // 反推 R 了，所以开仓时存下来。
    private readonly Dictionary<int, double> _positionRiskPrices = new();

    // 已经推过保护止损的持仓。券商拒单或止损落在市价另一侧时也算处理过，避免每个 tick 重试。
    private readonly HashSet<int> _positionsProtected = new();

    public PdhpdlOrderExecutor(Robot robot, string symbolName, string timeFrame, PdhpdlOrderPlanner planner, PdhpdlRiskGuard riskGuard,
        PdhpdlTradeCsvLogger csvLogger, IPdhpdlSymbolModel symbolModel, ConsecutiveEntryGate entryGate, double breakevenTriggerR,
        int breakevenOffsetTicks) {
        _robot = robot;
        _symbolName = symbolName;
        _timeFrame = timeFrame;
        _planner = planner;
        _riskGuard = riskGuard;
        _csvLogger = csvLogger;
        _symbolModel = symbolModel;
        _entryGate = entryGate;
        _breakevenTriggerR = breakevenTriggerR;
        _breakevenOffsetTicks = breakevenOffsetTicks;

        if (_riskGuard.NewsBlackoutWindowCount > 0)
            _robot.Print("*****News blackout windows loaded. Count: {0}", _riskGuard.NewsBlackoutWindowCount);

        _robot.Positions.Closed += OnPositionClosed;
        _robot.Positions.Opened += OnPositionOpened;
    }

    public void Stop() {
        _robot.Positions.Closed -= OnPositionClosed;
        _robot.Positions.Opened -= OnPositionOpened;
    }

    public void ManageOpenPositions() {
        CloseExposureBeforeRiskWindow();
        ApplyBreakevenProtection();
    }

    // 止盈是开仓时定死的 TakeProfitR×R，已经挂在订单上由券商执行，这里不需要盯。
    // 持仓期间唯一要做的是浮盈达到 BreakevenTriggerR 时把止损推到保本位。
    private void ApplyBreakevenProtection() {
        // 0 = 关闭。必须在这里挡掉：触发距离为 0 会让保护在开仓瞬间就「触发」，然后因为保本价
        // 落在市价另一侧而被跳过，机会白白消耗掉——看着像没保护，实则是行情决定的哑火。
        if (_breakevenTriggerR <= 0.0)
            return;

        foreach (Position position in _robot.Positions.Where(IsStrategyPosition).ToArray())
            ApplyBreakevenProtection(position);
    }

    private void ApplyBreakevenProtection(Position position) {
        if (_positionsProtected.Contains(position.Id))
            return;

        if (!_positionRiskPrices.TryGetValue(position.Id, out double riskPrice) || riskPrice <= 0.0)
            return;

        bool isLong = position.TradeType == TradeType.Buy;
        double profitDistance = _breakevenTriggerR * riskPrice;
        double trigger = isLong ? position.EntryPrice + profitDistance : position.EntryPrice - profitDistance;

        if (!HasReached(isLong, position.CurrentPrice, trigger))
            return;

        MoveStopToProtection(position, isLong);
    }

    private static bool HasReached(bool isLong, double price, double targetPrice) {
        return isLong ? price >= targetPrice : price <= targetPrice;
    }

    // The position must not turn back into a loss, so the stop moves to the entry price plus a
    // small offset in the profitable direction.
    private void MoveStopToProtection(Position position, bool isLong) {
        // 先记账再动手：券商拒单时也不要每个 tick 重试一次，日志里会留下失败原因。
        if (!_positionsProtected.Add(position.Id))
            return;

        double offset = _symbolModel.TickSize * _breakevenOffsetTicks;
        double protectiveStop = isLong ? position.EntryPrice + offset : position.EntryPrice - offset;

        if (!IsStopImprovement(position, isLong, protectiveStop))
            return;

        // 止损不能落在市价的另一侧：券商会拒单，或者直接把剩下的仓位按市价平掉。
        bool stopIsPastMarket = isLong ? protectiveStop >= position.CurrentPrice : protectiveStop <= position.CurrentPrice;

        if (stopIsPastMarket) {
            _robot.Print("*****Protective stop skipped | Position: {0}, Stop: {1}, Price: {2}", position.Id, protectiveStop,
                position.CurrentPrice);
            return;
        }

        TradeResult result = _robot.ModifyPosition(position, protectiveStop, position.TakeProfit, ProtectionType.Absolute);

        if (!result.IsSuccessful) {
            _robot.Print("*****Protective stop failed | Position: {0}, Stop: {1}, Error: {2}", position.Id, protectiveStop, result.Error);
            return;
        }

        _robot.Print("*****Protective stop set | Position: {0}, Entry: {1}, Stop: {2}", position.Id, position.EntryPrice, protectiveStop);
    }

    private static bool IsStopImprovement(Position position, bool isLong, double protectiveStop) {
        if (!position.StopLoss.HasValue)
            return true;

        return isLong ? protectiveStop > position.StopLoss.Value : protectiveStop < position.StopLoss.Value;
    }

    public bool ExecuteIfSignal(PdhpdlSignalModel signalModel) {
        if (signalModel == null || !signalModel.HasData)
            return false;

        if (!signalModel.IsLongSignal && !signalModel.IsShortSignal)
            return false;

        if (_riskGuard.ShouldBlockNewOrder(_robot.Server.Time)) {
            _robot.Print("*****Order skipped | Risk guard blocked new order. Time: {0}", _robot.Server.Time);
            return false;
        }

        if (signalModel.Strategy != StrategyModel.MultiplePosition && (HasOpenSymbolPosition() || HasOpenSymbolPendingOrder())) {
            _robot.Print("*****Order skipped | Existing position found on symbol: {0}", _symbolName);
            return false;
        }


        PdhpdlOrderPlanModel planModel = _planner.CreatePlan(signalModel, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Reason: {0}", planModel.RejectReason);
            return false;
        }

        planModel.SignalName = signalModel.Label;
        planModel.KeyLevel = signalModel.KeyLevel;
        planModel.SignalBarIndex = signalModel.BarIndex;

        // 快照必须在下单之前放好：市价单的 Positions.Opened 可能在 SubmitOrder 里就回调了。
        _pendingGateSnapshotsByLabel[planModel.Label] =
            new EntryGateSnapshot(planModel.DirectionModel, signalModel.LowerLowCount, signalModel.HigherHighCount);

        if (ExecutePlan(planModel))
            return true;

        _pendingGateSnapshotsByLabel.Remove(planModel.Label);
        return false;
    }

    private bool HasOpenSymbolPosition() {
        return _robot.Positions.Any(position => position.SymbolName == _symbolName);
    }

    private bool HasOpenSymbolPendingOrder() {
        return _robot.PendingOrders.Any(order => order.SymbolName == _symbolName);
    }

    // 大 K 线的挂单是「等价格回撤到中点」，回撤没来就说明这笔已经作废：只给它 PendingOrderExpiryBars
    // 根收盘 K 线的时间，超时撤单，避免行情早已走远后挂单还在原地等着被扫。
    public void CancelExpiredPendingOrders(int closedBarIndex) {
        ForgetFilledPendingOrders();

        foreach (PendingOrder order in _robot.PendingOrders.Where(IsStrategyPendingOrder).ToArray()) {
            if (!IsPendingOrderExpired(order, closedBarIndex))
                continue;

            CancelPendingOrder(order, $"unfilled after {PendingOrderExpiryBars} bars");
        }
    }

    private bool IsPendingOrderExpired(PendingOrder order, int closedBarIndex) {
        // 本次运行之前就存在的挂单没有下单 K 线记录，不归这条规则管。
        if (!_pendingOrderBarIndexById.TryGetValue(order.Id, out int placedBarIndex))
            return false;

        return closedBarIndex - placedBarIndex >= PendingOrderExpiryBars;
    }

    private void ForgetFilledPendingOrders() {
        HashSet<int> liveOrderIds = new(_robot.PendingOrders.Select(order => order.Id));

        foreach (int orderId in _pendingOrderBarIndexById.Keys.Where(id => !liveOrderIds.Contains(id)).ToArray())
            _pendingOrderBarIndexById.Remove(orderId);
    }

    private void CancelPendingOrder(PendingOrder order, string reason) {
        TradeResult result = _robot.CancelPendingOrder(order);

        if (!result.IsSuccessful) {
            _robot.Print("*****Pending cancel failed | Order: {0}, Reason: {1}, Error: {2}", order.Id, reason, result.Error);
            return;
        }

        ForgetCancelledPendingOrder(order);
        _robot.Print("*****Pending order cancelled | Order: {0}, Reason: {1}", order.Id, reason);
    }

    // 撤单后必须把这笔挂单的 CSV 行号/权益一起丢掉，否则同 label 的下一笔持仓会认领到它的旧记录。
    private void ForgetCancelledPendingOrder(PendingOrder order) {
        _pendingOrderBarIndexById.Remove(order.Id);

        if (_robot.PendingOrders.Any(other => other.Id != order.Id && other.Label == order.Label))
            return;

        _pendingCsvIdsByLabel.Remove(order.Label);
        _pendingEntryEquitiesByLabel.Remove(order.Label);
        _pendingRiskPricesByLabel.Remove(order.Label);
        _pendingGateSnapshotsByLabel.Remove(order.Label);
    }

    private void CloseExposureBeforeRiskWindow() {
        if (!_riskGuard.ShouldForceClose(_robot.Server.Time))
            return;

        foreach (PendingOrder order in _robot.PendingOrders.Where(IsStrategyPendingOrder).ToArray())
            CancelPendingOrder(order, "risk guard force close");

        foreach (Position position in _robot.Positions.Where(IsStrategyPosition).ToArray()) {
            TradeResult result = _robot.ClosePosition(position);

            if (!result.IsSuccessful)
                _robot.Print("*****Risk guard close failed | Position: {0}, Error: {1}", position.Id, result.Error);
        }
    }

    private bool ExecutePlan(PdhpdlOrderPlanModel planModel) {
        _robot.Print(
            "*****Order plan | Side: {0}, EntryMode: {1}, Entry: {2}, Stop: {3}, TakeProfit: {4}, RiskPrice: {5}, StopLossPips: {6}, RiskMoney: {7}, EstimatedRiskMoney: {8}, Lots: {9}, VolumeUnits: {10}",
            planModel.DirectionModel, planModel.EntryModel, planModel.EntryPrice, planModel.StopPrice, planModel.TakeProfitPrice,
            planModel.RiskPrice, planModel.StopLossPips, planModel.RiskMoney, planModel.EstimatedRiskMoney, planModel.Lots,
            planModel.VolumeInUnits);

        TradeResult result = SubmitOrder(planModel);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return false;
        }

        _robot.Print("*****Order submitted | Label: {0}", planModel.Label);

        if (planModel.IsMarketOrder) {
            return RecordMarketEntry(planModel, result.Position);
        }

        return RecordPendingEntry(planModel, result.PendingOrder);
    }

    private TradeResult SubmitOrder(PdhpdlOrderPlanModel planModel) {
        TradeType tradeType = ToTradeType(planModel.DirectionModel);

        if (planModel.IsMarketOrder) {
            return _robot.ExecuteMarketOrder(tradeType, _symbolName, planModel.VolumeInUnits, planModel.Label, planModel.StopLossPips,
                planModel.TakeProfitPips, EntryComment);
        }

        return _robot.PlaceLimitOrder(tradeType, _symbolName, planModel.VolumeInUnits, planModel.EntryPrice, planModel.Label,
            planModel.StopLossPips, planModel.TakeProfitPips, ProtectionType.Relative, null, EntryComment);
    }

    private bool RecordMarketEntry(PdhpdlOrderPlanModel planModel, Position position) {
        string csvId = _csvLogger.AppendEntry(planModel, position, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return false;

        _positionCsvIds[position.Id] = csvId;
        _positionEntryEquities[position.Id] = planModel.AccountEquity;
        _positionRiskPrices[position.Id] = planModel.RiskPrice;
        _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
        return true;
    }

    private bool RecordPendingEntry(PdhpdlOrderPlanModel planModel, PendingOrder order) {
        string csvId = _csvLogger.AppendPendingEntry(planModel, order, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return false;

        _pendingCsvIdsByLabel[order.Label] = csvId;
        _pendingEntryEquitiesByLabel[order.Label] = planModel.AccountEquity;
        _pendingRiskPricesByLabel[order.Label] = planModel.RiskPrice;
        _pendingOrderBarIndexById[order.Id] = planModel.SignalBarIndex;
        _robot.Print("*****CSV pending order record added. Id: {0}, Path: {1}", csvId, _csvLogger.FilePath);
        return true;
    }

    private void OnPositionOpened(PositionOpenedEventArgs args) {
        if (args?.Position == null)
            return;

        if (_pendingCsvIdsByLabel.TryGetValue(args.Position.Label, out string csvId)) {
            _positionCsvIds[args.Position.Id] = csvId;
            _pendingCsvIdsByLabel.Remove(args.Position.Label);
        }

        if (_pendingEntryEquitiesByLabel.TryGetValue(args.Position.Label, out double entryEquity)) {
            _positionEntryEquities[args.Position.Id] = entryEquity;
            _pendingEntryEquitiesByLabel.Remove(args.Position.Label);
        }

        if (_pendingRiskPricesByLabel.TryGetValue(args.Position.Label, out double riskPrice)) {
            _positionRiskPrices[args.Position.Id] = riskPrice;
            _pendingRiskPricesByLabel.Remove(args.Position.Label);
        }

        RecordEntryForGate(args.Position.Label);
    }

    // 仓位真正开出来才算一笔同向入场。用的是下单那一刻的 LL/HH 计数，也就是闸门放行时比对过的
    // 那个基准，这样「一个 LL 放行一笔」才对得上。
    private void RecordEntryForGate(string label) {
        if (string.IsNullOrWhiteSpace(label) || !_pendingGateSnapshotsByLabel.TryGetValue(label, out EntryGateSnapshot snapshot))
            return;

        _pendingGateSnapshotsByLabel.Remove(label);
        _entryGate.RecordEntry(snapshot.Direction, snapshot.LowerLowCount, snapshot.HigherHighCount);
        _robot.Print("*****Entry recorded | Side: {0}, ConsecutiveCount: {1}, LL: {2}, HH: {3}", snapshot.Direction,
            _entryGate.ConsecutiveCount, snapshot.LowerLowCount, snapshot.HigherHighCount);
    }

    private readonly struct EntryGateSnapshot {
        public EntryGateSnapshot(PdhpdlTradeDirectionModel direction, int lowerLowCount, int higherHighCount) {
            Direction = direction;
            LowerLowCount = lowerLowCount;
            HigherHighCount = higherHighCount;
        }

        public PdhpdlTradeDirectionModel Direction { get; }
        public int LowerLowCount { get; }
        public int HigherHighCount { get; }
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args?.Position == null || !IsStrategyPosition(args.Position))
            return;

        string csvId = GetPositionCsvId(args.Position);
        double entryEquity = GetPositionEntryEquity(args.Position);
        double closePrice = GetClosePrice(args.Position);
        string closeRecordId = _csvLogger.AppendClose(args.Position, args.Reason, csvId, _symbolName, _timeFrame, _robot.Server.Time,
            closePrice, entryEquity, _robot.Account.Equity);

        _positionCsvIds.Remove(args.Position.Id);
        _positionEntryEquities.Remove(args.Position.Id);
        _positionRiskPrices.Remove(args.Position.Id);
        _positionsProtected.Remove(args.Position.Id);

        if (!string.IsNullOrWhiteSpace(closeRecordId))
            _robot.Print("*****CSV close record added. Id: {0}, ProfitLoss: {1}", closeRecordId, args.Position.NetProfit);
    }

    private bool IsStrategyPosition(Position position) {
        return position.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(position.Label) &&
               position.Label.StartsWith(StrategyLabelPrefix);
    }

    private bool IsStrategyPendingOrder(PendingOrder order) {
        return order.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(order.Label) && order.Label.StartsWith(StrategyLabelPrefix);
    }

    private string GetPositionCsvId(Position position) {
        return _positionCsvIds.TryGetValue(position.Id, out string csvId) ? csvId : position.Id.ToString();
    }

    private double GetPositionEntryEquity(Position position) {
        return _positionEntryEquities.TryGetValue(position.Id, out double entryEquity) ? entryEquity : 0.0;
    }

    // 这笔持仓最近一次成交的平仓记录，实际成交价和盈亏从这里取。
    private HistoricalTrade GetLastHistoricalTrade(int positionId) {
        HistoricalTrade[] closedTrades = _robot.History.FindByPositionId(positionId);

        if (closedTrades == null || closedTrades.Length == 0)
            return null;

        return closedTrades.OrderByDescending(trade => trade.ClosingTime).First();
    }

    private double GetClosePrice(Position position) {
        HistoricalTrade lastTrade = GetLastHistoricalTrade(position.Id);

        if (lastTrade != null)
            return lastTrade.ClosingPrice;

        for (int i = position.Deals.Count - 1; i >= 0; i--) {
            Deal deal = position.Deals[i];

            if (deal.PositionImpact == DealPositionImpact.Closing && deal.ExecutionPrice.HasValue)
                return deal.ExecutionPrice.Value;
        }

        return 0.0;
    }

    private static TradeType ToTradeType(PdhpdlTradeDirectionModel directionModel) {
        return directionModel == PdhpdlTradeDirectionModel.Long ? TradeType.Buy : TradeType.Sell;
    }
}
