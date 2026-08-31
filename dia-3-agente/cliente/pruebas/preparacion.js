// jsdom no implementa algunas interfaces del navegador que PrimeVue usa para
// medir y posicionar sus componentes. Sin estas definiciones, montar cualquier
// vista que contenga un area de texto que crece o un dialogo falla con un error
// que no tiene relacion con lo que se esta probando.
if (!globalThis.ResizeObserver) {
    globalThis.ResizeObserver = class {
        observe() {}
        unobserve() {}
        disconnect() {}
    };
}

if (!globalThis.matchMedia) {
    globalThis.matchMedia = (consulta) => ({
        matches: false,
        media: consulta,
        onchange: null,
        addEventListener() {},
        removeEventListener() {},
        addListener() {},
        removeListener() {},
        dispatchEvent: () => false,
    });
}
