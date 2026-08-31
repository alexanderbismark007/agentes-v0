<script setup>
import { onMounted, reactive, ref } from 'vue';
import Button from 'primevue/button';
import Column from 'primevue/column';
import DataTable from 'primevue/datatable';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';
import Tag from 'primevue/tag';
import DetalleSolicitud from './DetalleSolicitud.vue';
import NuevaSolicitud from './NuevaSolicitud.vue';
import { api } from '../servicios/api';
import { Categorias, ColorEstado, ColorPrioridad, Estados, Prioridades } from '../config/tema';
import { useNotificador } from '../composables/useNotificador';

const { fallo } = useNotificador();

const solicitudes = ref([]);
const total = ref(0);
const cargando = ref(false);

const seleccionada = ref(null);
const mostrarNueva = ref(false);

// El filtro es reactivo y se envia tal cual a la API. La paginacion la resuelve
// el servidor: traer todo y paginar en el navegador funcionaria con las ocho
// solicitudes de ejemplo y fallaria con las de un semestre real.
const filtro = reactive({
    estado: null,
    categoria: null,
    prioridad: null,
    texto: '',
    pagina: 1,
    tamanoPagina: 10,
});

async function cargar() {
    cargando.value = true;
    try {
        const pagina = await api.listarSolicitudes(filtro);
        solicitudes.value = pagina.elementos;
        total.value = pagina.total;
    } catch (error) {
        fallo(error);
    } finally {
        cargando.value = false;
    }
}

function aplicarFiltros() {
    filtro.pagina = 1;
    cargar();
}

function limpiarFiltros() {
    filtro.estado = null;
    filtro.categoria = null;
    filtro.prioridad = null;
    filtro.texto = '';
    aplicarFiltros();
}

function paginar(evento) {
    filtro.pagina = evento.page + 1;
    filtro.tamanoPagina = evento.rows;
    cargar();
}

function abrir(fila) {
    seleccionada.value = fila.data ?? fila;
}

/// Tras crear o modificar una solicitud se recarga el listado completo en lugar
/// de retocar la fila en memoria. Es una llamada mas, pero garantiza que lo que
/// se ve coincide con lo que la base de datos tiene: el codigo correlativo, la
/// categoria sugerida y las transiciones permitidas los decide el servidor.
async function refrescar() {
    mostrarNueva.value = false;
    await cargar();
}

function fecha(valor) {
    return new Date(valor).toLocaleDateString('es-BO');
}

onMounted(cargar);
</script>

<template>
    <div class="encabezado-vista">
        <h1>Solicitudes</h1>
        <p>Registro y seguimiento de las solicitudes de la comunidad universitaria.</p>
    </div>

    <div class="barra-filtros">
        <InputText
            v-model="filtro.texto"
            placeholder="Buscar por titulo, descripcion o codigo"
            style="min-width: 260px"
            @keyup.enter="aplicarFiltros"
        />
        <Select
            v-model="filtro.estado"
            :options="Estados"
            option-label="nombre"
            option-value="valor"
            placeholder="Estado"
            show-clear
            style="min-width: 150px"
            @change="aplicarFiltros"
        />
        <Select
            v-model="filtro.categoria"
            :options="Categorias"
            option-label="nombre"
            option-value="valor"
            placeholder="Categoria"
            show-clear
            style="min-width: 165px"
            @change="aplicarFiltros"
        />
        <Select
            v-model="filtro.prioridad"
            :options="Prioridades"
            option-label="nombre"
            option-value="valor"
            placeholder="Prioridad"
            show-clear
            style="min-width: 150px"
            @change="aplicarFiltros"
        />
        <Button label="Buscar" icon="pi pi-search" @click="aplicarFiltros" />
        <Button label="Limpiar" icon="pi pi-filter-slash" severity="secondary" outlined @click="limpiarFiltros" />

        <Button
            label="Nueva solicitud"
            icon="pi pi-plus"
            style="margin-left: auto"
            @click="mostrarNueva = true"
        />
    </div>

    <DataTable
        :value="solicitudes"
        :loading="cargando"
        lazy
        paginator
        :rows="filtro.tamanoPagina"
        :total-records="total"
        :rows-per-page-options="[10, 20, 50]"
        :first="(filtro.pagina - 1) * filtro.tamanoPagina"
        selection-mode="single"
        data-key="id"
        current-page-report-template="{first} a {last} de {totalRecords}"
        paginator-template="FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink RowsPerPageDropdown CurrentPageReport"
        @page="paginar"
        @row-select="abrir"
    >
        <template #empty>
            <div class="vacio">No hay solicitudes que cumplan esos criterios.</div>
        </template>

        <Column field="codigo" header="Codigo" style="width: 10rem" />
        <Column field="titulo" header="Titulo" />
        <Column field="unidadDestino" header="Unidad" style="width: 15rem" />

        <Column header="Categoria" style="width: 9rem">
            <template #body="{ data }">{{ data.categoria }}</template>
        </Column>

        <Column header="Prioridad" style="width: 8rem">
            <template #body="{ data }">
                <Tag :value="data.prioridad" :severity="ColorPrioridad[data.prioridad] ?? 'secondary'" />
            </template>
        </Column>

        <Column header="Estado" style="width: 9rem">
            <template #body="{ data }">
                <Tag :value="data.estado" :severity="ColorEstado[data.estado] ?? 'secondary'" />
            </template>
        </Column>

        <Column header="Registrada" style="width: 8rem">
            <template #body="{ data }">{{ fecha(data.fechaCreacion) }}</template>
        </Column>
    </DataTable>

    <DetalleSolicitud
        :solicitud="seleccionada"
        @cerrar="seleccionada = null"
        @cambio="cargar"
    />

    <NuevaSolicitud
        :visible="mostrarNueva"
        @cerrar="mostrarNueva = false"
        @creada="refrescar"
    />
</template>
