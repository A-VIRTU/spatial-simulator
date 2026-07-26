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

        // 1. Parsování OSM dat
        var osmParser = new OsmOverpassParser();
        var osmData = osmParser.ParseOverpassJson(osmJson, "settlement_runarov");

        if (osmData.Entities.Count > 0)
        {
            await _worldRepository.AddManyAsync(osmData.Entities);
        }
        if (osmData.Edges.Count > 0)
        {
            await _connectivityRepository.AddManyAsync(osmData.Edges);
        }

        // 2. Parsování RÚIAN VFR XML dat
        var ruianParser = new RuianVfrParser();
        var ruianEntities = ruianParser.ParseVfrXml(ruianXml);
        if (ruianEntities.Count > 0)
        {
            await _worldRepository.AddManyAsync(ruianEntities);
        }

        // 3. Naplnění 110 reálných budov a uliční sítě Runářova
        var baseSeeder = new RunarovSeeder(_worldRepository, _connectivityRepository);
        await baseSeeder.SeedAsync();
    }
}
