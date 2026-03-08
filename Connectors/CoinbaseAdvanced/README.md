# Coinbase Advanced Trade Connector

A StockSharp connector for the Coinbase Advanced Trade API, supporting the new JWT authentication method and modern API endpoints.

## Features

✅ **JWT Authentication** - Uses the new Cloud API authentication method  
✅ **Real-time Market Data** - WebSocket streaming for ticks, order books, and trades  
✅ **Order Management** - Place, cancel, and track orders  
✅ **Portfolio & Positions** - Real-time account balances and positions  
✅ **Full StockSharp Integration** - Works with all StockSharp strategies and tools

## Prerequisites

- **.NET 10** or later
- **Coinbase Advanced Trade API credentials** (API Key Name + Private Key)
- **Coinbase.AdvancedTrade NuGet package** (v1.4.0 or later)

## Installation

### 1. Add the Project Reference

In your StockSharp-based project, add a reference to the `CoinbaseAdvanced` connector:

```xml
<ProjectReference Include="..\..\Connectors\CoinbaseAdvanced\CoinbaseAdvanced.csproj" />
```

### 2. Install the NuGet Package

The connector depends on the `Coinbase.AdvancedTrade` package:

```bash
dotnet add package Coinbase.AdvancedTrade
```

## Usage

### Basic Connection

```csharp
using CoinbaseAdvanced;
using StockSharp.Messages;

// Create connector with your API credentials
var connector = new CoinbaseAdvancedConnector(
    apiKeyName: "organizations/{org_id}/apiKeys/{key_id}",
    privateKey: "-----BEGIN EC PRIVATE KEY-----\n...\n-----END EC PRIVATE KEY-----"
);

// Subscribe to events
connector.Connected += () => Console.WriteLine("Connected!");
connector.SecurityReceived += (sub, security) => 
    Console.WriteLine($"Security: {security.Code}");

// Connect
connector.Connect();
```

### Using with Strategies

```csharp
using StockSharp.Algo.Strategies;

public class MyStrategy : Strategy
{
    protected override void OnStarted()
    {
        // Register for market data
        this.RegisterTrades(Security);
        
        base.OnStarted();
    }

    protected override void OnNewMyTrade(MyTrade trade)
    {
        this.AddInfoLog($"Trade: {trade.Trade.Price}");
    }
}

// Create and start strategy
var connector = new CoinbaseAdvancedConnector(apiKeyName, privateKey);
connector.Connect();

var btcUsd = connector.Securities.First(s => s.Code == "BTC-USD");
var portfolio = connector.Portfolios.First();

var strategy = new MyStrategy
{
    Connector = connector,
    Security = btcUsd,
    Portfolio = portfolio
};

strategy.Start();
```

### Market Data Subscription

```csharp
// Subscribe to ticks
var subscription = new Subscription(DataType.Ticks, btcUsd);
connector.Subscribe(subscription);

connector.TickTradeReceived += (sub, tick) =>
{
    Console.WriteLine($"{tick.SecurityId.SecurityCode}: {tick.TradePrice} x {tick.TradeVolume}");
};
```

### Order Management

```csharp
// Create a limit order
var order = new Order
{
    Security = btcUsd,
    Portfolio = portfolio,
    Direction = Sides.Buy,
    Type = OrderTypes.Limit,
    Price = 50000m,
    Volume = 0.01m
};

// Register the order
connector.RegisterOrder(order);

// Track order status
connector.OrderReceived += (sub, order) =>
{
    Console.WriteLine($"Order {order.Id}: {order.State}");
};

// Cancel an order
connector.CancelOrder(order);
```

## Configuration

### API Credentials

Get your API credentials from the Coinbase Developer Portal:

1. Go to https://portal.cdp.coinbase.com/
2. Create a new API key
3. Download the credentials (API Key Name + Private Key)
4. Store them securely (use environment variables in production)

```bash
# Environment variables
export COINBASE_API_KEY_NAME="organizations/xxx/apiKeys/yyy"
export COINBASE_PRIVATE_KEY="-----BEGIN EC PRIVATE KEY-----..."
```

### Supported Markets

The connector automatically loads all available trading pairs from Coinbase, including:

- **Crypto pairs**: BTC-USD, ETH-USD, SOL-USD, etc.
- **Stablecoins**: USDC, USDT
- **Cross-crypto**: BTC-ETH, ETH-BTC, etc.

## Architecture

```
CoinbaseAdvancedConnector
├── Inherits from: StockSharp.Algo.Connector
├── Uses: Coinbase.AdvancedTrade.CoinbaseClient
├── Implements:
│   ├── JWT Authentication
│   ├── REST API calls (orders, accounts, products)
│   ├── WebSocket streaming (market data, order updates)
│   └── StockSharp message mapping
└── Provides:
    ├── Security (Product) management
    ├── Portfolio (Account) management
    ├── Order execution
    └── Real-time market data
```

## Message Mapping

| Coinbase Model | StockSharp Model | Notes |
|----------------|------------------|-------|
| Product | Security | Trading pairs (BTC-USD, etc.) |
| Account | Portfolio + Position | Account balances |
| Order | Order + ExecutionMessage | Order lifecycle |
| Fill | MyTrade | Trade executions |
| Ticker | ExecutionMessage (Tick) | Market data |

## Troubleshooting

### Connection Issues

**Error: "Invalid API key"**
- Verify your API Key Name format: `organizations/{org_id}/apiKeys/{key_id}`
- Ensure the private key is correctly formatted with line breaks

**Error: "Authentication failed"**
- Check that JWT signature is correct
- Verify system clock is synchronized (JWT uses timestamps)

### Market Data Not Received

- Ensure WebSocket connection is established
- Check that you've subscribed to the security
- Verify the product is trading (status = "online")

### Orders Not Executing

- Check portfolio has sufficient balance
- Verify price is within market bounds
- Ensure order size meets minimum/maximum limits

## Sample Application

See `Sample/Program.cs` for a complete working example.

## API Reference

### Constructor

```csharp
CoinbaseAdvancedConnector(string apiKeyName, string privateKey)
```

### Methods

- `Connect()` - Connects to Coinbase
- `Disconnect()` - Disconnects from Coinbase  
- `RegisterOrder(Order)` - Place a new order
- `CancelOrder(Order)` - Cancel an existing order
- `Subscribe(Subscription)` - Subscribe to market data

### Events

- `Connected` - Fired when connected
- `Disconnected` - Fired when disconnected
- `SecurityReceived` - New security loaded
- `PortfolioReceived` - Portfolio updated
- `OrderReceived` - Order status changed
- `OwnTradeReceived` - Trade executed
- `TickTradeReceived` - Market tick received

## Contributing

This connector is part of the StockSharp framework. For issues or contributions:

1. Fork the repository
2. Create a feature branch
3. Submit a pull request

## License

Same as StockSharp framework.

## Resources

- [StockSharp Documentation](https://doc.stocksharp.com/)
- [Coinbase Advanced Trade API](https://docs.cdp.coinbase.com/advanced-trade/docs/welcome)
- [Coinbase.AdvancedTrade Package](https://github.com/your-repo/Coinbase.AdvancedTrade)

## Support

For questions or support:
- StockSharp Forum: https://stocksharp.com/forum/
- GitHub Issues: https://github.com/StockSharp/StockSharp/issues
