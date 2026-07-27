using System.Text;

namespace SpatialSimulator.Ingestion.Tools;

/// <summary>
/// Samostatný skript pro stahování SKUTEČNÝCH budov, silnic, polních cest, pěšin a potoků obce Runářov z OpenStreetMap Overpass API (`out body geom`).
/// Motivace: Stáhne přesné středové souřadnice i lomové body (geometry) všech budov, cestní sítě včetně polních cest a pěšin (highway=*) a vodních toků (waterway=*).
/// </summary>
public static class DownloadSourceData
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>
    /// Stáhne kompletní surová geodata (budovy, silnice, polní cesty, pěšiny, potoky) a uloží je do `Data/sources/runarov_osm_overpass_raw.json`.
    /// </summary>
    public static async Task Main(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string sourcesDir = Path.Combine(baseDir, "..", "..", "..", "..", "SpatialSimulator.Ingestion", "Data", "sources");
        Directory.CreateDirectory(sourcesDir);
        sourcesDir = Path.GetFullPath(sourcesDir);

        Console.WriteLine($"==================================================");
        Console.WriteLine($"[DownloadSourceData] Stahuji GEODATA (Budovy, Silnice, Polní cesty, Pěšiny, Potoky) pro Runářov");
        Console.WriteLine($"==================================================");

        string osmFilePath = Path.Combine(sourcesDir, "runarov_osm_overpass_raw.json");

        // Overpass QL dotaz s rozšířeným bbox (49.5630,16.8550,49.5830,16.9050) pro pokrytí polních cest a pěšin okolo celého Runářova
        string overpassQL = """
        [out:json][timeout:60];
        (
          way["building"](49.5630,16.8550,49.5830,16.9050);
          node["building"](49.5630,16.8550,49.5830,16.9050);
          way["highway"](49.5630,16.8550,49.5830,16.9050);
          way["waterway"](49.5630,16.8550,49.5830,16.9050);
          way["natural"="water"](49.5630,16.8550,49.5830,16.9050);
          node["amenity"](49.5630,16.8550,49.5830,16.9050);
          node["historic"](49.5630,16.8550,49.5830,16.9050);
        );
        out body geom;
        """;

        string url = "https://overpass-api.de/api/interpreter?data=" + Uri.EscapeDataString(overpassQL);

        try
        {
            Console.WriteLine("[1/1] Odesílám GET dotaz na Overpass API pro Budovy, Polní cesty, Pěšiny a Potoky...");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "SpatialSimulator/1.0 (contact@a-virtu.org)");

            var response = await HttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(osmFilePath, json, Encoding.UTF8);
                Console.WriteLine($"     -> Úspěšně staženo a uloženo do {Path.GetFileName(osmFilePath)} ({json.Length} znaků).");
            }
            else
            {
                Console.WriteLine($"     -> Odpověď z Overpass API: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"     -> Chyba stahování: {ex.Message}");
        }

        Console.WriteLine("==================================================");
    }
}
