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
/// Parser pro načítání geodat z OpenStreetMap Overpass API (budovy, silnice, cesty, pěšiny, vodní toky, plošný pokryv landcover, POI).
/// Motivace: Zabezpečuje kompletní import všech uzlů (včetně koncových bodů hran) a detailní české pojmenování zájmových míst.
/// </summary>
public class OsmOverpassParser
{
    /// <summary>
    /// Parsuje surový JSON z Overpass API a vytváří doménové entity i konektivní hrany podle technické specifikace.
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

                        // OPRAVA CHYBĚJÍCÍHO POSLEDNÍHO UZLU: Vytvoříme VŠECHNY body od 0 do polylineCoords.Count - 1
                        for (int i = 0; i < polylineCoords.Count; i++)
                        {
                            var pt = polylineCoords[i];
                            string nodeEntityId = $"water_node_{id}_{i}";
                            result.Entities.Add(new SpatialEntity
                            {
                                Id = nodeEntityId,
                                Type = SpatialEntityTypes.Place,
                                Name = $"Bod toku {waterName} #{i}",
                                ParentId = segment.Id,
                                Spatial = new SpatialComponent { Frame = "World", Kind = GeometryKind.Point, GlobalAnchor = new GeoAnchor { Lat = pt[1], Lon = pt[0] } },
                                Semantic = new SemanticComponent { Tags = ["water_node", "stream_point"] }
                            });
                        }

                        // Propojení jednotlivých bodů hranami
                        for (int i = 0; i < polylineCoords.Count - 1; i++)
                        {
                            var p1 = polylineCoords[i];
                            var p2 = polylineCoords[i + 1];
                            double dist = CalculateDistMeters(p1[1], p1[0], p2[1], p2[0]);

                            string w1Id = $"water_node_{id}_{i}";
                            string w2Id = $"water_node_{id}_{i + 1}";

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
                    string roadName = FormatRoadName(tags, highwayType);
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
                            Description = $"Úsek cestní sítě {roadName} (typ: {highwayType}, celkem {polylineCoords.Count} bodů)."
                        }
                    };
                    result.Entities.Add(roadEntity);

                    // OPRAVA CHYBĚJÍCÍHO POSLEDNÍHO UZLU: Vytvoříme VŠECHNY uzly cesty od 0 do polylineCoords.Count - 1
                    for (int i = 0; i < polylineCoords.Count; i++)
                    {
                        var pt = polylineCoords[i];
                        string nodeEntityId = $"road_node_{id}_{i}";
                        result.Entities.Add(new SpatialEntity
                        {
                            Id = nodeEntityId,
                            Type = SpatialEntityTypes.Place,
                            Name = $"Uzol cesty {roadName} #{i}",
                            ParentId = parentSettlementId,
                            Spatial = new SpatialComponent { Frame = "World", Kind = GeometryKind.Point, GlobalAnchor = new GeoAnchor { Lat = pt[1], Lon = pt[0] } },
                            Semantic = new SemanticComponent { Tags = ["road_node", "waypoint"] }
                        });
                    }

                    // Propojení hranami mezi uzly i a i+1
                    for (int i = 0; i < polylineCoords.Count - 1; i++)
                    {
                        var p1 = polylineCoords[i];
                        var p2 = polylineCoords[i + 1];
                        double dist = CalculateDistMeters(p1[1], p1[0], p2[1], p2[0]);

                        string n1Id = $"road_node_{id}_{i}";
                        string n2Id = $"road_node_{id}_{i + 1}";

                        bool isBridge = tags.ContainsKey("bridge") && tags["bridge"] != "no";
                        bool isFord = tags.ContainsKey("ford") && tags["ford"] != "no";
                        string[] pathTypes = ["track", "path", "footway", "bridleway", "steps", "pedestrian"];
                        bool isPath = pathTypes.Contains(highwayType);

                        result.Edges.Add(new ConnectivityEdge
                        {
                            Id = $"edge_road_{id}_{i}",
                            FromId = n1Id,
                            ToId = n2Id,
                            Kind = isBridge ? "Bridge" : (isFord ? "Ford" : (isPath ? "Path" : "Road")),
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

                    string bType = tags.TryGetValue("building", out var bt) ? bt : "house";
                    string name = tags.TryGetValue("name", out var bName) ? bName : FormatBuildingName(tags, houseNumInt, bType);
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
                            Tags = ["building", bType],
                            Description = $"Reálná budova z OpenStreetMap (OSM ID: {id}, typ: {bType}) na souřadnicích {centerLat:F6}° N, {centerLon:F6}° E.",
                            Attributes = new Dictionary<string, object>
                            {
                                { "osm_id", id },
                                { "house_number", houseNumInt },
                                { "building_type", bType }
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
                            Description = $"Přilehlý dvůr a zahrada k objektu Čp. {houseNumInt}."
                        }
                    });
                }
                // 5. Zájmové body POI (Kaplička, Pomník, Zastávka, Schránka, Kříž, Boží muka, Kontejnery...)
                else if (tags.ContainsKey("amenity") || tags.ContainsKey("historic") || tags.ContainsKey("tourism") || tags.ContainsKey("natural") || tags.ContainsKey("man_made"))
                {
                    string poiName = FormatPoiName(tags);
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
                            Tags = ["place", "poi", "osm", .. ExtractPoiTags(tags)],
                            Description = $"Reálné zájmové místo ({poiName}) z OpenStreetMap na souřadnicích {centerLat:F6}° N, {centerLon:F6}° E."
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

    /// <summary>
    /// Vytváří specifický, lidsky čitelný český název zájmového místa (POI) z OSM tagů namísto obecného "Zájmové místo".
    /// </summary>
    private static string FormatPoiName(Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
            return name;
        if (tags.TryGetValue("description", out var desc) && !string.IsNullOrWhiteSpace(desc))
            return desc;

        if (tags.TryGetValue("historic", out var historic))
        {
            return historic switch
            {
                "wayside_cross" => "Kříž u cesty / Boží muka",
                "wayside_shrine" => "Výklenková kaplička / Boží muka",
                "memorial" => "Památník / Pomník obětem",
                "monument" => "Pomník / Monument",
                "cross" => "Kříž u cesty",
                "archaeological_site" => "Archeologické naleziště",
                "castle" or "ruins" => "Zřícenina / Hrad",
                _ => $"Historická památka ({historic})"
            };
        }

        if (tags.TryGetValue("amenity", out var amenity))
        {
            return amenity switch
            {
                "recycling" => "Kontejnery na tříděný odpad",
                "post_box" => "Poštovní schránka Česká pošta",
                "bus_station" or "bus_stop" => "Autobusová zastávka",
                "shelter" => "Přístřešek / Zastávka",
                "fire_station" => "Hasičská zbrojnice",
                "place_of_worship" => "Kaple / Kostel",
                "pub" or "restaurant" => "Hostinec / Hospoda",
                "bench" => "Lavička k odpočinku",
                "drinking_water" or "water_point" => "Pramen / Pitná voda",
                "waste_basket" => "Odpadkový koš",
                _ => $"Zařízení obce ({amenity})"
            };
        }

        if (tags.TryGetValue("natural", out var natural))
        {
            return natural switch
            {
                "tree" => "Památný / Významný strom",
                "spring" => "Pramen / Studánka",
                "peak" => "Vrchol / Kopec",
                "cave_entrance" => "Vstup do jeskyně",
                _ => $"Přírodní prvek ({natural})"
            };
        }

        if (tags.TryGetValue("man_made", out var manMade))
        {
            return manMade switch
            {
                "cross" => "Kříž u cesty",
                "water_tower" => "Vodojem / Vodárenská věž",
                "survey_point" => "Geodetický bod",
                _ => $"Stavba / Objekt ({manMade})"
            };
        }

        if (tags.TryGetValue("tourism", out var tourism))
        {
            return tourism switch
            {
                "information" => "Informační tabule / Rozcestník",
                "viewpoint" => "Vyhlídka / Vyhlídkové místo",
                "picnic_site" => "Odpočívadlo / Piknikové místo",
                "attraction" => "Turistický cíl",
                _ => $"Turistické místo ({tourism})"
            };
        }

        return "Významný objekt / POI";
    }

    private static string FormatRoadName(Dictionary<string, string> tags, string highwayType)
    {
        if (tags.TryGetValue("name", out var rName) && !string.IsNullOrWhiteSpace(rName))
            return rName;

        return highwayType switch
        {
            "residential" => "Obecní ulice / Obytná zóna",
            "unclassified" => "Obecní cesta",
            "tertiary" or "secondary" => "Krajská silnice",
            "service" => "Příjezdová / Účelová cesta",
            "track" => "Polní / Lesní cesta",
            "path" => "Pěšina / Stezka",
            "footway" => "Chodník / Pěší zóna",
            "steps" => "Schodiště",
            _ => $"Cesta ({highwayType})"
        };
    }

    private static string FormatBuildingName(Dictionary<string, string> tags, int houseNum, string buildingType)
    {
        string desc = buildingType switch
        {
            "farm_auxiliary" or "barn" or "shed" => "Hospodářská budova / Stodola",
            "garage" or "garages" => "Garáž",
            "chapel" or "church" => "Kaple / Kostel",
            "commercial" or "retail" => "Komerční objekt / Prodejna",
            "apartments" => "Bytový dům",
            _ => "Rodinný dům"
        };
        return $"Čp. {houseNum} ({desc})";
    }

    private static List<string> ExtractPoiTags(Dictionary<string, string> tags)
    {
        var list = new List<string>();
        foreach (var k in new[] { "amenity", "historic", "natural", "man_made", "tourism", "leisure" })
        {
            if (tags.TryGetValue(k, out var val)) list.Add(val);
        }
        return list;
    }

    private static double CalculateDistMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * 111320.0;
        double dLon = (lon2 - lon1) * 72000.0;
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }
}
