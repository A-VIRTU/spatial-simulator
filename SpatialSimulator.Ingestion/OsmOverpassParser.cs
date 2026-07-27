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
    /// Seznam načtených sémanticko-prostorových entit (budovy, uliční úseky, potoky, plošný pokryv krajiny).
    /// </summary>
    public List<SpatialEntity> Entities { get; set; } = [];

    /// <summary>
    /// Seznam konektivních hran (silnice, uliční síť, cestní graf, potoky, mosty, brody).
    /// </summary>
    public List<ConnectivityEdge> Edges { get; set; } = [];
}

/// <summary>
/// Parser pro načítání geodat z OpenStreetMap Overpass API (budovy, silnice, cesty, pěšiny, vodní toky, plošný pokryv landcover).
/// Motivace: Naplňuje datový model přesně podle technické specifikace (LinearFeature, LinearSegment, LandCover, Yard, Relations).
/// </summary>
public class OsmOverpassParser
{
    /// <summary>
    /// Parsuje surový JSON z Overpass API a vytváří doménové entity i konektivní hrany podle nového technického návrhu.
    /// </summary>
    public OsmParseResult ParseOverpassJson(string jsonContent, string parentSettlementId = "settlement_runarov")
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

            // 1. Založení nadřazeného liniového prvku pro Runářovský potok
            var streamParent = new SpatialEntity
            {
                Id = "stream_runarovsky_potok",
                Type = SpatialEntityTypes.LinearFeature,
                Name = "Runářovský potok",
                ParentId = parentSettlementId,
                Semantic = new SemanticComponent
                {
                    Tags = ["hydrology", "stream", "waterway"],
                    Description = "Hlavní vodní tok (Runářovský potok) protékající katastrálním územím obce Runářov."
                },
                Provenance = new ProvenanceComponent { Source = "OPENSTREETMAP_DIBAVOD", Confidence = 1.0 },
                Generation = new GenerationComponent { State = GenerationState.Verified, Method = "osm-import" }
            };
            result.Entities.Add(streamParent);

            int houseIndex = 1;
            int streamSegmentIndex = 0;

            foreach (var elem in elements.EnumerateArray())
            {
                string type = elem.GetProperty("type").GetString() ?? "";
                long id = elem.GetProperty("id").GetInt64();

                Dictionary<string, string> tags = new();
                if (elem.TryGetProperty("tags", out var tagsObj))
                {
                    foreach (var tag in tagsObj.EnumerateObject())
                    {
                        tags[tag.Name] = tag.Value.GetString() ?? "";
                    }
                }

                // Získání lomových bodů geometrie
                List<List<double>> polylineCoords = new();
                if (elem.TryGetProperty("geometry", out var geomArr) && geomArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pt in geomArr.EnumerateArray())
                    {
                        if (pt.TryGetProperty("lat", out var pLat) && pt.TryGetProperty("lon", out var pLon))
                        {
                            polylineCoords.Add([pLon.GetDouble(), pLat.GetDouble()]);
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
                else if (polylineCoords.Count > 0)
                {
                    centerLat = polylineCoords.Average(g => g[1]);
                    centerLon = polylineCoords.Average(g => g[0]);
                    hasCoords = true;
                }

                if (!hasCoords) continue;

                // 2. Vodní toky (Runářovský potok -> LinearSegment)
                if (tags.ContainsKey("waterway") || (tags.TryGetValue("natural", out var natW) && natW == "water") || tags.ContainsKey("water"))
                {
                    string waterType = tags.TryGetValue("waterway", out var ww) ? ww : (tags.TryGetValue("water", out var w) ? w : "stream");
                    string waterName = tags.TryGetValue("name", out var wName) ? wName : (waterType == "stream" ? "Runářovský potok" : $"Vodní plocha / Rybník ({waterType})");

                    if (waterType == "stream" || waterType == "river" || waterType == "ditch")
                    {
                        int currentOrder = streamSegmentIndex++;
                        string segmentId = $"stream_segment_{id}_{currentOrder}";

                        var segment = new SpatialEntity
                        {
                            Id = segmentId,
                            Type = SpatialEntityTypes.LinearSegment,
                            Name = $"{waterName} — úsek #{currentOrder + 1}",
                            ParentId = streamParent.Id,
                            OrderIndex = currentOrder,
                            Spatial = new SpatialComponent
                            {
                                Frame = "World",
                                Kind = GeometryKind.Polyline,
                                GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon },
                                GlobalPolyline = new WorldPolyline { Coordinates = polylineCoords, WidthM = 1.8 }
                            },
                            Semantic = new SemanticComponent
                            {
                                Tags = ["stream_segment", "waterway"],
                                Description = $"Úsek vodního toku {waterName} s délkou {polylineCoords.Count} lomových bodů."
                            },
                            Relations = new List<SpatialRelation>
                            {
                                new SpatialRelation { Kind = "BorderedBy", TargetId = "settlement_runarov", Note = "koryto potoka v k.ú. Runářov" }
                            },
                            Provenance = new ProvenanceComponent { Source = "DIBAVOD_OSM", SourceRef = $"osm_way_{id}" }
                        };
                        result.Entities.Add(segment);

                        // Vytvoření hran vodního toku
                        for (int i = 0; i < polylineCoords.Count - 1; i++)
                        {
                            var p1 = polylineCoords[i];
                            var p2 = polylineCoords[i + 1];
                            double dist = CalculateDistMeters(p1[1], p1[0], p2[1], p2[0]);

                            string w1Id = $"water_node_{id}_{i}";
                            string w2Id = $"water_node_{id}_{i + 1}";

                            result.Entities.Add(new SpatialEntity
                            {
                                Id = w1Id,
                                Type = SpatialEntityTypes.Place,
                                Name = $"Bod toku {waterName} #{i}",
                                ParentId = segment.Id,
                                Spatial = new SpatialComponent { Frame = "World", Kind = GeometryKind.Point, GlobalAnchor = new GeoAnchor { Lat = p1[1], Lon = p1[0] } },
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
                    else
                    {
                        // Vodní plocha (rybník / nádrž) ako LandCover
                        string pondId = $"landcover_water_{id}";
                        result.Entities.Add(new SpatialEntity
                        {
                            Id = pondId,
                            Type = SpatialEntityTypes.LandCover,
                            Name = waterName,
                            ParentId = parentSettlementId,
                            Spatial = new SpatialComponent
                            {
                                Frame = "World",
                                Kind = GeometryKind.Polygon,
                                GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon, FootprintCoordinates = polylineCoords }
                            },
                            Semantic = new SemanticComponent
                            {
                                Tags = ["landcover", "water", "pond"],
                                Description = $"Vodní plocha / rybník {waterName} v k.ú. Runářov.",
                                Attributes = new Dictionary<string, object> { { "landCoverClass", "water" } }
                            }
                        });
                    }
                }
                // 3. Uliční a cestní síť (`highway=*`)
                else if (tags.TryGetValue("highway", out var highwayType))
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
                            Kind = GeometryKind.Polyline,
                            GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon },
                            GlobalPolyline = new WorldPolyline { Coordinates = polylineCoords, WidthM = 4.0 }
                        },
                        Semantic = new SemanticComponent
                        {
                            Tags = ["road", "highway", highwayType],
                            Description = $"Úsek cestní sítě {roadName} (typ: {highwayType})."
                        }
                    };
                    result.Entities.Add(roadEntity);

                    for (int i = 0; i < polylineCoords.Count - 1; i++)
                    {
                        var p1 = polylineCoords[i];
                        var p2 = polylineCoords[i + 1];
                        double dist = CalculateDistMeters(p1[1], p1[0], p2[1], p2[0]);

                        string n1Id = $"road_node_{id}_{i}";
                        string n2Id = $"road_node_{id}_{i + 1}";

                        result.Entities.Add(new SpatialEntity
                        {
                            Id = n1Id,
                            Type = SpatialEntityTypes.Place,
                            Name = $"Uzol cesty {roadName} #{i}",
                            ParentId = parentSettlementId,
                            Spatial = new SpatialComponent { Frame = "World", Kind = GeometryKind.Point, GlobalAnchor = new GeoAnchor { Lat = p1[1], Lon = p1[0] } },
                            Semantic = new SemanticComponent { Tags = ["road_node", "waypoint"] }
                        });

                        bool isBridge = tags.ContainsKey("bridge") && tags["bridge"] != "no";
                        bool isFord = tags.ContainsKey("ford") && tags["ford"] != "no";

                        result.Edges.Add(new ConnectivityEdge
                        {
                            Id = $"edge_road_{id}_{i}",
                            FromId = n1Id,
                            ToId = n2Id,
                            Kind = isBridge ? "Bridge" : (isFord ? "Ford" : "Road"),
                            CostMeters = dist,
                            Bidirectional = true,
                            State = "Open"
                        });
                    }
                }
                // 4. Budovy (`building=*`) a dvoře (`Yard`)
                else if (tags.ContainsKey("building") || type == "way")
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
                            Kind = GeometryKind.Polygon,
                            GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon, FootprintCoordinates = polylineCoords }
                        },
                        Semantic = new SemanticComponent
                        {
                            Tags = ["building", "residential"],
                            Description = $"Reálná budova z OpenStreetMap (OSM ID: {id}) na souřadnicích {centerLat:F6}° N, {centerLon:F6}° E.",
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

                    string yardId = $"yard_cp_{houseNumInt}";
                    result.Entities.Add(new SpatialEntity
                    {
                        Id = yardId,
                        Type = SpatialEntityTypes.Yard,
                        Name = $"Dvůr a zahrada Čp. {houseNumInt}",
                        ParentId = building.Id,
                        Spatial = new SpatialComponent
                        {
                            Frame = "Local",
                            Kind = GeometryKind.Box,
                            LocalBoundingBox = new BoundingBox3D { X = 0, Y = 0, Z = 0, W = 15, H = 2, D = 20 }
                        },
                        Semantic = new SemanticComponent
                        {
                            Tags = ["yard", "garden"],
                            Description = $"Přilehlý dvůr a zahrada k rodinnému domu Čp. {houseNumInt}."
                        }
                    });
                }
                // 5. Zájmové body POI (Kaplička, Pomník, Zastávka, Schránka)
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
                            Kind = GeometryKind.Point,
                            GlobalAnchor = new GeoAnchor { Lat = centerLat, Lon = centerLon }
                        },
                        Semantic = new SemanticComponent
                        {
                            Tags = ["place", "poi", "osm"],
                            Description = $"Reálné zájmové místo z OpenStreetMap ({poiName})."
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
