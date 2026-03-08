namespace StockSharp.CoinbaseAdvanced;

using System;
using System.Collections.Generic;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

using Ecng.Common;
using Ecng.Collections;

using StockSharp.Messages;
using StockSharp.Localization;

using Coinbase.AdvancedTrade;

/// <summary>
/// Message adapter for Coinbase Advanced Trade API.
/// </summary>
[OrderCondition(typeof(CoinbaseAdvancedOrderCondition))]
public partial class CoinbaseAdvancedMessageAdapter : MessageAdapter
{
	private CoinbaseClient _client;
	private CancellationTokenSource _cancellationTokenSource;

	/// <summary>
	/// Initializes a new instance of the <see cref="CoinbaseAdvancedMessageAdapter"/>.
	/// </summary>
	/// <param name="transactionIdGenerator">Transaction id generator.</param>
	public CoinbaseAdvancedMessageAdapter(IdGenerator transactionIdGenerator)
		: base(transactionIdGenerator)
	{
		HeartbeatInterval = TimeSpan.FromSeconds(5);

		this.AddMarketDataSupport();
		this.AddTransactionalSupport();

		this.AddSupportedMarketDataType(DataType.Ticks);
		this.AddSupportedMarketDataType(DataType.MarketDepth);
		this.AddSupportedMarketDataType(DataType.Level1);
		this.AddSupportedCandleTimeFrames(AllTimeFrames);
	}

	/// <inheritdoc />
	public override bool IsAllDownloadingSupported(DataType dataType)
		=> dataType == DataType.Securities || dataType == DataType.Transactions || dataType == DataType.PositionChanges || base.IsAllDownloadingSupported(dataType);

	/// <inheritdoc />
	public override bool IsSupportOrderBookIncrements => true;

	/// <inheritdoc />
	public static IEnumerable<TimeSpan> AllTimeFrames { get; } = new[]
	{
		TimeSpan.FromMinutes(1),
		TimeSpan.FromMinutes(5),
		TimeSpan.FromMinutes(15),
		TimeSpan.FromMinutes(30),
		TimeSpan.FromHours(1),
		TimeSpan.FromHours(2),
		TimeSpan.FromHours(6),
		TimeSpan.FromDays(1),
	};

	private static readonly DataType _tf5min = TimeSpan.FromMinutes(5).TimeFrame();

	/// <inheritdoc />
	public override bool IsSupportCandlesUpdates(MarketDataMessage subscription)
	{
		// Coinbase supports 5min tf live updates via WebSocket
		// Other timeframes will be built from ticks (handled by S# core)
		return subscription.DataType2 == _tf5min;
	}

	/// <inheritdoc />
	public override bool IsReplaceCommandEditCurrent => true;

	/// <inheritdoc />
	public override string[] AssociatedBoards { get; } = [BoardCodes.Coinbase];

	private void SubscribeWebSocketEvents()
	{
		// TODO: Subscribe to WebSocket events based on your Coinbase.AdvancedTrade client's API
		// Example pattern from old connector:
		// _client.OnTicker += OnTickerReceived;
		// _client.OnOrderBook += OnOrderBookReceived;
		// _client.OnTrade += OnTradeReceived;
		// _client.OnCandle += OnCandleReceived;
		// _client.OnOrder += OnOrderReceived;
		// _client.OnHeartbeat += OnHeartbeatReceived;
	}

	private void UnsubscribeWebSocketEvents()
	{
		// TODO: Unsubscribe from WebSocket events
		// Example:
		// _client.OnTicker -= OnTickerReceived;
		// _client.OnOrderBook -= OnOrderBookReceived;
		// etc...
	}

	/// <inheritdoc />
	protected override async ValueTask ResetAsync(ResetMessage resetMsg, CancellationToken cancellationToken)
	{
		_candlesTransIds.Clear();

		if (_client != null)
		{
			try
			{
				UnsubscribeWebSocketEvents();
				
				// TODO: Disconnect WebSocket if your client supports it
				// await _client.DisconnectAsync(cancellationToken);
				
				// TODO: Dispose client if IDisposable
				// (_client as IDisposable)?.Dispose();
			}
			catch (Exception ex)
			{
				await SendOutErrorAsync(ex, cancellationToken);
			}

			_client = null;
		}

		_cancellationTokenSource?.Cancel();
		_cancellationTokenSource?.Dispose();
		_cancellationTokenSource = null;

		await SendOutMessageAsync(new ResetMessage(), cancellationToken);
	}

	/// <inheritdoc />
	protected override async ValueTask ConnectAsync(ConnectMessage connectMsg, CancellationToken cancellationToken)
	{
		if (this.IsTransactional())
		{
			if (Key.IsEmpty())
				throw new InvalidOperationException(LocalizedStrings.KeyNotSpecified);

			if (Secret.IsEmpty())
				throw new InvalidOperationException(LocalizedStrings.SecretNotSpecified);
		}

		if (_client != null)
			throw new InvalidOperationException(LocalizedStrings.NotDisconnectPrevTime);

		_cancellationTokenSource = new CancellationTokenSource();

		// Create client with JWT authentication
		var apiKeyName = Key.UnSecure();
		var privateKey = Secret.UnSecure();

		_client = new CoinbaseClient(apiKeyName, privateKey);

		// Subscribe to WebSocket events
		SubscribeWebSocketEvents();

		// TODO: Connect WebSocket based on your client's API
		// await _client.ConnectAsync(cancellationToken);

		await SendOutMessageAsync(new ConnectMessage(), cancellationToken);
	}

	/// <inheritdoc />
	protected override ValueTask DisconnectAsync(DisconnectMessage disconnectMsg, CancellationToken cancellationToken)
	{
		if (_client == null)
			throw new InvalidOperationException(LocalizedStrings.ConnectionNotOk);

		try
		{
			_cancellationTokenSource?.Cancel();

			UnsubscribeWebSocketEvents();

			// TODO: Disconnect based on your client's API
			// await _client.DisconnectAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_ = SendOutErrorAsync(ex, cancellationToken);
		}

		return default;
	}

	/// <inheritdoc />
	protected override ValueTask TimeAsync(TimeMessage timeMsg, CancellationToken cancellationToken)
	{
		// Can send pings to keep WebSocket alive if needed
		return default;
	}

	private ValueTask OnHeartbeatReceived(/* TODO: add heartbeat type */ CancellationToken cancellationToken)
	{
		// Handle heartbeat to track connection health
		return default;
	}
}
