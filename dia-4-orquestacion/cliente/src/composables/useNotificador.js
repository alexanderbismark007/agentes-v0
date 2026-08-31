import { useToast } from 'primevue/usetoast';

/// Traduce el resultado de una operacion en un aviso para la persona.
///
/// Existe para que ninguna vista tenga que decidir como se muestra un error.
/// En particular, un error de validacion trae el detalle por campo y aqui se
/// arma un mensaje que dice que corregir, en lugar de un generico "datos
/// invalidos" que obliga a adivinar.
export function useNotificador() {
    const toast = useToast();

    function exito(mensaje, detalle = null) {
        toast.add({ severity: 'success', summary: mensaje, detail: detalle, life: 4000 });
    }

    function aviso(mensaje, detalle = null) {
        toast.add({ severity: 'warn', summary: mensaje, detail: detalle, life: 6000 });
    }

    function fallo(error) {
        const detalle = error?.errores
            ? Object.values(error.errores).flat().join(' ')
            : error?.detalle ?? null;

        // Un conflicto no es una falla del sistema, es una regla de negocio que
        // se cumplio. Se muestra como advertencia y no como error para que la
        // persona entienda que el sistema funciono como debia.
        const severidad = error?.status === 409 ? 'warn' : 'error';

        toast.add({
            severity: severidad,
            summary: error?.titulo ?? 'Ocurrio un problema',
            detail: detalle,
            life: 8000,
        });
    }

    return { exito, aviso, fallo };
}
