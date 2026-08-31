using FluentValidation;
using MesaAyuda.Api.Modulos.Agente.Contratos;

namespace MesaAyuda.Api.Modulos.Agente.Validacion;

public sealed class ConsultaAgenteValidador : AbstractValidator<ConsultaAgente>
{
    public ConsultaAgenteValidador()
    {
        RuleFor(x => x.Pregunta)
            .NotEmpty().WithMessage("La pregunta es obligatoria.")
            .MinimumLength(8).WithMessage("Formule una pregunta de al menos 8 caracteres.")
            .MaximumLength(1000).WithMessage("La pregunta no puede superar los 1000 caracteres.");
    }
}
