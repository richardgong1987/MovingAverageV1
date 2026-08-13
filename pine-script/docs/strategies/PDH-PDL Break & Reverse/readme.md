# PDH/PDL Break & Reverse V1

## 1. Strategy Overview

### Strategy Name

`PDH/PDL Break & Reverse V1`

### Market

`XAUUSD`

### Timeframe

`15m`

### Strategy Type

False breakout reversal strategy.

### Core Idea

Use the previous trading day's high and low as key levels.

- `PDH` = Previous Day High
- `PDL` = Previous Day Low

If price breaks above `PDH` but closes back below `PDH`, treat it as a false upside breakout and open a short position.

If price breaks below `PDL` but closes back above `PDL`, treat it as a false downside breakout and open a long position.

V1 only uses the signal candle itself.  
Do not wait for additional confirmation candles.

---

# 2. Definitions

## 2.1 PDH

`PDH` means the previous trading day's high.

```text
PDH = High of the previous daily candle