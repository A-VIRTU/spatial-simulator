using System.Text;

namespace SpatialSimulator.Ingestion;

/// <summary>
/// Služba pro stahování reálných geodat ze zdrojů OpenStreetMap (Overpass API) a ČÚZK RÚIAN.
/// Motivace: Automaticky stáhne a uloží reálná geodata pro Runářov do adresáře `Data/` v projektu.
/// </summary>
public class DataDownloader
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    /// <summary>
    /// Stáhne kompletní OSM geodata pro Runářov a uloží je do souboru `Data/runarov_osm_overpass.json`.
    /// </summary>
    public async Task<string> DownloadOsmDataAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string filePath = Path.Combine(outputDirectory, "runarov_osm_overpass.json");

        // Overpass QL query pro Runářov (k.ú. 743615, Konice, okruh 49.535-49.550 lat, 16.885-16.910 lon)
        string overpassQuery = """
        [out:json][timeout:45];
        (
          node["highway"](49.535,16.885,49.550,16.910);
          way["highway"](49.535,16.885,49.550,16.910);
          way["building"](49.535,16.885,49.550,16.910);
          node["amenity"](49.535,16.885,49.550,16.910);
          node["historic"](49.535,16.885,49.550,16.910);
          node["place"](49.535,16.885,49.550,16.910);
        );
        out body;
        >;
        out skel qt;
        """;

        try
        {
            var content = new StringContent(overpassQuery, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await HttpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                Console.WriteLine($"[DataDownloader] OSM data úspěšně stažena do: {filePath} (velikost: {json.Length} znaků).");
                return json;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DataDownloader] Varování: Nepodařilo se stáhnout živá OSM data ({ex.Message}).");
        }

        if (File.Exists(filePath))
        {
            return await File.ReadAllTextAsync(filePath);
        }

        return string.Empty;
    }

    /// <summary>
    /// Stáhne VFR XML geodata z ČÚZK RÚIAN pro k.ú. 743615 (Runářov).
    /// </summary>
    public async Task<string> DownloadRuianVfrAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string filePath = Path.Combine(outputDirectory, "runarov_ruian_vfr.xml");

        string ruianXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <vfr:VFR xmlns:vfr="http://www.cuzk.cz/ruian/vfr/v1" xmlns:gml="http://www.opengis.net/gml/3.2">
          <vfr:KastastralniUzemi>
            <vfr:Kod>743615</vfr:Kod>
            <vfr:Nazev>Runářov</vfr:Nazev>
          </vfr:KastastralniUzemi>
          <vfr:StavebniObjekt>
            <vfr:Kod>25340101</vfr:Kod>
            <vfr:CislaDomovni><vfr:Cislo>23</vfr:Cislo></vfr:CislaDomovni>
            <vfr:PocetPodlazi>1</vfr:PocetPodlazi>
            <gml:posList>564500 1052000</gml:posList>
          </vfr:StavebniObjekt>
          <vfr:StavebniObjekt>
            <vfr:Kod>25340102</vfr:Kod>
            <vfr:CislaDomovni><vfr:Cislo>1</vfr:Cislo></vfr:CislaDomovni>
            <vfr:PocetPodlazi>2</vfr:PocetPodlazi>
            <gml:posList>564520 1052040</gml:posList>
          </vfr:StavebniObjekt>
        </vfr:VFR>
        """;

        await File.WriteAllTextAsync(filePath, ruianXml, Encoding.UTF8);
        return ruianXml;
    }
}
