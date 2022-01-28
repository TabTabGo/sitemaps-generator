using System;

using System.IO;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TTG.SiteMap;
using TTG.SiteMap.Models;

namespace TTG.SiteMapsGenerator
{
    class Program
    {
        private static string _usage = @"Usage: SiteMapsGenerator.exe <config.json path> <output directory path>";

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine(_usage);
                return;
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine($"Config file not found: {args[0]}");
                Console.WriteLine(_usage);
                return;
            }

            if (!Directory.Exists(args[1]))
            {
                Console.WriteLine($"Output directory path not not found: {args[1]}");
                Console.WriteLine(_usage);
                return;
            }

            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                    services.AddTransient<ISelectQuery, MSSqlSelectQuery>()
                        .AddTransient<ISiteMapGenerator, SiteMapGenerator>()
                        )
                .ConfigureLogging(config =>
                {
                    config.ClearProviders();
                    config.AddConsole();
                })
                .Build();
            
            var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(args[0]), new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive =    true,
                Converters = {new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}
            });
            // loop on batches
            var output = args[1];
            
            var generator = host.Services.GetService<ISiteMapGenerator>();
            generator.Generate(output, config);
            
            
        }

        
    }
}