const RUTA_BASE = '/api/v1';

/// Envoltura unica sobre fetch.
///
/// Concentra aqui el manejo de errores para que ninguna vista tenga que
/// interpretar una respuesta HTTP. La API devuelve sus errores en formato
/// ProblemDetails, con un titulo y un detalle legibles, y esta funcion los
/// convierte en un error de JavaScript con esa informacion ya extraida.
export async function pedir(ruta, opciones = {}) {
    const cabeceras = {
        Accept: 'application/json',
        ...(opciones.body ? { 'Content-Type': 'application/json' } : {}),
        ...(opciones.headers || {}),
    };

    const respuesta = await fetch(`${RUTA_BASE}${ruta}`, { ...opciones, headers: cabeceras });

    if (respuesta.status === 204) {
        return null;
    }

    const texto = await respuesta.text();
    const datos = texto ? JSON.parse(texto) : null;

    if (!respuesta.ok) {
        // El codigo se adjunta porque no todo fallo es una falla del sistema:
        // un 409 significa que la operacion no corresponde en el estado actual,
        // y quien llama necesita poder distinguirlo de algo roto.
        throw {
            status: respuesta.status,
            titulo: datos?.title ?? 'No se pudo completar la operacion',
            detalle: datos?.detail ?? null,
            errores: datos?.errors ?? null,
        };
    }

    return datos;
}

function consulta(parametros) {
    const partes = Object.entries(parametros)
        .filter(([, valor]) => valor !== null && valor !== undefined && valor !== '')
        .map(([clave, valor]) => `${encodeURIComponent(clave)}=${encodeURIComponent(valor)}`);

    return partes.length > 0 ? `?${partes.join('&')}` : '';
}

/// Superficie de la API expresada como funciones con nombre.
///
/// Las vistas llaman a estas funciones y nunca construyen rutas por su cuenta:
/// si un endpoint cambia, se corrige en un solo lugar.
export const api = {
    salud: () => fetch('/salud').then((r) => r.json()),

    listarSolicitudes: (filtros = {}) => pedir(`/solicitudes${consulta(filtros)}`),

    obtenerSolicitud: (id) => pedir(`/solicitudes/${id}`),

    crearSolicitud: (datos) =>
        pedir('/solicitudes', { method: 'POST', body: JSON.stringify(datos) }),

    cambiarEstado: (id, nuevoEstado, motivo) =>
        pedir(`/solicitudes/${id}/estado`, {
            method: 'PATCH',
            body: JSON.stringify({ nuevoEstado, motivo }),
        }),

    reasignar: (id, datos) =>
        pedir(`/solicitudes/${id}/asignacion`, { method: 'PATCH', body: JSON.stringify(datos) }),

    listarComentarios: (id, incluirInternos = true) =>
        pedir(`/solicitudes/${id}/comentarios${consulta({ incluirInternos })}`),

    comentar: (id, datos) =>
        pedir(`/solicitudes/${id}/comentarios`, { method: 'POST', body: JSON.stringify(datos) }),

    resumen: () => pedir('/estadisticas/resumen'),

    consultar: (pregunta, nivelAcceso = 1) =>
        pedir('/conocimiento/consultas', {
            method: 'POST',
            body: JSON.stringify({ pregunta, nivelAcceso }),
        }),

    listarDocumentos: () => pedir('/conocimiento/documentos'),

    // La carga viaja como formulario, no como JSON: es un archivo. Por eso no
    // pasa por la envoltura, que asume un cuerpo serializado.
    subirDocumento: async (archivo, nivelAcceso = 1) => {
        const formulario = new FormData();
        formulario.append('archivo', archivo);

        const respuesta = await fetch(
            `/api/v1/conocimiento/documentos?nivelAcceso=${nivelAcceso}`,
            { method: 'POST', body: formulario },
        );

        const texto = await respuesta.text();
        const datos = texto ? JSON.parse(texto) : null;

        if (!respuesta.ok) {
            throw {
                status: respuesta.status,
                titulo: datos?.title ?? 'No se pudo indexar el documento',
                detalle: datos?.detail ?? null,
            };
        }

        return datos;
    },

    eliminarDocumento: (id) => pedir(`/conocimiento/documentos/${id}`, { method: 'DELETE' }),
};
