using MongoDB.Bson;
using MongoDB.Driver;
using SpatialSimulator.Domain.Repositories;
using SpatialSimulator.Infrastructure;
using SpatialSimulator.Infrastructure.Repositories;
using SpatialSimulator.Ingestion;
using SpatialSimulator.Ingestion.Tools;

namespace SpatialSimulator.Cli;

/// <summary>
/// Samostatný CLI nástroj pro nezávislé stahování surových geodat ze zdrojů a offline syntézu do MongoDB.
/// Motivace: Odděluje fázi síťového stahování od fázi syntézy tak, aby se syntéza dala spouštět opakovaně a s různým nastavením z lokálních souborů.
/// </summary>
public class Program
{
    /// <summary>
    /// Vstupní bod CLI konzolové aplikace.
    /// </summary>
    public static async Task Main(string[] args)
    {
        string command = args.Length > 0 ? args[0].ToLowerInvariant() : "all";

        Console.WriteLine("==================================================");
        Console.WriteLine("  SÉMANTICKÝ PROSTOROVÝ SIMULÁTOR — CLI TOOL");
        Console.WriteLine("==================================================");

        if (command == "download" || command == "all")
        {
            await DownloadSourceData.Main(args);
        }

        if (command == "synthesize" || command == "all")
        {
            await SynthesizeFromLocalFilesAsync();
        }
    }

    /// <summary>
    /// Načte lokálně uložené surové soubory z `SpatialSimulator.Ingestion/Data/sources/` a provede jejich syntézu do databáze MongoDB.
    /// </summary>
    public static async Task SynthesizeFromLocalFilesAsync()
    {
        Console.WriteLine("\n[Synthesizer] Zahajuji offline syntézu z lokálních souborů...");

        string connectionString = "mongodb://localhost:27017";
        string dbName = "SpatialSimulator_Runarov";

        MongoDbContext? dbContext = null;
        try
        {
            var client = new MongoClient(connectionString);
            var pingTask = client.GetDatabase(dbName).RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            if (await Task.WhenAny(pingTask, Task.Delay(2000)) == pingTask)
            {
                await pingTask;
                dbContext = new MongoDbContext(connectionString, dbName);
                await dbContext.EnsureIndexesAsync();

                // Kompletní vyčištění MongoDB pro novou offline syntézu
                await dbContext.Entities.DeleteManyAsync(_ => true);
                await dbContext.Edges.DeleteManyAsync(_ => true);
                Console.WriteLine("[Synthesizer] Databáze MongoDB vyčištěna pro novou syntézu.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Synthesizer] MongoDB nedostupné: {ex.Message}. Syntéza proběhne do paměťového repozitáře.");
        }

        IWorldRepository worldRepo = dbContext != null ? new MongoWorldRepository(dbContext) : new InMemoryWorldRepository();
        IConnectivityRepository connRepo = dbContext != null ? new MongoConnectivityRepository(dbContext) : new InMemoryConnectivityRepository();

        string sourcesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SpatialSimulator.Ingestion", "Data", "sources");
        sourcesDir = Path.GetFullPath(sourcesDir);

        var realSeeder = new RealRunarovSeeder(worldRepo, connRepo);
        await realSeeder.SeedRealRunarovAsync(sourcesDir);

        Console.WriteLine("[Synthesizer] Offline syntéza kompletní. Model Runářova uvožen do MongoDB.");
    }
}
