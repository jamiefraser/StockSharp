namespace StockSharp.CoinbaseAdvanced;

using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

using StockSharp.Messages;
using StockSharp.Localization;

/// <summary>
/// Coinbase Advanced Trade order condition.
/// </summary>
[Serializable]
[DataContract]
[Display(ResourceType = typeof(LocalizedStrings), Name = LocalizedStrings.CoinbaseKey)]
public class CoinbaseAdvancedOrderCondition : BaseWithdrawOrderCondition, IStopLossOrderCondition
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CoinbaseAdvancedOrderCondition"/>.
	/// </summary>
	public CoinbaseAdvancedOrderCondition()
	{
	}

	/// <summary>
	/// Activation price, when reached an order will be placed.
	/// </summary>
	[DataMember]
	[Display(
		ResourceType = typeof(LocalizedStrings),
		Name = LocalizedStrings.StopPriceKey,
		Description = LocalizedStrings.StopPriceDescKey,
		GroupName = LocalizedStrings.StopLossKey,
		Order = 0)]
	public decimal? StopPrice
	{
		get
		{
			if (Parameters.TryGetValue(nameof(StopPrice), out var value))
				return (decimal?)value;
			return null;
		}
		set => Parameters[nameof(StopPrice)] = value;
	}

	decimal? IStopLossOrderCondition.ClosePositionPrice { get; set; }

	decimal? IStopLossOrderCondition.ActivationPrice
	{
		get => StopPrice;
		set => StopPrice = value;
	}

	bool IStopLossOrderCondition.IsTrailing
	{
		get => false;
		set {  }
	}
}
