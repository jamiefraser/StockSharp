namespace StockSharp.CoinbaseAdvanced;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Ecng.Collections;
using Ecng.Common;

using StockSharp.Messages;
using StockSharp.Localization;

public partial class CoinbaseAdvancedMessageAdapter
{
	private readonly SynchronizedDictionary<string, long> _candlesTransIds = new();

	/// <inheritdoc />
	protected override async ValueTask SecurityLookupAsync(SecurityLookupMessage lookupMsg, CancellationToken cancellationToken)
	{
		var secTypes = lookupMsg.GetSecurityTypes();
		var left = lookupMsg.Count ?? long.MaxValue;

		// TODO: Get products from your Coinbase.AdvancedTrade client
		// The old connector supported both SPOT and FUTURE types
		// Example:
		// var spotProducts = await _client.GetProductsAsync("SPOT", cancellationToken);
		// var futureProducts = await _client.GetProductsAsync("FUTURE", cancellationToken);
		//
		// foreach (var product in spotProducts.Concat(futureProducts))
		// {
		//     var securityType = product.ProductType == "SPOT" ? SecurityTypes.CryptoCurrency : SecurityTypes.Future;
		//     
		//     var secMsg = new SecurityMessage
		//     {
		//         SecurityType = securityType,
		//         SecurityId = product.ProductId.ToStockSharpSecurityId(),
		//         Name = product.DisplayName,
		//         PriceStep = product.QuoteIncrement,
		//         VolumeStep = product.BaseIncrement,
		//         MinVolume = product.BaseMinSize,
		//         MaxVolume = product.BaseMaxSize,
		//         OriginalTransactionId = lookupMsg.TransactionId,
		//     };
		//     
		//     // For futures
		//     if (product.FutureProductDetails != null)
		//     {
		//         secMsg.ExpiryDate = product.FutureProductDetails.ContractExpiry;
		//         secMsg.Multiplier = product.FutureProductDetails.ContractSize;
		//         secMsg.UnderlyingSecurityCode = product.BaseCurrencyId?.ToUpperInvariant();
		//     }
		//     
		//     if (!secMsg.IsMatch(lookupMsg, secTypes))
		//         continue;
		//     
		//     await SendOutMessageAsync(secMsg, cancellationToken);
		//     
		//     if (--left <= 0)
		//         break;
		// }

		await SendSubscriptionResultAsync(lookupMsg, cancellationToken);
	}

	/// <inheritdoc />
	protected override async ValueTask OnLevel1SubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken)
	{
		await SendSubscriptionReplyAsync(mdMsg.TransactionId, cancellationToken);

		var symbol = mdMsg.SecurityId.SecurityCode;

		if (mdMsg.IsSubscribe)
		{
			// TODO: Subscribe to ticker based on your client's API
			// await _client.SubscribeTickerAsync(mdMsg.TransactionId, symbol, cancellationToken);

			await SendSubscriptionResultAsync(mdMsg, cancellationToken);
		}
		else
		{
			// TODO: Unsubscribe from ticker
			// await _client.UnsubscribeTickerAsync(mdMsg.OriginalTransactionId, symbol, cancellationToken);
		}
	}

	/// <inheritdoc />
	protected override async ValueTask OnMarketDepthSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken)
	{
		await SendSubscriptionReplyAsync(mdMsg.TransactionId, cancellationToken);

		var symbol = mdMsg.SecurityId.SecurityCode;

		if (mdMsg.IsSubscribe)
		{
			// TODO: Subscribe to order book
			// await _client.SubscribeOrderBookAsync(mdMsg.TransactionId, symbol, cancellationToken);

			await SendSubscriptionResultAsync(mdMsg, cancellationToken);
		}
		else
		{
			// TODO: Unsubscribe from order book
			// await _client.UnsubscribeOrderBookAsync(mdMsg.OriginalTransactionId, symbol, cancellationToken);
		}
	}

	/// <inheritdoc />
	protected override async ValueTask OnTicksSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken)
	{
		await SendSubscriptionReplyAsync(mdMsg.TransactionId, cancellationToken);

		var symbol = mdMsg.SecurityId.SecurityCode;

		if (mdMsg.IsSubscribe)
		{
			if (!mdMsg.IsHistoryOnly())
			{
				// TODO: Subscribe to trades
				// await _client.SubscribeTradesAsync(mdMsg.TransactionId, symbol, cancellationToken);
			}

			await SendSubscriptionResultAsync(mdMsg, cancellationToken);
		}
		else
		{
			// TODO: Unsubscribe from trades
			// await _client.UnsubscribeTradesAsync(mdMsg.OriginalTransactionId, symbol, cancellationToken);
		}
	}

	/// <inheritdoc />
	protected override async ValueTask OnTFCandlesSubscriptionAsync(MarketDataMessage mdMsg, CancellationToken cancellationToken)
	{
		await SendSubscriptionReplyAsync(mdMsg.TransactionId, cancellationToken);

		var symbol = mdMsg.SecurityId.SecurityCode;

		if (mdMsg.IsSubscribe)
		{
			var tf = mdMsg.GetTimeFrame();

			// Historical candles
			if (mdMsg.From is not null)
			{
				var from = mdMsg.From.Value.ToUnix();
				var to = (mdMsg.To ?? DateTime.UtcNow).ToUnix();
				var left = mdMsg.Count ?? long.MaxValue;
				var step = tf.Multiply(200).TotalSeconds;
				var granularity = GetCandleGranularity(tf);

				while (from < to)
				{
					// TODO: Get historical candles from your client
					// var candles = await _client.GetCandlesAsync(symbol, from, from + step, granularity, cancellationToken);
					// 
					// foreach (var candle in candles.OrderBy(c => c.Time))
					// {
					//     if (candle.Time < from || candle.Time > to)
					//         continue;
					//     
					//     await SendOutMessageAsync(new TimeFrameCandleMessage
					//     {
					//         OpenPrice = candle.Open,
					//         ClosePrice = candle.Close,
					//         HighPrice = candle.High,
					//         LowPrice = candle.Low,
					//         TotalVolume = candle.Volume,
					//         OpenTime = candle.Time,
					//         State = CandleStates.Finished,
					//         OriginalTransactionId = mdMsg.TransactionId,
					//     }, cancellationToken);
					//     
					//     if (--left <= 0)
					//         break;
					// }

					from += (long)step;

					if (left <= 0)
						break;
				}
			}

			// Live candles (only 5min supported via WebSocket)
			if (!mdMsg.IsHistoryOnly() && mdMsg.DataType2 == _tf5min)
			{
				_candlesTransIds[symbol] = mdMsg.TransactionId;
				
				// TODO: Subscribe to live candles
				// await _client.SubscribeCandlesAsync(mdMsg.TransactionId, symbol, cancellationToken);
				
				await SendSubscriptionResultAsync(mdMsg, cancellationToken);
			}
			else
			{
				await SendSubscriptionFinishedAsync(mdMsg.TransactionId, cancellationToken);
			}
		}
		else
		{
			_candlesTransIds.Remove(symbol);
			
			// TODO: Unsubscribe from candles
			// await _client.UnsubscribeCandlesAsync(mdMsg.OriginalTransactionId, symbol, cancellationToken);
		}
	}

	private string GetCandleGranularity(TimeSpan timeFrame)
	{
		// Map StockSharp timeframes to Coinbase granularity strings
		if (timeFrame == TimeSpan.FromMinutes(1)) return "ONE_MINUTE";
		if (timeFrame == TimeSpan.FromMinutes(5)) return "FIVE_MINUTE";
		if (timeFrame == TimeSpan.FromMinutes(15)) return "FIFTEEN_MINUTE";
		if (timeFrame == TimeSpan.FromMinutes(30)) return "THIRTY_MINUTE";
		if (timeFrame == TimeSpan.FromHours(1)) return "ONE_HOUR";
		if (timeFrame == TimeSpan.FromHours(2)) return "TWO_HOUR";
		if (timeFrame == TimeSpan.FromHours(6)) return "SIX_HOUR";
		if (timeFrame == TimeSpan.FromDays(1)) return "ONE_DAY";
		
		throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, LocalizedStrings.InvalidValue);
	}

	#region WebSocket Event Handlers

	// TODO: Implement WebSocket event handlers based on your client's event model

	private ValueTask OnTickerReceived(/* ticker data */ CancellationToken cancellationToken)
	{
		// Example implementation:
		// return SendOutMessageAsync(new Level1ChangeMessage
		// {
		//     SecurityId = ticker.ProductId.ToStockSharpSecurityId(),
		//     ServerTime = CurrentTime,
		// }
		// .TryAdd(Level1Fields.LastTradeId, ticker.LastTradeId)
		// .TryAdd(Level1Fields.LastTradePrice, ticker.LastTradePrice)
		// .TryAdd(Level1Fields.LastTradeVolume, ticker.LastTradeVolume)
		// .TryAdd(Level1Fields.HighPrice, ticker.High)
		// .TryAdd(Level1Fields.LowPrice, ticker.Low)
		// .TryAdd(Level1Fields.Volume, ticker.Volume)
		// .TryAdd(Level1Fields.BestBidPrice, ticker.BestBid)
		// .TryAdd(Level1Fields.BestAskPrice, ticker.BestAsk)
		// .TryAdd(Level1Fields.BestBidVolume, ticker.BestBidSize)
		// .TryAdd(Level1Fields.BestAskVolume, ticker.BestAskSize)
		// , cancellationToken);

		return default;
	}

	private ValueTask OnTradeReceived(/* trade data */ CancellationToken cancellationToken)
	{
		// Example implementation:
		// return SendOutMessageAsync(new ExecutionMessage
		// {
		//     DataTypeEx = DataType.Ticks,
		//     SecurityId = trade.ProductId.ToStockSharpSecurityId(),
		//     TradeId = trade.TradeId,
		//     TradePrice = trade.Price,
		//     TradeVolume = trade.Size,
		//     ServerTime = trade.Time,
		//     OriginSide = trade.Side == "buy" ? Sides.Buy : Sides.Sell,
		// }, cancellationToken);

		return default;
	}

	private ValueTask OnOrderBookReceived(/* order book data */ CancellationToken cancellationToken)
	{
		// Example implementation:
		// var bids = new List<QuoteChange>();
		// var asks = new List<QuoteChange>();
		// 
		// foreach (var change in orderBook.Changes)
		// {
		//     var side = change.Side == "buy" ? Sides.Buy : Sides.Sell;
		//     var quotes = side == Sides.Buy ? bids : asks;
		//     quotes.Add(new QuoteChange(change.Price, change.Size));
		// }
		// 
		// return SendOutMessageAsync(new QuoteChangeMessage
		// {
		//     SecurityId = orderBook.ProductId.ToStockSharpSecurityId(),
		//     Bids = bids.ToArray(),
		//     Asks = asks.ToArray(),
		//     ServerTime = CurrentTime,
		//     State = orderBook.Type == "snapshot" ? QuoteChangeStates.SnapshotComplete : QuoteChangeStates.Increment,
		// }, cancellationToken);

		return default;
	}

	private ValueTask OnCandleReceived(/* candle data */ CancellationToken cancellationToken)
	{
		// Example implementation:
		// if (!_candlesTransIds.TryGetValue(candle.Symbol, out var transId))
		//     return default;
		// 
		// return SendOutMessageAsync(new TimeFrameCandleMessage
		// {
		//     OpenPrice = candle.Open,
		//     ClosePrice = candle.Close,
		//     HighPrice = candle.High,
		//     LowPrice = candle.Low,
		//     TotalVolume = candle.Volume,
		//     OpenTime = candle.Time,
		//     State = CandleStates.Active,
		//     OriginalTransactionId = transId,
		// }, cancellationToken);

		return default;
	}

	#endregion
}
