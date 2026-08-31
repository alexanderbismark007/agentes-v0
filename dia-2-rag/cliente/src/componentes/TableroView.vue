<script setup>
import { onMounted, ref } from 'vue';
import Button from 'primevue/button';
import ProgressSpinner from 'primevue/progressspinner';
import Tag from 'primevue/tag';
import { api } from '../servicios/api';
import { ColorEstado, ColorPrioridad } from '../config/tema';
import { useNotificador } from '../composables/useNotificador';

const { fallo } = useNotificador();

const resumen = ref(null);
const cargando = ref(false);

async function cargar() {
    cargando.value = true;
    try {
        resumen.value = await api.resumen();
    } catch (error) {
        fallo(error);
    } finally {
        cargando.value = false;
    }
}

onMounted(cargar);
</script>

<template>
    <div class="encabezado-vista">
        <h1>Tablero operativo</h1>
        <p>Estado general de la mesa de ayuda al momento de la consulta.</p>
    </div>

    <div v-if="cargando && !resumen" class="vacio">
        <ProgressSpinner style="width: 40px; height: 40px" />
    </div>

    <template v-else-if="resumen">
        <div class="rejilla-indicadores">
            <div class="indicador">
                <span class="etiqueta">Total</span>
                <div class="valor">{{ resumen.total }}</div>
                <span class="nota">solicitudes registradas</span>
            </div>

            <div class="indicador">
                <span class="etiqueta">Abiertas</span>
                <div class="valor">{{ resumen.abiertas }}</div>
                <span class="nota">en atencion</span>
            </div>

            <div class="indicador">
                <span class="etiqueta">Rezagadas</span>
                <div class="valor">{{ resumen.rezagadas }}</div>
                <!--
                    Es el numero que interesa vigilar: solicitudes abiertas con
                    mas de tres dias. El total y las cerradas describen volumen;
                    este describe un problema.
                -->
                <span class="nota">abiertas con mas de tres dias</span>
            </div>

            <div class="indicador">
                <span class="etiqueta">Resolucion</span>
                <div class="valor">{{ resumen.horasPromedioResolucion }}</div>
                <span class="nota">horas promedio</span>
            </div>
        </div>

        <div class="distribuciones">
            <section class="indicador">
                <span class="etiqueta">Por estado</span>
                <ul>
                    <li v-for="conteo in resumen.porEstado" :key="conteo.clave">
                        <Tag :value="conteo.clave" :severity="ColorEstado[conteo.clave] ?? 'secondary'" />
                        <strong>{{ conteo.cantidad }}</strong>
                    </li>
                </ul>
            </section>

            <section class="indicador">
                <span class="etiqueta">Por categoria</span>
                <ul>
                    <li v-for="conteo in resumen.porCategoria" :key="conteo.clave">
                        <span>{{ conteo.clave }}</span>
                        <strong>{{ conteo.cantidad }}</strong>
                    </li>
                </ul>
            </section>

            <section class="indicador">
                <span class="etiqueta">Por prioridad</span>
                <ul>
                    <li v-for="conteo in resumen.porPrioridad" :key="conteo.clave">
                        <Tag :value="conteo.clave" :severity="ColorPrioridad[conteo.clave] ?? 'secondary'" />
                        <strong>{{ conteo.cantidad }}</strong>
                    </li>
                </ul>
            </section>
        </div>

        <div class="pie-tablero">
            <Button
                label="Actualizar"
                icon="pi pi-refresh"
                severity="secondary"
                outlined
                :loading="cargando"
                @click="cargar"
            />
            <small>Generado el {{ new Date(resumen.generadoEn).toLocaleString('es-BO') }}</small>
        </div>
    </template>
</template>

<style scoped>
.distribuciones {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
    gap: 1rem;
}

.distribuciones ul {
    list-style: none;
    margin: 0.75rem 0 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
}

.distribuciones li {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
    font-size: 0.9rem;
}

.pie-tablero {
    display: flex;
    align-items: center;
    gap: 1rem;
    margin-top: 1.5rem;
}

.pie-tablero small {
    color: var(--p-text-muted-color);
}
</style>
