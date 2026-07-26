using SpatialSimulator.Domain.Components;

namespace SpatialSimulator.Domain.Entities;

/// <summary>
/// Základní doménový model prostorové entity reprezentující libovolný uzel ve stromu obsahování.
/// Reprezentuje město, část obce, pozemek, dům, patro, místnost, nábytek, oblečení, kapsu, sirky i agenta.
/// Motivace: Polymorfní ECS architektura umožňuje dynamicky připojovat komponenty podle úrovně detailu a potřeby.
/// </summary>
public class SpatialEntity
{
    /// <summary>
    /// Unikátní identifikátor entity (slug nebo ObjectId).
    /// Motivace: Jednoznačná identifikace uzlu napříč databází a grafem.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>
    /// Typ entity z definovaných konstant v <see cref="SpatialEntityTypes"/>.
    /// Motivace: Určuje sémantickou roli uzlu ve stromu.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Lidsky čitelný název entity (např. "Čp. 23", "Kuchyň", "Zimní kabát").
    /// Motivace: Zobrazování v UI a v LLM promptech agentů.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Identifikátor rodičovské entity ve stromu obsahování.
    /// Motivace: Tvoří primární stromový vztah containment (co je v čem).
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Pole identifikátorů všech předků od kořene až po rodiče (Array of Ancestors vzor).
    /// Motivace: Umožňuje rychlé podstromové dotazy v databázi bez nutnosti rekurzivního prolézání.
    /// </summary>
    public List<string> Ancestors { get; set; } = [];

    /// <summary>
    /// Materializovaná cesta reprezentující stromovou větvenou trasu (např. "/settlement_runarov/building_23/floor_1/room_2").
    /// Motivace: Rychlé prefixové dotazy a přehledná čitelnost v logách.
    /// </summary>
    public string MaterializedPath { get; set; } = string.Empty;

    /// <summary>
    /// Hloubka zanoření v hierarchii stromu (0 pro kořen).
    /// Motivace: Snadné filtrování podle úrovně detailu.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Nepovinná prostorová komponenta (geometrická poloha a rozměry).
    /// Motivace: Není vyžadována u malých předmětů v kapse, ale je klíčová pro budovy a místnosti.
    /// </summary>
    public SpatialComponent? Spatial { get; set; }

    /// <summary>
    /// Sémantická komponenta s tagy, popisem a atributy.
    /// Motivace: Poskytuje popisný kontext pro rozhodování AI agentů.
    /// </summary>
    public SemanticComponent Semantic { get; set; } = new();

    /// <summary>
    /// Generační komponenta sledující stav domyšlení uzlu.
    /// Motivace: Řídí línou on-demand generaci místností a obsahu.
    /// </summary>
    public GenerationComponent Generation { get; set; } = new();

    /// <summary>
    /// Komponenta provenience definující původ dat.
    /// Motivace: Udržuje informaci o důvěryhodnosti zdroje (katastr vs. odhad LLM).
    /// </summary>
    public ProvenanceComponent? Provenance { get; set; }

    /// <summary>
    /// Nepovinná komponenta kapacity objektu.
    /// Motivace: Stanovuje fyzické limity počtu osob nebo vkládaných předmětů.
    /// </summary>
    public CapacityComponent? Capacity { get; set; }

    /// <summary>
    /// Agentní komponenta obsahující stav agenta. Vyplňuje se pouze pokud je Type == "Agent".
    /// Motivace: Uchovává specifická data agentního chování a polohy.
    /// </summary>
    public AgentComponent? Agent { get; set; }

    /// <summary>
    /// Externí reference na okolní databáze (např. {"ruian": "12345678", "osm": "way/123"}).
    /// Motivace: Zabezpečuje idempotenci při opakovaných importech z veřejných zdrojů.
    /// </summary>
    public Dictionary<string, string> ExternalRefs { get; set; } = new();

    /// <summary>
    /// Verze schématu dokumentu pro podporu budoucích migrací.
    /// Motivace: Zajišťuje zpětnou kompatibilitu při vývoji datového modelu.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Časové razítko vytvoření záznamu (UTC).
    /// Motivace: Auditní sledování historie uložení.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Časové razítko poslední aktualizace záznamu (UTC).
    /// Motivace: Sledování úprav a synchrónních změn.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
