import { createApp } from 'vue';
import PrimeVue from 'primevue/config';
import ConfirmationService from 'primevue/confirmationservice';
import ToastService from 'primevue/toastservice';
import App from './App.vue';
import { TemaMesaAyuda } from './config/tema';
import 'primeicons/primeicons.css';
import './css/app.css';

createApp(App)
    .use(PrimeVue, {
        theme: {
            preset: TemaMesaAyuda,
            options: {
                // El tema oscuro se activa segun la preferencia del sistema
                // operativo, no con un interruptor propio.
                darkModeSelector: 'system',
            },
        },
        locale: {
            emptyMessage: 'Sin resultados',
            emptyFilterMessage: 'Sin coincidencias',
            accept: 'Si',
            reject: 'No',
        },
    })
    .use(ToastService)
    .use(ConfirmationService)
    .mount('#aplicacion');
