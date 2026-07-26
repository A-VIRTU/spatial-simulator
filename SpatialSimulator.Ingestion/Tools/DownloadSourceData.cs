using System.Text;

namespace SpatialSimulator.Ingestion.Tools;

/// <summary>
/// Samostatný skript pro stahování SKUTEČNÝCH budov a souřadnic obce Runářov z OpenStreetMap Overpass API (out center).
/// Motivace: Stáhne přesné středové souřadnice (center.lat, center.lon) všech skutočných budov zakreslených na mapě Runářova.
/// </summary>
public static class DownloadSourceData
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>
    /// Stáhne kompletní surová geodata budov Runářova a uloží je do `Data/sources/runarov_osm_overpass_raw.json`.
    /// </summary>
    public static async Task Main(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string sourcesDir = Path.Combine(baseDir, "..", "..", "..", "..", "SpatialSimulator.Ingestion", "Data", "sources");
        Directory.CreateDirectory(sourcesDir);
        sourcesDir = Path.GetFullPath(sourcesDir);

        Console.WriteLine($"==================================================");
        Console.WriteLine($"[DownloadSourceData] Stahuji SKUTEČNÉ středové souřadnice budov z OSM Overpass API pro Runářov");
        Console.WriteLine($"==================================================");

        string osmFilePath = Path.Combine(sourcesDir, "runarov_osm_overpass_raw.json");

        string overpassQL = "[out:json][timeout:60];(way[\"building\"](49.5700,16.8650,49.5770,16.8850);node[\"building\"](49.5700,16.8650,49.5770,16.8850););out center;";
        string url = "https://overpass-api.de/api/interpreter?data=" + Uri.EscapeDataString(overpassQL);

        try
        {
            Console.WriteLine("[1/1] Odesílám GET dotaz na Overpass API...");
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
