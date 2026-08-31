# Día 2 — Arquitecturas RAG para sistemas de conocimiento

Segundo módulo del sistema **Mesa de Ayuda Institucional**.

Este proyecto contiene **todo lo del día 1** más el módulo de conocimiento documental: la
mesa de ayuda pasa a responder preguntas sobre el reglamento institucional, citando la
fuente de cada afirmación.

---

## El problema que se resuelve

Un modelo de lenguaje, por sí solo, no conoce el reglamento de la universidad. Si se le
pregunta por el plazo de entrega de un certificado, responderá algo verosímil e inventado.

La respuesta no es reentrenar el modelo. Es **darle el texto correcto en el momento de
responder** y exigirle que se limite a él. Eso es RAG: recuperar primero, generar después.

```
Pregunta
   │
   ▼
[1] Se convierte la pregunta en un vector
   │
   ▼
[2] Se buscan los fragmentos más cercanos en la base vectorial
   │
   ▼
[3] ¿Alguno supera la similitud mínima?
   │
   ├── No ──► "No encontré información"  (nunca se llama al modelo)
   │
   └── Sí ──► [4] Se arman los fragmentos numerados como contexto
                    │
                    ▼
              [5] El modelo responde SOLO con ese contexto
                    │
                    ▼
              Respuesta + fuentes citadas + similitud
```

El paso 3 es el que evita la mayoría de las respuestas inventadas: si no hay material
pertinente, el sistema lo dice en lugar de improvisar, y ni siquiera consume una llamada
al modelo.

---

## Qué se agrega respecto al día 1

| Componente | Ubicación | Función |
|------------|-----------|---------|
| `IProveedorEmbeddings` | `Nucleo/Proveedores/` | Segunda abstracción: texto a vector |
| `ProveedorEmbeddingsSimulado` | `Nucleo/Proveedores/` | Vectores locales deterministas |
| `ProveedorEmbeddingsNube` | `Nucleo/Proveedores/` | Vectores de un servicio externo |
| `Fragmentador` | `Modulos/Conocimiento/` | Divide el documento respetando su estructura |
| `LectorDocumentos` | `Modulos/Conocimiento/` | Extrae texto de PDF, TXT y MD |
| `IBuscadorSemantico` | `Modulos/Conocimiento/` | Recupera los fragmentos más parecidos |
| `ServicioIndexacion` | `Modulos/Conocimiento/` | Incorpora documentos a la base |
| `ServicioConsultas` | `Modulos/Conocimiento/` | Orquesta los cinco pasos del diagrama |

La base de datos cambia de `postgres:16` a **`pgvector/pgvector:pg16`**, que agrega el tipo
de dato vectorial y el operador de distancia.

---

## Las tres decisiones que definen la calidad

### 1. Cómo se corta el documento

Es el punto que más afecta el resultado y el que más se subestima. Fragmentos demasiado
grandes diluyen el tema y traen ruido; demasiado pequeños pierden el contexto necesario
para entender la respuesta.

El `Fragmentador` no corta cada N caracteres. Corta respetando la estructura: primero
párrafos, luego oraciones, y solo como último recurso a mitad de una. Además **arrastra la
referencia**: detecta encabezados como `Artículo 7` y los adjunta a cada fragmento, que es
lo que después permite citar con precisión.

```
TamanoFragmento         900   caracteres objetivo
SolapamientoFragmento   150   caracteres repetidos entre fragmentos contiguos
```

El solapamiento existe para que una idea partida al medio siga siendo recuperable desde
cualquiera de los dos lados del corte.

### 2. Qué se considera "suficientemente parecido"

```
SimilitudMinima   0.15
```

Un fragmento apenas relacionado es **peor que ningún fragmento**, porque induce al modelo
a construir una respuesta sobre material que no viene al caso. Por eso se descarta todo lo
que no supere el umbral, aunque haya sido lo mejor encontrado.

### 3. Qué se le pide al modelo

Las instrucciones del sistema son explícitas: responder solo con los fragmentos, citar cada
afirmación con su número entre corchetes, y **declarar la ausencia de información en lugar
de suponer**. Las instrucciones están a la vista en `ServicioConsultas`, no escondidas.

---

## Puesta en marcha

```powershell
.\tareas.ps1 levantar
```

Al arrancar, el sistema aplica las migraciones, carga las solicitudes de ejemplo e **indexa
automáticamente** los documentos de la carpeta `documentos/`. La huella de contenido evita
reprocesar lo que no cambió, así que reiniciar el servicio es barato.

| Recurso        | Dirección                        |
|----------------|----------------------------------|
| API            | http://localhost:8080            |
| Documentación  | http://localhost:8080/swagger    |
| Estado         | http://localhost:8080/salud      |

### Pruebas

```powershell
.\tareas.ps1 probar
```

98 pruebas (79 de la API y 19 de la interfaz), sin credenciales ni conexión a Internet.

---

## La interfaz web

Se agrega la vista **Conocimiento**: la consulta documental muestra la respuesta junto a cada fuente, su artículo y su similitud, con el color de la etiqueta indicando cuánto respalda la afirmación. Cuando no hay respaldo, el sistema lo declara en lugar de improvisar.

Vive en `cliente/` y se compila **dentro de la imagen**, así que `levantar` la deja lista
en http://localhost:8080. Node solo hace falta para trabajar con recarga en caliente:

```powershell
.\tareas.ps1 interfaz
```

---

## Endpoints nuevos

| Método   | Ruta                                          | Descripción                          |
|----------|-----------------------------------------------|--------------------------------------|
| `POST`   | `/api/v1/conocimiento/consultas`              | Responde una pregunta con fuentes    |
| `POST`   | `/api/v1/conocimiento/documentos`             | Carga e indexa un documento          |
| `GET`    | `/api/v1/conocimiento/documentos`             | Lista los documentos indexados       |
| `DELETE` | `/api/v1/conocimiento/documentos/{id}`        | Elimina un documento y sus fragmentos|

### Ejemplo

```bash
curl -X POST http://localhost:8080/api/v1/conocimiento/consultas \
  -H "Content-Type: application/json" \
  -d '{"pregunta":"¿En cuántos días hábiles se entrega el certificado de notas?","nivelAcceso":1}'
```

```json
{
  "pregunta": "¿En cuántos días hábiles se entrega el certificado de notas?",
  "respuesta": "El certificado de notas se entrega en tres días hábiles [1].",
  "tieneRespaldo": true,
  "fuentes": [
    {
      "numero": 1,
      "documento": "reglamento-mesa-de-ayuda",
      "referencia": "Artículo 8",
      "extracto": "Se establecen los siguientes plazos especiales: a) Certificado de notas: tres días hábiles...",
      "similitud": 0.734
    }
  ],
  "confianzaRecuperacion": 0.734,
  "proveedorEmbeddings": "simulado",
  "milisegundosTotales": 41
}
```

Todo lo necesario para auditar está en la respuesta: **qué fragmento** respalda la
afirmación, **de qué artículo** proviene, **cuán parecido** era y **qué proveedor**
la produjo.

---

## Trazabilidad y control de acceso

**Cada respuesta es verificable.** Se devuelve el extracto de cada fuente, no solo su
nombre. Quien lea la respuesta puede confirmar la cita sin abrir el documento.

**Los documentos tienen nivel de acceso.** Un documento marcado como `Interno` nunca
aparece en una consulta pública: el filtro se aplica **dentro de la búsqueda**, antes de
que el fragmento llegue al modelo. Filtrar después sería inútil, porque el contenido ya
habría salido de la base.

**Cambiar de proveedor de embeddings invalida el índice.** Los vectores generados por un
modelo no son comparables con los de otro. El documento guarda con qué modelo fue indexado
y, si cambia, se reindexa solo.

---

## Por qué 384 dimensiones

Los tres proveedores del ciclo producen vectores de **384 dimensiones**:

| Proveedor  | Cómo llega a 384                                    |
|------------|-----------------------------------------------------|
| `simulado` | Por construcción                                    |
| `nube`     | Se solicita ese tamaño al servicio                  |
| local (día 5) | Los modelos compactos la usan de forma nativa    |

Así, la columna vectorial de la base de datos se declara una sola vez y cambiar de
proveedor obliga a reindexar, pero **nunca a migrar el esquema**.

---

## Solución de problemas

**`type "vector" does not exist`.** La base no tiene pgvector. Verifique que la imagen en
`docker-compose.yml` sea `pgvector/pgvector:pg16` y ejecute `.\tareas.ps1 reiniciar`.

**Todas las consultas responden "No encontré información".** No hay documentos indexados.
Revise que existan archivos en `documentos/` y consulte
`GET /api/v1/conocimiento/documentos`.

**Las respuestas citan fragmentos que no vienen al caso.** Suba `SimilitudMinima` en
`appsettings.json`.

**Las respuestas quedan incompletas.** Suba `FragmentosRecuperados` o `TamanoFragmento`.

**Un PDF se indexa en cero fragmentos.** Es un PDF escaneado, sin capa de texto. Necesita
reconocimiento óptico de caracteres antes de poder indexarse.

---

## Ejercicios

Ver [EJERCICIOS.md](EJERCICIOS.md).

---

## Continúa en

**Día 3 — Agentes de IA.** Hasta aquí el sistema responde preguntas sobre documentos. En la
próxima sesión aprende a **decidir qué herramienta usar**: consultar el reglamento, buscar
en la base de solicitudes o calcular estadísticas, según lo que se le pregunte.
