using System.Text.Json;
using SpatialSimulator.Domain;
using SpatialSimulator.Domain.Components;
using SpatialSimulator.Domain.Entities;
using SpatialSimulator.Domain.Graph;

namespace SpatialSimulator.Ingestion;

/// <summary>
/// Výsledková přepravka (DTO) s entitami a hranami získanými parsováním OpenStreetMap Overpass JSON (budovy, silnice, cesty, pěšiny a potoky).
/// </summary>
public class OsmParseResult
{
    /// <summary>
    /// Seznam načtených sémanticko-prostorových entit (budovy, POI, vodní plochy, potoky).
    /// </summary>
    public List<SpatialEntity> Entities { get; set; } = [];

    /// <summary>
    /// Seznam konektivních hran (silnice, uliční síť, cestní graf, potoky).
    /// </summary>
    public List<ConnectivityEdge> Edges { get; set; } = [];
}

/// <summary>
/// Parser pro načítání surových geodat z OpenStreetMap Overpass API (budovy, silnice, cesty, pěšiny, vodní toky).
/// Motivace: Extrahuje SKUTEČNÉ zemepisné souřadnice budov, kompletní geomterii cestní sítě (`highway=*`)
/// a vodních toků (`waterway=*`, `natural=water`, Runářovský potok) pro vykreslení v mapě i navigaci agentů.
/// </summary>
public class OsmOverpassParser
{
    /// <summary>
    /// Parsuje surový JSON z Overpass API a vytváří kódové doménové entity i konektivní hrany se SKUTEČNÝMI souřadnicemi a geometrií.
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
            int roadIndex = 1;
            int waterIndex = 1;

            foreach (var elem in elements.EnumerateArray())
            {
                string type = elem.GetProperty("type").GetString() ?? "";
                long id = elem.GetProperty("id").GetInt64();

                // Extraction of tags
                Dictionary<string, string> tags = new();
                if (elem.TryGetProperty("tags", out var tagsObj))
                {
                    foreach (var tag in tagsObj.EnumerateObject())
                    {
                        tags[tag.Name] = tag.Value.GetString() ?? "";
                    }
                }

                // Získání geometrie (lomových bodů) z way
                List<(double Lat, double Lon)> geometry = new();
                if (elem.TryGetProperty("geometry", out var geomArr) && geomArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pt in geomArr.EnumerateArray())
                    {
                        if (pt.TryGetProperty("lat", out var pLat) && pt.TryGetProperty("lon", out var pLon))
                        {
                            geometry.Add((pLat.GetDouble(), pLon.GetDouble()));
                        }
                    }
                }

                // Středové souřadnice
                double centerLat = 0;
                double centerLon = 0;
                bool hasCoords = false;

                if (elem.TryGetProperty("center", out var centerObj))
                {
                    centerLat = centerObj.GetProperty("lat").GetDouble();
                    centerLon = centerObj.GetProperty("lon").GetDouble();
                    hasCoords = true;
                }
                else if (elem.TryGetProperty("lat", out var latProp) && elem.TryGetProperty("lon", out var lonProp))
                {
                    centerLat = latProp.GetDouble();
                    centerLon = lonProp.GetDouble();
                    hasCoords = true;
                }
                else if (geometry.Count > 0)
                {
                    centerLat = geometry.Average(g => g.Lat);
                    centerLon = geometry.Average(g => g.Lon);
                    hasCoords = true;
                }

                if (!hasCoords) continue;

                // 1. Zpracování silnic, cest a pěšin (`highway=*`)
                if (tags.TryGetValue("highway", out var highwayType))
                {
                    string roadName = tags.TryGetValue("name", out var rName) ? rName : $"Cesta / Ulica ({highwayType})";
                    string roadEntityId = $"road_osm_{id}";

                    var roadEntity = new SpatialEntity
                    {
                        Id = roadEntityId,
                        Type = SpatialEntityTypes.Place,
                        Name = roadName,
                        ParentId = parentSettlementId,
                        Spatial = new SpatialComponent
                        {
                            Frame = "World",
                            GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon }
                        },
                        Semantic = new SemanticComponent
                        {
                            Tags = ["road", "highway", highwayType, "osm"],
                            Description = $"Reálný uliční úsek OSM (ID: {id}, typ: {highwayType}).",
                            Attributes = new Dictionary<string, object>
                            {
                                { "highway_type", highwayType },
                                { "waypoint_count", geometry.Count }
                            }
                        },
                        Provenance = new ProvenanceComponent { Source = "OPENSTREETMAP", SourceRef = $"osm_way_{id}" }
                    };
                    result.Entities.Add(roadEntity);

                    // Vytvoření cestního grafu z lomových bodů geometrie
                    for (int i = 0; i < geometry.Count - 1; i++)
                    {
                        var p1 = geometry[i];
                        var p2 = geometry[i + 1];
                        double dist = CalculateDistMeters(p1.Lat, p1.Lon, p2.Lat, p2.Lon);

                        string n1Id = $"road_node_{id}_{i}";
                        string n2Id = $"road_node_{id}_{i + 1}";

                        // Přidání uzlů lomových bodů
                        result.Entities.Add(new SpatialEntity
                        {
                            Id = n1Id,
                            Type = SpatialEntityTypes.Place,
                            Name = $"Uzol cesty {roadName} #{i}",
                            ParentId = parentSettlementId,
                            Spatial = new SpatialComponent { Frame = "World", GlobalAnchor = new GeoAnchor { Lat = p1.Lat, Lon = p1.Lon } },
                            Semantic = new SemanticComponent { Tags = ["road_node", "waypoint"] }
                        });

                        result.Edges.Add(new ConnectivityEdge
                        {
                            Id = $"edge_road_{id}_{i}",
                            FromId = n1Id,
                            ToId = n2Id,
                            Kind = "Road",
                            CostMeters = dist,
                            Bidirectional = true,
                            State = "Open"
                        });
                    }
                }
                // 2. Zpracování vodních toků a vodních ploch (`waterway=*`, `natural=water`, Runářovský potok)
                else if (tags.ContainsKey("waterway") || tags.TryGetValue("natural", out var nat) && nat == "water" || tags.ContainsKey("water"))
                {
                    string waterType = tags.TryGetValue("waterway", out var ww) ? ww : (tags.TryGetValue("water", out var w) ? w : "stream");
                    string waterName = tags.TryGetValue("name", out var wName) ? wName : (waterType == "stream" ? "Runářovský potok" : $"Vodní plocha / Rybník ({waterType})");
                    string waterEntityId = $"water_osm_{id}";

                    var waterEntity = new SpatialEntity
                    {
                        Id = waterEntityId,
                        Type = SpatialEntityTypes.Area,
                        Name = waterName,
                        ParentId = parentSettlementId,
                        Spatial = new SpatialComponent
                        {
                            Frame = "World",
                            GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon }
                        },
                        Semantic = new SemanticComponent
                        {
                            Tags = ["waterway", "stream", "water", waterType, "natural"],
                            Description = $"Reálný vodní tok / vodní plocha z OSM ({waterName}, ID: {id}).",
                            Attributes = new Dictionary<string, object>
                            {
                                { "water_type", waterType },
                                { "waypoint_count", geometry.Count }
                            }
                        },
                        Provenance = new ProvenanceComponent { Source = "OPENSTREETMAP", SourceRef = $"osm_way_{id}" }
                    };
                    result.Entities.Add(waterEntity);

                    // Vytvoření hran toku potoka
                    for (int i = 0; i < geometry.Count - 1; i++)
                    {
                        var p1 = geometry[i];
                        var p2 = geometry[i + 1];
                        double dist = CalculateDistMeters(p1.Lat, p1.Lon, p2.Lat, p2.Lon);

                        string w1Id = $"water_node_{id}_{i}";
                        string w2Id = $"water_node_{id}_{i + 1}";

                        result.Entities.Add(new SpatialEntity
                        {
                            Id = w1Id,
                            Type = SpatialEntityTypes.Place,
                            Name = $"Bod toku {waterName} #{i}",
                            ParentId = parentSettlementId,
                            Spatial = new SpatialComponent { Frame = "World", GlobalAnchor = new GeoAnchor { Lat = p1.Lat, Lon = p1.Lon } },
                            Semantic = new SemanticComponent { Tags = ["water_node", "stream_point"] }
                        });

                        result.Edges.Add(new ConnectivityEdge
                        {
                            Id = $"edge_stream_{id}_{i}",
                            FromId = w1Id,
                            ToId = w2Id,
                            Kind = "Waterway",
                            CostMeters = dist,
                            State = "Open"
                        });
                    }
                }
                // 3. Zpracování budov (`building=*`)
                else if (tags.ContainsKey("building") || (type == "way" && !tags.ContainsKey("highway") && !tags.ContainsKey("waterway")))
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
                            GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon }
                        },
                        Semantic = new SemanticComponent
                        {
                            Tags = ["building", "real_osm"],
                            Description = $"Reálná budova z OpenStreetMap (OSM ID: {id}) na přesných souřadnicích {centerLat:F6}° N, {centerLon:F6}° E.",
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
                // 4. Zpracování POI (Kaple, Pomníky, Kříže, Zastávky)
                else if (tags.ContainsKey("amenity") || tags.ContainsKey("historic"))
                {
                    string poiName = tags.TryGetValue("name", out var pName) ? pName : (tags.TryGetValue("description", out var desc) ? desc : (tags.TryGetValue("historic", out var h) ? h : "Zájmové místo"));
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
                            GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon }
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

    private static double CalculateDistMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * 111320.0;
        double dLon = (lon2 - lon1) * 72000.0;
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }
}
