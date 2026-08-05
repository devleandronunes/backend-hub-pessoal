using FluentValidation;

namespace HubPessoal.Api.Filters;

public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return await next(context);
        }

        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(argument);
        if (!result.IsValid)
        {
            return Results.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}