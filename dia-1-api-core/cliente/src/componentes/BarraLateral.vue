<script setup>
defineProps({
    vistas: { type: Array, required: true },
    vistaActual: { type: String, required: true },
    estado: { type: Object, default: null },
});

defineEmits(['navegar']);
</script>

<template>
    <aside class="barra">
        <div class="marca">
            <i class="pi pi-building-columns" />
            <div>
                <strong>Mesa de Ayuda</strong>
                <small>Institucional</small>
            </div>
        </div>

        <nav>
            <button
                v-for="vista in vistas"
                :key="vista.clave"
                type="button"
                class="enlace"
                :class="{ activo: vista.clave === vistaActual }"
                @click="$emit('navegar', vista.clave)"
            >
                <i :class="vista.icono" />
                <span>{{ vista.titulo }}</span>
            </button>
        </nav>

        <!--
            El proveedor activo se muestra de forma permanente y no en una
            pantalla de configuracion escondida. Durante el ciclo se cambia de
            proveedor varias veces, y tener a la vista cual esta respondiendo
            evita atribuirle a un modelo el comportamiento de otro.
        -->
        <div v-if="estado" class="pie">
            <span class="etiqueta">Proveedor de lenguaje</span>
            <span class="valor">{{ estado.proveedorLenguaje }}</span>
        </div>

        <a class="documentacion" href="/swagger" target="_blank" rel="noopener">
            <i class="pi pi-book" />
            Documentacion de la API
        </a>
    </aside>
</template>

<style scoped>
.barra {
    width: 245px;
    flex-shrink: 0;
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
    padding: 1.25rem 1rem;
    background: var(--p-content-background);
    border-right: 1px solid var(--p-content-border-color);
}

.marca {
    display: flex;
    align-items: center;
    gap: 0.7rem;
}

.marca i {
    font-size: 1.5rem;
    color: var(--p-primary-color);
}

.marca strong {
    display: block;
    font-size: 1rem;
    line-height: 1.2;
}

.marca small {
    color: var(--p-text-muted-color);
    font-size: 0.78rem;
}

nav {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
}

.enlace {
    display: flex;
    align-items: center;
    gap: 0.65rem;
    width: 100%;
    padding: 0.6rem 0.75rem;
    border: 0;
    border-radius: var(--p-content-border-radius);
    background: transparent;
    color: var(--p-text-color);
    font: inherit;
    font-size: 0.92rem;
    text-align: left;
    cursor: pointer;
}

.enlace:hover {
    background: var(--p-content-hover-background);
}

.enlace.activo {
    background: var(--p-highlight-background);
    color: var(--p-highlight-color);
    font-weight: 600;
}

.pie {
    margin-top: auto;
    padding: 0.7rem 0.75rem;
    border-radius: var(--p-content-border-radius);
    background: var(--p-surface-100);
}

@media (prefers-color-scheme: dark) {
    .pie {
        background: var(--p-surface-800);
    }
}

.pie .etiqueta {
    display: block;
    font-size: 0.72rem;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: var(--p-text-muted-color);
}

.pie .valor {
    font-weight: 600;
    font-size: 0.9rem;
}

.documentacion {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.82rem;
    color: var(--p-text-muted-color);
    text-decoration: none;
}

.documentacion:hover {
    color: var(--p-primary-color);
}
</style>
