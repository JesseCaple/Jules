using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Jules.PluralKit;
using Microsoft.Extensions.Logging;

namespace Jules
{
    public sealed class Bot : IDisposable
    {
        private readonly IConfiguration configuration;

        private IServiceProvider services;
        private DiscordSocketClient client;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private CommandService commands;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public Bot(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task RunAsync()
        {
            this.services = ConfigureServices();
            this.commands = await ConfigureCommandsAsync();

            this.client = services.GetRequiredService<IDiscordClient>() as DiscordSocketClient;
            this.client.Log += msg => LegacyLog<DiscordSocketClient>(msg);
            this.client.MessageReceived += MessageReceived;

            var config = services.GetRequiredService<JulesConfig>();
            if (string.IsNullOrEmpty(config.Token))
            {
                services.GetRequiredService<ILogger<Bot>>()
                    .LogError("Bot", "Bot token must be set in configuration file.");
                return;
            }

            using (var exitTokenSource = new CancellationTokenSource())
            {
                Console.CancelKeyPress += delegate (object e, ConsoleCancelEventArgs args)
                {
                    args.Cancel = true;
                    exitTokenSource.Cancel();
                };

                await client.LoginAsync(TokenType.Bot, config.Token);
                await client.StartAsync();

                try
                {
                    await Task.Delay(Timeout.Infinite, exitTokenSource.Token);
                }
                catch (TaskCanceledException) { }
                services.GetRequiredService<ILogger<Bot>>()
                    .LogInformation("Bot", "Shutting Down");
            }
        }

        private IServiceProvider ConfigureServices() => new ServiceCollection()
            .AddLogging(c => c.AddConsole())
            .AddTransient(_ => this.configuration.GetSection("Jules").Get<JulesConfig>() ?? new JulesConfig())
            .AddSingleton<IDiscordClient, DiscordSocketClient>(_ => new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 10
            }))
            .AddTransient<WebhookExecutorService>()
            .AddSingleton<WebhookCacheService>()
            .AddSingleton<FronterCache>()
            .AddTransient<FronterProxyService>()
            .AddTransient<PluralKitAPIService>()
            .AddMemoryCache()
            .BuildServiceProvider(true);


        private async Task<CommandService> ConfigureCommandsAsync()
        {
            var commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Info,
                CaseSensitiveCommands = false,
            });
            await commands.AddModuleAsync<FronterCommands>(services);
            await commands.AddModuleAsync<BasicCommands>(services);
            commands.Log += msg => LegacyLog<CommandService>(msg);
            return commands;
        }

        private Task LegacyLog<T>(LogMessage message)
        {
            var logLevel = LogLevel.None;
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                    logLevel = LogLevel.Critical;
                    break;
                case LogSeverity.Error:
                    logLevel = LogLevel.Error;
                    break;
                case LogSeverity.Warning:
                    logLevel = LogLevel.Warning;
                    break;
                case LogSeverity.Info:
                    logLevel = LogLevel.Information;
                    break;
                case LogSeverity.Verbose:
                    logLevel = LogLevel.Trace;
                    break;
                case LogSeverity.Debug:
                    logLevel = LogLevel.Debug;
                    break;
            }
            var logger = services.GetRequiredService<ILogger<T>>();
            logger.Log(logLevel, message.Exception, message.Message);
            return Task.CompletedTask;
        }

        private async Task MessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot) return;

            await services.GetRequiredService<FronterProxyService>().HandleMessage(message);

            if (message.Content.StartsWith('.'))
            {
                var context = new SocketCommandContext(client, (SocketUserMessage) message);
                var result = await commands.ExecuteAsync(context, 1, services);
                if (!result.IsSuccess)
                {
                    if (result.Error != CommandError.UnknownCommand)
                    {
                        services.GetRequiredService<ILogger<Bot>>().LogError(result.ErrorReason);
                        await message.Channel.SendMessageAsync(result.ErrorReason);
                    }
                }
            }
            else if (message.Content.StartsWith("https://open.spotify", StringComparison.OrdinalIgnoreCase) 
                || message.Content.StartsWith("https://play.google.com/music", StringComparison.OrdinalIgnoreCase) 
                || message.Content.StartsWith("https://music.apple", StringComparison.OrdinalIgnoreCase))
            {
                string content = "> Universal Link: https://songwhip.com/convert?url=" + message.Content;
                await message.Channel.SendMessageAsync(content);
            }
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
