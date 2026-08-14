using System;

namespace cAlgo.Robots;

public class PdhpdlRiskGuard {
    private readonly PdhpdlRiskGuardConfigModel _configModel;

    public PdhpdlRiskGuard(PdhpdlRiskGuardConfigModel configModel) {
        _configModel = configModel ?? new PdhpdlRiskGuardConfigModel();
    }

    public bool ShouldBlockNewOrder(DateTime time) {
        return time.DayOfWeek == DayOfWeek.Sunday;
    }

    public bool TryGetStopLossPipsRejectReason(double stopLossPips, out string rejectReason) {
        rejectReason = "";

        if (stopLossPips <= 0.0) {
            rejectReason = "Stop loss pips is not positive.";
            return true;
        }

        if (_configModel.MinStopLossPips > 0.0 && stopLossPips < _configModel.MinStopLossPips) {
            rejectReason = $"Stop loss distance is too small. StopLossPips={stopLossPips}, MinStopLossPips={_configModel.MinStopLossPips}";
            return true;
        }

        return false;
    }

    // Risk money is the account currency you accept losing on one trade: a percentage of
    // equity (e.g. 1% of 10000 = 100), scaled by the safety factor. Non-positive inputs risk 0.
    public double CalculateRiskMoney(double equity, double riskPct) {
        if (equity <= 0.0 || riskPct <= 0.0)
            return 0.0;

        double proportionalRiskMoney = equity * riskPct / 100.0;
        double safetyFactor = Math.Max(0.1, Math.Min(_configModel.RiskSafetyFactor, 1.0));
        return proportionalRiskMoney * safetyFactor;
    }
}
