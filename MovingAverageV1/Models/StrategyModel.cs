namespace cAlgo.Robots;

// 「策略模式」：目前只决定同一品种上能不能同时存在多笔仓位。
// 由 PdhpdlOrderExecutor.ExecuteIfSignal 的持仓闸门读取。
public enum StrategyModel {
    All, // 已有持仓或挂单时不再开新单
    MultiplePosition, // 持仓情况下，照常能下单,
    Strong, // 强多头：K线收盘价格>RMA13>RMA55 | 强空头：K线收盘价格<RMA13<RMA55
    Weak, // 弱多头：RMA13>K线收盘价格>RMA55   | 弱空头：RMA13<K线收盘价格<RMA55
    StopWhenVolatility // 趋势转换或者震荡：RMA13>RMA55>K线收盘价格  （不交易） | 趋势转换或者震荡：RMA13<RMA55<K线收盘价格 （不交易）

}
