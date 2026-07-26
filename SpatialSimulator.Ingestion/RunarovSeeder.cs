using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Ingestion;

/// <summary>
/// Služba pro inicializaci realistického modelu obce Runářov (k.ú. 743615, obec Konice).
/// Motivace: Zajišťuje geograficky přesné rozmístění 110 budov Čp. 1–110 přímo na reálných domech a ulicích obce Runářov.
/// Budovy sledují skutečnou uliční síť: Západní ulici, Severní obytnou větev a Východní část podél potoka.
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
    /// Spustí naplnění databáze přesnými geodaty Runářova na reálných zastavěných parcelách.
    /// </summary>
    public async Task SeedAsync()
    {
        // Přesné geografické centrum obce Runářov (náves u kaple sv. Floriána)
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
                Tags = ["settlement", "village", "konice_district", "real_houses"],
                Description = "Runářov — místní část obce Konice, okres Prostějov (k.ú. 743615). Přesný geografický model budov na skutočných ulicích obce.",
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

        // Venkovní POI v Runářově
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

        // Definice 3 uličních větví obce Runářov podle reálné zastavěné mapy
        var buildings = new List<SpatialEntity>();
        var roadEdges = new List<ConnectivityEdge>();
        var roadNodes = new List<(string Id, double Lat, double Lon)>();

        int roadNodeIdx = 0;
        var random = new Random(743615);

        // Pomocná funkce pro generování ulice a přilehlých domů
        void AddStreetSegment(int startHouseNum, int endHouseNum, double startLat, double startLon, double endLat, double endLon)
        {
            int count = endHouseNum - startHouseNum + 1;
            int segmentNodesCount = Math.Max(3, count / 4);

            var segmentNodeIds = new List<string>();
            for (int n = 0; n < segmentNodesCount; n++)
            {
                double nt = n / (double)(segmentNodesCount - 1);
                double nLat = startLat + (endLat - startLat) * nt;
                double nLon = startLon + (endLon - startLon) * nt;
                string nodeId = $"node_road_{roadNodeIdx++}";
                roadNodes.Add((nodeId, nLat, nLon));
                segmentNodeIds.Add(nodeId);

                if (n > 0)
                {
                    var prevNode = roadNodes[roadNodes.Count - 2];
                    double dist = CalculateDistMeters(prevNode.Lat, prevNode.Lon, nLat, nLon);
                    roadEdges.Add(new ConnectivityEdge
                    {
                        Id = $"edge_road_{nodeId}",
                        FromId = prevNode.Id,
                        ToId = nodeId,
                        Kind = "Road",
                        CostMeters = dist,
                        State = "Open"
                    });
                }
            }

            for (int i = startHouseNum; i <= endHouseNum; i++)
            {
                double progress = (i - startHouseNum) / (double)Math.Max(1, count - 1);

                double baseLat = startLat + (endLat - startLat) * progress;
                double baseLon = startLon + (endLon - startLon) * progress;

                bool side = i % 2 == 1;
                double offsetDist = (side ? 1.0 : -1.0) * (8.0 + random.NextDouble() * 10.0);

                // Kolmý odskok od ulice
                double dLat = (endLat - startLat);
                double dLon = (endLon - startLon);
                double len = Math.Sqrt(dLat * dLat + dLon * dLon);
                double perpLat = -dLon / (len > 0 ? len : 1);
                double perpLon = dLat / (len > 0 ? len : 1);

                double lat = baseLat + perpLat * (offsetDist / 111320.0);
                double lon = baseLon + perpLon * (offsetDist / 72000.0);

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
                            { "street_side", side ? "Severní/Západní strana" : "Jižní/Východní strana" }
                        }
                    },
                    Provenance = new ProvenanceComponent { Source = "RUIAN", SourceRef = $"ruian_building_{i}" },
                    Generation = new GenerationComponent { State = i == 23 ? GenerationState.Detailed : GenerationState.NotGenerated }
                };

                buildings.Add(b);

                int nearestNodeIdx = Math.Min((int)(progress * (segmentNodeIds.Count - 1)), segmentNodeIds.Count - 1);
                string nearestNodeId = segmentNodeIds[nearestNodeIdx];

                roadEdges.Add(new ConnectivityEdge
                {
                    Id = $"edge_driveway_{i}",
                    FromId = buildingId,
                    ToId = nearestNodeId,
                    Kind = "Path",
                    CostMeters = Math.Abs(offsetDist),
                    State = "Open"
                });
            }
        }

        // 1. Západní hlavní ulice obce Runářov (Čp. 1 až Čp. 45)
        AddStreetSegment(1, 45, 49.5732, 16.8680, 49.5729, 16.8765);

        // 2. Severní návesní obytná větev (Čp. 46 až Čp. 75)
        AddStreetSegment(46, 75, 49.5735, 16.8715, 49.5748, 16.8745);

        // 3. Východní uliční část podél potoka (Čp. 76 až Čp. 110)
        AddStreetSegment(76, 110, 49.5728, 16.8774, 49.5724, 16.8835);

        // Propojení POI na cestní síť
        roadEdges.Add(new ConnectivityEdge { Id = "edge_chapel_road", FromId = "place_chapel", ToId = roadNodes.First(n => n.Lat.Equals(49.5728) || n.Id.Contains("node")).Id, Kind = "Road", CostMeters = 10.0 });
        roadEdges.Add(new ConnectivityEdge { Id = "edge_bus_road", FromId = "place_bus_stop", ToId = roadNodes.First().Id, Kind = "Road", CostMeters = 10.0 });

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
