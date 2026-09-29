namespace LnShelf.Shared;

/// <summary>
/// The enum converter every contract enum uses: names on the wire, in both directions,
/// and nothing else accepted.
/// </summary>
// A named subclass only because [JsonConverter(typeof(...))] cannot pass constructor
// arguments, and the argument is the point. The framework's converter accepts integers by
// default, so `{"status":7}` deserialized to an enum value no member names — which then
// travelled all the way to a switch that had no arm for it and surfaced as a 500 from the
// import commit. Refusing it here makes it what it is: a malformed request, a 400 from
// model binding, before any service sees it.
//
// Fixed at the contract rather than guarded in each command service, because every enum
// crossing the boundary has the same hole and a guard would have to be remembered for
// each one.
public sealed class StrictStringEnumConverter<TEnum>()
    : JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false)
    where TEnum : struct, Enum;
