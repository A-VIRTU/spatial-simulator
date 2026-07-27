using SpatialSimulator.Domain.Components;

namespace SpatialSimulator.Domain.Entities;

/// <summary>
/// Polymorfní doménová entita reprezentující libovolný prostorový uzel v simulátoru.
/// Motivace: Zabezpečuje jednotný datový model pro město, budovu, patro, místnost, potok, nábytek i agenta.
/// </summary>
public class SpatialEntity
{
    /// <summary>Unikátní řetězcové ID entity (napsáno jako slug nebo ObjectId).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Typ entity podle `SpatialEntityTypes` ("Settlement", "Building", "Floor", "LinearFeature", "LinearSegment"...).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Lidsky čitelný název entity.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>ID rodičovského uzlu v containment stromu.</summary>
    public string? ParentId { get; set; }

    /// <summary>Seznam ID všech předků od kořene k rodiči.</summary>
    public List<string> Ancestors { get; set; } = [];

    /// <summary>Materializovaná cesta stromem pro rychlé vyhledávání (např. "/settlement_runarov/building_cp_23").</summary>
    public string MaterializedPath { get; set; } = string.Empty;

    /// <summary>Hloubka zanoření ve stromu (0 pro kořen).</summary>
    public int Depth { get; set; }

    /// <summary>Pořadí uzlu mezi sourozenci (pro řazení úseků potoka nebo pater domu).</summary>
    public int? OrderIndex { get; set; }

    /// <summary>Prostorové a geometrické ukotvení entity.</summary>
    public SpatialComponent? Spatial { get; set; }

    /// <summary>Sémantický popis, tagy a atributy pro LLM agenty.</summary>
    public SemanticComponent Semantic { get; set; } = new();

    /// <summary>Stav líné generace podstromu (NotGenerated, Outlined, Detailed, Verified).</summary>
    public GenerationComponent Generation { get; set; } = new();

    /// <summary>Provenience dat (zdroj, spolehlivost, čas extrakce).</summary>
    public ProvenanceComponent Provenance { get; set; } = new();

    /// <summary>Kapacita objektu pro obyvatele nebo předměty.</summary>
    public CapacityComponent? Capacity { get; set; }

    /// <summary>Komponenta agenta (pouze pokud Type == SpatialEntityTypes.Agent).</summary>
    public AgentComponent? Agent { get; set; }

    /// <summary>Nehierarchické prostorové vztahy (překryvy, sousedství, břehy potoka).</summary>
    public List<SpatialRelation>? Relations { get; set; }

    /// <summary>Externí identifikátory (RÚIAN, OSM, DIBAVOD).</summary>
    public Dictionary<string, string> ExternalRefs { get; set; } = new();

    /// <summary>Verze datového schématu.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Čas vytvoření dokumentu.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Čas poslední aktualizace dokumentu.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
