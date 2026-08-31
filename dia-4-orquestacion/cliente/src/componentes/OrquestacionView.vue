<script setup>
import { onMounted, reactive, ref } from 'vue';
import Button from 'primevue/button';
import Column from 'primevue/column';
import DataTable from 'primevue/datatable';
import Message from 'primevue/message';
import Select from 'primevue/select';
import Tag from 'primevue/tag';
import { api } from '../servicios/api';
import { useNotificador } from '../composables/useNotificador';

const { fallo } = useNotificador();

const salud = ref(null);
const eventos = ref([]);
const total = ref(0);
const cargando = ref(false);

const filtro = reactive({
    resultado: null,
    origen: null,
    pagina: 1,
    tamanoPagina: 20,
});

const resultados = [
    { nombre: 'Recibido', valor: 1 },
    { nombre: 'Procesado', valor: 2 },
    { nombre: 'Fallido', valor: 3 },
    { nombre: 'Duplicado', valor: 4 },
    { nombre: 'Rechazado', valor: 5 },
];

const colorResultado = {
    Recibido: 'info',
    Procesado: 'success',
    Fallido: 'danger',
    Duplicado: 'secondary',
    Rechazado: 'warn',
};

async function cargar() {
    cargando.value = true;
    try {
        const [indicadores, pagina] = await Promise.all([
            api.saludOrquestacion(),
            api.listarEventos(filtro),
        ]);

        salud.value = indicadores;
        eventos.value = pagina.elementos;
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

function paginar(evento) {
    filtro.pagina = evento.page + 1;
    filtro.tamanoPagina = evento.rows;
    cargar();
}

function fecha(valor) {
    return new Date(valor).toLocaleString('es-BO');
}

/// La tasa de exito se pinta segun su valor porque es el numero que hay que
/// vigilar. Un flujo automatizado que nadie observa deja de funcionar sin que
/// nadie se entere.
function colorTasa(valor) {
    if (valor >= 95) return 'var(--p-green-500)';
    if (valor >= 80) return 'var(--p-orange-500)';
    return 'var(--p-red-500)';
}

onMounted(cargar);
</script>

<template>
    <div class="encabezado-vista">
        <h1>Automatización</h1>
        <p>Estado del canal por el que entran las solicitudes desde flujos externos.</p>
    </div>

    <template v-if="salud">
        <Message
            v-if="!salud.firmaExigida"
            severity="warn"
            :closable="false"
            icon="pi pi-shield"
        >
            La verificación de firma está desactivada. La dirección del webhook es pública:
            en un despliegue real, cualquiera podría registrar solicitudes a nombre de terceros.
        </Message>

        <div class="rejilla-indicadores">
            <div class="indicador">
                <span class="etiqueta">Eventos</span>
                <div class="valor">{{ salud.totalEventos }}</div>
                <span class="nota">recibidos en total</span>
            </div>

            <div class="indicador">
                <span class="etiqueta">Tasa de éxito</span>
                <div class="valor" :style="{ color: colorTasa(salud.tasaExito) }">
                    {{ salud.tasaExito }}%
                </div>
                <!--
                    Los duplicados no entran en el calculo: un reintento correcto
                    no es una falla del procesamiento.
                -->
                <span class="nota">sobre los eventos procesados</span>
            </div>

            <div class="indicador">
                <span class="etiqueta">Fallidos</span>
                <div class="valor">{{ salud.fallidos }}</div>
                <span class="nota">requieren revisión</span>
            </div>

            <div class="indicador">
                <span class="etiqueta">Duplicados</span>
                <div class="valor">{{ salud.duplicados }}</div>
                <span class="nota">reenvíos que no crearon trámite</span>
            </div>

            <div class="indicador">
                <span class="etiqueta">Rechazados</span>
                <div class="valor">{{ salud.rechazados }}</div>
                <span class="nota">firma inválida o datos incorrectos</span>
            </div>

            <div class="indicador">
                <span class="etiqueta">Tiempo medio</span>
                <div class="valor">{{ salud.milisegundosPromedio }}</div>
                <span class="nota">milisegundos por evento</span>
            </div>
        </div>

        <Message
            v-if="salud.totalEventos === 0"
            severity="secondary"
            :closable="false"
            icon="pi pi-info-circle"
        >
            Todavía no llegó ningún evento. Importe el flujo de n8n desde
            <code>flujos/mesa-ayuda-ingreso.json</code> y envíe el formulario.
        </Message>
    </template>

    <h3>Bitácora de eventos</h3>

    <p class="explicacion">
        Queda registro de todo lo que llegó, incluidos los intentos rechazados: son
        justamente los que interesa poder revisar después.
    </p>

    <div class="barra-filtros">
        <Select
            v-model="filtro.resultado"
            :options="resultados"
            option-label="nombre"
            option-value="valor"
            placeholder="Resultado"
            show-clear
            style="min-width: 165px"
            @change="aplicarFiltros"
        />
        <Button label="Actualizar" icon="pi pi-refresh" severity="secondary" outlined @click="cargar" />
    </div>

    <DataTable
        :value="eventos"
        :loading="cargando"
        lazy
        paginator
        :rows="filtro.tamanoPagina"
        :total-records="total"
        :rows-per-page-options="[20, 50]"
        :first="(filtro.pagina - 1) * filtro.tamanoPagina"
        data-key="id"
        @page="paginar"
    >
        <template #empty>
            <div class="vacio">No hay eventos registrados.</div>
        </template>

        <Column header="Momento" style="width: 12rem">
            <template #body="{ data }">{{ fecha(data.fechaCreacion) }}</template>
        </Column>

        <Column field="tipo" header="Tipo" style="width: 12rem" />
        <Column field="origen" header="Origen" style="width: 8rem" />

        <Column header="Resultado" style="width: 9rem">
            <template #body="{ data }">
                <Tag :value="data.resultado" :severity="colorResultado[data.resultado] ?? 'secondary'" />
            </template>
        </Column>

        <Column field="referencia" header="Referencia" style="width: 12rem" />

        <Column header="Detalle">
            <template #body="{ data }">
                <span class="detalle">{{ data.detalle ?? '—' }}</span>
            </template>
        </Column>

        <Column header="Tiempo" style="width: 6rem">
            <template #body="{ data }">{{ data.milisegundos }} ms</template>
        </Column>
    </DataTable>
</template>

<style scoped>
h3 {
    font-size: 1.05rem;
    margin: 1.5rem 0 0.5rem;
}

.explicacion {
    color: var(--p-text-muted-color);
    font-size: 0.88rem;
    max-width: 68ch;
    margin: 0 0 1rem;
}

.detalle {
    font-size: 0.85rem;
    color: var(--p-text-muted-color);
}

code {
    background: var(--p-surface-200);
    padding: 0.1rem 0.35rem;
    border-radius: 4px;
    font-size: 0.85em;
}

@media (prefers-color-scheme: dark) {
    code {
        background: var(--p-surface-700);
    }
}
</style>
