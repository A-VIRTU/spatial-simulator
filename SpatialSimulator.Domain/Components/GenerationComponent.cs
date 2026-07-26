namespace SpatialSimulator.Domain.Components;

/// <summary>
/// Stav domyšlení / generování detailů dané prostorové entity.
/// Motivace: Řídí líné (on-demand) dopočítávání detailů světa, aby nemusely být generovány všechny místnosti předem.
/// </summary>
public enum GenerationState
{
    /// <summary>
    /// Uzel existuje, ale jeho vnitřní obsah ještě nebyl vygenerován.
    /// </summary>
    NotGenerated,

    /// <summary>
    /// Je znám seznam dětských uzlů (např. názvy místností), ale ne jejich vnitřní vybavení.
    /// </summary>
    Outlined,

    /// <summary>
    /// Dětské uzly mají vyplněné vlastní atributy a sémantický popis.
    /// </summary>
    Detailed,

    /// <summary>
    /// Údaje byly ověřeny ručně nebo reálnou interakcí agenty (nejvyšší důvěra).
    /// </summary>
    Verified
}

/// <summary>
/// Generační komponenta sledovací proces doplňování informací o entitě.
/// Motivace: Poskytuje simulátoru přehled o metodě generování a stavu rozpracovanosti daného uzlu.
/// </summary>
public class GenerationComponent
{
    /// <summary>
    /// Aktuální úroveň detailu vygenerovaného obsahu entity.
    /// Motivace: Určuje, zda je nutné spustit generační službu před vstupem agenta.
    /// </summary>
    public GenerationState State { get; set; } = GenerationState.NotGenerated;

    /// <summary>
    /// Použitá metoda generování obsahu.
    /// Nabývá hodnot: "cadastre" (katastr), "osm" (OpenStreetMap), "rule-template" (pravidlová šablona), "llm" (LLM model), "manual" (ruční zadání).
    /// Motivace: Umožňuje auditovat a ladit zdroje generační logiky.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// Časové razítko (UTC), kdy byl obsah entity naposledy vygenerován či aktualizován.
    /// Motivace: Slouží k invalidaci nebo řízení re-generování starých dat.
    /// </summary>
    public DateTime? GeneratedAt { get; set; }

    /// <summary>
    /// Očekávaný počet dětských uzlů (nepovinný).
    /// Motivace: Pomáhá odhadnout kapacitu nebo verifikovat úplnost generování.
    /// </summary>
    public int? ExpectedChildCount { get; set; }
}
