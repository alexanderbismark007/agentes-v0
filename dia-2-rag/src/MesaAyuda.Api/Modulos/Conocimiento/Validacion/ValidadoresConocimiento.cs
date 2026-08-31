using FluentValidation;
using MesaAyuda.Api.Modulos.Conocimiento.Contratos;

namespace MesaAyuda.Api.Modulos.Conocimiento.Validacion;

public sealed class ConsultaDocumentalValidador : AbstractValidator<ConsultaDocumental>
{
    public ConsultaDocumentalValidador()
    {
        RuleFor(x => x.Pregunta)
            .NotEmpty().WithMessage("La pregunta es obligatoria.")
            .MinimumLength(8).WithMessage("Formule una pregunta de al menos 8 caracteres.")
            .MaximumLength(500).WithMessage("La pregunta no puede superar los 500 caracteres.");

        RuleFor(x => x.NivelAcceso)
            .IsInEnum().WithMessage("El nivel de acceso indicado no es válido.");
    }
}
