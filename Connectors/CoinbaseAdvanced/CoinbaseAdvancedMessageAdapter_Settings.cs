namespace StockSharp.CoinbaseAdvanced;

using System.ComponentModel.DataAnnotations;
using System.Security;

using Ecng.ComponentModel;
using Ecng.Serialization;

using StockSharp.Messages;
using StockSharp.Localization;

/// <summary>
/// The message adapter for Coinbase Advanced Trade API.
/// </summary>
/// <remarks>
/// Uses CDP Cloud API with JWT authentication (Key Name + EC Private Key in PEM format).
/// </remarks>
[MediaIcon(Media.MediaNames.coinbase)]
[Doc("topics/api/connectors/crypto_exchanges/coinbase_advanced.html")]
[Display(
	ResourceType = typeof(LocalizedStrings),
	Name = LocalizedStrings.CoinbaseKey,
	Description = LocalizedStrings.CryptoConnectorKey,
	GroupName = LocalizedStrings.CryptocurrencyKey)]
[MessageAdapterCategory(MessageAdapterCategories.Crypto | MessageAdapterCategories.RealTime | MessageAdapterCategories.OrderLog |
	MessageAdapterCategories.Free | MessageAdapterCategories.Level1 | MessageAdapterCategories.Transactions)]
public partial class CoinbaseAdvancedMessageAdapter : IKeySecretAdapter
{
	/// <summary>
	/// CDP API Key Name (format: organizations/{org_id}/apiKeys/{key_id}).
	/// </summary>
	[Display(
		ResourceType = typeof(LocalizedStrings),
		Name = LocalizedStrings.KeyKey,
		Description = LocalizedStrings.KeyKey + LocalizedStrings.Dot,
		GroupName = LocalizedStrings.ConnectionKey,
		Order = 0)]
	[BasicSetting]
	public SecureString Key { get; set; }

	/// <summary>
	/// CDP Private Key (EC PEM format for JWT signing).
	/// </summary>
	[Display(
		ResourceType = typeof(LocalizedStrings),
		Name = LocalizedStrings.SecretKey,
		Description = LocalizedStrings.SecretDescKey,
		GroupName = LocalizedStrings.ConnectionKey,
		Order = 1)]
	[BasicSetting]
	public SecureString Secret { get; set; }

	/// <inheritdoc />
	public override void Save(SettingsStorage storage)
	{
		base.Save(storage);

		storage.SetValue(nameof(Key), Key);
		storage.SetValue(nameof(Secret), Secret);
	}

	/// <inheritdoc />
	public override void Load(SettingsStorage storage)
	{
		base.Load(storage);

		Key = storage.GetValue<SecureString>(nameof(Key));
		Secret = storage.GetValue<SecureString>(nameof(Secret));
	}

	/// <inheritdoc />
	public override string ToString()
	{
		if (Key != null && Key.Length > 0)
			return base.ToString() + ": " + LocalizedStrings.Key + " = ***";
		return base.ToString();
	}
}
