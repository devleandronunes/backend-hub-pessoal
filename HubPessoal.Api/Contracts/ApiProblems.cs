namespace HubPessoal.Api.Contracts;

public static class ApiProblems
{
    public static IResult BadRequest(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);

    public static IResult Conflict(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status409Conflict);
}