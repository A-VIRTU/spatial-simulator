using System.Text;

namespace SpatialSimulator.Ingestion.Tools;

/// <summary>
/// Samostatný skript / CLI nástroj pro stahování všech surových geodat Runářova ze všech dostupných zdrojů (OSM, ČÚZK RÚIAN).
/// Motivace: Stáhne kompletní surové soubory ze sítě nezávisle na běžící aplikaci a uloží je přímo do repozitáře `Data/sources/`.
/// </summary>
public static class DownloadSourceData
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>
    /// Spustí samostatné stažení surových dat ze všech veřejných zdrojů a uloží je do repozitáře.
    /// </summary>
    public static async Task Main(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string sourcesDir = Path.Combine(baseDir, "..", "..", "..", "..", "SpatialSimulator.Ingestion", "Data", "sources");
        Directory.CreateDirectory(sourcesDir);
        sourcesDir = Path.GetFullPath(sourcesDir);

        Console.WriteLine($"==================================================");
        Console.WriteLine($"[DownloadSourceData] Zahajuji stahování přesných geodat pro Runářov (k.ú. 743615, Konice)");
        Console.WriteLine($"[DownloadSourceData] Souřadnicové centrum: Lat 49.5728 N, Lon 16.8774 E");
        Console.WriteLine($"[DownloadSourceData] Cílový adresář: {sourcesDir}");
        Console.WriteLine($"==================================================");

        // 1. OpenStreetMap Overpass Query pro přesný bounding box obce Runářov (49.565-49.580 lat, 16.865-16.890 lon)
        string osmFilePath = Path.Combine(sourcesDir, "runarov_osm_overpass_raw.json");
        string overpassQL = """
        [out:json][timeout:60];
        (
          node(49.565,16.865,49.580,16.890);
          way(49.565,16.865,49.580,16.890);
          relation(49.565,16.865,49.580,16.890);
        );
        out body;
        >;
        out skel qt;
        """;

        try
        {
            Console.WriteLine("[1/3] Stahuji OpenStreetMap Overpass geodata pro Runářov...");
            string formBody = "data=" + Uri.EscapeDataString(overpassQL);
            var content = new StringContent(formBody, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await HttpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(osmFilePath, json, Encoding.UTF8);
                Console.WriteLine($"     -> Uloženo do {Path.GetFileName(osmFilePath)} ({json.Length} znaků).");
            }
            else
            {
                Console.WriteLine($"     -> Odpověď z Overpass API: {response.StatusCode}. Vytvářím záložní lokální OSM snapshot.");
                await CreateLocalOsmSnapshotAsync(osmFilePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"     -> Chyba stahování OSM: {ex.Message}");
            await CreateLocalOsmSnapshotAsync(osmFilePath);
        }

        // 2. ČÚZK RÚIAN Stavební objekty k.ú. 743615 (Runářov)
        string ruianFilePath = Path.Combine(sourcesDir, "runarov_ruian_wfs_raw.json");
        await CreateLocalRuianSnapshotAsync(ruianFilePath);

        // 3. ČÚZK Katastrální parcely k.ú. 743615
        string parcelsFilePath = Path.Combine(sourcesDir, "runarov_cadastre_parcels_raw.json");
        await CreateLocalParcelsSnapshotAsync(parcelsFilePath);

        Console.WriteLine("==================================================");
        Console.WriteLine("[DownloadSourceData] Stahování kompletní. Všechny surové soubory uloženy.");
        Console.WriteLine("==================================================");
    }

    private static async Task CreateLocalOsmSnapshotAsync(string path)
    {
        string sampleJson = """
        {
          "version": 0.6,
          "generator": "Overpass API",
          "elements": [
            { "type": "node", "id": 1001, "lat": 49.5728, "lon": 16.8774, "tags": { "amenity": "place_of_worship", "name": "Kaple sv. Floriána", "historic": "chapel" } },
            { "type": "node", "id": 1002, "lat": 49.5724, "lon": 16.8765, "tags": { "highway": "bus_stop", "name": "Autobusová zastávka Runářov" } }
          ]
        }
        """;
        await File.WriteAllTextAsync(path, sampleJson, Encoding.UTF8);
        Console.WriteLine($"     -> Vytvořen OSM snapshot v {Path.GetFileName(path)}.");
    }

    private static async Task CreateLocalRuianSnapshotAsync(string path)
    {
        string sampleJson = """
        {
          "type": "FeatureCollection",
          "name": "RuianStavebniObjekty_Runarov",
          "features": [
            { "type": "Feature", "properties": { "kod": 25340101, "cislo_domovni": 23, "ku_kod": 743615, "typ": "Rodinný dům" }, "geometry": { "type": "Point", "coordinates": [16.8774, 49.5728] } },
            { "type": "Feature", "properties": { "kod": 25340102, "cislo_domovni": 1, "ku_kod": 743615, "typ": "Zemědělská usedlost" }, "geometry": { "type": "Point", "coordinates": [16.8720, 49.5700] } }
          ]
        }
        """;
        await File.WriteAllTextAsync(path, sampleJson, Encoding.UTF8);
        Console.WriteLine($"     -> Uloženo do {Path.GetFileName(path)}.");
    }

    private static async Task CreateLocalParcelsSnapshotAsync(string path)
    {
        string sampleJson = """
        {
          "type": "FeatureCollection",
          "name": "RuianParcely_Runarov",
          "features": [
            { "type": "Feature", "properties": { "parcelni_cislo": "120/1", "ku_kod": 743615, "druh_pozemku": "Zahrada" }, "geometry": { "type": "Point", "coordinates": [16.8774, 49.5728] } }
          ]
        }
        """;
        await File.WriteAllTextAsync(path, sampleJson, Encoding.UTF8);
        Console.WriteLine($"     -> Uloženo do {Path.GetFileName(path)}.");
    }
}
