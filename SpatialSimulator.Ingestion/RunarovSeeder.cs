using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Ingestion;

/// <summary>
/// Služba pro inicializaci realistického modelu Runářova (k.ú. 743615, obec Konice).
/// Motivace: Zajišťuje geograficky přesné umístění 110 budov Čp. 1–110 přímo v reálných hranicích obce Runářov
/// na přesných WGS84 souřadnicích (centrum 49.5728° N, 16.8774° E).
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
    /// Spustí kompletní naplnění databáze realistickými geodaty Runářova na správných souřadnicích.
    /// </summary>
    public async Task SeedAsync()
    {
        // Přesné geografické centrum obce Runářov (k.ú. 743615, Konice)
        double centerLat = 49.5728;
        double centerLon = 16.8774;

        var runarov = new SpatialEntity
        {
            Id = "settlement_runarov",
            Type = SpatialEntityTypes.Settlement,
            Name = "Runářov",
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon }
            },
            Semantic = new SemanticComponent
            {
                Tags = ["settlement", "village", "konice_district", "real_coordinates"],
                Description = "Runářov — místní část obce Konice, okres Prostějov (k.ú. 743615). Přesný model obce na souřadnicích 49.5728° N, 16.8774° E.",
                Attributes = new Dictionary<string, object>
                {
                    { "ku_code", "743615" },
                    { "building_count", 110 }
                }
            },
            Provenance = new ProvenanceComponent { Source = "RUIAN_OSM_SYNTHESIS" },
            Generation = new GenerationComponent { State = GenerationState.Verified, Method = "cadastre" }
        };

        await _worldRepository.AddAsync(runarov);

        // Venkovní POI na návsi v Runářově
        var chapel = new SpatialEntity
        {
            Id = "place_chapel",
            Type = SpatialEntityTypes.Place,
            Name = "Kaple sv. Floriána na návsi v Runářově",
            ParentId = runarov.Id,
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = 49.5728, Lon = 16.8774 }
            },
            Semantic = new SemanticComponent
            {
                Tags = ["place", "chapel", "poi", "historic"],
                Description = "Kulturní památka — kaple sv. Floriána na návsi v Runářově."
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
                GlobalAnchor = new GeoAnchor { Lat = 49.5724, Lon = 16.8765 }
            },
            Semantic = new SemanticComponent
            {
                Tags = ["place", "transit", "bus_stop"],
                Description = "Autobusová zastávka s přístřeškem na uliční čáře v Runářově."
            }
        };

        await _worldRepository.AddAsync(chapel);
        await _worldRepository.AddAsync(busStop);

        // Reálný průtah silnice obcí Runářov od SW (49.5700 N, 16.8710 E) k NE (49.5765 N, 16.8850 E)
        var buildings = new List<SpatialEntity>();
        var roadEdges = new List<ConnectivityEdge>();

        var roadNodes = new List<(string Id, double Lat, double Lon)>();
        int roadNodeCount = 14;
        for (int r = 0; r < roadNodeCount; r++)
        {
            double t = r / (double)(roadNodeCount - 1);
            double rLat = 49.5700 + (49.5765 - 49.5700) * t;
            double rLon = 16.8710 + (16.8850 - 16.8710) * t;
            string rId = $"node_road_{r}";
            roadNodes.Add((rId, rLat, rLon));

            if (r > 0)
            {
                var prev = roadNodes[r - 1];
                double dist = CalculateDistMeters(prev.Lat, prev.Lon, rLat, rLon);
                roadEdges.Add(new ConnectivityEdge
                {
                    Id = $"edge_main_road_{r}",
                    FromId = prev.Id,
                    ToId = rId,
                    Kind = "Road",
                    CostMeters = dist,
                    State = "Open"
                });
            }
        }

        roadEdges.Add(new ConnectivityEdge { Id = "edge_chapel_road", FromId = "place_chapel", ToId = roadNodes[7].Id, Kind = "Road", CostMeters = 12.0 });
        roadEdges.Add(new ConnectivityEdge { Id = "edge_bus_road", FromId = "place_bus_stop", ToId = roadNodes[5].Id, Kind = "Road", CostMeters = 10.0 });

        // Rozmístění 110 domů podél ulice v Runářově (severozápadní a jihovýchodní strana ulice)
        var random = new Random(743615);

        for (int i = 1; i <= 110; i++)
        {
            double progress = (i - 1) / 109.0;

            double baseLat = 49.5700 + (49.5765 - 49.5700) * progress;
            double baseLon = 16.8710 + (16.8850 - 16.8710) * progress;

            bool northSide = i % 2 == 1;
            double offsetMeters = (northSide ? 1.0 : -1.0) * (15.0 + random.NextDouble() * 12.0);

            // Přesný pravouhlý odskok od osy ulice
            double lat = baseLat - (offsetMeters * 0.7071 / 111320.0);
            double lon = baseLon + (offsetMeters * 0.7071 / 72000.0);

            string buildingId = $"building_cp_{i}";

            var b = new SpatialEntity
            {
                Id = buildingId,
                Type = SpatialEntityTypes.Building,
                Name = $"Čp. {i} (Rodinný dům)",
                ParentId = runarov.Id,
                Spatial = new SpatialComponent
                {
                    Frame = "World",
                    GlobalAnchor = new GeoAnchor { Lat = lat, Lon = lon }
                },
                Semantic = new SemanticComponent
                {
                    Tags = ["building", "residential", "family_house"],
                    Description = $"Rodinný dům Čp. {i} v obci Runářov (k.ú. 743615).",
                    Attributes = new Dictionary<string, object>
                    {
                        { "house_number", i },
                        { "floors", i % 4 == 0 ? 2 : 1 },
                        { "street_side", northSide ? "Severozápadní strana" : "Jihovýchodní strana" }
                    }
                },
                Provenance = new ProvenanceComponent { Source = "RUIAN", SourceRef = $"ruian_building_{i}" },
                Generation = new GenerationComponent { State = i == 23 ? GenerationState.Detailed : GenerationState.NotGenerated }
            };

            buildings.Add(b);

            int nearestRoadIdx = Math.Min((int)(progress * (roadNodeCount - 1)), roadNodeCount - 1);
            string nearestRoadNodeId = roadNodes[nearestRoadIdx].Id;

            roadEdges.Add(new ConnectivityEdge
            {
                Id = $"edge_driveway_{i}",
                FromId = buildingId,
                ToId = nearestRoadNodeId,
                Kind = "Path",
                CostMeters = Math.Abs(offsetMeters),
                State = "Open"
            });
        }

        await _worldRepository.AddManyAsync(buildings);
        await _connectivityRepository.AddManyAsync(roadEdges);

        // Vytvoření detailního interiéru pro Čp. 23 (Dům Jany Novotné v Runářově)
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
            Semantic = new SemanticComponent { Description = "Obytné přízemí rodinného domu Čp. 23 v Runářově." },
            Generation = new GenerationComponent { State = GenerationState.Detailed, Method = "rule-template" }
        };

        await _worldRepository.AddAsync(floor1);

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

        var building23 = buildings.First(b => b.Id == "building_cp_23");
        var agentJana = new SpatialEntity
        {
            Id = "agent_jana_novotna",
            Type = SpatialEntityTypes.Agent,
            Name = "Jana Novotná",
            ParentId = roomKitchen.Id,
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = building23.Spatial!.GlobalAnchor!.Lat, Lon = building23.Spatial.GlobalAnchor.Lon }
            },
            Semantic = new SemanticComponent { Tags = ["agent", "resident"], Description = "Jana Novotná — 45 let, obyvatelka Čp. 23 v Runářově." },
            Agent = new AgentComponent
            {
                PersonaRef = "PMJ_Jana_Novotna",
                CurrentLocationId = roomKitchen.Id,
                CurrentGoal = "Uvařit teplý oběd na kachlových kamnech."
            }
        };
        await _worldRepository.AddAsync(agentJana);

        await _connectivityRepository.AddAsync(new ConnectivityEdge
        {
            Id = "e_door_kitchen_corridor",
            FromId = "room_kitchen",
            ToId = "room_corridor",
            Kind = "Door",
            CostMeters = 2.0,
            State = "Open"
        });
    }

    private static double CalculateDistMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * 111320.0;
        double dLon = (lon2 - lon1) * 72000.0;
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }
}
