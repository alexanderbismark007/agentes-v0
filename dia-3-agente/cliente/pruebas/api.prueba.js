import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, pedir } from '../src/servicios/api';

function responder(cuerpo, estado = 200) {
    return vi.fn().mockResolvedValue({
        ok: estado >= 200 && estado < 300,
        status: estado,
        text: async () => (cuerpo === null ? '' : JSON.stringify(cuerpo)),
    });
}

afterEach(() => {
    vi.unstubAllGlobals();
});

describe('envoltura de peticiones', () => {
    it('antepone la ruta base de la version', async () => {
        const fetchFalso = responder({ ok: true });
        vi.stubGlobal('fetch', fetchFalso);

        await pedir('/solicitudes');

        expect(fetchFalso.mock.calls[0][0]).toBe('/api/v1/solicitudes');
    });

    it('no declara tipo de contenido cuando no hay cuerpo', async () => {
        const fetchFalso = responder({});
        vi.stubGlobal('fetch', fetchFalso);

        await pedir('/solicitudes');

        expect(fetchFalso.mock.calls[0][1].headers['Content-Type']).toBeUndefined();
    });

    it('declara tipo de contenido cuando envia un cuerpo', async () => {
        const fetchFalso = responder({});
        vi.stubGlobal('fetch', fetchFalso);

        await pedir('/solicitudes', { method: 'POST', body: '{}' });

        expect(fetchFalso.mock.calls[0][1].headers['Content-Type']).toBe('application/json');
    });

    it('devuelve nulo ante una respuesta sin contenido', async () => {
        vi.stubGlobal('fetch', responder(null, 204));

        await expect(pedir('/conocimiento/documentos/1', { method: 'DELETE' })).resolves.toBeNull();
    });

    it('convierte un ProblemDetails en un error con titulo y detalle', async () => {
        vi.stubGlobal('fetch', responder(
            { title: 'Conflicto con el estado actual', detail: 'No se puede pasar de Recibida a Cerrada.' },
            409,
        ));

        await expect(pedir('/solicitudes/1/estado', { method: 'PATCH', body: '{}' }))
            .rejects.toMatchObject({
                status: 409,
                titulo: 'Conflicto con el estado actual',
                detalle: 'No se puede pasar de Recibida a Cerrada.',
            });
    });

    it('conserva los errores por campo de una validacion', async () => {
        vi.stubGlobal('fetch', responder(
            { title: 'Los datos enviados no son validos', errors: { Titulo: ['El titulo es obligatorio.'] } },
            400,
        ));

        await expect(pedir('/solicitudes', { method: 'POST', body: '{}' }))
            .rejects.toMatchObject({ status: 400, errores: { Titulo: ['El titulo es obligatorio.'] } });
    });

    it('adjunta el codigo para que quien llame distinga un rechazo de una falla', async () => {
        vi.stubGlobal('fetch', responder({ title: 'Recurso no encontrado' }, 404));

        await expect(pedir('/solicitudes/1')).rejects.toMatchObject({ status: 404 });
    });
});

describe('construccion de la cadena de consulta', () => {
    it('omite los filtros vacios, nulos e indefinidos', async () => {
        const fetchFalso = responder({ elementos: [], total: 0 });
        vi.stubGlobal('fetch', fetchFalso);

        await api.listarSolicitudes({
            estado: 3,
            categoria: null,
            prioridad: undefined,
            texto: '',
            pagina: 1,
        });

        const ruta = fetchFalso.mock.calls[0][0];

        expect(ruta).toContain('estado=3');
        expect(ruta).toContain('pagina=1');
        expect(ruta).not.toContain('categoria');
        expect(ruta).not.toContain('prioridad');
        expect(ruta).not.toContain('texto');
    });

    it('no agrega interrogacion cuando no hay filtros', async () => {
        const fetchFalso = responder({ elementos: [], total: 0 });
        vi.stubGlobal('fetch', fetchFalso);

        await api.listarSolicitudes({});

        expect(fetchFalso.mock.calls[0][0]).toBe('/api/v1/solicitudes');
    });

    it('codifica los valores con caracteres especiales', async () => {
        const fetchFalso = responder({ elementos: [], total: 0 });
        vi.stubGlobal('fetch', fetchFalso);

        await api.listarSolicitudes({ texto: 'correo institucional & clave' });

        expect(fetchFalso.mock.calls[0][0]).toContain('correo%20institucional%20%26%20clave');
    });
});
