using System.Text.Json;
using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;

namespace SpatialSimulator.Ingestion;

/// <summary>
/// Výsledek parsování OpenStreetMap Overpass JSON dat.
/// Obsahuje extrahované prostory (budovy, zájmové body) a hrany konektivity (silnice, chodníky).
/// </summary>
public class OsmParseResult
{
    /// <summary>
    /// Seznam načtených prostorových entit.
    /// </summary>
    public List<SpatialEntity> Entities { get; set; } = [];

    /// <summary>
    /// Seznam načtených hran silniční a pěší sítě.
    /// </summary>
    public List<ConnectivityEdge> Edges { get; set; } = [];
}

/// <summary>
/// Parser pro import OpenStreetMap geodat z Overpass API JSON odpovídajících zadanému území.
/// Motivace: Získává uliční síť, venkovní objekty, lavičky, kapličky a cesty pro navigaci agentů v prostoru.
/// </summary>
public class OsmOverpassParser
{
    /// <summary>
    /// Sparuje Overpass JSON odpovídající dotazu na budovy a cestní síť.
    /// </summary>
    /// <param name="jsonContent">JSON text vrácený z Overpass API.</param>
    /// <param name="parentSettlementId">ID rodičovského sídla (např. "settlement_runarov").</param>
    /// <returns>Objekt <see cref="OsmParseResult"/> s entitami a hranami.</returns>
    public OsmParseResult ParseOverpassJson(string jsonContent, string parentSettlementId = "settlement_runarov")
    {
        var result = new OsmParseResult();
        if (string.IsNullOrWhiteSpace(jsonContent)) return result;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            if (!doc.RootElement.TryGetProperty("elements", out var elements)) return result;

            var nodeDict = new Dictionary<long, (double Lat, double Lon)>();

            // 1. První průchod: Načtení bodů (nodes)
            foreach (var elem in elements.EnumerateArray())
            {
                if (elem.GetProperty("type").GetString() == "node")
                {
                    long id = elem.GetProperty("id").GetInt64();
                    double lat = elem.GetProperty("lat").GetDouble();
                    double lon = elem.GetProperty("lon").GetDouble();
                    nodeDict[id] = (lat, lon);

                    // Pokud má uzlové atributy (zájmové body)
                    if (elem.TryGetProperty("tags", out var tags))
                    {
                        string? amenity = tags.TryGetProperty("amenity", out var a) ? a.GetString() : null;
                        string? name = tags.TryGetProperty("name", out var n) ? n.GetString() : null;

                        if (!string.IsNullOrEmpty(amenity) || !string.IsNullOrEmpty(name))
                        {
                            result.Entities.Add(new SpatialEntity
                            {
                                Id = $"place_osm_{id}",
                                Type = SpatialEntityTypes.Place,
                                Name = name ?? $"Místo ({amenity})",
                                ParentId = parentSettlementId,
                                Spatial = new SpatialComponent
                                {
                                    Frame = "World",
                                    GlobalAnchor = new GeoAnchor { Lat = lat, Lon = lon }
                                },
                                Semantic = new SemanticComponent
                                {
                                    Tags = ["place", "osm", amenity ?? "poi"],
                                    Description = $"Zájmové místo z OSM (ID: {id})."
                                },
                                Provenance = new ProvenanceComponent
                                {
                                    Source = "OSM_OVERPASS",
                                    SourceRef = id.ToString()
                                }
                            });
                        }
                    }
                }
            }

            // 2. Druhý průchod: Cesty (ways) pro cestní síť
            foreach (var elem in elements.EnumerateArray())
            {
                if (elem.GetProperty("type").GetString() == "way")
                {
                    long wayId = elem.GetProperty("id").GetInt64();
                    if (!elem.TryGetProperty("nodes", out var nodesArr)) continue;

                    string kind = "Road";
                    if (elem.TryGetProperty("tags", out var tags))
                    {
                        if (tags.TryGetProperty("highway", out var hw))
                        {
                            string hwType = hw.GetString() ?? "road";
                            kind = (hwType == "footway" || hwType == "path") ? "Path" : "Road";
                        }
                    }

                    var wayNodeIds = nodesArr.EnumerateArray().Select(n => n.GetInt64()).ToList();

                    // Vytvoření hran mezi sousedními body cesty
                    for (int i = 0; i < wayNodeIds.Count - 1; i++)
                    {
                        long fromId = wayNodeIds[i];
                        long toId = wayNodeIds[i + 1];

                        if (nodeDict.TryGetValue(fromId, out var fromCoords) && nodeDict.TryGetValue(toId, out var toCoords))
                        {
                            double cost = CalculateDistanceMeters(fromCoords.Lat, fromCoords.Lon, toCoords.Lat, toCoords.Lon);

                            result.Edges.Add(new ConnectivityEdge
                            {
                                Id = $"edge_osm_{wayId}_{i}",
                                FromId = $"place_osm_{fromId}",
                                ToId = $"place_osm_{toId}",
                                Kind = kind,
                                Bidirectional = true,
                                CostMeters = Math.Max(cost, 1.0),
                                Provenance = new ProvenanceComponent
                                {
                                    Source = "OSM_OVERPASS",
                                    SourceRef = wayId.ToString()
                                }
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chyba při parsování OSM Overpass JSON: {ex.Message}");
        }

        return result;
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double r = 6371000; // poloměr Země v metrech
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }

    private static double ToRadians(double val) => val * Math.PI / 180.0;
}
