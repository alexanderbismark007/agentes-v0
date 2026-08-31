using FluentValidation;
using MesaAyuda.Api.Modulos.Orquestacion.Contratos;

namespace MesaAyuda.Api.Modulos.Orquestacion.Validacion;

public sealed class SolicitudEntranteValidador : AbstractValidator<SolicitudEntrante>
{
    public SolicitudEntranteValidador()
    {
        RuleFor(x => x.Titulo)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MinimumLength(10).WithMessage("El título debe tener al menos 10 caracteres.")
            .MaximumLength(180);

        RuleFor(x => x.Descripcion)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MinimumLength(20).WithMessage("Describa el caso con al menos 20 caracteres.")
            .MaximumLength(4000);

        RuleFor(x => x.SolicitanteNombre)
            .NotEmpty().WithMessage("El nombre del solicitante es obligatorio.")
            .MaximumLength(160);

        RuleFor(x => x.SolicitanteCorreo)
            .NotEmpty().WithMessage("El correo del solicitante es obligatorio.")
            .EmailAddress().WithMessage("El correo del solicitante no tiene un formato válido.")
            .MaximumLength(160);

        RuleFor(x => x.Origen)
            .NotEmpty().WithMessage("Debe indicarse el sistema de origen.")
            .MaximumLength(60);

        RuleFor(x => x.ReferenciaExterna)
            .MaximumLength(120)
            .When(x => !string.IsNullOrWhiteSpace(x.ReferenciaExterna));
    }
}
