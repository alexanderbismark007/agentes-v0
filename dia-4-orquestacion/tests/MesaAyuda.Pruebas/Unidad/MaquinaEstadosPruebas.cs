using MesaAyuda.Api.Dominio.Solicitudes;
using MesaAyuda.Api.Nucleo.Errores;

namespace MesaAyuda.Pruebas.Unidad;

public class MaquinaEstadosPruebas
{
    [Theory]
    [InlineData(EstadoSolicitud.Recibida, EstadoSolicitud.EnRevision)]
    [InlineData(EstadoSolicitud.EnRevision, EstadoSolicitud.EnProceso)]
    [InlineData(EstadoSolicitud.EnProceso, EstadoSolicitud.Resuelta)]
    [InlineData(EstadoSolicitud.Resuelta, EstadoSolicitud.Cerrada)]
    [InlineData(EstadoSolicitud.Resuelta, EstadoSolicitud.EnProceso)]
    public void TransicionesDelFlujoNormalSonValidas(EstadoSolicitud origen, EstadoSolicitud destino)
    {
        Assert.True(MaquinaEstados.EsTransicionValida(origen, destino));
    }

    [Theory]
    [InlineData(EstadoSolicitud.Recibida, EstadoSolicitud.Resuelta)]
    [InlineData(EstadoSolicitud.Recibida, EstadoSolicitud.Cerrada)]
    [InlineData(EstadoSolicitud.Cerrada, EstadoSolicitud.EnProceso)]
    [InlineData(EstadoSolicitud.Rechazada, EstadoSolicitud.EnRevision)]
    public void NoSePuedeSaltarEtapasNiReabrirEstadosFinales(EstadoSolicitud origen, EstadoSolicitud destino)
    {
        Assert.False(MaquinaEstados.EsTransicionValida(origen, destino));
    }

    [Theory]
    [InlineData(EstadoSolicitud.Cerrada)]
    [InlineData(EstadoSolicitud.Rechazada)]
    public void LosEstadosFinalesNoTienenSalida(EstadoSolicitud estado)
    {
        Assert.True(MaquinaEstados.EsEstadoFinal(estado));
        Assert.Empty(MaquinaEstados.EstadosPermitidosDesde(estado));
    }

    [Fact]
    public void CualquierEstadoNoFinalPuedeSerRechazado()
    {
        EstadoSolicitud[] enCurso =
        [
            EstadoSolicitud.Recibida,
            EstadoSolicitud.EnRevision,
            EstadoSolicitud.EnProceso
        ];

        Assert.All(enCurso, estado =>
            Assert.True(MaquinaEstados.EsTransicionValida(estado, EstadoSolicitud.Rechazada)));
    }

    [Fact]
    public void CambiarEstadoAvanzaLaSolicitudYRegistraElCierre()
    {
        var solicitud = ConstruirSolicitud();

        solicitud.CambiarEstado(EstadoSolicitud.EnRevision);
        solicitud.CambiarEstado(EstadoSolicitud.EnProceso);
        solicitud.CambiarEstado(EstadoSolicitud.Resuelta);

        Assert.Equal(EstadoSolicitud.Resuelta, solicitud.Estado);
        Assert.Null(solicitud.FechaCierre);

        solicitud.CambiarEstado(EstadoSolicitud.Cerrada);

        Assert.Equal(EstadoSolicitud.Cerrada, solicitud.Estado);
        Assert.NotNull(solicitud.FechaCierre);
    }

    [Fact]
    public void UnaTransicionInvalidaEsRechazadaPorElDominio()
    {
        var solicitud = ConstruirSolicitud();

        var error = Assert.Throws<ExcepcionConflicto>(() => solicitud.CambiarEstado(EstadoSolicitud.Cerrada));

        Assert.Equal(StatusCodes.Status409Conflict, error.CodigoHttp);
        Assert.Contains("Recibida", error.Message);
    }

    [Fact]
    public void RepetirElEstadoActualEsUnConflicto()
    {
        var solicitud = ConstruirSolicitud();

        Assert.Throws<ExcepcionConflicto>(() => solicitud.CambiarEstado(EstadoSolicitud.Recibida));
    }

    [Fact]
    public void NoSePuedeComentarUnaSolicitudCerrada()
    {
        var solicitud = ConstruirSolicitud();
        solicitud.CambiarEstado(EstadoSolicitud.EnRevision);
        solicitud.CambiarEstado(EstadoSolicitud.EnProceso);
        solicitud.CambiarEstado(EstadoSolicitud.Resuelta);
        solicitud.CambiarEstado(EstadoSolicitud.Cerrada);

        Assert.Throws<ExcepcionConflicto>(() =>
            solicitud.AgregarComentario("Operador", "Comentario tardío", esInterno: false));
    }

    [Fact]
    public void NoSePuedeReasignarUnaSolicitudRechazada()
    {
        var solicitud = ConstruirSolicitud();
        solicitud.CambiarEstado(EstadoSolicitud.Rechazada);

        Assert.Throws<ExcepcionConflicto>(() =>
            solicitud.Reasignar(CategoriaSolicitud.Academica, PrioridadSolicitud.Alta, "Dirección de Carrera"));
    }

    private static Solicitud ConstruirSolicitud() => new(
        "SOL-2026-000001",
        "Solicitud de prueba para el flujo",
        "Descripción suficientemente larga para representar un caso real de la mesa de ayuda.",
        "Persona Solicitante",
        "persona@correo.upea.bo",
        "Mesa de Ayuda",
        CategoriaSolicitud.Otra,
        PrioridadSolicitud.Media);
}
