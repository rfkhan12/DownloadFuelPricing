using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DownloadFuelPricing
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = Host.CreateDefaultBuilder(args)
				.ConfigureAppConfiguration((hostingContext, config) =>
				{
					config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
				})
				.ConfigureServices((hostContext, services) =>
				{
					services.AddHttpClient();
					services.AddHostedService<FuelPriceService>();
				});
			var host = builder.Build();
			host.Run();
		}
	}
}