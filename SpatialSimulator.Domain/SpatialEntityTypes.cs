namespace SpatialSimulator.Domain;

/// <summary>
/// Definice konstantních řetězců pro typy prostorových entit.
/// Slouží pro jednotné označování uzlů v hierarchickém stromu obsahování (containment tree).
/// </summary>
public static class SpatialEntityTypes
{
    /// <summary>
    /// Sídlo / Obec (např. Runářov, Konice, Moravská Třebová).
    /// </summary>
    public const string Settlement = "Settlement";

    /// <summary>
    /// Mezivrstva pro část obce, náves, čtvrť nebo ulici.
    /// </summary>
    public const string Area = "Area";

    /// <summary>
    /// Pozemek zavedený z katastru nemovitostí (RÚIAN).
    /// </summary>
    public const string Parcel = "Parcel";

    /// <summary>
    /// Stavební objekt / Budova spojená se zemí.
    /// </summary>
    public const string Building = "Building";

    /// <summary>
    /// Podlaží / Patro uvnitř budovy.
    /// </summary>
    public const string Floor = "Floor";

    /// <summary>
    /// Místnost na daném podlaží (kuchyň, chodba, obývák, ložnice).
    /// </summary>
    public const string Room = "Room";

    /// <summary>
    /// Nábytek neposuvný nebo pomalu měnitelný (stůl, skříň, kamna).
    /// </summary>
    public const string Furniture = "Furniture";

    /// <summary>
    /// Pevné vybavení interiéru (dřez, umyvadlo, radiátor).
    /// </summary>
    public const string Fixture = "Fixture";

    /// <summary>
    /// Kontejnery obsahující další věci (kapsa, taška, zásuvka, krabice).
    /// </summary>
    public const string Container = "Container";

    /// <summary>
    /// Jednotlivé přenosné předměty (sirky, klíč, dokument).
    /// </summary>
    public const string Item = "Item";

    /// <summary>
    /// AI agent operující v simulovaném prostředí.
    /// </summary>
    public const string Agent = "Agent";

    /// <summary>
    /// Oblečení nošené agentem (funguje i jako kontejner pro kapsy).
    /// </summary>
    public const string Clothing = "Clothing";

    /// <summary>
    /// Venkovní zájmový bod nebo křižovatka bez budovy (kaplička, studna, zastávka).
    /// </summary>
    public const string Place = "Place";
}
