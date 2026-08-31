# Día 4 — Orquestación de procesos inteligentes con n8n

Cuarto módulo del sistema **Mesa de Ayuda Institucional**.

Este proyecto contiene **todo lo de los días 1, 2 y 3** más el módulo de orquestación. Hasta
aquí, todo ocurría porque alguien llamaba a la API. Ahora el sistema **reacciona solo**.

---

## El flujo completo

```
Estudiante completa un formulario
            │
            ▼
      ┌───────────┐
      │    n8n    │
      └─────┬─────┘
            │  POST /orquestacion/entrada/solicitudes
            ▼
   ┌────────────────────┐
   │  ¿Firma válida?    │──── no ──► 401 + registro del intento
   └─────────┬──────────┘
             sí
             ▼
   ┌────────────────────┐
   │ ¿Ya se procesó?    │──── sí ──► devuelve el mismo código, no duplica
   └─────────┬──────────┘
             no
             ▼
   Registra la solicitud (día 1) + la clasifica
             │
             ▼
   Consulta el reglamento (día 2)
             │
     ┌───────┴────────┐
     │                │
 con respaldo    sin respaldo
     │                │
     ▼                ▼
 correo con      acuse simple
 la norma        de recepción
 y sus fuentes
     │                │
     └───────┬────────┘
             ▼
   Registra el envío en el expediente
             │
             ▼
        Bitácora
```

Si algo falla, el flujo **avisa al equipo de sistemas**. Un proceso automatizado que falla
en silencio equivale a un trámite perdido.

---

## Las tres propiedades que lo hacen usable

Automatizar es fácil. Automatizar de forma que se pueda dejar corriendo sin vigilancia
permanente exige tres cosas, y ninguna es opcional.

### 1. Idempotencia

**Toda automatización reintenta.** El nodo HTTP de n8n está configurado con tres reintentos;
la red se cae; el usuario hace doble clic. Sin protección, cada reintento sería un trámite
duplicado.

La API calcula la huella del cuerpo recibido y, si ya procesó un evento idéntico, devuelve
el mismo resultado en lugar de crear una segunda solicitud:

```bash
# Enviar tres veces exactamente el mismo cuerpo
curl -X POST http://localhost:8080/api/v1/orquestacion/entrada/solicitudes \
  -H "Content-Type: application/json" -d @solicitud.json
```

```json
{ "codigo": "SOL-2026-000009", "duplicado": false }
{ "codigo": "SOL-2026-000009", "duplicado": true  }
{ "codigo": "SOL-2026-000009", "duplicado": true  }
```

Una sola solicitud. Y responde `200`, no un error: el flujo externo debe poder reintentar
sin miedo.

### 2. Autenticidad

La dirección de un webhook es **pública**. Sin verificación, cualquiera que la conozca
puede registrar solicitudes a nombre de terceros.

La firma HMAC-SHA256 lo resuelve sin credenciales por usuario: quien envía calcula un
código a partir del cuerpo y de un secreto compartido; quien recibe repite el cálculo y
compara.

```powershell
# Activar en .env
EXIGIR_FIRMA=true
SECRETO_WEBHOOK=un-secreto-largo-y-aleatorio
```

Detalles que importan:

- La firma se calcula sobre el **cuerpo exacto recibido**. Por eso el endpoint lee el texto
  en crudo: deserializar y volver a serializar produciría un texto distinto y una firma
  distinta.
- La comparación es de **tiempo constante**. Comparar cadenas con el operador habitual se
  detiene en el primer carácter distinto, y esa diferencia de tiempo permite deducir la
  firma correcta byte a byte.
- Alterar **un solo carácter** del cuerpo después de firmar invalida la firma.

### 3. Trazabilidad

Cada evento queda asentado: lo que llegó, quién lo envió, qué se decidió, cuánto tardó y
cuántos intentos hubo. **También los rechazados** — son justamente los que interesa revisar.

```bash
curl http://localhost:8080/api/v1/orquestacion/eventos
curl http://localhost:8080/api/v1/orquestacion/salud
```

```json
{
  "totalEventos": 12,
  "procesados": 10,
  "fallidos": 1,
  "duplicados": 1,
  "rechazados": 0,
  "tasaExito": 90.9,
  "milisegundosPromedio": 187.4,
  "firmaExigida": true
}
```

Los duplicados **no** castigan la tasa de éxito: un reintento correcto no es una falla.

---

## Puesta en marcha

```powershell
.\tareas.ps1 levantar
```

Levanta cuatro servicios:

| Servicio | Dirección | Función |
|----------|-----------|---------|
| API | http://localhost:8080 | El sistema |
| n8n | http://localhost:5678 | Orquestador de flujos |
| Correo | http://localhost:8025 | Bandeja de prueba |
| PostgreSQL | localhost:5432 | Base de datos |

> El servidor de correo es **Mailpit**: acepta todo lo que se le envía y **no reenvía nada
> al exterior**. Se puede demostrar el envío sin riesgo de escribirle realmente a una
> persona.

### Importar el flujo en n8n

1. Abra http://localhost:5678
2. Menú **⋯ → Import from File**
3. Seleccione `flujos/mesa-ayuda-ingreso.json`
4. Configure la credencial SMTP:
   - Host: `correo` · Puerto: `1025` · Sin SSL/TLS · Usuario y clave: cualquiera
5. Active el flujo y abra la dirección del formulario

Cada envío del formulario aparecerá en la bandeja de http://localhost:8025.

### Pruebas

```powershell
.\tareas.ps1 probar
```

153 pruebas (134 de la API y 19 de la interfaz), sin credenciales ni conexión a Internet.

---

## La interfaz web

Se agrega la vista **Automatización**: los indicadores de salud del canal y la bitácora completa de eventos, incluidos los rechazados. La tasa de éxito se pinta según su valor para que una caída se note sin tener que leer números.

Vive en `cliente/` y se compila **dentro de la imagen**, así que `levantar` la deja lista
en http://localhost:8080. Node solo hace falta para trabajar con recarga en caliente:

```powershell
.\tareas.ps1 interfaz
```

---

## Endpoints nuevos

| Método | Ruta                                              | Descripción                        |
|--------|---------------------------------------------------|------------------------------------|
| `POST` | `/api/v1/orquestacion/entrada/solicitudes`         | Recibe una solicitud automatizada  |
| `GET`  | `/api/v1/orquestacion/eventos`                     | Bitácora de eventos                |
| `GET`  | `/api/v1/orquestacion/salud`                       | Indicadores de la automatización   |
| `POST` | `/api/v1/orquestacion/firmar`                      | Calcula una firma (solo desarrollo)|

> `/firmar` es una ayuda para configurar el flujo durante la sesión. Queda **deshabilitado
> en producción**, porque expone el uso del secreto compartido.

---

## Por qué el correo no lo arma n8n

El cuerpo del acuse lo construye la API, no el flujo. Podría haberse hecho al revés, pero:

- La decisión de **qué se le dice a un ciudadano** es lógica de negocio, y pertenece al
  sistema, no a la configuración de una herramienta.
- Es **verificable con pruebas automatizadas**. Un texto armado con expresiones dentro de
  un nodo de n8n no lo es.
- Si mañana se cambia de orquestador, el contenido no se pierde.

n8n hace lo que sabe hacer bien: **conectar**. Disparar, reintentar, ramificar, enviar,
avisar. La inteligencia queda del lado del sistema.

---

## Solución de problemas

**n8n no alcanza la API.** Dentro de la red de Docker el nombre del servicio es `api`, no
`localhost`. La URL correcta es `http://api:8080/...`.

**El correo no llega a Mailpit.** Verifique la credencial SMTP: host `correo`, puerto
`1025`, sin cifrado.

**n8n pide reconfigurar las credenciales tras reiniciar.** Falta `N8N_CLAVE_CIFRADO` en el
archivo `.env`. Sin ella, n8n genera una clave nueva en cada arranque y no puede descifrar
lo guardado.

**Recibo 401 con la firma activada.** Recuerde que la firma cubre el cuerpo exacto: un
espacio de diferencia la invalida. Use `/api/v1/orquestacion/firmar` para calcularla.

**El flujo duplica solicitudes.** No debería. Si ocurre, revise que el cuerpo enviado sea
idéntico entre reintentos: un campo con la marca de tiempo del envío cambia la huella.

---

## Ejercicios

Ver [EJERCICIOS.md](EJERCICIOS.md).

---

## Continúa en

**Día 5 — Modelos locales con Docker y Ollama.** Todo el sistema funciona, pero depende de
un servicio externo para razonar. En la última sesión se agrega el proveedor local y **el
sistema entero pasa a funcionar sin salir de la institución**, cambiando una variable de
entorno.
