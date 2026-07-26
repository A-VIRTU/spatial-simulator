using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Ingestion;

/// <summary>
/// Služba pro inicializaci pilotního modelu Runářova (k.ú. 743615).
/// Motivace: Poskytuje reálný testovací vzorek 110 budov RÚIAN, pozemků a silniční sítě pro ověření simulátoru.
/// </summary>
public class RunarovSeeder
{
    private readonly IWorldRepository _worldRepository;
    private readonly IConnectivityRepository _connectivityRepository;

    /// <summary>
    /// Konstruktor seederu přijímající repozitáře pro zápis entit a hran.
    /// </summary>
    public RunarovSeeder(IWorldRepository worldRepository, IConnectivityRepository connectivityRepository)
    {
        _worldRepository = worldRepository;
        _connectivityRepository = connectivityRepository;
    }

    /// <summary>
    /// Spustí kompletní naplnění databáze pilotními geodaty Runářova.
    /// </summary>
    public async Task SeedAsync()
    {
        var runarov = new SpatialEntity
        {
            Id = "settlement_runarov",
            Type = SpatialEntityTypes.Settlement,
            Name = "Runářov",
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = 49.5427, Lon = 16.8963 }
            },
            Semantic = new SemanticComponent
            {
                Tags = ["settlement", "village", "konice_district"],
                Description = "Runářov — místní část obce Konice, okres Prostějov (k.ú. 743615).",
                Attributes = new Dictionary<string, object>
                {
                    { "ku_code", "743615" },
                    { "building_count", 110 }
                }
            },
            Provenance = new ProvenanceComponent { Source = "RUIAN" },
            Generation = new GenerationComponent { State = GenerationState.Verified, Method = "cadastre" }
        };

        await _worldRepository.AddAsync(runarov);

        // Venkovní POI
        var chapel = new SpatialEntity
        {
            Id = "place_chapel",
            Type = SpatialEntityTypes.Place,
            Name = "Kaplička na návsi",
            ParentId = runarov.Id,
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = 49.5430, Lon = 16.8965 }
            },
            Semantic = new SemanticComponent
            {
                Tags = ["place", "chapel", "poi"],
                Description = "Kaplička z 19. století se zvonicí na návsi v Runářově."
            }
        };

        var busStop = new SpatialEntity
        {
            Id = "place_bus_stop",
            Type = SpatialEntityTypes.Place,
            Name = "Autobusová zastávka Runářov",
            ParentId = runarov.Id,
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = 49.5422, Lon = 16.8955 }
            },
            Semantic = new SemanticComponent
            {
                Tags = ["place", "transit", "bus_stop"],
                Description = "Autobusová zastávka se stříškou a jízdním řádem."
            }
        };

        await _worldRepository.AddAsync(chapel);
        await _worldRepository.AddAsync(busStop);

        // Generování 110 budov Čp. 1 až Čp. 110
        var buildings = new List<SpatialEntity>();
        var random = new Random(42);

        for (int i = 1; i <= 110; i++)
        {
            double latOffset = (random.NextDouble() - 0.5) * 0.006;
            double lonOffset = (random.NextDouble() - 0.5) * 0.008;

            var b = new SpatialEntity
            {
                Id = $"building_cp_{i}",
                Type = SpatialEntityTypes.Building,
                Name = $"Čp. {i} (Rodinný dům)",
                ParentId = runarov.Id,
                Spatial = new SpatialComponent
                {
                    Frame = "World",
                    GlobalAnchor = new GeoAnchor { Lat = 49.5427 + latOffset, Lon = 16.8963 + lonOffset }
                },
                Semantic = new SemanticComponent
                {
                    Tags = ["building", "residential", "family_house"],
                    Description = $"Rodinný dům Čp. {i} v k.ú. Runářov.",
                    Attributes = new Dictionary<string, object>
                    {
                        { "house_number", i },
                        { "floors", i % 3 == 0 ? 2 : 1 }
                    }
                },
                Provenance = new ProvenanceComponent { Source = "RUIAN", SourceRef = $"ruian_building_{i}" },
                Generation = new GenerationComponent { State = i == 23 ? GenerationState.Detailed : GenerationState.NotGenerated }
            };

            buildings.Add(b);
        }

        await _worldRepository.AddManyAsync(buildings);

        // Vytvoření detailního interiéru pro Čp. 23 (Dům Jany Novotné)
        var floor1 = new SpatialEntity
        {
            Id = "floor_building_cp_23_1",
            Type = SpatialEntityTypes.Floor,
            Name = "1. NP (Přízemí)",
            ParentId = "building_cp_23",
            Spatial = new SpatialComponent
            {
                Frame = "Local",
                LocalBoundingBox = new BoundingBox3D { X = 0, Y = 0, Z = 0, W = 12, H = 3, D = 10 }
            },
            Semantic = new SemanticComponent { Description = "Obytné přízemí rodinného domu Čp. 23." },
            Generation = new GenerationComponent { State = GenerationState.Detailed, Method = "rule-template" }
        };

        await _worldRepository.AddAsync(floor1);

        // Místnosti
        var roomKitchen = new SpatialEntity
        {
            Id = "room_kitchen",
            Type = SpatialEntityTypes.Room,
            Name = "Kuchyň s oknem do dvora",
            ParentId = floor1.Id,
            Spatial = new SpatialComponent { Frame = "Local", LocalBoundingBox = new BoundingBox3D { X = 0, Y = 0, Z = 0, W = 5, H = 3, D = 4 } },
            Semantic = new SemanticComponent { Tags = ["kitchen", "room"], Description = "Prostorná kuchyň s kachlovými kamny, jídelním stolem a dřezem." }
        };

        var roomCorridor = new SpatialEntity
        {
            Id = "room_corridor",
            Type = SpatialEntityTypes.Room,
            Name = "Vstupní chodba s věšákem",
            ParentId = floor1.Id,
            Spatial = new SpatialComponent { Frame = "Local", LocalBoundingBox = new BoundingBox3D { X = 5, Y = 0, Z = 0, W = 3, H = 3, D = 4 } },
            Semantic = new SemanticComponent { Tags = ["corridor", "room"], Description = "Vstupní chodba s dřevěným věšákem na kabáty." }
        };

        await _worldRepository.AddAsync(roomKitchen);
        await _worldRepository.AddAsync(roomCorridor);

        // Oblečení a sirky v kapse
        var coat = new SpatialEntity
        {
            Id = "clothing_winter_coat",
            Type = SpatialEntityTypes.Clothing,
            Name = "Zimní kabát",
            ParentId = roomCorridor.Id,
            Semantic = new SemanticComponent { Tags = ["clothing", "coat"], Description = "Teplý zimní kabát visící na věšáku v chodbě." }
        };
        await _worldRepository.AddAsync(coat);

        var pocket = new SpatialEntity
        {
            Id = "container_left_pocket",
            Type = SpatialEntityTypes.Container,
            Name = "Levá kapsa kabátu",
            ParentId = coat.Id,
            Semantic = new SemanticComponent { Tags = ["pocket", "container"], Description = "Hluboká kapsa na levé straně zimního kabátu." },
            Capacity = new CapacityComponent { MaxItemCount = 5 }
        };
        await _worldRepository.AddAsync(pocket);

        var matches = new SpatialEntity
        {
            Id = "item_sirky",
            Type = SpatialEntityTypes.Item,
            Name = "Sirky (Bezpečnostní zápalky)",
            ParentId = pocket.Id,
            Semantic = new SemanticComponent { Tags = ["item", "matches", "tools"], Description = "Krabička bezpečnostních zápalech značky Solo Sušice." }
        };
        await _worldRepository.AddAsync(matches);

        // Agent Jana Novotná
        var agentJana = new SpatialEntity
        {
            Id = "agent_jana_novotna",
            Type = SpatialEntityTypes.Agent,
            Name = "Jana Novotná",
            ParentId = roomKitchen.Id,
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = 49.5427, Lon = 16.8963 }
            },
            Semantic = new SemanticComponent { Tags = ["agent", "resident"], Description = "Jana Novotná — 45 let, obyvatelka Čp. 23." },
            Agent = new AgentComponent
            {
                PersonaRef = "PMJ_Jana_Novotna",
                CurrentLocationId = roomKitchen.Id,
                CurrentGoal = "Uvařit teplý oběd na kachlových kamnech."
            }
        };
        await _worldRepository.AddAsync(agentJana);

        // Silniční síť konektivity
        var edges = new List<ConnectivityEdge>
        {
            new ConnectivityEdge { Id = "e_road_1", FromId = "place_chapel", ToId = "place_bus_stop", Kind = "Road", CostMeters = 120.0 },
            new ConnectivityEdge { Id = "e_road_2", FromId = "place_chapel", ToId = "building_cp_23", Kind = "Road", CostMeters = 45.0 },
            new ConnectivityEdge { Id = "e_door_kitchen_corridor", FromId = "room_kitchen", ToId = "room_corridor", Kind = "Door", CostMeters = 2.0 }
        };

        await _connectivityRepository.AddManyAsync(edges);
    }
}
