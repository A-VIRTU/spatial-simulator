namespace SpatialSimulator.Domain.Components;

/// <summary>
/// Sémantická komponenta entity obsahující slovní popisy, značky a strukturované atributy.
/// Motivace: Poskytuje bohatý sémantický kontext pro LLM agentní prompty a vyhledávání, nezávisle na prostorové geometrii.
/// </summary>
public class SemanticComponent
{
    /// <summary>
    /// Seznam sémantických tagů (např. ["residential", "family_house", "kitchen"]).
    /// Motivace: Umožňuje rychlé klasifikace a filtrování entit.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Přirozený textový popis prostředí určený přímo pro vložení do LLM promptu agenta.
    /// Motivace: Poskytuje agentům představu o vzhledu a atmosféře místa.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Slovník strukturovaných doménových atributů (např. počet podlaží, kód využití budovy, typ vytápění).
    /// Motivace: Ukládá přesné údaje získané z katastru (RÚIAN) nebo dotazníků.
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();
}
