using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Jules
{
    public sealed class Program
    {
        static void Main(string[] args)
        {
            using (var bot = new Bot(BuildConfiguration(args)))
            {
                bot.RunAsync().GetAwaiter().GetResult();
            } 
        }

        public static IConfiguration BuildConfiguration(string[] args) => new ConfigurationBuilder()
            .AddCommandLine(args)
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("config.json", true)
            .Build();
    }
}
