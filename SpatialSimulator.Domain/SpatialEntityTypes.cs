namespace SpatialSimulator.Domain;

/// <summary>
/// Konstanty definující standardní typy sémanticko-prostorových entit v systému podle technické specifikace.
/// Motivace: Poskytuje sjednocenou množinu řetězcových typů uzlů pro containment strom.
/// </summary>
public static class SpatialEntityTypes
{
    /// <summary>Sídlo / obec / městys (např. Runářov, Konice).</summary>
    public const string Settlement = "Settlement";

    /// <summary>Oblast / čtvrť / část obce / náves.</summary>
    public const string Area = "Area";

    /// <summary>Katastrální parcela (pozemecký uzel z katastru).</summary>
    public const string Parcel = "Parcel";

    /// <summary>Venkovní dvůr nebo zahrada přiléhající k budově/parcele (nezastavěná plocha parcel).</summary>
    public const string Yard = "Yard";

    /// <summary>Stavební objekt / budova.</summary>
    public const string Building = "Building";

    /// <summary>Podlaží / patro budovy (1. NP, 2. NP...)</summary>
    public const string Floor = "Floor";

    /// <summary>Místnost uvnitř podlaží.</summary>
    public const string Room = "Room";

    /// <summary>Zájmové venkovní místo (kaple, zastávka, křižovatka, studna).</summary>
    public const string Place = "Place";

    /// <summary>Celkový liniový prvek (např. Runářovský potok, hlavní cesta, souvislý plot).</summary>
    public const string LinearFeature = "LinearFeature";

    /// <summary>Úsek liniového prvku se sekvenčním OrderIndex (např. 80m úsek potoka).</summary>
    public const string LinearSegment = "LinearSegment";

    /// <summary>Plošný pokryv území mimo zástavbu (les, pole, louka, rybník, sad).</summary>
    public const string LandCover = "LandCover";

    /// <summary>Kus nábytku nebo venkovní přístřešek (altán, kůlna, kamna, věšák).</summary>
    public const string Furniture = "Furniture";

    /// <summary>Oblečení nošené agentem (zimní kabát, bunda).</summary>
    public const string Clothing = "Clothing";

    /// <summary>Úložný kontejner (kapsa, zásuvka, taška, krabice).</summary>
    public const string Container = "Container";

    /// <summary>Fyzický předmět (sirky, klíče, peněženka).</summary>
    public const string Item = "Item";

    /// <summary>AI agent / obyvatel lokality.</summary>
    public const string Agent = "Agent";
}
