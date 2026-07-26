using System.Xml.Linq;
using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;

namespace SpatialSimulator.Ingestion;

/// <summary>
/// Parser pro import dat z VFR XML souborů ČÚZK (RÚIAN výměnný formát).
/// Motivace: Umožňuje načítat reálná geodata českých obcí přímo ze zpracovaných VFR souborů ČÚZK
/// a převádět je na doménové prostory `SpatialEntity` a `SpatialComponent`.
/// </summary>
public class RuianVfrParser
{
    /// <summary>
    /// Načte a sparuje VFR XML soubor a vrátí seznam prostorových entit (budov a pozemků).
    /// </summary>
    /// <param name="xmlContent">Textový obsah VFR XML souboru ČÚZK.</param>
    /// <returns>Seznam vygenerovaných doménových entit.</returns>
    public List<SpatialEntity> ParseVfrXml(string xmlContent)
    {
        var result = new List<SpatialEntity>();
        if (string.IsNullOrWhiteSpace(xmlContent)) return result;

        try
        {
            var doc = XDocument.Parse(xmlContent);
            XNamespace vfr = "http://www.cuzk.cz/ruian/vfr/v1";
            XNamespace gml = "http://www.opengis.net/gml/3.2";

            // Sparování stavebních objektů
            var buildingElements = doc.Descendants(vfr + "StavebniObjekt");
            foreach (var elem in buildingElements)
            {
                var kod = elem.Element(vfr + "Kod")?.Value ?? Guid.NewGuid().ToString("n");
                var cisloDomovni = elem.Element(vfr + "CislaDomovni")?.Element(vfr + "Cislo")?.Value;
                var pocetPodlaziStr = elem.Element(vfr + "PocetPodlazi")?.Value;
                int.TryParse(pocetPodlaziStr, out int floors);

                var posList = elem.Descendants(gml + "posList").FirstOrDefault()?.Value;

                double lat = 49.5427;
                double lon = 16.8963;

                if (!string.IsNullOrWhiteSpace(posList))
                {
                    var coords = posList.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                    if (coords.Length >= 2 && double.TryParse(coords[0], out double y) && double.TryParse(coords[1], out double x))
                    {
                        (lat, lon) = SpatialProjection.SJtskToWgs84(y, x);
                    }
                }

                var entity = new SpatialEntity
                {
                    Id = $"building_ruian_{kod}",
                    Type = SpatialEntityTypes.Building,
                    Name = !string.IsNullOrEmpty(cisloDomovni) ? $"Čp. {cisloDomovni}" : $"Stavební objekt {kod}",
                    ParentId = "settlement_runarov",
                    Spatial = new SpatialComponent
                    {
                        Frame = "World",
                        GlobalAnchor = new GeoAnchor { Lat = lat, Lon = lon }
                    },
                    Semantic = new SemanticComponent
                    {
                        Tags = ["building", "ruian_vfr", "residential"],
                        Description = $"Stavební objekt z RÚIAN VFR V1 (Kód: {kod}).",
                        Attributes = new Dictionary<string, object>
                        {
                            { "ruian_kod", kod },
                            { "floors", floors > 0 ? floors : 1 }
                        }
                    },
                    Provenance = new ProvenanceComponent
                    {
                        Source = "RUIAN_VFR_XML",
                        SourceRef = kod,
                        Confidence = 1.0
                    },
                    Generation = new GenerationComponent
                    {
                        State = GenerationState.Verified,
                        Method = "cadastre"
                    }
                };

                result.Add(entity);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chyba při parsování VFR XML: {ex.Message}");
        }

        return result;
    }
}
