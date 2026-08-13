namespace cAlgo.Robots;

// 连续同向入场的闸门。
//
// 换方向后的第一笔不受限制。同方向再开一笔，必须是「上一笔同向入场之后」MarketStructure
// 又新出现了一个结构点：作空要 LL，作多要 HH。一个 LL/HH 只放行一笔，所以第 3 笔、第 4 笔
// 各自还要再等一个新的——不是「历史上出现过就一直放行」。
//
// 判断方式是比较 MarketStructure 的累计计数：只增不减，比上一笔入场时记下的值大，就说明
// 这中间确实又出了一个。
//
// 计数只认真正开出来的仓位。信号被风控或「本品种已有持仓」闸门拦掉的不算——默认
// StrategyModel.All 下有持仓时信号会被直接跳过，按信号计数会被这些没成交的信号刷高，
// 反过来把真正该做的那一笔挡在门外。
public class ConsecutiveEntryGate {
    private PdhpdlTradeDirectionModel? _lastDirection;
    private int _structureCountAtLastEntry;

    // 当前方向上已经连续开了几笔。不参与放行判断，只用于日志。
    public int ConsecutiveCount { get; private set; }

    public bool IsAllowed(PdhpdlTradeDirectionModel direction, int lowerLowCount, int higherHighCount) {
        if (_lastDirection != direction)
            return true;

        return StructureCount(direction, lowerLowCount, higherHighCount) > _structureCountAtLastEntry;
    }

    public void RecordEntry(PdhpdlTradeDirectionModel direction, int lowerLowCount, int higherHighCount) {
        ConsecutiveCount = _lastDirection == direction ? ConsecutiveCount + 1 : 1;
        _lastDirection = direction;
        _structureCountAtLastEntry = StructureCount(direction, lowerLowCount, higherHighCount);
    }

    private static int StructureCount(PdhpdlTradeDirectionModel direction, int lowerLowCount, int higherHighCount) {
        return direction == PdhpdlTradeDirectionModel.Short ? lowerLowCount : higherHighCount;
    }
}
