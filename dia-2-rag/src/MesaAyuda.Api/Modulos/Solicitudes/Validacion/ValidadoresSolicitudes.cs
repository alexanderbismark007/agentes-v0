using FluentValidation;
using MesaAyuda.Api.Modulos.Solicitudes.Contratos;

namespace MesaAyuda.Api.Modulos.Solicitudes.Validacion;

public sealed class CrearSolicitudValidador : AbstractValidator<CrearSolicitud>
{
    public CrearSolicitudValidador()
    {
        RuleFor(x => x.Titulo)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MinimumLength(10).WithMessage("El título debe tener al menos 10 caracteres.")
            .MaximumLength(180).WithMessage("El título no puede superar los 180 caracteres.");

        RuleFor(x => x.Descripcion)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MinimumLength(20).WithMessage("Describa el caso con al menos 20 caracteres.")
            .MaximumLength(4000).WithMessage("La descripción no puede superar los 4000 caracteres.");

        RuleFor(x => x.SolicitanteNombre)
            .NotEmpty().WithMessage("El nombre del solicitante es obligatorio.")
            .MaximumLength(160);

        RuleFor(x => x.SolicitanteCorreo)
            .NotEmpty().WithMessage("El correo del solicitante es obligatorio.")
            .EmailAddress().WithMessage("El correo del solicitante no tiene un formato válido.")
            .MaximumLength(160);

        RuleFor(x => x.UnidadDestino)
            .MaximumLength(120)
            .When(x => !string.IsNullOrWhiteSpace(x.UnidadDestino));

        RuleFor(x => x.Prioridad)
            .IsInEnum().WithMessage("La prioridad indicada no es válida.");

        RuleFor(x => x.Categoria)
            .IsInEnum().WithMessage("La categoría indicada no es válida.")
            .When(x => x.Categoria.HasValue);
    }
}

public sealed class CambiarEstadoValidador : AbstractValidator<CambiarEstado>
{
    public CambiarEstadoValidador()
    {
        RuleFor(x => x.NuevoEstado)
            .IsInEnum().WithMessage("El estado indicado no es válido.");

        RuleFor(x => x.Motivo)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Motivo));
    }
}

public sealed class ReasignarSolicitudValidador : AbstractValidator<ReasignarSolicitud>
{
    public ReasignarSolicitudValidador()
    {
        RuleFor(x => x.Categoria).IsInEnum().WithMessage("La categoría indicada no es válida.");
        RuleFor(x => x.Prioridad).IsInEnum().WithMessage("La prioridad indicada no es válida.");

        RuleFor(x => x.UnidadDestino)
            .NotEmpty().WithMessage("La unidad de destino es obligatoria.")
            .MaximumLength(120);
    }
}

public sealed class CrearComentarioValidador : AbstractValidator<CrearComentario>
{
    public CrearComentarioValidador()
    {
        RuleFor(x => x.Autor)
            .NotEmpty().WithMessage("El autor del comentario es obligatorio.")
            .MaximumLength(160);

        RuleFor(x => x.Contenido)
            .NotEmpty().WithMessage("El contenido del comentario es obligatorio.")
            .MaximumLength(2000).WithMessage("El comentario no puede superar los 2000 caracteres.");
    }
}
