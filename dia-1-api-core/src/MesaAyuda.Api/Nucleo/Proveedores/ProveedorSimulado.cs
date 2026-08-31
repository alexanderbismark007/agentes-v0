using System.Text;
using System.Text.Json;

namespace MesaAyuda.Api.Nucleo.Proveedores;

/// <summary>
/// Proveedor determinista que no realiza llamadas de red. Aplica reglas de
/// palabras clave sobre el texto recibido y devuelve el mismo resultado ante la
/// misma entrada.
///
/// Cumple dos funciones: permite ejecutar el proyecto y sus pruebas sin
/// credenciales, y sirve de referencia para comparar contra la salida de un
/// modelo real.
/// </summary>
public sealed class ProveedorSimulado : IProveedorLenguaje
{
    private static readonly Dictionary<string, string[]> SenalesPorCategoria = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Academica"] = ["materia", "docente", "nota", "examen", "inscripcion", "inscripción", "semestre", "kardex", "titulacion", "titulación", "malla"],
        ["Administrativa"] = ["certificado", "tramite", "trámite", "solicitud", "carta", "constancia", "archivo", "secretaria", "secretaría"],
        ["Tecnologica"] = ["sistema", "plataforma", "correo", "contrasena", "contraseña", "usuario", "acceso", "servidor", "aplicacion", "aplicación", "red", "internet", "wifi"],
        ["Financiera"] = ["pago", "deuda", "arancel", "factura", "beca", "descuento", "cuota", "reembolso", "matricula", "matrícula"],
        ["Infraestructura"] = ["aula", "laboratorio", "silla", "iluminacion", "iluminación", "bano", "baño", "agua", "electricidad", "limpieza", "ascensor"]
    };

    public string Nombre => "simulado";

    public Task<RespuestaLenguaje> CompletarAsync(PeticionLenguaje peticion, CancellationToken cancelacion = default)
    {
        var texto = string.Join(' ', peticion.Mensajes
            .Where(m => m.Rol == RolMensaje.Usuario)
            .Select(m => m.Contenido));

        var (categoria, confianza, coincidencias) = Clasificar(texto);

        var contenido = peticion.RespuestaJson
            ? SerializarClasificacion(categoria, confianza, coincidencias)
            : RedactarResumen(texto, categoria);

        var respuesta = new RespuestaLenguaje(
            contenido,
            Modelo: "reglas-locales-v1",
            TokensEntrada: EstimarTokens(texto),
            TokensSalida: EstimarTokens(contenido));

        return Task.FromResult(respuesta);
    }

    private static (string Categoria, double Confianza, string[] Coincidencias) Clasificar(string texto)
    {
        var normalizado = texto.ToLowerInvariant();

        var puntajes = SenalesPorCategoria
            .Select(par => new
            {
                Categoria = par.Key,
                Coincidencias = par.Value.Where(senal => normalizado.Contains(senal, StringComparison.Ordinal)).ToArray()
            })
            .Where(x => x.Coincidencias.Length > 0)
            .OrderByDescending(x => x.Coincidencias.Length)
            .ThenBy(x => x.Categoria, StringComparer.Ordinal)
            .ToList();

        if (puntajes.Count == 0)
        {
            return ("Otra", 0.25d, []);
        }

        var mejor = puntajes[0];
        var total = puntajes.Sum(x => x.Coincidencias.Length);

        // La confianza combina cuánto domina la categoría ganadora sobre el
        // resto y cuántas señales encontró en términos absolutos.
        var dominancia = (double)mejor.Coincidencias.Length / total;
        var soporte = Math.Min(mejor.Coincidencias.Length / 3d, 1d);
        var confianza = Math.Round(0.45d + (0.35d * dominancia) + (0.20d * soporte), 2);

        return (mejor.Categoria, Math.Clamp(confianza, 0d, 1d), mejor.Coincidencias);
    }

    private static string SerializarClasificacion(string categoria, double confianza, string[] coincidencias)
    {
        var carga = new
        {
            categoria,
            confianza,
            justificacion = coincidencias.Length > 0
                ? $"Se encontraron los términos: {string.Join(", ", coincidencias)}."
                : "No se encontraron términos característicos de ninguna categoría."
        };

        return JsonSerializer.Serialize(carga, OpcionesJson.Predeterminadas);
    }

    private static string RedactarResumen(string texto, string categoria)
    {
        var limpio = texto.Replace('\n', ' ').Replace('\r', ' ').Trim();
        var oraciones = limpio.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var constructor = new StringBuilder();
        constructor.Append("Resumen (").Append(categoria).Append("): ");
        constructor.Append(oraciones.Length > 0 ? oraciones[0] : limpio);
        constructor.Append('.');

        return constructor.ToString();
    }

    /// <summary>
    /// Aproximación suficiente para registrar consumo sin depender de un
    /// tokenizador externo: alrededor de cuatro caracteres por token.
    /// </summary>
    private static int EstimarTokens(string texto) =>
        string.IsNullOrEmpty(texto) ? 0 : (int)Math.Ceiling(texto.Length / 4d);
}
