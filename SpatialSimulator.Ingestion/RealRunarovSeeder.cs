using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;
using SpatialSimulator.Domain.Repositories;

namespace SpatialSimulator.Ingestion;

/// <summary>
/// Pokročilý seeder pro syntézu a import REÁLNÝCH geodat Runářova stažených z OpenStreetMap (Overpass API) a ČÚZK RÚIAN.
/// Motivace: Vytváří plně realistický sémanticko-prostorový model Runářova na základě reálných geodat uložených v souborech v projektu.
/// </summary>
public class RealRunarovSeeder
{
    private readonly IWorldRepository _worldRepository;
    private readonly IConnectivityRepository _connectivityRepository;

    /// <summary>
    /// Konstruktor přijímající repozitáře pro zápis.
    /// </summary>
    public RealRunarovSeeder(IWorldRepository worldRepository, IConnectivityRepository connectivityRepository)
    {
        _worldRepository = worldRepository;
        _connectivityRepository = connectivityRepository;
    }

    /// <summary>
    /// Stáhne reálná geodata ze zdrojů (OSM & RÚIAN), uloží je jako JSON/XML soubory v `SpatialSimulator.Ingestion/Data/` a provede syntézu modelu.
    /// </summary>
    public async Task SeedRealRunarovAsync(string dataDirectory)
    {
        var downloader = new DataDownloader();
        string osmJson = await downloader.DownloadOsmDataAsync(dataDirectory);
        string ruianXml = await downloader.DownloadRuianVfrAsync(dataDirectory);

        // 1. Založení sídla Runářov
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
                Tags = ["settlement", "village", "konice_district", "real_geodata"],
                Description = "Runářov — místní část obce Konice, okres Prostějov (k.ú. 743615). Model vytvořen syntézou reálných geodat OSM a ČÚZK RÚIAN.",
                Attributes = new Dictionary<string, object>
                {
                    { "ku_code", "743615" },
                    { "source", "OSM_Overpass_And_RUIAN_VFR" }
                }
            },
            Provenance = new ProvenanceComponent { Source = "OSM_AND_RUIAN", Confidence = 1.0 },
            Generation = new GenerationComponent { State = GenerationState.Verified, Method = "cadastre" }
        };

        await _worldRepository.AddAsync(runarov);

        // 2. Parsování OSM dat
        var osmParser = new OsmOverpassParser();
        var osmData = osmParser.ParseOverpassJson(osmJson, runarov.Id);

        if (osmData.Entities.Count > 0)
        {
            await _worldRepository.AddManyAsync(osmData.Entities);
        }
        if (osmData.Edges.Count > 0)
        {
            await _connectivityRepository.AddManyAsync(osmData.Edges);
        }

        // 3. Parsování RÚIAN VFR XML dat
        var ruianParser = new RuianVfrParser();
        var ruianEntities = ruianParser.ParseVfrXml(ruianXml);
        if (ruianEntities.Count > 0)
        {
            await _worldRepository.AddManyAsync(ruianEntities);
        }

        // 4. Doplnění 110 reálných budov a venkovních zájmových bodů Runářova
        var baseSeeder = new RunarovSeeder(_worldRepository, _connectivityRepository);
        await baseSeeder.SeedAsync();
    }
}
