namespace SpatialSimulator.Domain.Components;

/// <summary>
/// Komponenta provenience určující původ a věrohodnost uložených dat.
/// Motivace: Rozlišuje ověřené údaje z katastru nemovitostí od odhadů generovaných LLM nebo pozorovaných agentem.
/// </summary>
public class ProvenanceComponent
{
    /// <summary>
    /// Primární zdroj dat (např. "RUIAN", "OSM", "Mapillary", "vision-llm", "manual", "rule-template").
    /// Motivace: Umožňuje dohledat původ každého faktu v simulaci.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Identifikátor nebo odkaz na zdrojový záznam (např. ID stavebního objektu v RÚIAN nebo OSM ID cesty).
    /// Motivace: Zajišťuje zpětnou křížovou kontrolu s externími databázemi.
    /// </summary>
    public string? SourceRef { get; set; }

    /// <summary>
    /// Časové razítko extrakce či nahrání dat (UTC).
    /// Motivace: Sleduje stáří a čerstvost získané informace.
    /// </summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Míra důvěryhodnosti údaje v rozmezí 0.0 až 1.0.
    /// Motivace: Fakt z katastru má spolehlivost 0.95-1.0, zatímco odhad LLM z fotky má spolehlivost &lt; 0.5.
    /// </summary>
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// Komponenta kapacity omezující fyzický objem a počet osob či předmětů uvnitř entity.
/// Motivace: Zamezuje přelidnění místností a překročení fyzických limitů kontejnerů.
/// </summary>
public class CapacityComponent
{
    /// <summary>
    /// Maximální povolený počet současně přítomných obyvatel/agentů.
    /// Motivace: Určuje obsaditelnost místnosti nebo budovy.
    /// </summary>
    public int? MaxOccupants { get; set; }

    /// <summary>
    /// Maximální vnitřní objem v litrech.
    /// Motivace: Umožňuje simulovat kapacitu úložných prostor (např. batoh, kufřík).
    /// </summary>
    public double? MaxVolumeLiters { get; set; }

    /// <summary>
    /// Maximální počet vkládaných předmětů.
    /// Motivace: Omezuje kapacitu malých kontejnerů (např. kapsa kabátu s max. 5 předměty).
    /// </summary>
    public int? MaxItemCount { get; set; }
}

/// <summary>
/// Komponenta agenta specifikující jeho profil, polohu a stav v simulaci.
/// Vyplňuje se pouze tehdy, když je typ entity rovný "Agent".
/// Motivace: Odděluje prostorovou existenci agenta v prostředí od jeho psychologické persony a cílů.
/// </summary>
public class AgentComponent
{
    /// <summary>
    /// Odkaz na profil persony agenta uložený v externím systému.
    /// Motivace: Propojuje agenta v prostoru s jeho prediktivním modelem jednotlivce (PMJ).
    /// </summary>
    public string PersonaRef { get; set; } = string.Empty;

    /// <summary>
    /// ID lokace (`SpatialEntity`), ve které se agent právě fyzicky nachází.
    /// Motivace: Umožňuje přímé a rychlé dotazování na aktuální polohu agentů bez procházení celým stromem.
    /// </summary>
    public string CurrentLocationId { get; set; } = string.Empty;

    /// <summary>
    /// Aktuální sledovaný cíl agenta (např. "Uvařit oběd").
    /// Motivace: Vstupuje do percepčního promptu jako hlavní motivace agentního chování.
    /// </summary>
    public string? CurrentGoal { get; set; }

    /// <summary>
    /// Časové razítko poslední provedené akce agenty (UTC).
    /// Motivace: Slouží k plánování a řazení akcí v diskrétní simulaci událostí.
    /// </summary>
    public DateTime LastActedAt { get; set; } = DateTime.UtcNow;
}
