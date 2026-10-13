namespace LnShelf.Client.Components;

/// <summary>
/// The icon set. Phosphor regular, vendored as path data so the client makes no
/// request for an icon font or sprite sheet.
/// </summary>
public enum IconName
{
    Books,
    CalendarBlank,
    MagnifyingGlass,
    ChartBar,
    Close,
    FileArrowUp,
    Check,
    BookOpen,
    ArrowUpRight,
    CaretLeft,
    Plus,
    SquaresFour,
    GridNine,
    ImageSquare,
    User,
    CaretRight,
    DotsThree,
    SlidersHorizontal,
    CaretUp,
    CaretDown,
    Compass,
}

public partial class Icon
{
    /// <summary>Which icon to draw.</summary>
    [Parameter]
    public required IconName Name { get; set; }

    /// <summary>Edge length in pixels. design.md sizes icons 12-16.</summary>
    [Parameter]
    public int Size { get; set; } = 15;

    private string PathData => Name switch
    {
        IconName.Books => BooksPath,
        IconName.CalendarBlank => CalendarBlankPath,
        IconName.MagnifyingGlass => MagnifyingGlassPath,
        IconName.ChartBar => ChartBarPath,
        IconName.Close => ClosePath,
        IconName.FileArrowUp => FileArrowUpPath,
        IconName.Check => CheckPath,
        IconName.BookOpen => BookOpenPath,
        IconName.Compass => CompassPath,
        IconName.ArrowUpRight => ArrowUpRightPath,
        IconName.CaretLeft => CaretLeftPath,
        IconName.Plus => PlusPath,
        IconName.SquaresFour => SquaresFourPath,
        IconName.GridNine => GridNinePath,
        IconName.ImageSquare => ImageSquarePath,
        IconName.User => UserPath,
        IconName.CaretRight => CaretRightPath,
        IconName.DotsThree => DotsThreePath,
        IconName.SlidersHorizontal => SlidersHorizontalPath,
        IconName.CaretUp => CaretUpPath,
        IconName.CaretDown => CaretDownPath,
        _ => throw new ArgumentOutOfRangeException(nameof(Name), Name, "Unknown icon."),
    };

    private const string BooksPath =
        "M231.65,194.55,198.46,36.75a16,16,0,0,0-19-12.39L132.65,34.42a16.08,16.08,0,0,0-12.3,19l33.19," +
        "157.8A16,16,0,0,0,169.16,224a16.25,16.25,0,0,0,3.38-.36l46.81-10.06A16.09,16.09,0,0,0,231.65," +
        "194.55ZM136,50.15c0-.06,0-.09,0-.09l46.8-10,3.33,15.87L139.33,66Zm6.62,31.47,46.82-10.05,3.34," +
        "15.9L146,97.53Zm6.64,31.57,46.82-10.06,13.3,63.24-46.82,10.06ZM216,197.94l-46.8,10-3.33-15.87L212.67," +
        "182,216,197.85C216,197.91,216,197.94,216,197.94ZM104,32H56A16,16,0,0,0,40,48V208a16,16,0,0,0,16," +
        "16h48a16,16,0,0,0,16-16V48A16,16,0,0,0,104,32ZM56,48h48V64H56Zm0,32h48v96H56Zm48,128H56V192h48v16Z";

    private const string CalendarBlankPath =
        "M208,32H184V24a8,8,0,0,0-16,0v8H88V24a8,8,0,0,0-16,0v8H48A16,16,0,0,0,32,48V208a16,16,0,0,0,16," +
        "16H208a16,16,0,0,0,16-16V48A16,16,0,0,0,208,32ZM72,48v8a8,8,0,0,0,16,0V48h80v8a8,8,0,0,0,16,0V48h24V80H48V48Z" +
        "M208,208H48V96H208V208Z";

    private const string MagnifyingGlassPath =
        "M229.66,218.34l-50.07-50.06a88.11,88.11,0,1,0-11.31,11.31l50.06,50.07a8,8,0,0,0,11.32-11.32ZM40," +
        "112a72,72,0,1,1,72,72A72.08,72.08,0,0,1,40,112Z";

    private const string ChartBarPath =
        "M224,200h-8V40a8,8,0,0,0-8-8H152a8,8,0,0,0-8,8V80H96a8,8,0,0,0-8,8v40H48a8,8,0,0,0-8,8v64H32a8,8,0,0,0,0," +
        "16H224a8,8,0,0,0,0-16ZM160,48h40V200H160ZM104,96h40V200H104ZM56,144H88v56H56Z";

    private const string ClosePath =
        "M205.66,194.34a8,8,0,0,1-11.32,11.32L128,139.31,61.66,205.66a8,8,0,0,1-11.32-11.32L116.69,128,50.34," +
        "61.66A8,8,0,0,1,61.66,50.34L128,116.69l66.34-66.35a8,8,0,0,1,11.32,11.32L139.31,128Z";

    private const string FileArrowUpPath =
        "M213.66,82.34l-56-56A8,8,0,0,0,152,24H56A16,16,0,0,0,40,40V216a16,16,0,0,0,16,16H200a16,16,0,0,0,16-16V88A8," +
        "8,0,0,0,213.66,82.34ZM160,51.31,188.69,80H160ZM200,216H56V40h88V88a8,8,0,0,0,8,8h48V216Zm-42.34-77.66a8,8,0," +
        "0,1-11.32,11.32L136,139.31V184a8,8,0,0,1-16,0V139.31l-10.34,10.35a8,8,0,0,1-11.32-11.32l24-24a8,8,0,0,1,11.32,0Z";

    private const string CompassPath =
        "M128,24A104,104,0,1,0,232,128,104.11,104.11,0,0,0,128,24Zm0,192a88,88,0,1,1,88-88A88.1,88.1,0,0,1,128,216ZM" +
        "172.42,72.84l-64,32a8.05,8.05,0,0,0-3.58,3.58l-32,64A8,8,0,0,0,80,184a8.1,8.1,0,0,0,3.58-.84l64-32a8.05,8.05," +
        "0,0,0,3.58-3.58l32-64a8,8,0,0,0-10.74-10.74ZM138,138,97.89,158.11,118,118l40.15-20.07Z";

    private const string BookOpenPath =
        "M232,48H160a40,40,0,0,0-32,16A40,40,0,0,0,96,48H24a8,8,0,0,0-8,8V200a8,8,0,0,0,8,8H96a24,24,0,0,1,24,24,8," +
        "8,0,0,0,16,0,24,24,0,0,1,24-24h72a8,8,0,0,0,8-8V56A8,8,0,0,0,232,48ZM96,192H32V64H96a24,24,0,0,1,24,24V200A" +
        "39.81,39.81,0,0,0,96,192Zm128,0H160a39.81,39.81,0,0,0-24,8V88a24,24,0,0,1,24-24h64Z";

    private const string ArrowUpRightPath =
        "M200,64V168a8,8,0,0,1-16,0V83.31L69.66,197.66a8,8,0,0,1-11.32-11.32L172.69,72H88a8,8,0,0,1,0-16H192A8,8,0,0,1,200,64Z";

    private const string CaretLeftPath =
        "M165.66,202.34a8,8,0,0,1-11.32,11.32l-80-80a8,8,0,0,1,0-11.32l80-80a8,8,0,0,1,11.32,11.32L91.31,128Z";

    private const string CheckPath =
        "M229.66,77.66l-128,128a8,8,0,0,1-11.32,0l-56-56a8,8,0,0,1,11.32-11.32L96,188.69,218.34,66.34a8,8,0,0,1," +
        "11.32,11.32Z";

    private const string PlusPath =
        "M224,128a8,8,0,0,1-8,8H136v80a8,8,0,0,1-16,0V136H40a8,8,0,0,1,0-16h80V40a8,8,0,0,1,16,0v80h80A8," +
        "8,0,0,1,224,128Z";

    private const string SquaresFourPath =
        "M104,40H56A16,16,0,0,0,40,56v48a16,16,0,0,0,16,16h48a16,16,0,0,0,16-16V56A16,16,0,0,0,104,40Zm0," +
        "64H56V56h48ZM200,40H152a16,16,0,0,0-16,16v48a16,16,0,0,0,16,16h48a16,16,0,0,0,16-16V56A16,16,0,0," +
        "0,200,40Zm0,64H152V56h48ZM104,136H56a16,16,0,0,0-16,16v48a16,16,0,0,0,16,16h48a16,16,0,0,0,16-16V" +
        "152A16,16,0,0,0,104,136Zm0,64H56V152h48Zm96-64H152a16,16,0,0,0-16,16v48a16,16,0,0,0,16,16h48a16," +
        "16,0,0,0,16-16V152A16,16,0,0,0,200,136Zm0,64H152V152h48Z";

    // Nine cells on the same 256 grid as the Phosphor set, drawn here rather than
    // vendored: it reads as "more per row" next to SquaresFour, which is the whole
    // job of the density toggle's second channel.
    private const string GridNinePath =
        "M40,40H88V88H40ZM104,40H152V88H104ZM168,40H216V88H168ZM40,104H88V152H40ZM104,104H152V152H104ZM168," +
        "104H216V152H168ZM40,168H88V216H40ZM104,168H152V216H104ZM168,168H216V216H168Z";

    private const string ImageSquarePath =
        "M216,40H40A16,16,0,0,0,24,56V200a16,16,0,0,0,16,16H216a16,16,0,0,0,16-16V56A16,16,0,0,0,216,40Zm0," +
        "16V158.75l-26.07-26.06a16,16,0,0,0-22.63,0l-20,20-44-44a16,16,0,0,0-22.62,0L40,149.37V56ZM40,172l52" +
        "-52,80,80H40Zm176,28H194.63l-36-36,20-20L216,181.38V200ZM144,100a12,12,0,1,1,12,12A12,12,0,0,1,144," +
        "100Z";

    private const string UserPath =
        "M230.92,212c-15.23-26.33-38.7-45.21-66.09-54.16a72,72,0,1,0-73.66,0C63.78,166.78,40.31,185.66,25.08,212a8,8,0,1,0," +
        "13.85,8c18.84-32.56,52.14-52,89.07-52s70.23,19.44,89.07,52a8,8,0,1,0,13.85-8ZM72,96a56,56,0,1,1,56,56A56.06," +
        "56.06,0,0,1,72,96Z";

    private const string CaretUpPath =
        "M213.66,165.66a8,8,0,0,1-11.32,0L128,91.31,53.66,165.66a8,8,0,0,1-11.32-11.32l80-80a8,8,0,0,1,11.32,0l80,80A8,8,0,0,1,213.66,165.66Z";

    private const string CaretDownPath =
        "M213.66,101.66l-80,80a8,8,0,0,1-11.32,0l-80-80A8,8,0,0,1,53.66,90.34L128,164.69l74.34-74.35a8,8,0,0,1,11.32,11.32Z";

    private const string CaretRightPath =
        "M181.66,133.66l-80,80a8,8,0,0,1-11.32-11.32L164.69,128,90.34,53.66a8,8,0,0,1,11.32-11.32l80,80A8,8,0,0,1,181.66,133.66Z";

    private const string DotsThreePath =
        "M140,128a12,12,0,1,1-12-12A12,12,0,0,1,140,128Zm56-12a12,12,0,1,0,12,12A12,12,0,0,0,196,116ZM60,116a12,12,0,1,0," +
        "12,12A12,12,0,0,0,60,116Z";

    // Three rails with a hollow knob on each, on the same 256 grid and 16-unit stroke as the
    // Phosphor set, drawn here rather than vendored like GridNine. Each rail stops short of
    // its knob so the ring stays hollow; the inner circle winds the other way, which is what
    // cuts the hole under the default fill rule.
    private const string SlidersHorizontalPath =
        "M40,64H136V80H40ZM184,64H216V80H184ZM136,72a24,24,0,1,0,48,0a24,24,0,1,0-48,0ZM152,72a8,8,0,1,1,16,0a8,8,0,1,1-16,0Z" +
        "M40,120H72V136H40ZM120,120H216V136H120ZM72,128a24,24,0,1,0,48,0a24,24,0,1,0-48,0ZM88,128a8,8,0,1,1,16,0a8,8,0,1,1-16,0Z" +
        "M40,176H152V192H40ZM200,176H216V192H200ZM152,184a24,24,0,1,0,48,0a24,24,0,1,0-48,0ZM168,184a8,8,0,1,1,16,0a8,8,0,1,1-16,0Z";
}
