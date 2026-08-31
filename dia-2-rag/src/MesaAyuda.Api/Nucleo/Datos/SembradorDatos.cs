using MesaAyuda.Api.Dominio.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace MesaAyuda.Api.Nucleo.Datos;

/// <summary>
/// Carga un conjunto de solicitudes de ejemplo para que la API tenga datos
/// desde el primer arranque. Solo actúa si la base está vacía.
/// </summary>
public static class SembradorDatos
{
    private sealed record Semilla(
        string Titulo,
        string Descripcion,
        string Solicitante,
        string Correo,
        string Unidad,
        CategoriaSolicitud Categoria,
        PrioridadSolicitud Prioridad,
        EstadoSolicitud[] Avances,
        int DiasAtras);

    private static readonly Semilla[] Semillas =
    [
        new("Error al inscribirse en la materia de Compiladores",
            "El sistema muestra un error de cupo agotado al intentar inscribir la materia de Compiladores, pero el listado indica que aún hay cupos disponibles.",
            "Marcela Quispe", "marcela.quispe@correo.upea.bo", "Unidad de Sistemas",
            CategoriaSolicitud.Tecnologica, PrioridadSolicitud.Alta,
            [EstadoSolicitud.EnRevision, EstadoSolicitud.EnProceso], 6),

        new("Solicitud de certificado de notas del semestre anterior",
            "Requiero un certificado de notas del semestre anterior para presentarlo en un proceso de postulación laboral que cierra en dos semanas.",
            "Javier Mamani", "javier.mamani@correo.upea.bo", "Secretaría Académica",
            CategoriaSolicitud.Administrativa, PrioridadSolicitud.Media,
            [EstadoSolicitud.EnRevision, EstadoSolicitud.EnProceso, EstadoSolicitud.Resuelta], 12),

        new("Proyector del laboratorio tres no enciende",
            "El proyector del laboratorio tres no enciende desde el lunes. La clase de bases de datos se está dictando sin material de apoyo visual.",
            "Docente Rubén Alanoca", "ruben.alanoca@correo.upea.bo", "Unidad de Infraestructura",
            CategoriaSolicitud.Infraestructura, PrioridadSolicitud.Alta,
            [EstadoSolicitud.EnRevision], 2),

        new("Consulta sobre el pago de arancel de titulación",
            "Deseo conocer el monto vigente del arancel de titulación y si existe algún descuento para egresados que concluyeron la gestión pasada.",
            "Lucía Torrez", "lucia.torrez@correo.upea.bo", "Dirección Administrativa Financiera",
            CategoriaSolicitud.Financiera, PrioridadSolicitud.Baja,
            [], 1),

        new("No puedo acceder al correo institucional",
            "Olvidé la contraseña del correo institucional y la opción de recuperación envía el código a un número telefónico que ya no utilizo.",
            "Diego Herrera", "diego.herrera@correo.upea.bo", "Unidad de Sistemas",
            CategoriaSolicitud.Tecnologica, PrioridadSolicitud.Critica,
            [EstadoSolicitud.EnRevision, EstadoSolicitud.EnProceso, EstadoSolicitud.Resuelta, EstadoSolicitud.Cerrada], 20),

        new("Revisión de nota de la materia de Estructuras de Datos",
            "La nota registrada en el sistema no coincide con la planilla firmada por el docente al cierre del semestre. Solicito la revisión correspondiente.",
            "Ana Colque", "ana.colque@correo.upea.bo", "Dirección de Carrera",
            CategoriaSolicitud.Academica, PrioridadSolicitud.Alta,
            [EstadoSolicitud.EnRevision, EstadoSolicitud.EnProceso], 9),

        new("Falta de iluminación en el aula doscientos cuatro",
            "Cuatro de las seis luminarias del aula doscientos cuatro están quemadas, lo que dificulta las clases del turno noche.",
            "Delegado estudiantil", "delegatura@correo.upea.bo", "Unidad de Infraestructura",
            CategoriaSolicitud.Infraestructura, PrioridadSolicitud.Media,
            [EstadoSolicitud.EnRevision, EstadoSolicitud.Rechazada], 15),

        new("Actualización de datos personales en el kardex",
            "Mi apellido materno figura mal escrito en el kardex académico y necesito corregirlo antes de iniciar el trámite de titulación.",
            "Pedro Chávez", "pedro.chavez@correo.upea.bo", "Secretaría Académica",
            CategoriaSolicitud.Administrativa, PrioridadSolicitud.Media,
            [EstadoSolicitud.EnRevision], 4)
    ];

    public static async Task SembrarAsync(ContextoMesaAyuda contexto, CancellationToken cancelacion = default)
    {
        if (await contexto.Solicitudes.AnyAsync(cancelacion))
        {
            return;
        }

        var correlativo = 1;
        var anio = DateTimeOffset.UtcNow.Year;

        foreach (var semilla in Semillas)
        {
            var solicitud = new Solicitud(
                $"SOL-{anio}-{correlativo:D6}",
                semilla.Titulo,
                semilla.Descripcion,
                semilla.Solicitante,
                semilla.Correo,
                semilla.Unidad,
                semilla.Categoria,
                semilla.Prioridad);

            solicitud.RegistrarSugerencia(semilla.Categoria, 0.8d, "semilla");

            foreach (var avance in semilla.Avances)
            {
                solicitud.CambiarEstado(avance);
            }

            // Se envejecen las semillas para que los indicadores de rezago y de
            // tiempo promedio de resolución tengan valores representativos.
            solicitud.FijarFechaCreacion(DateTimeOffset.UtcNow.AddDays(-semilla.DiasAtras));

            contexto.Solicitudes.Add(solicitud);
            correlativo++;
        }

        await contexto.SaveChangesAsync(cancelacion);
    }
}
