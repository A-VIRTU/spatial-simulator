using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Ingestion;

public class RunarovSeeder
{
    private readonly IWorldRepository _worldRepo;
    private readonly IConnectivityRepository _connectivityRepo;

    public RunarovSeeder(IWorldRepository worldRepo, IConnectivityRepository connectivityRepo)
    {
        _worldRepo = worldRepo;
        _connectivityRepo = connectivityRepo;
    }

    public async Task SeedAsync()
    {
        var settlement = new SpatialEntity
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
                Attributes = new Dictionary<string, object> { { "ku_code", "743615" }, { "building_count", 110 } }
            },
            Generation = new GenerationComponent { State = GenerationState.Verified, Method = "cadastre" },
            Provenance = new ProvenanceComponent { Source = "RUIAN", Confidence = 1.0 }
        };

        await _worldRepo.AddAsync(settlement);

        var navesArea = new SpatialEntity
        {
            Id = "area_naves",
            Type = SpatialEntityTypes.Area,
            Name = "Náves Runářov",
            ParentId = settlement.Id,
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = 49.5428, Lon = 16.8965 }
            },
            Semantic = new SemanticComponent
            {
                Tags = ["area", "village_square"],
                Description = "Centrální náves Runářova s kapličkou a autobusovou zastávkou."
            },
            Generation = new GenerationComponent { State = GenerationState.Detailed, Method = "osm" },
            Provenance = new ProvenanceComponent { Source = "OSM", Confidence = 0.95 }
        };

        await _worldRepo.AddAsync(navesArea);

        var kaplickaPlace = new SpatialEntity
        {
            Id = "place_kaplicka",
            Type = SpatialEntityTypes.Place,
            Name = "Kaplička na návsi",
            ParentId = navesArea.Id,
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = 49.5429, Lon = 16.8964 }
            },
            Semantic = new SemanticComponent
            {
                Tags = ["place", "chapel", "poi"],
                Description = "Historická návesní kaplička."
            },
            Generation = new GenerationComponent { State = GenerationState.Verified, Method = "osm" }
        };

        var zastavkaPlace = new SpatialEntity
        {
            Id = "place_zastavka",
            Type = SpatialEntityTypes.Place,
            Name = "Autobusová zastávka Runářov",
            ParentId = navesArea.Id,
            Spatial = new SpatialComponent
            {
                Frame = "World",
                GlobalAnchor = new GeoAnchor { Lat = 49.5426, Lon = 16.8967 }
            },
            Semantic = new SemanticComponent
            {
                Tags = ["place", "bus_stop", "transport"],
                Description = "Zastávka příměstských autobusových linek."
            },
            Generation = new GenerationComponent { State = GenerationState.Verified, Method = "osm" }
        };

        await _worldRepo.AddManyAsync([kaplickaPlace, zastavkaPlace]);

        var buildings = new List<SpatialEntity>();
        var floors = new List<SpatialEntity>();

        for (int i = 1; i <= 110; i++)
        {
            double latOffset = 49.5400 + ((i % 11) * 0.0005);
            double lonOffset = 16.8900 + ((i / 11) * 0.0008);
            int floorCount = (i % 5 == 0) ? 2 : 1;

            string bId = $"building_cp_{i}";
            var building = new SpatialEntity
            {
                Id = bId,
                Type = SpatialEntityTypes.Building,
                Name = $"Čp. {i}",
                ParentId = settlement.Id,
                Spatial = new SpatialComponent
                {
                    Frame = "World",
                    GlobalAnchor = new GeoAnchor { Lat = latOffset, Lon = lonOffset }
                },
                Semantic = new SemanticComponent
                {
                    Tags = ["building", i % 4 == 0 ? "agricultural" : "residential"],
                    Description = $"Rodinný dům Čp. {i} v Runářově.",
                    Attributes = new Dictionary<string, object>
                    {
                        { "house_number", i },
                        { "floors", floorCount },
                        { "usage", i % 4 == 0 ? "zemědělská stavba" : "rodinný dům" }
                    }
                },
                Generation = new GenerationComponent { State = GenerationState.Detailed, Method = "cadastre" },
                Provenance = new ProvenanceComponent { Source = "RUIAN", SourceRef = $"SO-{743615000 + i}", Confidence = 0.95 },
                ExternalRefs = new Dictionary<string, string> { { "ruian", $"{743615000 + i}" } }
            };

            buildings.Add(building);

            for (int f = 1; f <= floorCount; f++)
            {
                floors.Add(new SpatialEntity
                {
                    Id = $"floor_{bId}_{f}",
                    Type = SpatialEntityTypes.Floor,
                    Name = f == 1 ? "Přízemí" : $"{f}. patro",
                    ParentId = bId,
                    Semantic = new SemanticComponent
                    {
                        Tags = ["floor"],
                        Description = $"Podlaží {f} budovy Čp. {i}"
                    },
                    Generation = new GenerationComponent { State = GenerationState.Outlined, Method = "rule-template" }
                });
            }
        }

        await _worldRepo.AddManyAsync(buildings);
        await _worldRepo.AddManyAsync(floors);

        var edges = new List<ConnectivityEdge>
        {
            new ConnectivityEdge
            {
                Id = "edge_road_naves_zastavka",
                FromId = kaplickaPlace.Id,
                ToId = zastavkaPlace.Id,
                Kind = "Road",
                Bidirectional = true,
                CostMeters = 85.0,
                State = "Open",
                Provenance = new ProvenanceComponent { Source = "OSM", Confidence = 1.0 }
            }
        };

        for (int i = 1; i <= 15; i++)
        {
            edges.Add(new ConnectivityEdge
            {
                Id = $"edge_path_cp_{i}",
                FromId = kaplickaPlace.Id,
                ToId = $"building_cp_{i}",
                Kind = "Path",
                Bidirectional = true,
                CostMeters = 20.0 + (i * 5.0),
                State = "Open",
                Provenance = new ProvenanceComponent { Source = "rule-template", Confidence = 0.9 }
            });
        }

        await _connectivityRepo.AddManyAsync(edges);
    }
}
