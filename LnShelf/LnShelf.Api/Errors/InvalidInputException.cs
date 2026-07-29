namespace LnShelf.Api.Errors;

/// <summary>
/// The caller sent something the API cannot work with. A 400, not a bug.
/// </summary>
// Its own type so the central handler can tell "you uploaded a PDF" apart from
// "this service has a defect". Everything else keeps bubbling as a 500, which is
// what an unexpected state should look like.
public sealed class InvalidInputException(string message) : Exception(message);

/// <summary>
/// The request is well formed but the current state does not allow it. A 409.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
