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
		var price = isMarket ? (decimal?)null : regMsg.Price;

		// TODO: Place order via your client
		// var result = await _client.PlaceOrderAsync(new OrderRequest
		// {
		//     ClientOrderId = regMsg.TransactionId.ToString(),
		//     ProductId = regMsg.SecurityId.SecurityCode,
		//     Side = regMsg.Side == Sides.Buy ? "buy" : "sell",
		//     OrderType = regMsg.OrderType == OrderTypes.Market ? "market" : "limit",
		//     Price = price,
		//     Size = regMsg.Volume,
		//     TimeInForce = regMsg.TimeInForce?.ToNativeString(),
		//     StopPrice = condition?.StopPrice,
		// }, cancellationToken);
		// 
		// var orderState = result.Status.ToOrderState();
		// 
		// if (orderState == OrderStates.Failed)
		// {
		//     await SendOutMessageAsync(new ExecutionMessage
		//     {
		//         DataTypeEx = DataType.Transactions,
		//         ServerTime = result.CreatedAt,
		//         OriginalTransactionId = regMsg.TransactionId,
		//         OrderState = OrderStates.Failed,
		//         Error = new InvalidOperationException(result.FailureReason),
		//         HasOrderInfo = true,
		//     }, cancellationToken);
		// }
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

		// Cancel all orders
		if (cancelMsg.Mode.HasFlag(OrderGroupCancelModes.CancelOrders))
		{
			// TODO: Get all active orders
			// var orders = await _client.GetOrdersAsync(cancellationToken);
			// 
			// foreach (var order in orders)
			// {
			//     // Filter by SecurityId if specified
			//     if (cancelMsg.SecurityId != default && cancelMsg.SecurityId.SecurityCode != order.ProductId)
			//         continue;
			//     
			//     // Filter by Side if specified
			//     if (cancelMsg.Side != null && cancelMsg.Side != (order.Side == "buy" ? Sides.Buy : Sides.Sell))
			//         continue;
			//     
			//     try
			//     {
			//         await _client.CancelOrderAsync(order.OrderId, cancellationToken);
			//     }
			//     catch (Exception ex)
			//     {
			//         this.AddErrorLog($"Failed to cancel order {order.OrderId}: {ex.Message}");
			//         errors.Add(ex);
			//     }
			// }
		}

		// Close all positions (sell all crypto holdings)
		if (cancelMsg.Mode.HasFlag(OrderGroupCancelModes.ClosePositions))
		{
			// TODO: Get all accounts/balances
			// var accounts = await _client.GetAccountsAsync(cancellationToken);
			// 
			// foreach (var account in accounts)
			// {
			//     var available = account.AvailableBalance;
			//     
			//     if (available <= 0)
			//         continue;
			//     
			//     // Skip base currencies
			//     if (account.Currency.EqualsIgnoreCase("USD") ||
			//         account.Currency.EqualsIgnoreCase("USDT") ||
			//         account.Currency.EqualsIgnoreCase("USDC"))
			//         continue;
			//     
			//     // Filter by SecurityId if specified
			//     if (cancelMsg.SecurityId != default && cancelMsg.SecurityId.SecurityCode != account.Currency)
			//         continue;
			//     
			//     // Filter by Side if specified (spot positions are always long)
			//     if (cancelMsg.Side != null && cancelMsg.Side != Sides.Sell)
			//         continue;
			//     
			//     try
			//     {
			//         // Place market sell order to close position
			//         var product = $"{account.Currency}-USD";
			//         
			//         await _client.PlaceOrderAsync(new OrderRequest
			//         {
			//             ClientOrderId = TransactionIdGenerator.GetNextId().ToString(),
			//             ProductId = product,
			//             Side = "sell",
			//             OrderType = "market",
			//             Size = available,
			//         }, cancellationToken);
			//     }
			//     catch (Exception ex)
			//     {
			//         this.AddErrorLog($"Failed to close position for {account.Currency}: {ex.Message}");
			//         errors.Add(ex);
			//     }
			// }
		}

		// Send result
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

		// Send portfolio message
		if (transId != 0)
		{
			await SendOutMessageAsync(new PortfolioMessage
			{
				PortfolioName = PortfolioName,
				BoardCode = BoardCodes.Coinbase,
				OriginalTransactionId = transId,
			}, cancellationToken);
		}

		// TODO: Get accounts and send positions
		// var accounts = await _client.GetAccountsAsync(cancellationToken);
		// 
		// foreach (var account in accounts)
		// {
		//     await SendOutMessageAsync(new PositionChangeMessage
		//     {
		//         PortfolioName = PortfolioName,
		//         SecurityId = new SecurityId
		//         {
		//             SecurityCode = account.Currency,
		//             BoardCode = BoardCodes.Coinbase,
		//         },
		//         ServerTime = CurrentTime,
		//     }
		//     .TryAdd(PositionChangeTypes.CurrentValue, account.AvailableBalance, true)
		//     .TryAdd(PositionChangeTypes.BlockedValue, account.Hold, true), cancellationToken);
		// }

		if (transId != 0)
			await SendSubscriptionResultAsync(lookupMsg, cancellationToken);
	}

	/// <inheritdoc />
	protected override async ValueTask OrderStatusAsync(OrderStatusMessage statusMsg, CancellationToken cancellationToken)
	{
		await SendSubscriptionReplyAsync(statusMsg.TransactionId, cancellationToken);

		if (!statusMsg.IsSubscribe)
			return;

		// TODO: Get all orders
		// var orders = await _client.GetOrdersAsync(cancellationToken);
		// 
		// foreach (var order in orders)
		//     await ProcessOrder(order, statusMsg.TransactionId, cancellationToken);

		// Subscribe to live order updates if not history-only
		if (!statusMsg.IsHistoryOnly())
		{
			// TODO: Subscribe to order updates via WebSocket
			// await _client.SubscribeOrdersAsync(statusMsg.TransactionId, cancellationToken);
		}

		await SendSubscriptionResultAsync(statusMsg, cancellationToken);
	}

	private ValueTask ProcessOrder(/* order object */ long originTransId, CancellationToken cancellationToken)
	{
		// TODO: Convert order from your client to ExecutionMessage
		// Example:
		// if (!long.TryParse(order.ClientOrderId, out var transId))
		//     transId = 0;
		// 
		// var state = order.Status.ToOrderState();
		// 
		// return SendOutMessageAsync(new ExecutionMessage
		// {
		//     ServerTime = originTransId == 0 ? CurrentTime : order.CreatedAt,
		//     DataTypeEx = DataType.Transactions,
		//     SecurityId = order.ProductId.ToStockSharpSecurityId(),
		//     TransactionId = originTransId == 0 ? 0 : transId,
		//     OriginalTransactionId = originTransId,
		//     OrderState = state,
		//     Error = state == OrderStates.Failed ? new InvalidOperationException(order.FailureReason) : null,
		//     OrderType = order.OrderType == "market" ? OrderTypes.Market : OrderTypes.Limit,
		//     Side = order.Side == "buy" ? Sides.Buy : Sides.Sell,
		//     OrderStringId = order.OrderId,
		//     OrderPrice = order.Price ?? 0,
		//     OrderVolume = order.Size,
		//     Balance = order.RemainingSize,
		//     HasOrderInfo = true,
		// }, cancellationToken);

		return default;
	}

	private ValueTask OnOrderReceived(/* order from WebSocket */ CancellationToken cancellationToken)
	{
		// Forward WebSocket order updates to ProcessOrder
		// return ProcessOrder(order, 0, cancellationToken);

		return default;
	}
}
