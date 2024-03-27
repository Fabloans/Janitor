using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Janitor.Handler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Janitor
{
    internal class JanitorCore
    {
        private readonly IConfiguration _config;

        private DiscordSocketClient _client;

        public JanitorCore()
        {
            var _builder = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(path: "config.json")
                    .AddEnvironmentVariables();

            _config = _builder.Build();
        }

        public async Task MainAsync()
        {
            using (var services = ConfigureServices())
            {
                var client = services.GetRequiredService<DiscordSocketClient>();
                _client = client;

                services.GetRequiredService<HandleEvents>();

                client.Log += LogAsync;
                client.Ready += ReadyAsync;

                await client.LoginAsync(TokenType.Bot,
                    _config["Token"]);
                await client.StartAsync();

                await Task.Delay(Timeout.Infinite);
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine();
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        {
            Console.WriteLine();
            Console.WriteLine($"Connected as -> {_client.CurrentUser}");
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                    .AddSingleton(_config)
                    .AddSingleton(x => new DiscordSocketClient(new DiscordSocketConfig()
                    {
                        GatewayIntents = GatewayIntents.All
                    }))
                    .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                    .AddSingleton(x => new HandleEvents(x.GetRequiredService<DiscordSocketClient>()))
                    .AddSingleton<HandleEvents>()
                    .BuildServiceProvider();
        }

    }
}
