import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

// El resultado de la compilacion se escribe directamente en wwwroot, que es la
// carpeta desde la que ASP.NET sirve archivos estaticos. Asi no hace falta un
// servidor web adicional: la misma aplicacion que expone la API entrega la
// interfaz, en un solo contenedor y un solo puerto.
export default defineConfig({
    plugins: [vue()],

    build: {
        outDir: '../src/MesaAyuda.Api/wwwroot',
        emptyOutDir: true,
    },

    server: {
        port: 5173,
        host: true,

        // Durante el desarrollo con recarga en caliente, Vite sirve la interfaz
        // y reenvia a la API todo lo que empiece por /api. En produccion no hace
        // falta porque ambas cosas salen del mismo origen.
        proxy: {
            '/api': { target: 'http://localhost:8080', changeOrigin: true },
            '/salud': { target: 'http://localhost:8080', changeOrigin: true },
        },
    },

    test: {
        environment: 'jsdom',
        setupFiles: ['./pruebas/preparacion.js'],
        include: ['pruebas/**/*.prueba.js'],
    },
});
