using SpatialSimulator.Domain.Components;

namespace SpatialSimulator.Domain.Events;

/// <summary>
/// Záznam události v globální časové ose událostí (GTU).
/// Motivace: Tvoří primární log simulace událostí i zkušenostní paměťový stream agentů.
/// </summary>
public class SimEvent
{
    /// <summary>
    /// Unikátní identifikátor události.
    /// Motivace: Jednoznačné dohledání události v GTU.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>
    /// Simulované časové razítko vzniku události (UTC).
    /// Motivace: Řazení událostí v simulovaném čase diskrétní simulace.
    /// </summary>
    public DateTime Ts { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Druh události ("Observation", "Action", "Dialogue", "Reflection", "StateChange").
    /// Motivace: Klasifikuje událost pro potřeby vyhledávání v paměti a vizualizace.
    /// </summary>
    public string Kind { get; set; } = "Observation";

    /// <summary>
    /// Identifikátor lokace (`SpatialEntity`), kde k události došlo.
    /// Motivace: Prostorové ukotvení událostí v prostředí simulace.
    /// </summary>
    public string? LocationId { get; set; }

    /// <summary>
    /// Seznam ID přítomných nebo zúčastněných agentů.
    /// Motivace: Umožňuje rychlé dotazování na osobní paměťový stream konkrétního agenta.
    /// </summary>
    public List<string> Participants { get; set; } = [];

    /// <summary>
    /// Přirozený textový popis události (např. "Jana našla v kapse krabičku sirek").
    /// Motivace: Vstupuje do paměťového algoritmu vyhledávání a LLM promptů.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Subjektivní skóre důležitosti události na škále 1.0 až 10.0 (dle Park et al. 2023).
    /// Motivace: Používá se v algoritmu Stanford paměti při vyhodnocování skóre paměti.
    /// </summary>
    public double Importance { get; set; } = 1.0;

    /// <summary>
    /// Nepovinný vektorový embedding textu události pro sémantické vyhledávání.
    /// Motivace: Umožňuje kosinovou podobnost při vektorovém vyhledávání vzpomínek.
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Seznam ID zdrojových událostí v případě, že se jedná o událost typu "Reflection".
    /// Motivace: Udržuje zakladatelský strom vyvozených abstraktních reflexí z primárních pozorování.
    /// </summary>
    public List<string>? DerivedFrom { get; set; }

    /// <summary>
    /// Komponenta provenience události.
    /// Motivace: Ukládá původ vzniku události (akce agenta vs. systémový fakt).
    /// </summary>
    public ProvenanceComponent? Provenance { get; set; }
}
