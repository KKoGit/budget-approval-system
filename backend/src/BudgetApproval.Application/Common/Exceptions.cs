namespace BudgetApproval.Application.Common;

/// <summary>The requested resource does not exist (or the caller may not know that it exists).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>The caller is authenticated but not allowed to perform this operation.</summary>
public sealed class ForbiddenAccessException(string message) : Exception(message);

/// <summary>
/// Someone else changed the record after the caller loaded it. The client should reload and retry
/// rather than silently overwrite the other person's decision.
/// </summary>
public sealed class ConcurrencyConflictException(string message) : Exception(message);
