<script setup>
import { onMounted, ref } from 'vue';
import Toast from 'primevue/toast';
import ConfirmDialog from 'primevue/confirmdialog';
import BarraLateral from './componentes/BarraLateral.vue';
import TableroView from './componentes/TableroView.vue';
import SolicitudesView from './componentes/SolicitudesView.vue';
import ConocimientoView from './componentes/ConocimientoView.vue';
import AgenteView from './componentes/AgenteView.vue';
import { api } from './servicios/api';

const vistaActual = ref('tablero');
const estadoServicio = ref(null);

// Las vistas se declaran aqui y no dentro de la barra lateral, porque es esta
// aplicacion la que sabe que puede mostrar. Cada dia del ciclo agrega una
// entrada a esta lista y su componente correspondiente.
const vistas = [
    { clave: 'tablero', titulo: 'Tablero', icono: 'pi pi-chart-bar', componente: TableroView },
    { clave: 'solicitudes', titulo: 'Solicitudes', icono: 'pi pi-inbox', componente: SolicitudesView },
    { clave: 'conocimiento', titulo: 'Conocimiento', icono: 'pi pi-book', componente: ConocimientoView },
    { clave: 'agente', titulo: 'Agente', icono: 'pi pi-sparkles', componente: AgenteView },
];

function componenteActual() {
    return vistas.find((vista) => vista.clave === vistaActual.value)?.componente ?? TableroView;
}

onMounted(async () => {
    try {
        estadoServicio.value = await api.salud();
    } catch {
        // Que no se pueda leer el estado no debe impedir usar el sistema: la
        // barra lateral simplemente no muestra el proveedor activo.
        estadoServicio.value = null;
    }
});
</script>

<template>
    <div class="disposicion">
        <BarraLateral
            :vistas="vistas"
            :vista-actual="vistaActual"
            :estado="estadoServicio"
            @navegar="vistaActual = $event"
        />

        <main class="contenido">
            <component :is="componenteActual()" />
        </main>

        <Toast position="bottom-right" />
        <ConfirmDialog />
    </div>
</template>
