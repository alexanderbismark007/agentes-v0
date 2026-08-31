<script setup>
import { onMounted, ref } from 'vue';
import Button from 'primevue/button';
import Column from 'primevue/column';
import DataTable from 'primevue/datatable';
import FileUpload from 'primevue/fileupload';
import Message from 'primevue/message';
import ProgressSpinner from 'primevue/progressspinner';
import Tag from 'primevue/tag';
import Textarea from 'primevue/textarea';
import { api } from '../servicios/api';
import { useNotificador } from '../composables/useNotificador';

const { exito, fallo } = useNotificador();

const pregunta = ref('');
const respuesta = ref(null);
const consultando = ref(false);

const documentos = ref([]);
const cargandoDocumentos = ref(false);

const ejemplos = [
    '¿En cuántos días hábiles se entrega el certificado de notas?',
    '¿Cuál es el plazo para presentar un reclamo?',
    '¿Qué unidad atiende las solicitudes tecnológicas?',
    '¿Cuánto tiempo se conservan los expedientes?',
];

async function consultar() {
    if (pregunta.value.trim().length < 8) {
        return;
    }

    consultando.value = true;
    respuesta.value = null;

    try {
        respuesta.value = await api.consultar(pregunta.value.trim());
    } catch (error) {
        fallo(error);
    } finally {
        consultando.value = false;
    }
}

async function cargarDocumentos() {
    cargandoDocumentos.value = true;
    try {
        documentos.value = await api.listarDocumentos();
    } catch (error) {
        fallo(error);
    } finally {
        cargandoDocumentos.value = false;
    }
}

async function subir(evento) {
    try {
        const resultado = await api.subirDocumento(evento.files[0]);

        if (resultado.sinCambios) {
            exito('El documento ya estaba indexado', 'Su contenido no cambió, no se reprocesó.');
        } else {
            exito(`${resultado.titulo} indexado`, `${resultado.fragmentos} fragmentos en ${resultado.milisegundosTotales} ms.`);
        }

        await cargarDocumentos();
    } catch (error) {
        fallo(error);
    }
}

async function eliminar(documento) {
    try {
        await api.eliminarDocumento(documento.id);
        exito(`${documento.titulo} eliminado.`);
        await cargarDocumentos();
    } catch (error) {
        fallo(error);
    }
}

/// El color de la similitud comunica cuánto respalda la respuesta.
///
/// Un número suelto no dice nada a quien no conoce la escala; el color permite
/// leer de un vistazo si conviene confiar en la cita o verificarla.
function colorSimilitud(valor) {
    if (valor >= 0.5) return 'success';
    if (valor >= 0.3) return 'warn';
    return 'danger';
}

onMounted(cargarDocumentos);
</script>

<template>
    <div class="encabezado-vista">
        <h1>Consulta documental</h1>
        <p>Preguntas sobre el reglamento institucional, respondidas con las fuentes que las respaldan.</p>
    </div>

    <div class="campo">
        <Textarea
            v-model="pregunta"
            rows="2"
            auto-resize
            placeholder="Escriba su pregunta sobre el reglamento"
            @keydown.ctrl.enter="consultar"
        />
    </div>

    <div class="barra-filtros">
        <Button
            label="Consultar"
            icon="pi pi-search"
            :loading="consultando"
            :disabled="pregunta.trim().length < 8"
            @click="consultar"
        />
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

    <section v-else-if="respuesta" class="respuesta">
        <!--
            Cuando no hay respaldo documental el sistema lo declara en lugar de
            improvisar. Se muestra como advertencia, no como error: responder
            "no encontré información" es el comportamiento correcto.
        -->
        <Message
            v-if="!respuesta.tieneRespaldo"
            severity="warn"
            :closable="false"
            icon="pi pi-exclamation-triangle"
        >
            {{ respuesta.respuesta }}
        </Message>

        <template v-else>
            <div class="indicador texto-respuesta">
                <span class="etiqueta">Respuesta</span>
                <p>{{ respuesta.respuesta }}</p>
            </div>

            <h3>Fuentes que la respaldan</h3>

            <article v-for="fuente in respuesta.fuentes" :key="fuente.numero" class="fuente">
                <header>
                    <strong>[{{ fuente.numero }}]</strong>
                    <span>{{ fuente.documento }}</span>
                    <span v-if="fuente.referencia" class="referencia">{{ fuente.referencia }}</span>
                    <Tag
                        :value="`similitud ${fuente.similitud}`"
                        :severity="colorSimilitud(fuente.similitud)"
                    />
                </header>
                <p>{{ fuente.extracto }}</p>
            </article>
        </template>

        <footer class="metadatos">
            <span>Embeddings: <strong>{{ respuesta.proveedorEmbeddings }}</strong></span>
            <span>Lenguaje: <strong>{{ respuesta.proveedorLenguaje }}</strong></span>
            <span v-if="respuesta.modeloLenguaje">Modelo: <strong>{{ respuesta.modeloLenguaje }}</strong></span>
            <span>Mejor similitud: <strong>{{ respuesta.confianzaRecuperacion }}</strong></span>
            <span>{{ respuesta.milisegundosTotales }} ms</span>
            <span v-if="respuesta.tokensEntrada">
                {{ respuesta.tokensEntrada }} / {{ respuesta.tokensSalida }} tokens
            </span>
        </footer>
    </section>

    <h3 style="margin-top: 2rem">Documentos indexados</h3>

    <div class="barra-filtros">
        <FileUpload
            mode="basic"
            name="archivo"
            accept=".pdf,.txt,.md"
            :auto="true"
            choose-label="Indexar documento"
            custom-upload
            @uploader="subir"
        />
        <Button label="Actualizar" icon="pi pi-refresh" severity="secondary" outlined @click="cargarDocumentos" />
    </div>

    <DataTable :value="documentos" :loading="cargandoDocumentos" data-key="id">
        <template #empty>
            <div class="vacio">No hay documentos indexados.</div>
        </template>

        <Column field="titulo" header="Documento" />
        <Column field="nombreArchivo" header="Archivo" />
        <Column field="cantidadFragmentos" header="Fragmentos" style="width: 8rem" />
        <Column field="modeloEmbeddings" header="Modelo" style="width: 9rem" />

        <Column header="Acceso" style="width: 8rem">
            <template #body="{ data }">
                <Tag
                    :value="data.nivelAcceso"
                    :severity="data.nivelAcceso === 'Interno' ? 'warn' : 'secondary'"
                />
            </template>
        </Column>

        <Column style="width: 4rem">
            <template #body="{ data }">
                <Button icon="pi pi-trash" severity="danger" text @click="eliminar(data)" />
            </template>
        </Column>
    </DataTable>
</template>

<style scoped>
.respuesta {
    margin-bottom: 1.5rem;
}

.texto-respuesta p {
    margin: 0.5rem 0 0;
    font-size: 1.02rem;
    line-height: 1.55;
}

h3 {
    font-size: 1.05rem;
    margin: 1.5rem 0 0.75rem;
}

.fuente {
    background: var(--p-content-background);
    border: 1px solid var(--p-content-border-color);
    border-radius: var(--p-content-border-radius);
    padding: 0.85rem 1rem;
    margin-bottom: 0.75rem;
}

.fuente header {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-bottom: 0.5rem;
    font-size: 0.88rem;
}

.fuente .referencia {
    color: var(--p-primary-color);
    font-weight: 600;
}

.fuente p {
    margin: 0;
    font-size: 0.88rem;
    color: var(--p-text-muted-color);
    line-height: 1.5;
    white-space: pre-line;
}

.metadatos {
    display: flex;
    flex-wrap: wrap;
    gap: 1.25rem;
    margin-top: 1rem;
    font-size: 0.8rem;
    color: var(--p-text-muted-color);
}
</style>
