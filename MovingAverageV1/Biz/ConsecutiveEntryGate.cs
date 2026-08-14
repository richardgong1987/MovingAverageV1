namespace cAlgo.Robots;

// 连续同向入场的闸门。
//
// 换方向后的第一笔不受限制。同方向再开一笔，要同时满足两件事：
//   1. MarketStructure 最后标出的结构点站在自己这一边 —— 作空要红色标记（LL 或 LH），
//      作多要绿色标记（HH 或 HL）；
//   2. 这个结构点是上一笔同向入场之后才出现的。
//
// 第 2 条让结构点变成一次性的令牌：一个 LL 放行一笔作空，用掉就作废，第 3 笔要再等一个新的
// 红色标记。只看颜色是不够的 —— 颜色不变的那段时间里每个信号都会被放行，等于没有闸门。
// 判断方式是比较 MarketStructure 的 PivotCount：它只增不减，比上一笔入场时记下的值大，
// 就说明这中间确实又新确认了一个结构点。
//
// 计数只认真正开出来的仓位。信号被风控或「本品种已有持仓」闸门拦掉的不算——默认
// StrategyModel.All 下有持仓时信号会被直接跳过，按信号计数会把令牌白白用掉。
public class ConsecutiveEntryGate {
    private PdhpdlTradeDirectionModel? _lastDirection;
    private int _pivotCountAtLastEntry;

    // 当前方向上已经连续开了几笔。不参与放行判断，只用于日志。
    public int ConsecutiveCount { get; private set; }

    public bool IsAllowed(PdhpdlTradeDirectionModel direction, MarketStructurePivotModel latestPivot, int pivotCount) {
        if (_lastDirection != direction)
            return true;

        if (!IsPivotAligned(direction, latestPivot))
            return false;

        return pivotCount > _pivotCountAtLastEntry;
    }

    public void RecordEntry(PdhpdlTradeDirectionModel direction, int pivotCount) {
        ConsecutiveCount = _lastDirection == direction ? ConsecutiveCount + 1 : 1;
        _lastDirection = direction;
        _pivotCountAtLastEntry = pivotCount;
    }

    private static bool IsPivotAligned(PdhpdlTradeDirectionModel direction, MarketStructurePivotModel latestPivot) {
        if (direction == PdhpdlTradeDirectionModel.Short) {
            return latestPivot == MarketStructurePivotModel.LowerLow || latestPivot == MarketStructurePivotModel.LowerHigh;
        }

        return latestPivot == MarketStructurePivotModel.HigherHigh || latestPivot == MarketStructurePivotModel.HigherLow;
    }
}
