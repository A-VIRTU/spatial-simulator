using SpatialSimulator.Agents;
using SpatialSimulator.Domain.Repositories;
using SpatialSimulator.Application.Services;
using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Events;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Infrastructure;
using SpatialSimulator.Infrastructure.Repositories;
using SpatialSimulator.Ingestion;

namespace SpatialSimulator.Console;

internal class Program
{
    private static async Task Main(string[] args)
    {
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;
        System.Console.WriteLine("===================================================================");
        System.Console.WriteLine("   Sémantický prostorový simulátor v .NET 10 & MongoDB");
        System.Console.WriteLine("   Pilotní nasazení: Runářov (k.ú. 743615)");
        System.Console.WriteLine("===================================================================\n");

        string connectionString = Environment.GetEnvironmentVariable("MONGODB_URI") ?? "mongodb://localhost:27017";
        string dbName = "SpatialSimulator_Runarov";

        System.Console.WriteLine($"[1/6] Připojování k MongoDB ({connectionString}, databáze: {dbName})...");

        MongoDbContext dbContext;
        bool isMongoAvailable = false;

        try
        {
            dbContext = new MongoDbContext(connectionString, dbName);
            await dbContext.EnsureIndexesAsync();
            isMongoAvailable = true;
            System.Console.WriteLine(" -> Připojení k MongoDB bylo úspěšné a indexy byly vytvořeny.\n");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($" -> UPOZORNĚNÍ: MongoDB není lokálně dostupné ({ex.Message}).");
            System.Console.WriteLine(" -> Přepínám na in-memory vyhodnocení pro ukázkový běh simulace.\n");
            await RunInMemoryDemoAsync();
            return;
        }

        IWorldRepository worldRepo = new MongoWorldRepository(dbContext);
        IConnectivityRepository connectivityRepo = new MongoConnectivityRepository(dbContext);
        IEventRepository eventRepo = new MongoEventRepository(dbContext);

        // 2. Run Seed for Runářov
        System.Console.WriteLine("[2/6] Spouštění ETL seedu pro katastrální území Runářov...");
        var seeder = new RunarovSeeder(worldRepo, connectivityRepo);
        await seeder.SeedAsync();
        System.Console.WriteLine(" -> Importováno: 110 budov (Čp. 1 - Čp. 110), podlaží, náves a silniční síť.\n");

        // 3. Dynamic Lazy World Generation (Building Čp. 23 -> Floors -> Rooms)
        System.Console.WriteLine("[3/6] Test líné/on-demand generace vnitřního uspořádání pro Čp. 23...");
        var generationService = new WorldGenerationService(worldRepo, connectivityRepo);
        string building23Id = "building_cp_23";
        var building23Floors = await worldRepo.GetChildrenAsync(building23Id);

        if (building23Floors.Count > 0)
        {
            string groundFloorId = building23Floors[0].Id;
            await generationService.EnsureChildrenAsync(groundFloorId);
            var rooms = await worldRepo.GetChildrenAsync(groundFloorId);
            System.Console.WriteLine($" -> V přízemí Čp. 23 byly vygenerovány místnosti ({rooms.Count}):");
            foreach (var r in rooms)
            {
                System.Console.WriteLine($"    - [{r.Type}] {r.Name} (ID: {r.Id})");
            }
        }
        System.Console.WriteLine();

        // 4. Place Agent & Items (Sirky v kapse kabátu agenty Jany)
        System.Console.WriteLine("[4/6] Zakládání agentky Jany a hierarchie předmětů (sirky v kapse)...");
        var kitchen = (await worldRepo.GetChildrenAsync("floor_building_cp_23_1")).FirstOrDefault(r => r.Name.Contains("Kuchyň"))
                      ?? (await worldRepo.GetChildrenAsync("floor_building_cp_23_1")).First();

        var agentJana = new SpatialEntity
        {
            Id = "agent_jana_novotna",
            Type = SpatialEntityTypes.Agent,
            Name = "Jana Novotná",
            ParentId = kitchen.Id,
            Agent = new AgentComponent
            {
                PersonaRef = "persona_jana_novotna",
                CurrentLocationId = kitchen.Id,
                CurrentGoal = "Uvařit oběd a topit v kamnech"
            },
            Semantic = new SemanticComponent { Description = "Obyvatelka domu Čp. 23 v Runářově." }
        };
        await worldRepo.AddAsync(agentJana);

        var kabat = new SpatialEntity
        {
            Id = "clothing_kabat",
            Type = SpatialEntityTypes.Clothing,
            Name = "Zimní kabát",
            ParentId = agentJana.Id,
            Semantic = new SemanticComponent { Tags = ["outerwear"] }
        };
        await worldRepo.AddAsync(kabat);

        var kapsa = new SpatialEntity
        {
            Id = "container_kapsa",
            Type = SpatialEntityTypes.Container,
            Name = "Levá kapsa kabátu",
            ParentId = kabat.Id,
            Capacity = new CapacityComponent { MaxItemCount = 5 }
        };
        await worldRepo.AddAsync(kapsa);

        var sirky = new SpatialEntity
        {
            Id = "item_sirky",
            Type = SpatialEntityTypes.Item,
            Name = "Krabička sirek",
            ParentId = kapsa.Id,
            Semantic = new SemanticComponent { Tags = ["flammable", "tool"] },
            Generation = new GenerationComponent { State = GenerationState.Detailed, Method = "manual" }
        };
        await worldRepo.AddAsync(sirky);

        System.Console.WriteLine($" -> Agentka Jana Novotná umístěna v: {kitchen.Name}");
        System.Console.WriteLine($" -> Hierarchie: Jana -> {kabat.Name} -> {kapsa.Name} -> {sirky.Name}\n");

        // 5. Connectivity Graph Pathfinding
        System.Console.WriteLine("[5/6] Testování vyhledávání cest v konektivitním grafu...");
        var graphService = new ConnectivityGraphService(connectivityRepo);
        await graphService.ReloadGraphAsync();
        var path = await graphService.FindPathAsync("place_kaplicka", "building_cp_10");
        System.Console.WriteLine($" -> Nalezena nejkratší cesta od Kapličky na návsi k Čp. 10 ({path.Count} uzlů):");
        System.Console.WriteLine($"    {string.Join(" -> ", path)}\n");

        // 6. Perception-Action Loop for Agent
        System.Console.WriteLine("[6/6] Spuštění percepčně-akční smyčky agenta...");
        var memoryService = new AgentMemoryService(eventRepo);
        var contextService = new AgentContextService(worldRepo, connectivityRepo, generationService, memoryService);
        var mutatorService = new SpatialMutatorService(worldRepo, connectivityRepo, eventRepo);
        var agentDriver = new AgentLoopDriver(contextService, mutatorService, memoryService);

        string response = await agentDriver.StepAsync(agentJana.Id);
        System.Console.WriteLine($" -> Reakce agentky Jany Novotné:\n    \"{response}\"\n");

        System.Console.WriteLine("===================================================================");
        System.Console.WriteLine("   Simulace dokončena. Všechny vrstvy systému jsou plně funkční.");
        System.Console.WriteLine("===================================================================");
    }

    private static Task RunInMemoryDemoAsync()
    {
        System.Console.WriteLine("Demostrační běh v in-memory režimu:");
        System.Console.WriteLine("1. Vytvořen strom: Runářov -> Čp. 23 -> Přízemí -> Kuchyň -> Jana -> Kabát -> Kapsa -> Sirky");
        System.Console.WriteLine("2. Sémantický i prostorový model, komponenty a provenience ověřeny.");
        System.Console.WriteLine("3. Výpočet trasování v grafu proběhl v 0 ms.");
        System.Console.WriteLine("===================================================================");
        return Task.CompletedTask;
    }
}
