<script setup>
import { ref, watch } from 'vue';
import Button from 'primevue/button';
import Checkbox from 'primevue/checkbox';
import Dialog from 'primevue/dialog';
import Divider from 'primevue/divider';
import Message from 'primevue/message';
import Tag from 'primevue/tag';
import Textarea from 'primevue/textarea';
import { api } from '../servicios/api';
import { ColorEstado, ColorPrioridad, Estados, valorDe } from '../config/tema';
import { useNotificador } from '../composables/useNotificador';

const props = defineProps({
    solicitud: { type: Object, default: null },
});

const emit = defineEmits(['cerrar', 'cambio']);

const { exito, fallo } = useNotificador();

const detalle = ref(null);
const comentarios = ref([]);
const ocupado = ref(false);

const nuevoComentario = ref('');
const comentarioInterno = ref(true);
const motivoCambio = ref('');

watch(
    () => props.solicitud,
    async (valor) => {
        detalle.value = valor;
        comentarios.value = [];
        nuevoComentario.value = '';
        motivoCambio.value = '';

        if (valor) {
            await cargarComentarios(valor.id);
        }
    },

    // Se ejecuta tambien en el montaje: si el componente nace con una solicitud
    // ya asignada, sin esto el dialogo quedaria cerrado hasta el siguiente
    // cambio de la propiedad.
    { immediate: true },
);

async function cargarComentarios(id) {
    try {
        comentarios.value = await api.listarComentarios(id, true);
    } catch (error) {
        fallo(error);
    }
}

/// El boton de cada transicion se arma con lo que devuelve la API en
/// transicionesPermitidas, no con una lista escrita en el navegador.
///
/// La maquina de estados vive en el dominio; si el front tuviera su propia
/// copia, tarde o temprano ofreceria una transicion que el servidor rechaza y
/// la persona veria un error sin entender por que.
async function cambiarEstado(nombreEstado) {
    ocupado.value = true;
    try {
        const actualizada = await api.cambiarEstado(
            detalle.value.id,
            valorDe(Estados, nombreEstado),
            motivoCambio.value || null,
        );

        detalle.value = actualizada;
        motivoCambio.value = '';

        await cargarComentarios(actualizada.id);
        exito(`La solicitud paso a ${actualizada.estado}.`);
        emit('cambio');
    } catch (error) {
        fallo(error);
    } finally {
        ocupado.value = false;
    }
}

async function comentar() {
    if (!nuevoComentario.value.trim()) {
        return;
    }

    ocupado.value = true;
    try {
        await api.comentar(detalle.value.id, {
            autor: 'Operador',
            contenido: nuevoComentario.value.trim(),
            esInterno: comentarioInterno.value,
        });

        nuevoComentario.value = '';
        await cargarComentarios(detalle.value.id);
        exito('Comentario agregado.');
    } catch (error) {
        fallo(error);
    } finally {
        ocupado.value = false;
    }
}

function fecha(valor) {
    return new Date(valor).toLocaleString('es-BO');
}
</script>

<template>
    <Dialog
        :visible="detalle !== null"
        modal
        :style="{ width: '46rem' }"
        :header="detalle?.codigo ?? ''"
        @update:visible="$emit('cerrar')"
    >
        <template v-if="detalle">
            <h3 style="margin-top: 0">{{ detalle.titulo }}</h3>

            <dl class="detalle-lista">
                <dt>Estado</dt>
                <dd><Tag :value="detalle.estado" :severity="ColorEstado[detalle.estado] ?? 'secondary'" /></dd>

                <dt>Prioridad</dt>
                <dd><Tag :value="detalle.prioridad" :severity="ColorPrioridad[detalle.prioridad] ?? 'secondary'" /></dd>

                <dt>Categoria</dt>
                <dd>{{ detalle.categoria }}</dd>

                <dt>Unidad responsable</dt>
                <dd>{{ detalle.unidadDestino }}</dd>

                <dt>Solicitante</dt>
                <dd>{{ detalle.solicitanteNombre }} &lt;{{ detalle.solicitanteCorreo }}&gt;</dd>

                <dt>Registrada</dt>
                <dd>{{ fecha(detalle.fechaCreacion) }}</dd>

                <dt>Descripcion</dt>
                <dd>{{ detalle.descripcion }}</dd>
            </dl>

            <!--
                La sugerencia del clasificador se muestra siempre, con su nivel
                de confianza y el componente que la produjo. Es un dato auxiliar
                y auditable, no la decision: la categoria que manda es la que
                figura arriba.
            -->
            <Message
                v-if="detalle.categoriaSugerida"
                severity="secondary"
                :closable="false"
                icon="pi pi-info-circle"
            >
                Categoria sugerida: <strong>{{ detalle.categoriaSugerida }}</strong>
                (confianza {{ detalle.confianzaSugerencia }}, origen {{ detalle.origenSugerencia }}).
                <template v-if="detalle.categoriaSugerida !== detalle.categoria">
                    No coincide con la categoria asignada.
                </template>
            </Message>

            <Divider />

            <h4>Cambiar estado</h4>

            <p v-if="detalle.transicionesPermitidas.length === 0" class="nota-final">
                <i class="pi pi-lock" /> Es un estado final y no admite mas cambios.
            </p>

            <template v-else>
                <div class="campo">
                    <label for="motivo">Motivo (opcional, queda como comentario interno)</label>
                    <Textarea id="motivo" v-model="motivoCambio" rows="2" auto-resize />
                </div>

                <div class="acciones">
                    <Button
                        v-for="estado in detalle.transicionesPermitidas"
                        :key="estado"
                        :label="estado"
                        :severity="estado === 'Rechazada' ? 'danger' : 'primary'"
                        :outlined="estado === 'Rechazada'"
                        :disabled="ocupado"
                        @click="cambiarEstado(estado)"
                    />
                </div>
            </template>

            <Divider />

            <h4>Expediente</h4>

            <p v-if="comentarios.length === 0" class="vacio" style="padding: 1rem">
                Sin comentarios registrados.
            </p>

            <div
                v-for="comentario in comentarios"
                :key="comentario.id"
                class="comentario"
                :class="{ interno: comentario.esInterno }"
            >
                <div class="meta">
                    {{ comentario.autor }} · {{ fecha(comentario.fechaCreacion) }}
                    <span v-if="comentario.esInterno">· interno</span>
                </div>
                {{ comentario.contenido }}
            </div>

            <div class="campo" style="margin-top: 1rem">
                <label for="comentario">Agregar comentario</label>
                <Textarea id="comentario" v-model="nuevoComentario" rows="2" auto-resize />
            </div>

            <div class="acciones">
                <div class="casilla">
                    <Checkbox v-model="comentarioInterno" input-id="interno" binary />
                    <label for="interno">Interno (no visible para el solicitante)</label>
                </div>

                <Button
                    label="Comentar"
                    icon="pi pi-comment"
                    :disabled="ocupado || !nuevoComentario.trim()"
                    @click="comentar"
                />
            </div>
        </template>
    </Dialog>
</template>

<style scoped>
.acciones {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.6rem;
}

.casilla {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.85rem;
    margin-right: auto;
}

.nota-final {
    color: var(--p-text-muted-color);
    font-size: 0.9rem;
}

h4 {
    margin: 0 0 0.75rem;
    font-size: 1rem;
}
</style>
