<script setup>
import { onMounted, ref } from 'vue';
import Button from 'primevue/button';
import Column from 'primevue/column';
import DataTable from 'primevue/datatable';
import Dialog from 'primevue/dialog';
import Message from 'primevue/message';
import ProgressSpinner from 'primevue/progressspinner';
import Tag from 'primevue/tag';
import Textarea from 'primevue/textarea';
import ToggleSwitch from 'primevue/toggleswitch';
import { api } from '../servicios/api';
import { useNotificador } from '../composables/useNotificador';

const { exito, fallo } = useNotificador();

const pregunta = ref('');
const autorizarEscritura = ref(false);
const respuesta = ref(null);
const consultando = ref(false);

const herramientas = ref([]);
const mostrarHerramientas = ref(false);

const reporte = ref(null);
const generandoReporte = ref(false);

const ejemplos = [
    '¿Cuál es el resumen de indicadores de la mesa de ayuda?',
    'Muéstrame las solicitudes tecnológicas en proceso',
    '¿Qué plazo establece el reglamento para resolver una solicitud?',
];

async function consultar() {
    if (pregunta.value.trim().length < 8) {
        return;
    }

    consultando.value = true;
    respuesta.value = null;

    try {
        respuesta.value = await api.consultarAgente(pregunta.value.trim(), autorizarEscritura.value);
    } catch (error) {
        fallo(error);
    } finally {
        consultando.value = false;
    }
}

async function generarReporte() {
    generandoReporte.value = true;
    try {
        reporte.value = await api.generarReporte();
        exito('Reporte generado.');
    } catch (error) {
        fallo(error);
    } finally {
        generandoReporte.value = false;
    }
}

onMounted(async () => {
    try {
        herramientas.value = await api.listarHerramientas();
    } catch (error) {
        fallo(error);
    }
});
</script>

<template>
    <div class="encabezado-vista">
        <h1>Agente</h1>
        <p>Consultas que el agente resuelve eligiendo qué herramienta usar en cada paso.</p>
    </div>

    <div class="campo">
        <Textarea
            v-model="pregunta"
            rows="2"
            auto-resize
            placeholder="Escriba su consulta"
            @keydown.ctrl.enter="consultar"
        />
    </div>

    <div class="barra-filtros">
        <Button label="Consultar" icon="pi pi-send" :loading="consultando" @click="consultar" />

        <!--
            El interruptor es la aprobacion humana. Sin el, una herramienta que
            modifica datos queda retenida por mas que el modelo insista en
            usarla: la frontera esta en el codigo del agente, no en el texto de
            sus instrucciones.
        -->
        <div class="autorizacion">
            <ToggleSwitch v-model="autorizarEscritura" input-id="autorizar" />
            <label for="autorizar">
                Autorizar acciones que modifican datos
            </label>
        </div>

        <Button
            label="Herramientas"
            icon="pi pi-wrench"
            severity="secondary"
            outlined
            style="margin-left: auto"
            @click="mostrarHerramientas = true"
        />
    </div>

    <div class="barra-filtros">
        <Button
            v-for="ejemplo in ejemplos"
            :key="ejemplo"
            :label="ejemplo"
            severity="secondary"
            outlined
            size="small"
            @click="pregunta = ejemplo; consultar()"
        />
    </div>

    <div v-if="consultando" class="vacio">
        <ProgressSpinner style="width: 40px; height: 40px" />
    </div>

    <section v-else-if="respuesta">
        <Message v-if="respuesta.alcanzoLimite" severity="warn" :closable="false">
            El agente agotó el límite de pasos sin concluir. La respuesta es parcial.
        </Message>

        <div class="indicador">
            <span class="etiqueta">Respuesta</span>
            <p class="texto">{{ respuesta.respuesta }}</p>
        </div>

        <!--
            Las acciones retenidas se muestran aparte y con enfasis: son lo que
            el sistema decidio NO hacer, y esa decision es tan relevante como la
            respuesta misma.
        -->
        <template v-if="respuesta.accionesPendientes.length > 0">
            <h3>Acciones retenidas</h3>

            <Message
                v-for="accion in respuesta.accionesPendientes"
                :key="accion.herramienta"
                severity="warn"
                :closable="false"
                icon="pi pi-lock"
            >
                <strong>{{ accion.herramienta }}</strong> — {{ accion.motivo }}
                <pre>{{ accion.argumentos }}</pre>
            </Message>
        </template>

        <h3>Traza de ejecución</h3>

        <p v-if="respuesta.pasos.length === 0" class="vacio" style="padding: 1rem">
            El agente respondió sin usar herramientas.
        </p>

        <ol v-else class="traza">
            <li v-for="paso in respuesta.pasos" :key="paso.numero">
                <div class="cabecera-paso">
                    <Tag :value="`${paso.numero}`" severity="secondary" />
                    <strong>{{ paso.herramienta }}</strong>
                    <Tag
                        :value="paso.exitosa ? 'correcta' : 'fallida'"
                        :severity="paso.exitosa ? 'success' : 'danger'"
                    />
                    <span class="tiempo">{{ paso.milisegundos }} ms</span>
                </div>
                <pre>{{ paso.argumentos }}</pre>
                <p v-if="paso.resultado" class="resultado">{{ paso.resultado }}</p>
            </li>
        </ol>

        <footer class="metadatos">
            <span>Proveedor: <strong>{{ respuesta.proveedorLenguaje }}</strong></span>
            <span>{{ respuesta.tokensEntrada }} / {{ respuesta.tokensSalida }} tokens</span>
            <span>{{ respuesta.milisegundosTotales }} ms</span>
        </footer>
    </section>

    <h3 style="margin-top: 2rem">Reporte ejecutivo</h3>

    <p class="explicacion">
        El reporte no usa el ciclo de decisión del agente: los indicadores se calculan
        siempre igual y el modelo solo redacta su lectura. Un reporte que la dirección va a
        leer no debe variar según lo que el modelo decida consultar ese día.
    </p>

    <Button
        label="Generar reporte"
        icon="pi pi-file"
        :loading="generandoReporte"
        @click="generarReporte"
    />

    <div v-if="reporte" class="indicador" style="margin-top: 1rem">
        <span class="etiqueta">{{ reporte.titulo }}</span>
        <p class="texto">{{ reporte.contenido }}</p>

        <details>
            <summary>Datos sobre los que se elaboró</summary>
            <pre v-for="dato in reporte.datosConsultados" :key="dato.numero">{{ dato.resultado }}</pre>
        </details>
    </div>

    <Dialog v-model:visible="mostrarHerramientas" modal header="Herramientas disponibles" :style="{ width: '46rem' }">
        <p class="explicacion">
            El conjunto es cerrado: el agente no puede ejecutar nada que no esté aquí.
        </p>

        <DataTable :value="herramientas" data-key="nombre">
            <Column field="nombre" header="Herramienta" style="width: 14rem" />
            <Column field="descripcion" header="Cuándo se usa" />
            <Column header="Riesgo" style="width: 8rem">
                <template #body="{ data }">
                    <Tag
                        :value="data.riesgo"
                        :severity="data.requiereAprobacion ? 'danger' : 'secondary'"
                    />
                </template>
            </Column>
        </DataTable>
    </Dialog>
</template>

<style scoped>
.autorizacion {
    display: flex;
    align-items: center;
    gap: 0.55rem;
    font-size: 0.88rem;
}

h3 {
    font-size: 1.05rem;
    margin: 1.5rem 0 0.75rem;
}

.texto {
    margin: 0.5rem 0 0;
    line-height: 1.55;
    white-space: pre-line;
}

.explicacion {
    color: var(--p-text-muted-color);
    font-size: 0.88rem;
    max-width: 62ch;
    margin: 0 0 1rem;
}

.traza {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
}

.traza li {
    background: var(--p-content-background);
    border: 1px solid var(--p-content-border-color);
    border-radius: var(--p-content-border-radius);
    padding: 0.85rem 1rem;
}

.cabecera-paso {
    display: flex;
    align-items: center;
    gap: 0.55rem;
    margin-bottom: 0.5rem;
    font-size: 0.9rem;
}

.tiempo {
    margin-left: auto;
    font-size: 0.8rem;
    color: var(--p-text-muted-color);
}

pre {
    margin: 0;
    font-size: 0.8rem;
    background: var(--p-surface-100);
    padding: 0.5rem 0.65rem;
    border-radius: var(--p-content-border-radius);
    overflow-x: auto;
    white-space: pre-wrap;
    word-break: break-word;
}

@media (prefers-color-scheme: dark) {
    pre {
        background: var(--p-surface-800);
    }
}

.resultado {
    margin: 0.5rem 0 0;
    font-size: 0.85rem;
    color: var(--p-text-muted-color);
}

.metadatos {
    display: flex;
    flex-wrap: wrap;
    gap: 1.25rem;
    margin-top: 1rem;
    font-size: 0.8rem;
    color: var(--p-text-muted-color);
}

details summary {
    cursor: pointer;
    font-size: 0.85rem;
    color: var(--p-text-muted-color);
    margin-top: 0.75rem;
}
</style>
