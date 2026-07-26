using SpatialSimulator.Domain.Components;

namespace SpatialSimulator.Domain.Graph;

/// <summary>
/// Hrana grafu konektivity reprezentující přechod nebo viditelnost mezi dvěma uzly.
/// Motivace: Tvoří samostatný graf propustnosti (traversal graph) nezávislý na stromu obsahování (containment tree).
/// Slouží pro pathfinding a určení, odkud kam se dá dojít.
/// </summary>
public class ConnectivityEdge
{
    /// <summary>
    /// Unikátní identifikátor hrany.
    /// Motivace: Jednoznačná identifikace přechodu.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>
    /// ID výchozí prostorové entity.
    /// Motivace: Počáteční uzel přechodu.
    /// </summary>
    public string FromId { get; set; } = string.Empty;

    /// <summary>
    /// ID cílové prostorové entity.
    /// Motivace: Cílový uzel přechodu.
    /// </summary>
    public string ToId { get; set; } = string.Empty;

    /// <summary>
    /// Druh/typ přechodu (např. "Door", "Corridor", "Stairs", "Path", "Road", "Window", "Gate").
    /// Motivace: Určuje sémantický typ propojení pro rozhodování agenta.
    /// </summary>
    public string Kind { get; set; } = "Door";

    /// <summary>
    /// Příznak, zda je přechod obousměrný (výchozí true).
    /// Motivace: Jednosměrné hrany umožňují simulovat jednosměrky, skoky z okna nebo tajné dveře.
    /// </summary>
    public bool Bidirectional { get; set; } = true;

    /// <summary>
    /// Délka/cena přechodu v metrech pro algoritmy nejkratší cesty (Dijkstra).
    /// Motivace: Výpočet časové a fyzické náročnosti pohybu agenta.
    /// </summary>
    public double CostMeters { get; set; } = 1.0;

    /// <summary>
    /// Odhadovaná časová náročnost přechodu v sekundách (nepovinná).
    /// Motivace: Umožňuje přesné plánování času v simulaci.
    /// </summary>
    public double? CostSeconds { get; set; }

    /// <summary>
    /// Aktuální stav přechodu ("Open", "Closed", "Locked").
    /// Motivace: Uzamčené nebo zavřené dveře blokují průchod agenta a vyžadují akci (klíč).
    /// </summary>
    public string State { get; set; } = "Open";

    /// <summary>
    /// Seznam podmínek nutných pro použití přechodu (např. ["has_key:room_12"]).
    /// Motivace: Podporuje interaktivní logiku přístupu do uzamčených prostor.
    /// </summary>
    public List<string>? RequiredConditions { get; set; }

    /// <summary>
    /// Komponenta provenience hrany.
    /// Motivace: Určuje originální zdroj hrany (z mapy OSM vs. vygenerované dveře).
    /// </summary>
    public ProvenanceComponent? Provenance { get; set; }
}
