import Aura from '@primeuix/themes/aura';
import { definePreset } from '@primeuix/themes';

// Aura trae esmeralda como color primario. Sin este preset, los botones, las
// etiquetas, el foco y las casillas salen verdes por su cuenta, sin relacion con
// la identidad del sistema. El preset ata todo PrimeVue a una misma escala azul,
// sobria y con contraste suficiente para leerse en un proyector.
//
// Cambiar la identidad visual del sistema completo se hace aqui y en ningun
// otro lugar: ninguna vista define colores propios.
export const TemaMesaAyuda = definePreset(Aura, {
    semantic: {
        primary: {
            50: '#f2f6fa',
            100: '#dfe9f3',
            200: '#bed3e7',
            300: '#93b5d6',
            400: '#6291c0',
            500: '#3a6fa5',
            600: '#245685',
            700: '#1d466d',
            800: '#173857',
            900: '#12293f',
            950: '#0b1a29',
        },
        colorScheme: {
            light: {
                primary: {
                    color: '{primary.600}',
                    contrastColor: '#ffffff',
                    hoverColor: '{primary.700}',
                    activeColor: '{primary.800}',
                },
                highlight: {
                    background: '{primary.50}',
                    focusBackground: '{primary.100}',
                    color: '{primary.700}',
                    focusColor: '{primary.800}',
                },
            },
            dark: {
                primary: {
                    color: '{primary.300}',
                    contrastColor: '{primary.950}',
                    hoverColor: '{primary.200}',
                    activeColor: '{primary.100}',
                },
                highlight: {
                    background: 'color-mix(in srgb, {primary.400}, transparent 84%)',
                    focusBackground: 'color-mix(in srgb, {primary.400}, transparent 76%)',
                    color: '{primary.100}',
                    focusColor: '{primary.50}',
                },
            },
        },
    },
});

/// Colores con los que se pinta cada estado y cada prioridad.
///
/// Viven aqui, y no repartidos por las vistas, porque el mismo estado debe
/// verse igual en la tabla, en el detalle y en el tablero. Si el criterio
/// cambia, cambia en un solo lugar.
export const ColorEstado = {
    Recibida: 'info',
    EnRevision: 'warn',
    EnProceso: 'warn',
    Resuelta: 'success',
    Cerrada: 'secondary',
    Rechazada: 'danger',
};

export const ColorPrioridad = {
    Baja: 'secondary',
    Media: 'info',
    Alta: 'warn',
    Critica: 'danger',
};

/// Los enumerados viajan como numero en el cuerpo de las peticiones. Estas
/// listas alimentan los desplegables y traducen entre el nombre que ve la
/// persona y el valor que espera la API.
export const Estados = [
    { nombre: 'Recibida', valor: 1 },
    { nombre: 'EnRevision', valor: 2 },
    { nombre: 'EnProceso', valor: 3 },
    { nombre: 'Resuelta', valor: 4 },
    { nombre: 'Cerrada', valor: 5 },
    { nombre: 'Rechazada', valor: 6 },
];

export const Categorias = [
    { nombre: 'Academica', valor: 1 },
    { nombre: 'Administrativa', valor: 2 },
    { nombre: 'Tecnologica', valor: 3 },
    { nombre: 'Financiera', valor: 4 },
    { nombre: 'Infraestructura', valor: 5 },
    { nombre: 'Otra', valor: 99 },
];

export const Prioridades = [
    { nombre: 'Baja', valor: 1 },
    { nombre: 'Media', valor: 2 },
    { nombre: 'Alta', valor: 3 },
    { nombre: 'Critica', valor: 4 },
];

export function valorDe(lista, nombre) {
    return lista.find((elemento) => elemento.nombre === nombre)?.valor ?? null;
}
