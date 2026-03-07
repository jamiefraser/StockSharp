namespace StockSharp.Samples.CrossPlatform.ConsoleApp;

using System;
using System.Linq;

using Ecng.Common;
using Ecng.Logging;

using Microsoft.Extensions.Configuration;

using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

static class Program
{
	private static void Main()
	{
		var configuration = new ConfigurationBuilder()
			.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
			.Build();

		var logger = new LogManager();
		logger.Listeners.Add(new ConsoleLogListener());

		var connector = new Connector();
		logger.Sources.Add(connector);

		//
		// !!! IMPORTANT !!!
		// The code show how to connect to Coinbase.
		// Use your own adapter and proper setup
		// !!! DO NOT FORGET ADD NUGET PACKAGE WITH REQUIRED CONNECTOR !!!
		// https://stocksharp.com/products/nuget_manual/#privateserver
		//
		var key = configuration["Coinbase:Key"] ?? "<Your key>";
		var secret = configuration["Coinbase:Secret"] ?? "<Your secret>";

		connector.Adapter.InnerAdapters.Add(new Coinbase.CoinbaseMessageAdapter(connector.TransactionIdGenerator)
		{
			Key = key.Secure(),
			Secret = secret.Secure(),
			//IsDemo = true
		});

		connector.ConnectionError += Console.WriteLine;
		connector.Error += Console.WriteLine;

		// clear all auto-subscriptions on connect
		// and send necessary subscriptions manually from code
		connector.SubscriptionsOnConnect.Clear();

		connector.Connect();

		//--------------------------Security--------------------------------------------------------------------------------
		Console.WriteLine("Securities:");
		Security security = null;
		connector.LookupSecuritiesResult += (message, securities, arg3) =>
		{
			foreach (var security1 in securities)
			{
				Console.WriteLine(security1);
			}

			security = securities.First();
		};
		connector.Subscribe(new(new SecurityLookupMessage { SecurityId = new() { SecurityCode = "BTC-USD" }, SecurityType = SecurityTypes.CryptoCurrency }));
		Console.ReadLine();

		//--------------------------Portfolio--------------------------------------------------------------------------------
		Console.WriteLine("Portfolios:");
		if (!connector.Portfolios.Any())
		{
			connector.PositionReceived += (subscription, position) =>
			{
				Console.WriteLine(position);
				connector.UnSubscribe(subscription);
			};

			connector.Subscribe(new(DataType.PositionChanges, security));
		}
		else
		{
			foreach (var connectorPortfolio in connector.Portfolios)
			{
				Console.WriteLine(connectorPortfolio);
			}
		}
		Console.ReadLine();

		IOrderBookMessage lastDepth = null;
		//--------------------------MarketDepth--------------------------------------------------------------------------------
		Console.WriteLine("MarketDepth (wait for prices):");
		connector.OrderBookReceived += (subscription, depth) =>
		{
			Console.WriteLine(depth.GetBestBid());
			Console.WriteLine(depth.GetBestAsk());

			connector.UnSubscribe(subscription);
			lastDepth = depth;
		};

		connector.Subscribe(new(DataType.MarketDepth, security));
		Console.ReadLine();

		////--------------------------Order--------------------------------------------------------------------------------
		Console.WriteLine("Order:");
		Console.Write("Do you want to buy 1? (Y/N)");

		var str = Console.ReadLine();
		if (str != null && str.ToUpper() != "Y") return;

		var bestBidPrice = lastDepth?.GetBestBid()?.Price;

		var portfolio = connector.Portfolios.FirstOrDefault();
		if (portfolio == null)
		{
			Console.WriteLine("No portfolios found. Cannot place order.");
			return;
		}

		var order = new Order
		{
			Security = security,
			Portfolio = portfolio,
			Price = bestBidPrice ?? 0,
			Type = bestBidPrice == null ? OrderTypes.Market : OrderTypes.Limit,
			Volume = 1m,
			Side = Sides.Buy,
		};

		connector.OrderReceived += (s, o) => Console.WriteLine(o);
		connector.RegisterOrder(order);

		Console.ReadLine();
		Console.ReadLine();
	}
}
