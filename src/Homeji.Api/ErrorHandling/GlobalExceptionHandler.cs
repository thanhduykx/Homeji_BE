using System.Data.Common;
using System.Diagnostics;
using Homeji.Application.Common.Exceptions;
using Homeji.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.ErrorHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly Action<ILogger, string, string, Exception?> LogUnhandledException =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(1, nameof(GlobalExceptionHandler)),
            "Unhandled exception while processing {Method} {Path}");

    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
        _environment = environment;
        _configuration = configuration;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = CreateProblemDetails(httpContext, exception);

        if (problemDetails.Status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(
                _logger,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception);
        }

        httpContext.Response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }

    private ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        ProblemDetails problemDetails = exception switch
        {
            RequestValidationException validationException => new HttpValidationProblemDetails(
                validationException.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
            },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = exception.Message,
            },
            ConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Resource conflict",
                Detail = exception.Message,
                Extensions =
                {
                    ["errors"] = new Dictionary<string, string[]>
                    {
                        ["email"] = [exception.Message],
                    },
                },
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = exception.Message,
            },
            ForbiddenAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = exception.Message,
            },
            DomainException => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Business rule violation",
                Detail = exception.Message,
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
            },
        };

        problemDetails.Instance = context.Request.Path;
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        var exposeDetails = _environment.IsDevelopment()
            || _configuration.GetValue("Api:ExposeErrorDetails", false);

        if (exposeDetails && problemDetails.Status >= StatusCodes.Status500InternalServerError)
        {
            problemDetails.Detail = exception.Message;
            problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
            if (exception is DbUpdateException or DbException)
            {
                problemDetails.Extensions["dbHint"] =
                    "Likely schema/migration drift. Run: dotnet ef database update";
            }
        }

        return problemDetails;
    }
}
