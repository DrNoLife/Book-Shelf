using Npgsql;

namespace LnShelf.Api.Data;

/// <summary>
/// What a failed save was, in the terms callers decide on.
/// </summary>
// One place that knows how Postgres spells "duplicate key", so the error handler and the
// add-series race recovery cannot disagree about which failures count.
public static class DbUpdateExceptionExtensions
{
    extension(DbUpdateException exception)
    {
        /// <summary>A unique index refused the row: somebody else wrote it first.</summary>
        public bool IsUniqueViolation =>
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
