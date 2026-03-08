using System;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

using Ecng.Common;

using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.CoinbaseAdvanced;

namespace CoinbaseAdvanced.Sample
{
    /// <summary>
    /// Sample program demonstrating Coinbase Advanced Trade connector usage.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Coinbase Advanced Trade Connector Sample");
            Console.WriteLine("=========================================\n");

            // Replace with your actual API credentials
            var apiKeyName = Environment.GetEnvironmentVariable("COINBASE_API_KEY_NAME") 
                             ?? "your-api-key-name";
            var privateKey = Environment.GetEnvironmentVariable("COINBASE_PRIVATE_KEY") 
                             ?? "your-private-key";

            if (apiKeyName == "your-api-key-name" || privateKey == "your-private-key")
            {
                Console.WriteLine("ERROR: Please set COINBASE_API_KEY_NAME and COINBASE_PRIVATE_KEY environment variables");
                Console.WriteLine("Or modify the source code with your credentials.");
                return;
            }

            // Create connector with Coinbase Advanced adapter
            var connector = new Connector();

            var adapter = new CoinbaseAdvancedMessageAdapter(connector.TransactionIdGenerator)
            {
                Key = apiKeyName.To<SecureString>(),
                Secret = privateKey.To<SecureString>()
            };

            connector.Adapter.InnerAdapters.Add(adapter);

            // Subscribe to events
            connector.Connected += () =>
            {
                Console.WriteLine("✓ Connected to Coinbase");
            };

            connector.Disconnected += () =>
            {
                Console.WriteLine("✗ Disconnected from Coinbase");
            };

            connector.ConnectionError += ex =>
            {
                Console.WriteLine($"✗ Connection Error: {ex.Message}");
            };

            connector.SecurityReceived += (subscription, security) =>
            {
                Console.WriteLine($"Security: {security.Code} - {security.Name}");
            };

            connector.PortfolioReceived += (subscription, portfolio) =>
            {
                Console.WriteLine($"Portfolio: {portfolio.Name} - {portfolio.Currency}");
            };

            connector.PositionReceived += (subscription, position) =>
            {
                Console.WriteLine($"Position: {position.Security?.Code ?? "Unknown"} = {position.CurrentValue}");
            };

            connector.TickTradeReceived += (subscription, trade) =>
            {
                Console.WriteLine($"Tick: {trade.SecurityId.SecurityCode} @ {trade.Price} x {trade.Volume}");
            };

            connector.OrderReceived += (subscription, order) =>
            {
                Console.WriteLine($"Order: {order.Security.Code} {order.Side} {order.State} @ {order.Price} x {order.Volume}");
            };

            connector.OwnTradeReceived += (subscription, trade) =>
            {
                Console.WriteLine($"Own Trade: {trade.Order.Security.Code} @ {trade.Trade.Price} x {trade.Trade.Volume}");
            };

            try
            {
                // Connect
                Console.WriteLine("\nConnecting to Coinbase...");
                connector.Connect();

                // Wait for connection
                await Task.Delay(5000);

                // Check if we have any securities (indicates successful connection)
                if (connector.Securities.Any())
                {
                    Console.WriteLine($"\n✓ Successfully connected!");
                    Console.WriteLine($"Loaded {connector.Securities.Count()} securities");
                    Console.WriteLine($"Loaded {connector.Portfolios.Count()} portfolios");

                    // Example: Get BTC-USD security
                    var btcUsd = connector.Securities.FirstOrDefault(s => s.Code == "BTC-USD");
                    if (btcUsd != null)
                    {
                        Console.WriteLine($"\nBTC-USD Security Details:");
                        Console.WriteLine($"  Price Step: {btcUsd.PriceStep}");
                        Console.WriteLine($"  Volume Step: {btcUsd.VolumeStep}");
                        Console.WriteLine($"  Min Volume: {btcUsd.MinVolume}");
                        Console.WriteLine($"  Max Volume: {btcUsd.MaxVolume}");
                        Console.WriteLine($"  State: {btcUsd.State}");

                        // Subscribe to market data
                        Console.WriteLine("\nSubscribing to BTC-USD ticks...");
                        var subscription = new Subscription(DataType.Ticks, btcUsd);
                        connector.Subscribe(subscription);
                    }

                    // Keep running for demo
                    Console.WriteLine("\nPress any key to disconnect and exit...");
                    Console.ReadKey();
                }
                else
                {
                    Console.WriteLine($"\n✗ Connection failed. State: {connector.ConnectionState}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Disconnect
                Console.WriteLine("\nDisconnecting...");
                connector.Disconnect();
                await Task.Delay(1000);
                connector.Dispose();
            }

            Console.WriteLine("\nDone!");
        }
    }
}
