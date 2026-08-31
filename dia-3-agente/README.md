# Día 3 — Agentes de IA integrados con APIs y bases de datos

Tercer módulo del sistema **Mesa de Ayuda Institucional**.

Este proyecto contiene **todo lo de los días 1 y 2** más el módulo del agente: el sistema
deja de responder una sola clase de pregunta y pasa a **decidir qué hacer** según lo que se
le consulte.

---

## Chatbot, asistente y agente

| | Genera texto | Usa contexto externo | Elige qué hacer | Ejecuta acciones |
|---|---|---|---|---|
| Chatbot | Sí | No | No | No |
| Asistente documental (día 2) | Sí | Sí, siempre el mismo | No | No |
| **Agente (día 3)** | Sí | Sí | **Sí** | **Sí, bajo control** |

El día 2 el sistema siempre hacía lo mismo: buscar en documentos. El agente, ante la misma
interfaz, decide si consultar el reglamento, buscar en la base de solicitudes, calcular
estadísticas o abrir un expediente concreto.

---

## El ciclo de razonamiento

```
Pregunta + catálogo de herramientas
        │
        ▼
   ┌─────────────┐
   │   MODELO    │  decide: ¿herramienta o respuesta final?
   └──────┬──────┘
          │
   ┌──────┴───────┐
   │              │
respuesta    herramienta
   │              │
   │              ▼
   │      ¿modifica datos?
   │         │        │
   │        no       sí
   │         │        │
   │         │   ¿autorizado por un operador?
   │         │      │            │
   │         │     sí           no
   │         │      │            │
   │         ▼      ▼            ▼
   │      EJECUTAR            RETENER
   │         │              (acción pendiente)
   │         ▼                   │
   │   resultado ────────────────┘
   │         │
   │         └──► vuelve al modelo (hasta 6 iteraciones)
   ▼
Respuesta + traza completa
```

---

## Los tres límites

Un agente sin límites es un riesgo operativo, no una funcionalidad. Este tiene tres, y los
tres están en el código, no en el texto del prompt.

### 1. Conjunto cerrado de herramientas

El agente **no escribe SQL** ni llama a servicios arbitrarios. Solo puede invocar lo que
está declarado como `IHerramientaAgente`, y cada herramienta valida sus propios parámetros
antes de tocar nada. Si el modelo pide una herramienta que no existe, se le informa el
error y se le da otra oportunidad.

| Herramienta | Riesgo | Qué hace |
|---|---|---|
| `buscar_solicitudes` | Lectura | Busca con filtros acotados, máximo 25 resultados |
| `ver_solicitud` | Lectura | Devuelve el expediente completo de un código |
| `obtener_estadisticas` | Lectura | Indicadores agregados de la mesa |
| `consultar_reglamento` | Lectura | Reutiliza el RAG del día 2 |
| `cambiar_estado_solicitud` | **Escritura** | **Requiere aprobación humana** |

### 2. Límite de iteraciones

```
MaximoIteraciones                6
TiempoMaximoHerramientaSegundos  30
```

Cada iteración es una llamada al modelo. Ese número acota directamente el costo máximo y la
latencia máxima de una respuesta. Si se agota, el agente lo declara con `alcanzoLimite` en
lugar de fingir que concluyó.

### 3. Aprobación humana para escribir

Esta es la parte importante, y conviene demostrarla en clase tal como está:

**El modelo propone la acción de todos modos, aunque las instrucciones le digan que no está
autorizada.** El proveedor simulado lo hace a propósito. Y el sistema la retiene igual.

```bash
# Sin autorización
curl -X POST http://localhost:8080/api/v1/agente/consultas \
  -H "Content-Type: application/json" \
  -d '{"pregunta":"Cambia el estado de la solicitud SOL-2026-000004 a EnRevision","autorizarEscritura":false}'
```

```json
{
  "accionesPendientes": [
    {
      "herramienta": "cambiar_estado_solicitud",
      "argumentos": "{\"codigo\":\"SOL-2026-000004\",\"nuevoEstado\":\"EnRevision\"}",
      "motivo": "La herramienta modifica datos y requiere aprobación de un operador."
    }
  ]
}
```

La solicitud **no cambió**. Con `"autorizarEscritura": true`, la misma consulta la ejecuta.

> **La lección:** el prompt no es una frontera de seguridad. Un modelo puede ignorar una
> instrucción; el código que valida antes de ejecutar, no.

Y aun con autorización, **la máquina de estados del día 1 sigue mandando**: pedir que una
solicitud `Recibida` salte directamente a `Cerrada` falla, porque el dominio lo prohíbe.
Tres capas independientes, ninguna confía en la anterior.

---

## Trazabilidad

Toda respuesta del agente incluye la traza de cómo se construyó:

```json
{
  "respuesta": "...",
  "pasos": [
    {
      "numero": 1,
      "herramienta": "obtener_estadisticas",
      "argumentos": "{}",
      "exitosa": true,
      "resultado": "8 solicitudes analizadas",
      "milisegundos": 63
    }
  ],
  "accionesPendientes": [],
  "alcanzoLimite": false,
  "proveedorLenguaje": "simulado",
  "tokensEntrada": 412,
  "tokensSalida": 88
}
```

Sin esa traza el agente sería una caja negra, y una decisión automatizada que no se puede
auditar no es admisible en un entorno institucional.

---

## Puesta en marcha

```powershell
.\tareas.ps1 levantar
```

| Recurso        | Dirección                        |
|----------------|----------------------------------|
| API            | http://localhost:8080            |
| Documentación  | http://localhost:8080/swagger    |

### Pruebas

```powershell
.\tareas.ps1 probar
```

102 pruebas, sin credenciales ni conexión a Internet.

---

## Endpoints nuevos

| Método | Ruta                                  | Descripción                                |
|--------|---------------------------------------|--------------------------------------------|
| `POST` | `/api/v1/agente/consultas`            | Resuelve una consulta usando herramientas  |
| `GET`  | `/api/v1/agente/herramientas`         | Catálogo de herramientas y su nivel de riesgo |
| `POST` | `/api/v1/agente/reportes/ejecutivo`   | Genera el reporte ejecutivo                |

### Ejemplos de selección de herramienta

| Pregunta | Herramienta elegida |
|---|---|
| "¿Qué plazo establece el reglamento para resolver una solicitud?" | `consultar_reglamento` |
| "¿Cuál es el resumen de indicadores de la mesa de ayuda?" | `obtener_estadisticas` |
| "Muéstrame las solicitudes tecnológicas en proceso" | `buscar_solicitudes` |
| "Dame el detalle de la solicitud SOL-2026-000004" | `ver_solicitud` |

---

## El reporte ejecutivo se construye distinto, y a propósito

En el reporte **no hay elección de herramientas**: los indicadores se calculan siempre de la
misma forma y el modelo solo redacta su lectura.

Un reporte que la dirección va a leer no debe variar según lo que el modelo decida
consultar ese día. Además, los datos duros se devuelven junto al texto, de modo que cada
afirmación pueda contrastarse contra el número que la originó.

> **Criterio general:** use un agente cuando la pregunta sea abierta y no se sepa de
> antemano qué se necesita. Use un flujo fijo cuando el resultado deba ser reproducible.

---

## Solución de problemas

**El agente responde `alcanzoLimite: true`.** Agotó las iteraciones. Revise la traza para
ver si quedó girando entre las mismas herramientas, o suba `MaximoIteraciones`.

**El agente elige la herramienta equivocada.** Con el proveedor `simulado` es esperable en
preguntas ambiguas: elige por palabras clave, no por comprensión. Compárelo con
`PROVEEDOR_LENGUAJE=nube`.

**Una acción queda pendiente y esperaba que se ejecutara.** Envíe `"autorizarEscritura": true`.

**Un paso figura con `exitosa: false`.** La herramienta falló o el dominio rechazó la
operación. El campo `resultado` indica el motivo; el agente continúa con esa información.

---

## Ejercicios

Ver [EJERCICIOS.md](EJERCICIOS.md).

---

## Continúa en

**Día 4 — Orquestación con n8n.** Hasta aquí todo ocurre porque alguien llama a la API. En
la próxima sesión el sistema **reacciona solo**: un formulario dispara un flujo que
clasifica la solicitud, consulta el reglamento, responde por correo y registra el resultado.
