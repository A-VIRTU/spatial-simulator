using System.Text.Json;
using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;

namespace SpatialSimulator.Ingestion;

/// <summary>
/// Výsledková přepravka (DTO) s entitami a hranami získanými parsováním OpenStreetMap Overpass JSON.
/// </summary>
public class OsmParseResult
{
    /// <summary>
    /// Seznam načtených sémanticko-prostorových entit (budovy, POI, zájmová místa).
    /// </summary>
    public List<SpatialEntity> Entities { get; set; } = [];

    /// <summary>
    /// Seznam konektivních hran (ulice, chodníky).
    /// </summary>
    public List<ConnectivityEdge> Edges { get; set; } = [];
}

/// <summary>
/// Parser pro načítání surových JSON dat z OpenStreetMap Overpass API (včetně středových souřadnic `center`).
/// Motivace: Extrahuje SKUTEČNÉ zemepisné souřadnice (lat, lon) všech budov a POI přímo z reálných polygonů OpenStreetMap.
/// </summary>
public class OsmOverpassParser
{
    /// <summary>
    /// Parsuje surový JSON řetězec z Overpass API a vytváří kódové doménové entity se SKUTEČNÝMI souřadnicemi.
    /// </summary>
    public OsmParseResult ParseOverpassJson(string jsonContent, string parentSettlementId)
    {
        var result = new OsmParseResult();
        if (string.IsNullOrWhiteSpace(jsonContent)) return result;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            if (!root.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            int houseIndex = 1;

            foreach (var elem in elements.EnumerateArray())
            {
                string type = elem.GetProperty("type").GetString() ?? "";
                long id = elem.GetProperty("id").GetInt64();

                double lat = 0;
                double lon = 0;
                bool hasCoords = false;

                // 1. Získání přesných středových souřadnic (center.lat, center.lon) z way nebo z přímo z node
                if (elem.TryGetProperty("center", out var centerObj))
                {
                    lat = centerObj.GetProperty("lat").GetDouble();
                    lon = centerObj.GetProperty("lon").GetDouble();
                    hasCoords = true;
                }
                else if (elem.TryGetProperty("lat", out var latProp) && elem.TryGetProperty("lon", out var lonProp))
                {
                    lat = latProp.GetDouble();
                    lon = lonProp.GetDouble();
                    hasCoords = true;
                }

                if (!hasCoords) continue;

                // Extraction of tags
                Dictionary<string, string> tags = new();
                if (elem.TryGetProperty("tags", out var tagsObj))
                {
                    foreach (var tag in tagsObj.EnumerateObject())
                    {
                        tags[tag.Name] = tag.Value.GetString() ?? "";
                    }
                }

                // Check if element is a building
                bool isBuilding = tags.ContainsKey("building") || type == "way";

                if (isBuilding)
                {
                    string houseNumberStr = tags.TryGetValue("addr:housenumber", out var hNum) ? hNum : houseIndex.ToString();
                    int houseNumInt = int.TryParse(houseNumberStr, out var parsedNum) ? parsedNum : houseIndex;
                    houseIndex++;

                    string name = tags.TryGetValue("name", out var bName) ? bName : $"Čp. {houseNumInt} (Rodinný dům)";
                    string entityId = $"building_cp_{houseNumInt}";

                    var building = new SpatialEntity
                    {
                        Id = entityId,
                        Type = SpatialEntityTypes.Building,
                        Name = name,
                        ParentId = parentSettlementId,
                        Spatial = new SpatialComponent
                        {
                            Frame = "World",
                            GlobalAnchor = new GeoAnchor { Lat = lat, Lon = lon }
                        },
                        Semantic = new SemanticComponent
                        {
                            Tags = ["building", "real_osm"],
                            Description = $"Reálná budova z OpenStreetMap (OSM ID: {id}) na přesných souřadnicích {lat:F6}° N, {lon:F6}° E.",
                            Attributes = new Dictionary<string, object>
                            {
                                { "osm_id", id },
                                { "house_number", houseNumInt }
                            }
                        },
                        Provenance = new ProvenanceComponent { Source = "OPENSTREETMAP", SourceRef = $"osm_{type}_{id}" },
                        Generation = new GenerationComponent { State = houseNumInt == 23 ? GenerationState.Detailed : GenerationState.Verified, Method = "osm-import" }
                    };

                    result.Entities.Add(building);
                }
                else if (tags.ContainsKey("amenity") || tags.ContainsKey("historic"))
                {
                    string poiName = tags.TryGetValue("name", out var pName) ? pName : (tags.TryGetValue("historic", out var h) ? h : "Zájmové místo");
                    string poiId = $"place_osm_{id}";

                    var poi = new SpatialEntity
                    {
                        Id = poiId,
                        Type = SpatialEntityTypes.Place,
                        Name = poiName,
                        ParentId = parentSettlementId,
                        Spatial = new SpatialComponent
                        {
                            Frame = "World",
                            GlobalAnchor = new GeoAnchor { Lat = lat, Lon = lon }
                        },
                        Semantic = new SemanticComponent
                        {
                            Tags = ["place", "poi", "osm"],
                            Description = $"Reálný zájmový bod z OpenStreetMap (OSM ID: {id})."
                        },
                        Provenance = new ProvenanceComponent { Source = "OPENSTREETMAP", SourceRef = $"osm_{type}_{id}" }
                    };

                    result.Entities.Add(poi);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OsmOverpassParser] Chyba při parsování OSM JSON: {ex.Message}");
        }

        return result;
    }
}
