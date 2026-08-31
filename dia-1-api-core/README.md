# Día 1 — Ingeniería de software asistida por inteligencia artificial

Primer módulo del sistema **Mesa de Ayuda Institucional**. En esta sesión se construye el
núcleo del producto: una API REST que registra y da seguimiento a las solicitudes que
presenta la comunidad universitaria.

Todo lo que se agregue en los días siguientes se apoya sobre lo que se define aquí.

---

## Qué se construye

Una API REST con:

- Registro de solicitudes con **código correlativo** (`SOL-2026-000001`).
- **Validación por campo** con mensajes claros, devueltos en formato ProblemDetails.
- **Máquina de estados** que impide transiciones inválidas en el ciclo de vida.
- Expediente de comentarios, con separación entre comentarios públicos e internos.
- Listado con filtros, búsqueda por texto y paginación.
- Indicadores operativos para el tablero de la mesa de ayuda.
- **Documentación interactiva** generada automáticamente.
- **48 pruebas automatizadas** que cubren dominio, validación y endpoints.

Y una decisión de diseño que sostiene todo el ciclo: la **abstracción de proveedores**.

---

## La pieza que amarra los cinco días

El sistema nunca habla directamente con un servicio de modelos. Habla con una interfaz:

```csharp
public interface IProveedorLenguaje
{
    string Nombre { get; }
    Task<RespuestaLenguaje> CompletarAsync(PeticionLenguaje peticion, CancellationToken cancelacion = default);
}
```

Hoy existen dos implementaciones:

| Proveedor  | Qué hace                                                        | Cuándo usarlo                            |
|------------|-----------------------------------------------------------------|------------------------------------------|
| `simulado` | Reglas de palabras clave. Sin red, sin credenciales, determinista. | Valor por defecto, pruebas y clase.     |
| `nube`     | Consulta un servicio externo por HTTP.                          | Demostración con un modelo real.         |

Se cambia de uno a otro con **una variable de entorno**, sin tocar una línea de lógica:

```
PROVEEDOR_LENGUAJE=simulado
PROVEEDOR_LENGUAJE=nube
```

El día 5 se agrega una tercera implementación para modelos locales. Los módulos escritos
en los días 2, 3 y 4 seguirán funcionando sin modificación alguna. Ese es el motivo por el
que esta interfaz aparece desde el primer día.

---

## Requisitos

- .NET SDK 8.0 o superior
- Docker Desktop
- PostgreSQL, solo si se decide ejecutar sin contenedores

Verificación rápida:

```powershell
dotnet --version
docker --version
```

---

## Puesta en marcha

### Opción A — Con Docker (recomendada)

```powershell
.\tareas.ps1 levantar
```

Levanta la base de datos y la API, aplica las migraciones y carga ocho solicitudes de
ejemplo. Al terminar:

| Recurso        | Dirección                        |
|----------------|----------------------------------|
| API            | http://localhost:8080            |
| Documentación  | http://localhost:8080/swagger    |
| Estado         | http://localhost:8080/salud      |

Para detener:

```powershell
.\tareas.ps1 bajar
```

### Opción B — Sin Docker

Requiere PostgreSQL en `localhost:5432` con la base `mesa_ayuda`.

```powershell
.\tareas.ps1 ejecutar
```

### Pruebas

```powershell
.\tareas.ps1 probar
```

---

## Endpoints

| Método  | Ruta                                     | Descripción                                    |
|---------|------------------------------------------|------------------------------------------------|
| `POST`  | `/api/v1/solicitudes`                    | Registra una solicitud y la clasifica          |
| `GET`   | `/api/v1/solicitudes`                    | Lista con filtros y paginación                 |
| `GET`   | `/api/v1/solicitudes/{id}`               | Detalle de una solicitud                       |
| `PATCH` | `/api/v1/solicitudes/{id}/estado`        | Avanza el estado                               |
| `PATCH` | `/api/v1/solicitudes/{id}/asignacion`    | Cambia categoría, prioridad y unidad           |
| `POST`  | `/api/v1/solicitudes/{id}/comentarios`   | Agrega un comentario al expediente             |
| `GET`   | `/api/v1/solicitudes/{id}/comentarios`   | Lista comentarios                              |
| `GET`   | `/api/v1/estadisticas/resumen`           | Indicadores operativos                         |
| `GET`   | `/salud`                                 | Estado del servicio y proveedor activo         |

### Ejemplo

```bash
curl -X POST http://localhost:8080/api/v1/solicitudes \
  -H "Content-Type: application/json" \
  -d '{
    "titulo": "No puedo acceder al correo institucional",
    "descripcion": "Olvidé la contraseña de mi usuario y la recuperación no llega.",
    "solicitanteNombre": "Marcela Quispe",
    "solicitanteCorreo": "marcela.quispe@correo.upea.bo",
    "prioridad": 3
  }'
```

El clasificador detecta los términos *contraseña* y *usuario*, sugiere la categoría
`Tecnologica` y la solicitud se dirige a la Unidad de Sistemas. La respuesta conserva la
sugerencia, su nivel de confianza y **qué componente la produjo**:

```json
{
  "codigo": "SOL-2026-000009",
  "categoria": "Tecnologica",
  "categoriaSugerida": "Tecnologica",
  "confianzaSugerencia": 0.87,
  "origenSugerencia": "simulado",
  "estado": "Recibida",
  "transicionesPermitidas": ["EnRevision", "Rechazada"]
}
```

---

## Organización del código

```
src/MesaAyuda.Api/
├── Nucleo/                  Lo que no pertenece a ningún módulo en particular
│   ├── Configuracion/         Opciones enlazadas desde appsettings
│   ├── Datos/                 Contexto, mapeos, migraciones y datos de ejemplo
│   ├── Errores/               Excepciones de negocio y traducción a HTTP
│   └── Proveedores/           Abstracción de modelos y sus implementaciones
├── Dominio/                 Entidades y reglas de negocio
│   └── Solicitudes/           Solicitud, comentarios y máquina de estados
├── Modulos/                 Un módulo por capacidad del sistema
│   └── Solicitudes/           Casos de uso, validación y endpoints
└── Extensiones/             Registro de dependencias, agrupado por área
```

La regla de dependencia es en una sola dirección: **Módulos → Dominio → Núcleo**. El
dominio no conoce HTTP ni la base de datos; los módulos no se conocen entre sí.

---

## Ciclo de vida de una solicitud

```
                 ┌──────────────┐
                 │   Recibida   │
                 └──────┬───────┘
                        │
                 ┌──────▼───────┐
                 │  EnRevision  │
                 └──────┬───────┘
                        │
                 ┌──────▼───────┐
            ┌────┤  EnProceso   │
            │    └──────┬───────┘
            │           │
            │    ┌──────▼───────┐
            └───►│   Resuelta   │
                 └──────┬───────┘
                        │
                 ┌──────▼───────┐
                 │   Cerrada    │  ← estado final
                 └──────────────┘

Desde Recibida, EnRevision y EnProceso también se puede pasar a Rechazada (estado final).
```

Las transiciones viven en `MaquinaEstados`, no repartidas por los endpoints. Cualquier
intento inválido devuelve `409 Conflict` con una explicación de qué sí se puede hacer.

---

## Criterios de diseño aplicados

**La sugerencia nunca decide sola.** La categoría propuesta se guarda junto a la solicitud
con su nivel de confianza y el nombre del componente que la produjo, pero si el solicitante
declara una categoría, esa prevalece. Cualquier auditoría posterior puede reconstruir por
qué una solicitud terminó donde terminó.

**Una falla del clasificador no pierde la solicitud.** Si el proveedor no responde, se
registra la advertencia y la solicitud se guarda igual, con categoría `Otra` y confianza
cero. El servicio de fondo nunca depende de la disponibilidad del modelo.

**Las reglas viven en el dominio.** `Solicitud` no permite ser dejada en un estado
inconsistente, sin importar qué código la use.

**Las pruebas no necesitan Internet ni credenciales.** El proveedor `simulado` es
determinista, y por eso las 48 pruebas corren en segundos en cualquier máquina.

---

## Solución de problemas

**El puerto 8080 está ocupado.** Cambie `PUERTO_API` en el archivo `.env`.

**La API no conecta con la base de datos.** El contenedor de la API espera a que
PostgreSQL declare estar sano. Si persiste: `.\tareas.ps1 reiniciar`.

**Quiero empezar con la base vacía.** `.\tareas.ps1 reiniciar` borra el volumen de datos.

**Cambié a `nube` y obtengo 503.** Falta la clave de API. Defina `NUBE_CLAVE_API` en el
archivo `.env`, o vuelva a `PROVEEDOR_LENGUAJE=simulado`.

---

## Ejercicios

Ver [EJERCICIOS.md](EJERCICIOS.md).

---

## Continúa en

**Día 2 — Arquitecturas RAG.** Se agrega el módulo de conocimiento: la mesa de ayuda pasa
a responder consultas sobre el reglamento institucional citando la fuente de cada
afirmación.
