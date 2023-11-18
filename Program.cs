using MailKit.Net.Imap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Rwb.ImapCommandReceiver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //System.Console.WriteLine("-- Imap --");
            //System.Console.WriteLine($"DOTNET_ENVIRONMENT = {Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}");

            using (IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging((context, cfg) =>
                {
                    cfg.ClearProviders();
                    cfg.AddConfiguration(context.Configuration.GetSection("Logging"));
                    cfg.AddSimpleConsole(configure =>
                    {
                        configure.IncludeScopes = true;
                        configure.SingleLine = true;
                        configure.TimestampFormat = "dd MMM HH:mm ";
                    });
                })
                .AddAppsettingsWithAspNetCoreEnvironment()
                .AddImapCommandReceiver()
                .Build()
                )

            {
                using (IServiceScope scope = host.Services.CreateScope())
                {
                    ImapCommandReceiver r = scope.ServiceProvider.GetRequiredService<ImapCommandReceiver>();
                    r.RunAsync().Wait();
                }
            }
            //System.Console.WriteLine("Done.");
        }
    }

    public static class StartupExtensions
    {
        public static IHostBuilder AddAppsettingsWithAspNetCoreEnvironment(this IHostBuilder builder)
        {
            return builder.ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json");

                string? env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
                if (!string.IsNullOrEmpty(env))
                {
                    cfg.AddJsonFile($"appsettings.{env}.json", true);
                }
            });
        }

        public static IHostBuilder AddImapCommandReceiver(this IHostBuilder builder)
        {
            return builder.ConfigureServices((context, services) =>
            {
                services.AddScoped<ImapCommandReceiver>();
                services.ConfigureOptions<ImapCommandReceiverOptions>(context.Configuration);
                services.AddScoped<MailkitLogger>();
                services.AddScoped<ImapClient>((IServiceProvider serviceProvicer) =>
                {
                    return new ImapClient(serviceProvicer.GetRequiredService<MailkitLogger>());
                });
            });
        }

        public static IServiceCollection ConfigureOptions<T>(this IServiceCollection services, IConfiguration configuration) where T : class
        {
            string className = typeof(T).Name;
            if (!className.EndsWith("Options"))
            {
                throw new Exception($"Cannot configure appsettings section for class {className} because the class name does not end with 'Options'.");
            }

            string configurationSectionName = className.Substring(0, className.Length - "Options".Length);
            IConfigurationSection configSection = configuration.GetSection(configurationSectionName);
            if (configSection == null) // configSection.Value is not populated at this point! https://stackoverflow.com/questions/46017593/configuration-getsection-always-returns-value-property-null
            {
                throw new NullReferenceException($"Configuration section named {configurationSectionName} (to bind to class {className}) is required but got null.");
            }

            services.Configure<T>(configSection);
            return services;
        }

    }
}
