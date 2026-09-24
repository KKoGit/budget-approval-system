using BudgetApproval.Application.Common;
using BudgetApproval.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApproval.Api.Infrastructure;

/// <summary>
/// Translates application exceptions into RFC 9457 problem details. Every error the UI can act on carries a
/// stable <c>code</c>; unexpected errors are logged with a trace id and returned without internal detail.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, code, detail) = exception switch
        {
            BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Code, e.Message),
            ConcurrencyConflictException e => (StatusCodes.Status409Conflict, "Changed by someone else", "CONCURRENCY_CONFLICT", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", "NOT_FOUND", e.Message),
            ForbiddenAccessException e => (StatusCodes.Status403Forbidden, "Not allowed", "FORBIDDEN", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "UNEXPECTED",
                  "Something went wrong on the server. Quote the trace id when reporting this.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogInformation("{Code}: {Message}", code, exception.Message);

        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
