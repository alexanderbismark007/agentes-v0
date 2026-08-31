# Día 5 — Despliegue seguro de modelos locales con Docker y Ollama

Quinto y último módulo del sistema **Mesa de Ayuda Institucional**.

Este proyecto contiene **todo el ciclo**: la API, el conocimiento documental, el agente, la
orquestación y ahora la inferencia local. Es el sistema completo, corriendo sin salir de la
institución.

---

## Lo que se agregó, y lo que no hubo que tocar

Para que todo el sistema funcione con un modelo propio se agregaron **dos clases**:

```
Nucleo/Proveedores/ProveedorLocal.cs              (~170 líneas)
Nucleo/Proveedores/ProveedorEmbeddingsLocal.cs    (~120 líneas)
```

Y **dos líneas** en el registro de dependencias.

Lo que **no** hubo que modificar:

- El módulo de conocimiento del día 2, con toda su lógica de recuperación.
- El agente del día 3, con sus cinco herramientas y su ciclo de razonamiento.
- La orquestación del día 4, con su idempotencia y su firma.
- El dominio, los endpoints, las validaciones ni el esquema de la base de datos.

Eso es lo que compró la decisión del día 1 de definir `IProveedorLenguaje` antes de
necesitarlo. La migración a local no fue una reescritura: fue **una variable de entorno**.

```
PROVEEDOR_LENGUAJE=local
PROVEEDOR_EMBEDDINGS=local
```

---

## Local frente a nube

| | Nube | Local |
|---|---|---|
| Los datos salen de la institución | Sí | **No** |
| Costo por consulta | Sí | No |
| Costo de infraestructura | No | Sí |
| Calidad de respuesta | Mayor | Menor a igual tamaño |
| Latencia | Estable | Depende del equipo |
| Funciona sin Internet | No | **Sí** |
| Disponibilidad | Del proveedor | Propia |

Para una mesa de ayuda que maneja datos de estudiantes, la primera fila suele decidir la
discusión. Las demás son consecuencias que hay que administrar.

---

## Requisitos de recursos

| Modelo | Memoria | Comentario |
|---|---|---|
| `qwen2.5:1.5b` | ~2 GB | Muy liviano, respuestas breves |
| `llama3.2:3b` | ~4 GB | **Predeterminado**, buen equilibrio |
| `llama3.1:8b` | ~8 GB | Mejor calidad, exige más equipo |
| `all-minilm` | ~150 MB | Embeddings, 384 dimensiones |

La primera consulta es **notoriamente más lenta**: el modelo debe cargarse en memoria. Por
eso el tiempo de espera del proveedor local es de 180 segundos y no de 60.

> Sin GPU todo funciona, solo que más lento. Para la clase, `qwen2.5:1.5b` responde en un
> tiempo razonable en cualquier portátil.

---

## Puesta en marcha

```powershell
copy .env.ejemplo .env    # y edite las claves
.\tareas.ps1 levantar
```

**La primera vez descarga los modelos**, lo que puede tomar varios minutos según la
conexión. Quedan guardados en un volumen: los arranques siguientes son inmediatos.

| Servicio | Dirección | Publica puerto |
|----------|-----------|----------------|
| API | http://localhost:8080 | Sí |
| n8n | http://localhost:5678 | Sí |
| Correo | http://localhost:8025 | Sí |
| PostgreSQL | — | **No** |
| Ollama | — | **No** |

### Pruebas

```powershell
.\tareas.ps1 probar
```

145 pruebas. Las del proveedor local **no necesitan Ollama**: sustituyen el transporte HTTP
y verifican que el adaptador construya la petición correcta, interprete la respuesta y
traduzca cada falla a un error comprensible.

---

## Las cinco decisiones de seguridad

### 1. Ollama no publica puertos

**Ollama no tiene autenticación.** Cualquiera que alcance su puerto puede usar el modelo,
descargar otros modelos y consumir toda la memoria del equipo.

Por eso el servicio `modelos` no declara `ports:`. Solo la API lo alcanza, por la red
interna. Exponerlo "para probar" es el error más común al desplegar un modelo local.

### 2. Dos redes, una de ellas sin salida

```yaml
networks:
  interna:
    internal: true     # sin salida a Internet
  publica:
```

La base de datos y el modelo viven solo en `interna`. Aunque alguien lograra ejecutar algo
dentro de esos contenedores, **no podría sacar datos hacia afuera**.

### 3. Límites de memoria y CPU

```yaml
deploy:
  resources:
    limits:
      memory: 6g
      cpus: "4"
```

Un modelo sin límite puede consumir toda la memoria del equipo y dejar sin recursos al
resto de los servicios. Además se acota cuántos modelos permanecen cargados
(`OLLAMA_MAX_LOADED_MODELS`) y por cuánto tiempo (`OLLAMA_KEEP_ALIVE`), porque cada modelo
cargado ocupa memoria de forma permanente.

### 4. Sin claves por defecto

A diferencia de los días anteriores, varios valores **no tienen valor por defecto**:

```yaml
POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?Defina POSTGRES_PASSWORD en el archivo .env}
SECRETO_WEBHOOK: ${SECRETO_WEBHOOK:?Defina SECRETO_WEBHOOK en el archivo .env}
N8N_CLAVE_CIFRADO: ${N8N_CLAVE_CIFRADO:?Defina N8N_CLAVE_CIFRADO en el archivo .env}
```

El compose **se niega a arrancar** si faltan. Es deliberado: una clave por defecto que nadie
cambia es una clave pública.

En este día, además, la firma de webhooks viene **activada** (`EXIGIR_FIRMA=true`) y n8n
exige autenticación.

### 5. La API no corre como root

Definido desde el día 1 en el `Dockerfile`, y sigue vigente.

---

## La ventana de contexto: un detalle que rompe el RAG en silencio

```csharp
Opciones = new OpcionesModelo
{
    Contexto = _configuracion.VentanaContexto   // 4096
}
```

El valor por defecto de Ollama es corto. Sin fijarlo, los fragmentos que el módulo de
conocimiento entrega **se truncan sin aviso**: el modelo responde con la mitad del contexto
y nadie se entera de que faltó información.

Es el tipo de falla que no produce ningún error y que solo se detecta comparando respuestas.

---

## Verificar la dimensión antes de fallar

El proveedor local comprueba que el modelo produzca vectores de 384 dimensiones. Si no:

```
El modelo local 'nomic-embed-text' produce vectores de 768 dimensiones, pero el
sistema almacena vectores de 384. Use un modelo de la dimensión esperada o cambie
Dimensiones.Estandar y genere una migración nueva.
```

Sin esa comprobación, el error aparecería al insertar en la base de datos, con un mensaje
del motor difícil de relacionar con la causa real.

---

## Migrar de nube a local: el procedimiento completo

```powershell
# 1. Cambiar el proveedor
#    PROVEEDOR_LENGUAJE=local
#    PROVEEDOR_EMBEDDINGS=local

# 2. Levantar
.\tareas.ps1 levantar

# 3. Reindexar los documentos
#    Los vectores de un modelo NO son comparables con los de otro.
#    El sistema lo detecta solo: el documento guarda con qué modelo fue
#    indexado y se reprocesa al arrancar.
```

**No hace falta migrar el esquema**, porque los tres proveedores producen 384 dimensiones.
Esa coincidencia no fue casual: se decidió el día 2 precisamente para que este momento
fuera indoloro.

---

## Comparar los tres proveedores

El sistema informa cuál está activo:

```bash
curl http://localhost:8080/salud
```

```json
{
  "estado": "activo",
  "proveedorLenguaje": "local",
  "proveedorEmbeddings": "local",
  "dimensionesVector": 384
}
```

Y cada respuesta declara su origen y su consumo, lo que permite comparar los tres sobre las
mismas preguntas:

| | `simulado` | `local` | `nube` |
|---|---|---|---|
| Credenciales | No | No | Sí |
| Internet | No | Solo para descargar | Sí |
| Costo por consulta | No | No | Sí |
| Determinista | Sí | No | No |
| Calidad | Reglas fijas | Media | Alta |

---

## Solución de problemas

**La primera consulta demora muchísimo.** Es esperable: el modelo se carga en memoria. Las
siguientes son mucho más rápidas mientras siga cargado (`OLLAMA_KEEP_ALIVE`).

**`No se pudo contactar al servicio de modelos local`.** El contenedor no está listo.
Verifique con `docker compose ps` y `docker compose logs modelos`.

**`Verifique que el modelo esté descargado`.** La descarga falló o se usó un nombre
distinto al del `.env`. Revise `docker compose logs descarga-modelos`.

**El equipo se queda sin memoria.** Use un modelo más pequeño (`qwen2.5:1.5b`) o baje
`LIMITE_MEMORIA_MODELOS`.

**Las respuestas empeoraron respecto de la nube.** Es esperable con un modelo pequeño.
Compare con `llama3.1:8b` si el equipo lo permite, y decida con datos si la diferencia
justifica enviar información fuera de la institución.

**Docker rechaza arrancar por variables faltantes.** Correcto. Copie `.env.ejemplo` a
`.env` y complete las claves.

---

## Ejercicios

Ver [EJERCICIOS.md](EJERCICIOS.md).

---

## Cierre del ciclo

Cinco días, un solo sistema:

| Día | Se agregó | La decisión que perduró |
|---|---|---|
| 1 | API, dominio, pruebas | La abstracción de proveedores |
| 2 | Conocimiento documental | 384 dimensiones para los tres proveedores |
| 3 | Agente con herramientas | La seguridad en el código, no en el prompt |
| 4 | Orquestación | Idempotencia y bitácora |
| 5 | Inferencia local | Ninguna reescritura fue necesaria |

La lección del ciclo no es cómo usar una herramienta de inteligencia artificial. Es cómo
integrarla **de modo que pueda ser reemplazada**. En un campo donde los modelos cambian
cada pocos meses, esa es la única decisión de arquitectura que no caduca.
