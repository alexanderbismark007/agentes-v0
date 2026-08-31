import { describe, expect, it, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import PrimeVue from 'primevue/config';
import ConfirmationService from 'primevue/confirmationservice';
import ToastService from 'primevue/toastservice';
import DetalleSolicitud from '../src/componentes/DetalleSolicitud.vue';
import { Categorias, ColorEstado, ColorPrioridad, Estados, Prioridades, valorDe } from '../src/config/tema';

vi.mock('../src/servicios/api', () => ({
    api: {
        listarComentarios: vi.fn().mockResolvedValue([]),
        cambiarEstado: vi.fn(),
        comentar: vi.fn(),
    },
}));

function solicitud(extras = {}) {
    return {
        id: '11111111-1111-1111-1111-111111111111',
        codigo: 'SOL-2026-000001',
        titulo: 'No puedo acceder al correo institucional',
        descripcion: 'Olvide la contrasena de mi usuario.',
        solicitanteNombre: 'Marcela Quispe',
        solicitanteCorreo: 'marcela.quispe@correo.upea.bo',
        unidadDestino: 'Unidad de Sistemas',
        categoria: 'Tecnologica',
        prioridad: 'Alta',
        estado: 'Recibida',
        categoriaSugerida: 'Tecnologica',
        confianzaSugerencia: 0.87,
        origenSugerencia: 'simulado',
        fechaCreacion: '2026-08-31T12:00:00+00:00',
        fechaActualizacion: '2026-08-31T12:00:00+00:00',
        transicionesPermitidas: ['EnRevision', 'Rechazada'],
        ...extras,
    };
}

// El dialogo de PrimeVue se traslada al cuerpo del documento, asi que lo
// renderizado no aparece en el arbol del componente sino en document.body.
async function montar(datos) {
    document.body.innerHTML = "";

    const componente = mount(DetalleSolicitud, {
        props: { solicitud: datos },
        attachTo: document.body,
        global: { plugins: [PrimeVue, ToastService, ConfirmationService] },
    });

    // El dialogo se abre en el siguiente ciclo y su contenido se traslada al
    // cuerpo del documento; hacen falta varios ciclos para que quede montado.
    for (let ciclo = 0; ciclo < 4; ciclo += 1) {
        await componente.vm.$nextTick();
        await new Promise((seguir) => setTimeout(seguir, 0));
    }

    return document.body.innerHTML;
}

describe('detalle de una solicitud', () => {
    it('ofrece exactamente las transiciones que declara la API', async () => {
        const texto = await montar(solicitud());

        expect(texto).toContain('EnRevision');
        expect(texto).toContain('Rechazada');

        // La maquina de estados vive en el dominio. Si el cliente tuviera su
        // propia lista, ofreceria transiciones que el servidor rechaza.
        expect(texto).not.toContain('Cerrada');
        expect(texto).not.toContain('Resuelta');
    });

    it('declara que un estado final no admite cambios', async () => {
        const texto = await montar(solicitud({ estado: 'Cerrada', transicionesPermitidas: [] }));

        expect(texto).toContain('estado final y no admite mas cambios');
    });

    it('muestra la sugerencia del clasificador con su trazabilidad', async () => {
        const texto = await montar(solicitud());

        expect(texto).toContain('0.87');
        expect(texto).toContain('simulado');
    });

    it('advierte cuando la sugerencia no coincide con la categoria asignada', async () => {
        const texto = await montar(solicitud({ categoria: 'Administrativa' }));

        expect(texto).toContain('No coincide con la categoria asignada');
    });

    it('no advierte nada cuando sugerencia y categoria coinciden', async () => {
        const texto = await montar(solicitud());

        expect(texto).not.toContain('No coincide con la categoria asignada');
    });
});

describe('correspondencia con los enumerados de la API', () => {
    it('cada estado tiene color asignado', () => {
        Estados.forEach((estado) => expect(ColorEstado[estado.nombre]).toBeDefined());
    });

    it('cada prioridad tiene color asignado', () => {
        Prioridades.forEach((prioridad) => expect(ColorPrioridad[prioridad.nombre]).toBeDefined());
    });

    it('los valores numericos coinciden con los del dominio', () => {
        // Si alguien agrega un valor al enumerado del servidor sin reflejarlo
        // aqui, los desplegables enviarian un numero equivocado.
        expect(valorDe(Estados, 'Recibida')).toBe(1);
        expect(valorDe(Estados, 'Cerrada')).toBe(5);
        expect(valorDe(Categorias, 'Tecnologica')).toBe(3);
        expect(valorDe(Categorias, 'Otra')).toBe(99);
        expect(valorDe(Prioridades, 'Critica')).toBe(4);
    });

    it('un nombre desconocido no devuelve un valor inventado', () => {
        expect(valorDe(Estados, 'Inexistente')).toBeNull();
    });
});
