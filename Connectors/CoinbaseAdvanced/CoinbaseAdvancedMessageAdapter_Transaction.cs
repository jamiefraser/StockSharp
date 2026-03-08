namespace StockSharp.CoinbaseAdvanced;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Ecng.Common;

using StockSharp.Messages;
using StockSharp.Localization;

public partial class CoinbaseAdvancedMessageAdapter
{
	private string PortfolioName => nameof(CoinbaseAdvanced) + "_" + Key.ToId();

	/// <inheritdoc />
	protected override async ValueTask RegisterOrderAsync(OrderRegisterMessage regMsg, CancellationToken cancellationToken)
	{
		var condition = (CoinbaseAdvancedOrderCondition)regMsg.Condition;

		// Handle conditional orders (e.g., withdrawals)
		if (regMsg.OrderType == OrderTypes.Conditional)
		{
			if (condition?.IsWithdraw == true)
			{
				// TODO: Implement withdrawal via your client
				// var withdrawId = await _client.WithdrawAsync(
				//     regMsg.SecurityId.SecurityCode,
				//     regMsg.Volume,
				//     condition.WithdrawInfo,
				//     cancellationToken);
				// 
				// await SendOutMessageAsync(new ExecutionMessage
				// {
				//     DataTypeEx = DataType.Transactions,
				//     OrderStringId = withdrawId,
				//     ServerTime = CurrentTime,
				//     OriginalTransactionId = regMsg.TransactionId,
				//     OrderState = OrderStates.Done,
				//     HasOrderInfo = true,
				// }, cancellationToken);
				// 
				// // Refresh portfolio after withdrawal
				// await PortfolioLookupAsync(null, cancellationToken);
				
				return;
			}

			throw new NotSupportedException(LocalizedStrings.OrderUnsupportedType.Put(regMsg.OrderType, regMsg.TransactionId));
		}

		// Regular orders (Limit/Market)
		if (regMsg.OrderType != OrderTypes.Limit && regMsg.OrderType != OrderTypes.Market && regMsg.OrderType != null)
			throw new NotSupportedException(LocalizedStrings.OrderUnsupportedType.Put(regMsg.OrderType, regMsg.TransactionId));

		var isMarket = regMsg.OrderType == OrderTypes.Market;
		var oSide = regMsg.Side == Sides.Buy ? Coinbase.AdvancedTrade.Enums.OrderSide.BUY : Coinbase.AdvancedTrade.Enums.OrderSide.SELL;

		Coinbase.AdvancedTrade.Models.Order orderResult;

		if (isMarket)
		{
			orderResult = await _client.Orders.CreateMarketOrderAsync(
				regMsg.SecurityId.SecurityCode,
				oSide,
				regMsg.Volume.ToString(),
				true
			);
		}
		else
		{
			var postOnly = regMsg.TimeInForce == TimeInForce.PutInQueue;
			orderResult = await _client.Orders.CreateLimitOrderGTCAsync(
				regMsg.SecurityId.SecurityCode,
				oSide,
				regMsg.Volume.ToString(),
				regMsg.Price.ToString(),
				postOnly,
				true
			);
		}

		ProcessCreateOrderResult(orderResult, regMsg, cancellationToken);
	}

	private void ProcessCreateOrderResult(Coinbase.AdvancedTrade.Models.Order result, OrderRegisterMessage regMsg, CancellationToken cancellationToken)
	{
		if (result != null && !string.IsNullOrEmpty(result.OrderId))
		{
			lock (_orderIdToTransId)
			{
				_orderIdToTransId[result.OrderId] = regMsg.TransactionId;
			}
		}

		var state = result?.Status switch
		{
			"OPEN" => OrderStates.Active,
			"FILLED" => OrderStates.Done,
			"CANCELLED" => OrderStates.Done,
			"EXPIRED" => OrderStates.Done,
			"FAILED" => OrderStates.Failed,
			_ => OrderStates.None,
		};

		if (state == OrderStates.Failed || state == OrderStates.Done)
		{
			if (result != null && !string.IsNullOrEmpty(result.OrderId))
			{
				lock (_orderIdToTransId)
				{
					_orderIdToTransId.Remove(result.OrderId);
				}
			}
		}

		if (state == OrderStates.Failed)
		{
			_ = SendOutMessageAsync(new ExecutionMessage
			{
				DataTypeEx = DataType.Transactions,
				ServerTime = CurrentTime,
				OriginalTransactionId = regMsg.TransactionId,
				OrderState = OrderStates.Failed,
				Error = new InvalidOperationException(result.RejectReason ?? result.RejectMessage ?? "Unknown error"),
				HasOrderInfo = true,
			}, cancellationToken);
		}
	}

	/// <inheritdoc />
	protected override async ValueTask CancelOrderAsync(OrderCancelMessage cancelMsg, CancellationToken cancellationToken)
	{
		if (cancelMsg.OrderStringId.IsEmpty())
			throw new InvalidOperationException(LocalizedStrings.OrderNoExchangeId.Put(cancelMsg.OriginalTransactionId));

		// TODO: Cancel order via your client
		// await _client.CancelOrderAsync(cancelMsg.OrderStringId, cancellationToken);
	}

	/// <inheritdoc />
	protected override async ValueTask ReplaceOrderAsync(OrderReplaceMessage replaceMsg, CancellationToken cancellationToken)
	{
		// TODO: Edit order via your client
		// await _client.EditOrderAsync(
		//     replaceMsg.OldOrderStringId,
		//     replaceMsg.Price,
		//     replaceMsg.Volume,
		//     cancellationToken);
	}

	/// <inheritdoc />
	protected override async ValueTask CancelOrderGroupAsync(OrderGroupCancelMessage cancelMsg, CancellationToken cancellationToken)
	{
		var errors = new List<Exception>();

		if (cancelMsg.Mode.HasFlag(OrderGroupCancelModes.CancelOrders))
		{
			var orders = await _client.Orders.ListOrdersAsync(
				productId: cancelMsg.SecurityId == default ? null : cancelMsg.SecurityId.SecurityCode,
				orderStatus: new[] { Coinbase.AdvancedTrade.Enums.OrderStatus.OPEN }
			);

			if (orders != null && orders.Count > 0)
			{
				var idsToCancel = new List<string>();
				foreach (var order in orders)
				{
					if (cancelMsg.Side != null && cancelMsg.Side != (order.Side.EqualsIgnoreCase("buy") ? Sides.Buy : Sides.Sell))
						continue;

					idsToCancel.Add(order.OrderId);
				}

				if (idsToCancel.Count > 0)
				{
					var results = await _client.Orders.CancelOrdersAsync(idsToCancel.ToArray());
					foreach (var res in results)
					{
						if (!res.Success)
						{
							errors.Add(new InvalidOperationException($"Failed to cancel order {res.OrderId}: {res.FailureReason}"));
						}
					}
				}
			}
		}

		if (cancelMsg.Mode.HasFlag(OrderGroupCancelModes.ClosePositions))
		{
			var accounts = await _client.Accounts.ListAccountsAsync(limit: 250);
			if (accounts != null)
			{
				foreach (var account in accounts)
				{
					var available = account.AvailableBalance.Value.To<decimal>();

					if (available <= 0)
						continue;

					if (account.Currency.EqualsIgnoreCase("USD") ||
						account.Currency.EqualsIgnoreCase("USDT") ||
						account.Currency.EqualsIgnoreCase("USDC"))
						continue;

					if (cancelMsg.SecurityId != default && cancelMsg.SecurityId.SecurityCode != account.Currency)
						continue;

					if (cancelMsg.Side != null && cancelMsg.Side != Sides.Sell)
						continue;

					try
					{
						var product = $"{account.Currency}-USD";
						await _client.Orders.CreateMarketOrderAsync(
							product,
							Coinbase.AdvancedTrade.Enums.OrderSide.SELL,
							available.ToString(),
							true
						);
					}
					catch (Exception ex)
					{
						errors.Add(new InvalidOperationException($"Failed to close position for {account.Currency}: {ex.Message}"));
					}
				}
			}
		}

		if (errors.Count > 0)
		{
			await SendOutMessageAsync(new ExecutionMessage
			{
				DataTypeEx = DataType.Transactions,
				OriginalTransactionId = cancelMsg.TransactionId,
				ServerTime = CurrentTime,
				HasOrderInfo = true,
				Error = errors.Count == 1 ? errors[0] : new AggregateException(errors),
			}, cancellationToken);
		}
	}

	/// <inheritdoc />
	protected override async ValueTask PortfolioLookupAsync(PortfolioLookupMessage lookupMsg, CancellationToken cancellationToken)
	{
		var transId = lookupMsg?.TransactionId ?? 0;

		if (transId != 0)
			await SendSubscriptionReplyAsync(transId, cancellationToken);

		if (lookupMsg?.IsSubscribe == false)
			return;

		if (transId != 0)
		{
			await SendOutMessageAsync(new PortfolioMessage
			{
				PortfolioName = PortfolioName,
				BoardCode = BoardCodes.Coinbase,
				OriginalTransactionId = transId,
			}, cancellationToken);
		}

		var accounts = await _client.Accounts.ListAccountsAsync(limit: 250);
		if (accounts != null)
		{
			foreach (var account in accounts)
			{
				await SendOutMessageAsync(new PositionChangeMessage
				{
					PortfolioName = PortfolioName,
					SecurityId = new SecurityId
					{
						SecurityCode = account.Currency,
						BoardCode = BoardCodes.Coinbase,
					},
					ServerTime = CurrentTime,
				}
				.TryAdd(PositionChangeTypes.CurrentValue, account.AvailableBalance.Value.To<decimal>(), true)
				.TryAdd(PositionChangeTypes.BlockedValue, account.Hold.Value.To<decimal>(), true), cancellationToken);
			}
		}

		if (transId != 0)
			await SendSubscriptionResultAsync(lookupMsg, cancellationToken);
	}

	/// <inheritdoc />
	protected override async ValueTask OrderStatusAsync(OrderStatusMessage statusMsg, CancellationToken cancellationToken)
	{
		await SendSubscriptionReplyAsync(statusMsg.TransactionId, cancellationToken);

		if (!statusMsg.IsSubscribe)
			return;

		var orders = await _client.Orders.ListOrdersAsync();
		if (orders != null)
		{
			foreach (var order in orders)
				await ProcessOrder(order, statusMsg.TransactionId, cancellationToken);
		}

		var fills = await _client.Orders.ListFillsAsync();
		if (fills != null)
		{
			foreach (var fill in fills)
			{
				await SendOutMessageAsync(new ExecutionMessage
				{
					ServerTime = CurrentTime, // Fills don't have a parseable standard DateTime easily mapping on basic DTO without custom string parse
					DataTypeEx = DataType.Ticks,
					SecurityId = new SecurityId { SecurityCode = fill.ProductId, BoardCode = BoardCodes.Coinbase },
					OrderStringId = fill.OrderId,
					TradeId = fill.TradeId.To<long>(),
					TradePrice = fill.Price.To<decimal>(),
					TradeVolume = fill.Size.To<decimal>(),
					OriginSide = fill.Side.EqualsIgnoreCase("buy") ? Sides.Buy : Sides.Sell,
				}, cancellationToken);
			}
		}

		if (!statusMsg.IsHistoryOnly())
		{
			await _client.WebSocket.SubscribeAsync(new string[] { }, Coinbase.AdvancedTrade.Enums.ChannelType.User);
		}

		await SendSubscriptionResultAsync(statusMsg, cancellationToken);
	}

	private ValueTask ProcessOrder(Coinbase.AdvancedTrade.Models.Order order, long originTransId, CancellationToken cancellationToken)
	{
		if (!long.TryParse(order.ClientOrderId, out var transId))
		{
			lock (_orderIdToTransId)
			{
				if (_orderIdToTransId.TryGetValue(order.OrderId, out var mappedId))
					transId = mappedId;
				else
					transId = 0;
			}
		}

		var state = order.Status switch
		{
			"OPEN" => OrderStates.Active,
			"FILLED" => OrderStates.Done,
			"CANCELLED" => OrderStates.Done,
			"EXPIRED" => OrderStates.Done,
			"FAILED" => OrderStates.Failed,
			_ => OrderStates.None,
		};

		var p = 0m;
		if (order.OrderConfiguration?.LimitGtc != null) p = order.OrderConfiguration.LimitGtc.LimitPrice.To<decimal>();
		else if (order.OrderConfiguration?.LimitGtd != null) p = order.OrderConfiguration.LimitGtd.LimitPrice.To<decimal>();
		else if (order.OrderConfiguration?.StopLimitGtc != null) p = order.OrderConfiguration.StopLimitGtc.LimitPrice.To<decimal>();
		else if (order.OrderConfiguration?.StopLimitGtd != null) p = order.OrderConfiguration.StopLimitGtd.LimitPrice.To<decimal>();

		var vol = 0m;
		if (order.OrderConfiguration?.LimitGtc != null) vol = order.OrderConfiguration.LimitGtc.BaseSize.To<decimal>();
		else if (order.OrderConfiguration?.LimitGtd != null) vol = order.OrderConfiguration.LimitGtd.BaseSize.To<decimal>();
		else if (order.OrderConfiguration?.StopLimitGtc != null) vol = order.OrderConfiguration.StopLimitGtc.BaseSize.To<decimal>();
		else if (order.OrderConfiguration?.StopLimitGtd != null) vol = order.OrderConfiguration.StopLimitGtd.BaseSize.To<decimal>();
		else if (order.OrderConfiguration?.MarketIoc != null) vol = order.OrderConfiguration.MarketIoc.BaseSize.To<decimal>();

		if (state == OrderStates.Done || state == OrderStates.Failed)
		{
			lock (_orderIdToTransId)
			{
				_orderIdToTransId.Remove(order.OrderId);
			}
		}

		return SendOutMessageAsync(new ExecutionMessage
		{
			ServerTime = order.CreatedTime ?? CurrentTime,
			DataTypeEx = DataType.Transactions,
			SecurityId = new SecurityId { SecurityCode = order.ProductId, BoardCode = BoardCodes.Coinbase },
			TransactionId = originTransId == 0 ? 0 : transId,
			OriginalTransactionId = originTransId,
			OrderState = state,
			Error = state == OrderStates.Failed ? new InvalidOperationException(order.RejectReason) : null,
			OrderType = order.OrderType.EqualsIgnoreCase("market") ? OrderTypes.Market : OrderTypes.Limit,
			Side = order.Side.EqualsIgnoreCase("buy") ? Sides.Buy : Sides.Sell,
			OrderStringId = order.OrderId,
			OrderPrice = p,
			OrderVolume = vol,
			Balance = vol - order.FilledSize.To<decimal>(),
			HasOrderInfo = true,
		}, cancellationToken);
	}

	private void OnOrderReceived(object sender, Coinbase.AdvancedTrade.WebSocketMessageEventArgs<Coinbase.AdvancedTrade.Models.WebSocket.UserMessage> e)
	{
		foreach (var evt in e.Message.Events)
		{
			foreach (var order in evt.Orders)
			{
				_ = ProcessOrderWebSocket(order, 0, _cancellationTokenSource?.Token ?? default);
			}
		}
	}

	private ValueTask ProcessOrderWebSocket(Coinbase.AdvancedTrade.Models.WebSocket.UserOrder order, long originTransId, CancellationToken cancellationToken)
	{
		if (!long.TryParse(order.ClientOrderId, out var transId))
		{
			lock (_orderIdToTransId)
			{
				if (_orderIdToTransId.TryGetValue(order.OrderId, out var mappedId))
					transId = mappedId;
				else
					transId = 0;
			}
		}

		var state = order.Status switch
		{
			"OPEN" => OrderStates.Active,
			"FILLED" => OrderStates.Done,
			"CANCELLED" => OrderStates.Done,
			"EXPIRED" => OrderStates.Done,
			"FAILED" => OrderStates.Failed,
			_ => OrderStates.None,
		};

		if (state == OrderStates.Done || state == OrderStates.Failed)
		{
			lock (_orderIdToTransId)
			{
				_orderIdToTransId.Remove(order.OrderId);
			}
		}

		return SendOutMessageAsync(new ExecutionMessage
		{
			ServerTime = order.CreationTime,
			DataTypeEx = DataType.Transactions,
			SecurityId = new SecurityId { SecurityCode = order.ProductId, BoardCode = BoardCodes.Coinbase },
			TransactionId = originTransId == 0 ? 0 : transId,
			OriginalTransactionId = originTransId,
			OrderState = state,
			Error = null,
			OrderType = order.OrderType.EqualsIgnoreCase("market") ? OrderTypes.Market : OrderTypes.Limit,
			Side = order.OrderSide.EqualsIgnoreCase("buy") ? Sides.Buy : Sides.Sell,
			OrderStringId = order.OrderId,
			OrderPrice = order.AvgPrice.To<decimal>(),
			OrderVolume = order.CumulativeQuantity.To<decimal>() + order.LeavesQuantity.To<decimal>(),
			Balance = order.LeavesQuantity.To<decimal>(),
			HasOrderInfo = true,
		}, cancellationToken);
	}
}
