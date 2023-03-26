using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;

namespace DownloadFuelPricing
{
	public class FuelPriceService : IHostedService, IDisposable
	{
		private readonly IConfiguration _config;
		private readonly HttpClient _httpClient;
		private readonly Timer _timer;
		private bool _isRunning = false;

		public FuelPriceService(IConfiguration config, HttpClient httpClient)
		{
			_config = config;
			_httpClient = httpClient;
			_timer = new Timer(ExecuteTask, default, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			Console.WriteLine("FuelPriceService is starting.");

			var delay = int.Parse(_config["TaskExecutionDelay"] ?? "0");
			_timer.Change(TimeSpan.Zero, TimeSpan.FromDays(delay));
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			Console.WriteLine("FuelPriceService is stopping.");

			_timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

			return Task.CompletedTask;
		}

		public void Dispose()
		{
			_timer.Dispose();
		}

		private async void ExecuteTask(object? state)
		{
			if (_isRunning)
			{
				return;
			}
			_isRunning = true;

			Console.WriteLine("Downloading fuel prices...");

			try
			{
				var url = _config["FuelPriceApiUrl"];
				var daysCount = int.Parse(_config["DaysCount"] ?? "0");
				var result = await _httpClient.GetAsync(url);
				var content = await result.Content.ReadAsStringAsync();
				var json = JObject.Parse(content);

				var series = json["response"]["data"];
				var prices = new List<(DateTime period, decimal price)>();

				foreach (var item in series)
				{
					var period = (DateTime)item["period"];
					var price = (decimal)item["value"];

					if (DateTime.UtcNow.Subtract(period).TotalDays > daysCount)
					{
						continue;
					}

					prices.Add((period, price));
				}

				Console.WriteLine($"Found {prices.Count} fuel prices.");

				if (prices.Count > 0)
				{
					Console.WriteLine("Saving fuel prices to database...");

					var connectionString = _config["DatabaseConnectionString"];

					await using (var connection = new SqlConnection(connectionString))
					{
						await connection.OpenAsync();

						foreach (var (period, price) in prices)
						{
							var command = new SqlCommand(
								"INSERT INTO FuelPrices (Period, Price) SELECT @Period, @Price " +
								"WHERE NOT EXISTS (SELECT * FROM FuelPrices WHERE Period = @Period)"
								, connection);
							command.Parameters.AddWithValue("@Period", period);
							command.Parameters.AddWithValue("@Price", price);

							await command.ExecuteNonQueryAsync();
						}
					}

					Console.WriteLine("Fuel prices saved to database.");
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				throw;
			}
			finally
			{
				_isRunning = false;
			}
		
		}
	}
}