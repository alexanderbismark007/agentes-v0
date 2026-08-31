using FluentValidation;
using FluentValidation.Results;

namespace MesaAyuda.Api.Nucleo.Errores;

/// <summary>
/// Puente entre FluentValidation y las respuestas HTTP de validación.
/// </summary>
public static class ExtensionesValidacion
{
    /// <summary>
    /// Valida el objeto y, si falla, devuelve un resultado 400 con el detalle por campo.
    /// </summary>
    public static async Task<IResult?> ValidarAsync<T>(
        this IValidator<T> validador,
        T instancia,
        CancellationToken cancelacion = default)
    {
        ValidationResult resultado = await validador.ValidateAsync(instancia, cancelacion);
        if (resultado.IsValid)
        {
            return null;
        }

        var errores = resultado.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return Results.ValidationProblem(
            errores,
            title: "Los datos enviados no son válidos",
            statusCode: StatusCodes.Status400BadRequest);
    }
}
