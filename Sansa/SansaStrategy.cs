using System;
using System.Collections.Generic;
using TradingMotion.SDKv2.Algorithms;
using TradingMotion.SDKv2.Algorithms.InputParameters;
using TradingMotion.SDKv2.Markets;
using TradingMotion.SDKv2.Markets.Charts;
using TradingMotion.SDKv2.Markets.Indicators.OverlapStudies;
using TradingMotion.SDKv2.Markets.Orders;

namespace Sansa
{
    public class SansaStrategy : Strategy
    {

        /// <summary>
        /// Strategy required constructor
        /// </summary>
        /// <param Name="mainChart">The Chart over the Strategy will run</param>
        /// <param Name="secondaryCharts">Secondary charts that the Strategy can use</param>
        public SansaStrategy(Chart mainChart, List<Chart> secondaryCharts)
            : base(mainChart, secondaryCharts)
        {

        }

        /// <summary>
        /// Strategy Name
        /// </summary>
        /// <returns>The complete name of the strategy</returns>
        public override string Name
        {
            get { return "Sansa Strategy"; }
        }

        /// <summary>
        /// Security filter that ensures the OpenPosition will be closed at the end of the trading session.
        /// </summary>
        /// <returns>
        /// True if the opened position must be closed automatically on session's close, false otherwise
        /// </returns>
        public override bool ForceCloseIntradayPosition
        {
            get { return false; }
        }

        /// <summary>
        /// Security filter that sets a maximum open position level, and ensures that the strategy will never exceeds it
        /// </summary>
        /// <returns>
        /// The maximum opened lots allowed (any side)
        /// </returns>
        public override uint MaxOpenPosition
        {
            get { return 1; }
        }

        /// <summary>
        /// Flag that indicates if the strategy uses advanced Order management or standard
        /// </summary>
        /// <returns>
        /// True if strategy uses advanced Order management. This means that the strategy uses the advanced methods (InsertOrder/CancelOrder/ModifyOrder) in opposite of the simple ones (Buy/Sell/ExitLong/ExitShort).
        /// </returns>
        public override bool UsesAdvancedOrderManagement
        {
            get { return false; }
        }

        /// <summary>
        /// Creates the set of exposed Parameters for the strategy
        /// </summary>
        /// <returns>The exposed Parameters collection</returns>
        public override InputParameterList SetInputParameters()
        {
            return new InputParameterList
            {
                new InputParameter("Slow Weighted Moving Average Period", 30),
                new InputParameter("Fast Weighted Moving Average Period", 5),

                new InputParameter("Ticks Take-Profit", 190),
                new InputParameter("Ticks Stop-Loss", 60)
            };
        }

        /// <summary>
        /// Callback executed when the strategy starts executing. This is the right place
        /// to create the Indicators that the strategy will use.
        /// </summary>
        public override void OnInitialize()
        {
            log.Debug("Sansa Strategy onInitialize()");

            var indSlowWMA = new WMAIndicator(Bars.Close, (int)GetInputParameter("Slow Weighted Moving Average Period"));
            AddIndicator("Slow WMA", indSlowWMA); // Add indicator to the main chart

            if (ContainsSecondaryChart("FGBL", BarPeriodType.Minute, 120))
            {
                Chart bund120min = GetSecondaryChart("FGBL", BarPeriodType.Minute, 120); 

                var indFastWMA = new WMAIndicator(Bars.Close, (int)GetInputParameter("Fast Weighted Moving Average Period"));
                bund120min.AddIndicator("Fast WMA", indFastWMA); // Add indicator secondary chart
            }
            else
            {
                throw new Exception("Not Found Secondary Chart FGBL 120'");
            }
        }

        /// <summary>
        /// Callback executed for every new Bar. This is the right place
        /// to check your Indicators/trading rules and place the orders accordingly.
        /// </summary>
        public override void OnNewBar()
        {
            var indSlowWma = (WMAIndicator)GetIndicator("Slow WMA");
            var indFastWma = (WMAIndicator)GetSecondaryChart(0).GetIndicator("Fast WMA");
            
            if (GetOpenPosition() == 0)
            {
                if (indFastWma.GetWMA()[0] > indSlowWma.GetWMA()[0] && //Check if Fast weighted moving average is higher than Slow moving average in current bar
                    indFastWma.GetWMA()[1] < indSlowWma.GetWMA()[1]) //Check if Fast weighted moving average was lower than Slow moving average in previous bar
                {
                    //Going Long (Buying 1 Contract at Market price)
                    Buy(OrderType.Market, 1, 0, "Open long position");
                    log.Info("Open long position " + Bars.Bars[0].Close);
                }
            }

            //Place Take-Profit and Stop-Loss orders
            PlaceExitOrders();
        }

        /// <summary>
        /// Helper method to place money management orders (Take profit and Stop Loss)
        /// </summary>
        protected void PlaceExitOrders()
        {
            if (GetOpenPosition() > 0)
            {
                var ticksTakeProfit = (int)GetInputParameter("Ticks Take-Profit");
                var ticksStopLoss = (int)GetInputParameter("Ticks Stop-Loss");

                var takeProfitLevel = GetFilledOrders()[0].FillPrice + (ticksTakeProfit * Symbol.TickSize);
                var stopLossLevel = GetFilledOrders()[0].FillPrice - (ticksStopLoss * Symbol.TickSize);

                ExitLong(OrderType.Limit, Symbol.RoundToNearestTick(takeProfitLevel), "Take Profit " + takeProfitLevel);
                ExitLong(OrderType.Stop, Symbol.RoundToNearestTick(stopLossLevel), "Stop Loss " + stopLossLevel);
            }
        }
    }
}
