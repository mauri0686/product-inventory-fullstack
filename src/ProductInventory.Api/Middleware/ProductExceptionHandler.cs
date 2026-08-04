using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProductInventory.Application.Exceptions;
using ProductInventory.Domain.Exceptions;

namespace ProductInventory.Api.Middleware;

public sealed class ProductExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ProductExceptionHandler> _logger;
    private const string ProblemJsonContentType = "application/problem+json";

    public ProductExceptionHandler(ILogger<ProductExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        ProblemDetails problem;
        var isUnexpected = false;

        switch (exception)
        {
            case ProductNotFoundException ex:
                problem = CreateProblem(404, ProductNotFoundException.ErrorCode, ex.Message, traceId);
                break;

            case ProductNameConflictException ex:
                problem = CreateProblem(409, ProductNameConflictException.ErrorCode, ex.Message, traceId);
                break;

            case ProductDomainException ex:
                problem = CreateProblem(422, "product.invalid", ex.Message, traceId);
                break;

            default:
                problem = CreateProblem(500, "server.error", "An unexpected error occurred.", traceId);
                isUnexpected = true;
                break;
        }

        if (isUnexpected)
            _logger.LogError(exception, "Unhandled exception for trace {TraceId}", traceId);
        else
            _logger.LogInformation(
                "Handled application exception {ErrorCode} for trace {TraceId}",
                problem.Extensions["errorCode"],
                traceId);

        httpContext.Response.StatusCode = problem.Status ?? 500;
        httpContext.Response.ContentType = ProblemJsonContentType;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static ProblemDetails CreateProblem(int status, string errorCode, string detail, string traceId)
    {
        return new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = status switch
            {
                400 => "Bad Request",
                404 => "Not Found",
                409 => "Conflict",
                422 => "Unprocessable Entity",
                500 => "Internal Server Error",
                _ => "Error"
            },
            Status = status,
            Detail = detail,
            Extensions =
            {
                ["traceId"] = traceId,
                ["errorCode"] = errorCode
            }
        };
    }
}
