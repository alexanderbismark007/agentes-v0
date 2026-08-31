<script setup>
import { reactive, ref, watch } from 'vue';
import Button from 'primevue/button';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Message from 'primevue/message';
import Select from 'primevue/select';
import Textarea from 'primevue/textarea';
import { api } from '../servicios/api';
import { Categorias, Prioridades } from '../config/tema';
import { useNotificador } from '../composables/useNotificador';

const props = defineProps({
    visible: { type: Boolean, default: false },
});

const emit = defineEmits(['cerrar', 'creada']);

const { exito, fallo } = useNotificador();

const enviando = ref(false);

// Los errores por campo llegan del validador del servidor. El formulario no
// duplica esas reglas: si lo hiciera, habria dos definiciones de lo que es un
// dato valido y tarde o temprano dejarian de coincidir.
const errores = ref({});

const formulario = reactive({
    titulo: '',
    descripcion: '',
    solicitanteNombre: '',
    solicitanteCorreo: '',
    unidadDestino: '',
    categoria: null,
    prioridad: 2,
});

watch(
    () => props.visible,
    (abierto) => {
        if (abierto) {
            Object.assign(formulario, {
                titulo: '',
                descripcion: '',
                solicitanteNombre: '',
                solicitanteCorreo: '',
                unidadDestino: '',
                categoria: null,
                prioridad: 2,
            });
            errores.value = {};
        }
    },
);

function errorDe(campo) {
    const clave = Object.keys(errores.value).find(
        (nombre) => nombre.toLowerCase() === campo.toLowerCase(),
    );

    return clave ? errores.value[clave][0] : null;
}

async function enviar() {
    enviando.value = true;
    errores.value = {};

    try {
        const creada = await api.crearSolicitud({
            ...formulario,
            unidadDestino: formulario.unidadDestino || null,
        });

        exito(
            `Solicitud ${creada.codigo} registrada.`,
            `Clasificada como ${creada.categoria} y derivada a ${creada.unidadDestino}.`,
        );

        emit('creada', creada);
    } catch (error) {
        if (error?.errores) {
            errores.value = error.errores;
        }
        fallo(error);
    } finally {
        enviando.value = false;
    }
}
</script>

<template>
    <Dialog
        :visible="visible"
        modal
        header="Nueva solicitud"
        :style="{ width: '40rem' }"
        @update:visible="$emit('cerrar')"
    >
        <Message severity="secondary" :closable="false" icon="pi pi-sparkles">
            Si no indica categoria ni unidad, el sistema las deduce del texto y registra la
            sugerencia junto con su nivel de confianza.
        </Message>

        <div class="campo" style="margin-top: 1rem">
            <label for="titulo">Titulo</label>
            <InputText id="titulo" v-model="formulario.titulo" :invalid="!!errorDe('Titulo')" />
            <small v-if="errorDe('Titulo')" class="error">{{ errorDe('Titulo') }}</small>
        </div>

        <div class="campo">
            <label for="descripcion">Descripcion</label>
            <Textarea
                id="descripcion"
                v-model="formulario.descripcion"
                rows="4"
                auto-resize
                :invalid="!!errorDe('Descripcion')"
            />
            <small v-if="errorDe('Descripcion')" class="error">{{ errorDe('Descripcion') }}</small>
        </div>

        <div class="fila">
            <div class="campo">
                <label for="nombre">Solicitante</label>
                <InputText
                    id="nombre"
                    v-model="formulario.solicitanteNombre"
                    :invalid="!!errorDe('SolicitanteNombre')"
                />
                <small v-if="errorDe('SolicitanteNombre')" class="error">
                    {{ errorDe('SolicitanteNombre') }}
                </small>
            </div>

            <div class="campo">
                <label for="correo">Correo</label>
                <InputText
                    id="correo"
                    v-model="formulario.solicitanteCorreo"
                    :invalid="!!errorDe('SolicitanteCorreo')"
                />
                <small v-if="errorDe('SolicitanteCorreo')" class="error">
                    {{ errorDe('SolicitanteCorreo') }}
                </small>
            </div>
        </div>

        <div class="fila">
            <div class="campo">
                <label for="categoria">Categoria (opcional)</label>
                <Select
                    id="categoria"
                    v-model="formulario.categoria"
                    :options="Categorias"
                    option-label="nombre"
                    option-value="valor"
                    placeholder="Deducir del texto"
                    show-clear
                />
            </div>

            <div class="campo">
                <label for="prioridad">Prioridad</label>
                <Select
                    id="prioridad"
                    v-model="formulario.prioridad"
                    :options="Prioridades"
                    option-label="nombre"
                    option-value="valor"
                />
            </div>
        </div>

        <div class="campo">
            <label for="unidad">Unidad responsable (opcional)</label>
            <InputText id="unidad" v-model="formulario.unidadDestino" placeholder="Deducir de la categoria" />
        </div>

        <template #footer>
            <Button label="Cancelar" severity="secondary" text @click="$emit('cerrar')" />
            <Button label="Registrar" icon="pi pi-check" :loading="enviando" @click="enviar" />
        </template>
    </Dialog>
</template>

<style scoped>
.fila {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1rem;
}

.error {
    color: var(--p-red-500);
    font-size: 0.8rem;
}
</style>
