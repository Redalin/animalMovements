using System.Text.Json;
using AnimalMovements.Application.Exceptions;
using AnimalMovements.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AnimalMovements.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString()
                            ?? httpContext.TraceIdentifier;

        ProblemDetails problemDetails;

        switch (exception)
        {
            case BadInputException badInput:
                logger.LogWarning(
                    "Bad input on {Method} {Path}: {Message}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    badInput.Message);

                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = badInput.Message,
                    Type = "https://httpstatuses.io/400"
                };
                if (badInput.Errors.Count > 0)
                {
                    problemDetails.Extensions["errors"] = badInput.Errors;
                }
                break;

            case BadHttpRequestException:
            case JsonException:
                logger.LogWarning(
                    exception,
                    "Malformed HTTP/JSON payload on {Method} {Path}.",
                    httpContext.Request.Method,
                    httpContext.Request.Path);

                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = "The request payload is malformed or contains invalid field types. Check field names, enum values (Cattle/Deer), and date format (YYYY-MM-DD).",
                    Type = "https://httpstatuses.io/400"
                };
                break;

            case DomainValidationException domainEx:
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Business Rule Validation Failed",
                    Detail = domainEx.Message,
                    Type = "https://httpstatuses.io/422"
                };
                problemDetails.Extensions["ruleCode"] = domainEx.RuleCode;
                if (!string.IsNullOrWhiteSpace(domainEx.Field))
                {
                    problemDetails.Extensions["field"] = domainEx.Field;
                }
                break;

            case ConcurrencyConflictException concurrencyEx:
                logger.LogWarning(concurrencyEx, "Concurrency conflict on {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = concurrencyEx.Message,
                    Type = "https://httpstatuses.io/409"
                };
                break;

            default:
                logger.LogError(
                    exception,
                    "Unhandled exception on {Method} {Path} (CorrelationId: {CorrelationId}).",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    correlationId);

                problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred. Please contact support with the correlationId.",
                    Type = "https://httpstatuses.io/500"
                };
                break;
        }

        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
