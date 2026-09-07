# Construir el sistema desde cero, en cinco días

Cinco textos para copiar y pegar en un agente de programación. Cada uno construye el módulo
de una sesión sobre lo que dejó el anterior.

No hace falta clonar nada: se empieza con una carpeta vacía.

**Cómo usarlos.** Abra el agente en una carpeta vacía, pegue el prompt del día 1 y deje que
trabaje. Al día siguiente, en la misma carpeta, pegue el del día 2. Y así.

Cada prompt exige que el agente **deje las pruebas en verde y el sistema levantándose**
antes de darse por terminado. Si un día queda a medias, el siguiente no va a funcionar.

> **Qué esperar.** Un agente competente produce un sistema equivalente en arquitectura y
> comportamiento, no idéntico carácter por carácter. Los nombres de archivo, el orden de los
> métodos y la redacción de los comentarios variarán. Lo que no debe variar son las
> decisiones de diseño: están escritas de forma explícita en cada prompt porque son
> justamente lo que se enseña.

---

## Día 1 — API, dominio y pruebas

```
Vamos a construir un sistema durante cinco sesiones. Hoy es la primera. Trabajá en la
carpeta actual, que está vacía.

EL PRODUCTO
Una "Mesa de Ayuda Institucional" para una universidad pública: registra y da seguimiento
a las solicitudes que presenta la comunidad universitaria. Los días siguientes le van a
agregar consulta documental, un agente, automatización y modelos locales.

STACK, SIN NEGOCIAR
- API: .NET 8 con Minimal APIs, EF Core, PostgreSQL, FluentValidation, Swashbuckle, xUnit.
- Interfaz: Vue 3 con PrimeVue 4 y Vite, en una carpeta cliente/ aparte.
- Todo se levanta con docker compose.

ESTRUCTURA
Organizá el código así, y respetá la dirección de las dependencias: Módulos → Dominio →
Núcleo. El dominio no conoce HTTP ni la base de datos, y ningún módulo conoce a otro.

  src/MesaAyuda.Api/
    Nucleo/          configuración, datos, errores, proveedores
    Dominio/         entidades y reglas de negocio
    Modulos/         un módulo por capacidad del sistema
    Extensiones/     registro de dependencias, agrupado por área
  tests/             pruebas de la API
  cliente/           interfaz web

QUÉ CONSTRUIR
1. Entidad Solicitud con: código correlativo legible (SOL-2026-000001), título,
   descripción, datos del solicitante, unidad responsable, categoría, prioridad, estado,
   y una colección de comentarios que pueden ser públicos o internos.
2. Enumerados: estado (Recibida, EnRevision, EnProceso, Resuelta, Cerrada, Rechazada),
   categoría (Academica, Administrativa, Tecnologica, Financiera, Infraestructura, Otra) y
   prioridad (Baja, Media, Alta, Critica).
3. Una máquina de estados que defina qué transiciones son válidas. Cerrada y Rechazada son
   finales. Desde cualquier estado no final se puede rechazar.
4. Endpoints: crear, listar con filtros y paginación, ver detalle, cambiar estado,
   reasignar, comentar, listar comentarios, un resumen de indicadores operativos y un
   endpoint de salud.
5. Validación por campo con mensajes claros. Todos los errores en formato ProblemDetails.
6. Documentación interactiva con Swagger.
7. Datos de ejemplo: ocho solicitudes al arrancar, con antigüedades distintas para que los
   indicadores de rezago tengan valores representativos.

LA DECISIÓN MÁS IMPORTANTE DE HOY
Definí una interfaz propia para hablar con modelos de lenguaje:

  public interface IProveedorLenguaje
  {
      string Nombre { get; }
      Task<RespuestaLenguaje> CompletarAsync(PeticionLenguaje peticion, CancellationToken ct = default);
  }

Y dos implementaciones: una llamada "simulado", que clasifica con reglas de palabras clave,
sin red ni credenciales y de forma determinista; y otra llamada "nube", que consulta un
servicio HTTP compatible con el formato de la API de OpenAI. Se elige entre ellas con una
variable de entorno. El valor por defecto es "simulado".

Usala en un solo lugar hoy: al registrar una solicitud, para sugerir su categoría.

Esta abstracción existe desde hoy aunque parezca prematura. En la sesión 5 se agrega una
tercera implementación para modelos locales y el sistema entero debe migrar cambiando una
variable de entorno, sin tocar los módulos que se escriban en el medio.

DECISIONES QUE DEBÉS RESPETAR
- La sugerencia del clasificador NUNCA reemplaza lo que declara el solicitante. Si indicó
  categoría, esa manda. La sugerencia se guarda aparte, junto con su nivel de confianza y
  el nombre del componente que la produjo, para poder auditarla después.
- Si el clasificador falla o tarda, la solicitud se registra igual, con categoría Otra y
  confianza cero. Un trámite no puede perderse porque un modelo no respondió.
- Las reglas del ciclo de vida viven dentro de la entidad Solicitud, no en los endpoints.
  Debe ser imposible dejarla en un estado inconsistente desde afuera.
- La respuesta de cada solicitud incluye las transiciones permitidas desde su estado
  actual. La interfaz arma sus botones con eso, no con una lista propia.
- Los identificadores los genera el dominio, no la base de datos. Declaralo explícitamente
  en la configuración de EF Core.
- La prioridad se guarda como número, no como texto: el listado ordena por urgencia y un
  orden alfabético pondría "Alta" antes que "Critica".
- Un cuerpo JSON mal formado debe devolver 400, no 500.

LA INTERFAZ WEB
En cliente/, con Vue 3 y PrimeVue. Un solo preset define la identidad visual (una escala
azul sobria, legible en proyector); ninguna vista declara colores propios. Una sola capa
de acceso a la API traduce los ProblemDetails a errores con título, detalle y errores por
campo ya extraídos.

Dos vistas: un tablero de indicadores y un listado de solicitudes con filtros, detalle,
expediente de comentarios y cambio de estado.

La interfaz no duplica reglas del servidor: ni la máquina de estados ni las validaciones.
Los botones de transición salen de lo que devuelve la API; los errores del formulario, del
validador del servidor.

Vite compila a wwwroot y ASP.NET sirve los estáticos con fallback de SPA, cuidando que las
rutas de la API y de Swagger sigan funcionando. El Dockerfile compila la interfaz en una
etapa propia con Node, de modo que quien solo ejecute docker compose up no necesite tener
Node instalado.

PRUEBAS
Deben correr sin credenciales, sin Internet y sin Docker. El proveedor simulado es
determinista justamente para eso. Cubrí la máquina de estados, la interpretación de la
respuesta del clasificador, la validación, y los endpoints con una base en memoria.
En el cliente, la capa de API y el comportamiento del detalle de una solicitud.

ENTREGABLES
- docker-compose.yml con la API y PostgreSQL.
- .env.ejemplo con los valores de configuración y sin credenciales reales.
- .gitignore que excluya bin, obj, node_modules, wwwroot y .env.
- .gitattributes que normalice los finales de línea a LF, con excepciones para .ps1 y .sln.
- tareas.ps1 con: restaurar, construir, probar, ejecutar, interfaz, levantar, bajar,
  reiniciar, registros, migracion y limpiar.
- README.md que explique qué se construyó y por qué, no solo cómo usarlo.
- EJERCICIOS.md con ejercicios graduados y preguntas de discusión.
- CONVENCIONES.md con las reglas de este proyecto, para que las sesiones siguientes lo lean
  antes de empezar.

REGLAS PERMANENTES DEL PROYECTO
Escribilas en CONVENCIONES.md, porque van a regir los cinco días:
- Todo en español: código, nombres, comentarios y documentación.
- Los comentarios explican POR QUÉ, no QUÉ. Si un comentario repite lo que dice el código,
  sobra. Comentá las decisiones, los riesgos y lo que sorprendería a quien lea después.
- Nada en el repositorio debe mencionar herramientas de inteligencia artificial ni asistentes
  de programación: ni en el código, ni en los comentarios, ni en la documentación, ni en los
  mensajes de commit. Es material didáctico de autoría propia.
- Ningún secreto versionado. Las credenciales van en .env, que no se sube.
- Cada día debe quedar compilando, con las pruebas en verde y levantándose con un comando.

ANTES DE DARTE POR TERMINADO
Verificá de verdad, no supongas:
1. dotnet test en verde.
2. npm test en verde.
3. docker compose up --build levanta el sistema.
4. La interfaz responde en http://localhost:8080, Swagger en /swagger, y la API devuelve
   las solicitudes de ejemplo.
5. Una transición inválida devuelve 409 y un cuerpo mal formado devuelve 400.
Contame qué verificaste y con qué resultado. Si algo no funciona, decilo; no lo tapes.
```

---

## Día 2 — Conocimiento documental (RAG)

```
Continuamos el proyecto de la sesión anterior. Antes de escribir nada, leé CONVENCIONES.md
y el README.md para entender qué existe y con qué criterios está hecho.

Hoy la mesa de ayuda pasa a responder preguntas sobre el reglamento institucional, citando
la fuente de cada afirmación.

EL PROBLEMA QUE RESOLVEMOS
Un modelo de lenguaje no conoce el reglamento de la universidad. Si se le pregunta por un
plazo, responde algo verosímil e inventado. La solución no es reentrenarlo: es darle el
texto correcto en el momento de responder y exigirle que se limite a él.

QUÉ CONSTRUIR
1. Una segunda abstracción, IProveedorEmbeddings, con las mismas dos implementaciones que
   la de lenguaje: "simulado" (vectores locales deterministas, por bolsa de palabras
   proyectada con una función de dispersión estable) y "nube".
2. Entidades Documento y FragmentoDocumento. El documento guarda una huella de su contenido
   y con qué modelo fue indexado.
3. Un fragmentador que divida el texto respetando su estructura: primero párrafos, luego
   oraciones, y solo como último recurso a mitad de una. Con solapamiento entre fragmentos
   contiguos, y que arrastre la referencia del artículo ("Artículo 14") para poder citarla.
4. Lectura de PDF, TXT y MD.
5. Búsqueda por similitud sobre PostgreSQL con la extensión pgvector.
6. Un servicio de consultas y sus endpoints: consultar, indexar un documento, listar y
   eliminar.
7. Un reglamento de ejemplo en documentos/, que se indexe solo al arrancar.

EL ORDEN DE LOS PASOS IMPORTA
  1. La pregunta se convierte a vector con el MISMO modelo con que se indexó.
  2. Se recuperan los fragmentos más cercanos.
  3. Se descartan los que no superen una similitud mínima.
  4. Si no queda ninguno, se responde "no encontré información" y NO se llama al modelo.
  5. Si quedan, se arman numerados como contexto y el modelo responde solo con eso.

El paso 4 es el que evita la mayoría de las respuestas inventadas. Un fragmento apenas
relacionado es peor que ningún fragmento: induce al modelo a construir una respuesta sobre
material que no viene al caso.

DECISIONES QUE DEBÉS RESPETAR
- Los tres proveedores del ciclo deben producir vectores de 384 dimensiones. El simulado
  por construcción; al servicio en la nube se le solicita ese tamaño explícitamente. Así la
  columna vectorial se declara una sola vez y cambiar de proveedor obliga a reindexar pero
  nunca a migrar el esquema. En la sesión 5 se va a agradecer.
- Cada respuesta devuelve las fuentes que la respaldan: documento, artículo, un extracto
  del fragmento y su similitud. Devolver solo el nombre del documento no permite verificar
  la cita.
- Los documentos tienen nivel de acceso. El filtro se aplica DENTRO de la búsqueda, antes
  de que el fragmento llegue al modelo. Filtrar después es inútil: el contenido ya salió.
- Un documento ya indexado y sin cambios no se reprocesa. Para eso está la huella.
- Si cambia el proveedor de embeddings, los vectores guardados dejan de ser comparables. El
  documento recuerda con qué modelo fue indexado y se reprocesa solo.
- El mapeo al tipo vectorial es específico de PostgreSQL: declaralo condicionalmente, para
  que las pruebas puedan correr sobre un almacén en memoria.
- La búsqueda por similitud debe resolverse dentro del motor de base de datos, no trayendo
  todos los fragmentos a memoria. Dejá también una implementación equivalente en memoria
  detrás de la misma interfaz, para las pruebas y para poder ejecutar sin la extensión.

LA IMAGEN DE POSTGRES CAMBIA
De postgres:16 a pgvector/pgvector:pg16. Y revisá que el .dockerignore no excluya los
documentos que hay que indexar.

EL PROVEEDOR SIMULADO TAMBIÉN CAMBIA
Ahora tiene que saber responder consultas documentales, no solo clasificar. Que extraiga
del contexto la oración que más términos comparte con la pregunta y la devuelva citando su
fragmento. No redacta ni razona: extrae. Ese contraste con un modelo real es lo que se
quiere mostrar en clase.

LA INTERFAZ
Una vista nueva: la consulta documental, mostrando la respuesta junto a cada fuente, su
artículo y su similitud, con el color de la etiqueta indicando cuánto respalda la
afirmación. Cuando no hay respaldo, se muestra como advertencia y no como error: responder
"no encontré información" es el comportamiento correcto, no una falla.

Agregá también la carga y el listado de documentos indexados.

ANTES DE DARTE POR TERMINADO
1. Las pruebas de la API y del cliente en verde, incluidas las del día 1.
2. El sistema levanta con docker compose y el reglamento queda indexado solo.
3. Una pregunta cuya respuesta está en el reglamento devuelve el artículo correcto citado.
4. Una pregunta ajena al reglamento responde que no hay información, sin inventar.
5. Un documento marcado como interno no aparece en una consulta pública.
Contame qué verificaste y con qué resultado.
```

---

## Día 3 — Agente con herramientas

```
Continuamos el proyecto. Leé CONVENCIONES.md y los README de las sesiones anteriores antes
de empezar.

Hasta ahora el sistema siempre hacía lo mismo ante una consulta. Hoy aprende a DECIDIR qué
hacer: consultar el reglamento, buscar en la base de solicitudes, calcular estadísticas o
abrir un expediente concreto, según lo que se le pregunte.

QUÉ CONSTRUIR
1. Un contrato IHerramientaAgente con: nombre, descripción, nivel de riesgo (Lectura o
   Escritura) y un esquema JSON de sus parámetros.
2. Cinco herramientas: buscar solicitudes con filtros acotados, ver el detalle de una por
   su código, obtener los indicadores, consultar el reglamento reutilizando el módulo del
   día 2, y cambiar el estado de una solicitud.
3. El ciclo de razonamiento: se le presenta al modelo la pregunta y el catálogo; responde
   eligiendo una herramienta o dando la respuesta final; si eligió una, se ejecuta y su
   resultado vuelve a la conversación; y se repite.
4. Endpoints: consultar al agente, listar las herramientas disponibles y generar un reporte
   ejecutivo.

LOS TRES LÍMITES, Y LOS TRES EN CÓDIGO
Un agente sin límites es un riesgo operativo, no una funcionalidad.

1. Conjunto cerrado de herramientas. El agente no escribe SQL ni llama a servicios
   arbitrarios: solo puede invocar lo declarado, y cada herramienta valida sus propios
   parámetros. Si pide una que no existe, se le informa el error y se le da otra vuelta.
2. Máximo de iteraciones y tiempo límite por herramienta. Cada iteración es una llamada al
   modelo: ese número acota el costo y la latencia máxima. Si se agota, el agente lo declara
   en lugar de fingir que concluyó.
3. Aprobación humana obligatoria para toda herramienta que modifique datos.

ESTO ES LO MÁS IMPORTANTE DE LA SESIÓN
La barrera de seguridad tiene que estar en el código, no en el texto que se le da al modelo.

Para demostrarlo, hacé que el proveedor simulado PROPONGA la acción de escritura aunque las
instrucciones le digan que no está autorizada. Es intencional: reproduce lo que hace un
modelo real cuando ignora una instrucción. El agente debe retenerla igual y devolverla como
"acción pendiente", con la herramienta y los argumentos que se propusieron.

Y aun con autorización concedida, la máquina de estados del día 1 sigue mandando: pedir que
una solicitud Recibida salte a Cerrada debe fallar. Tres capas independientes, ninguna
confía en la anterior.

TRAZABILIDAD
Toda respuesta incluye la traza: qué herramientas se ejecutaron, en qué orden, con qué
argumentos, si tuvieron éxito y cuánto tardaron. Más las acciones retenidas y el consumo de
tokens. Sin esa traza el agente es una caja negra, y una decisión automatizada que no se
puede auditar no es admisible en un entorno institucional.

INTERPRETAR LA DECISIÓN DEL MODELO
Es el punto más frágil de todo agente: depende de que respete un formato. Sé tolerante con
la forma y estricto con el contenido. Aceptá el JSON envuelto en texto o en un bloque de
código. Y tené en cuenta que los modelos pequeños rotulan mal los campos: si viene el nombre
de una herramienta donde debería ir la acción, interpretalo igual. Si no se puede
interpretar nada, devolvé un mensaje legible; nunca le muestres al usuario el JSON crudo con
la mecánica interna del agente.

EL REPORTE EJECUTIVO SE CONSTRUYE DISTINTO, A PROPÓSITO
Ahí NO hay elección de herramientas: los indicadores se calculan siempre igual y el modelo
solo redacta su lectura. Un reporte que la dirección va a leer no debe variar según lo que
el modelo decida consultar ese día. Devolvé los datos duros junto al texto, para que cada
afirmación pueda contrastarse.

El criterio general: agente cuando la pregunta es abierta y no se sabe de antemano qué se
necesita; flujo fijo cuando el resultado debe ser reproducible.

LA INTERFAZ
Una consola del agente: la pregunta, un interruptor para autorizar acciones que modifican
datos, la respuesta, la traza paso a paso y las acciones retenidas destacadas. Es lo que el
sistema decidió NO hacer, y esa decisión importa tanto como la respuesta. Agregá también el
catálogo de herramientas con su nivel de riesgo.

ANTES DE DARTE POR TERMINADO
1. Todas las pruebas en verde.
2. El agente elige la herramienta correcta para preguntas de distinto tipo.
3. Sin autorización, pedirle un cambio de estado deja una acción pendiente y la solicitud
   NO cambia. Verificá el estado real en la base, no solo lo que respondió.
4. Con autorización, la misma consulta sí lo ejecuta.
5. Con autorización, una transición que el dominio prohíbe falla igual.
Contame qué verificaste y con qué resultado.
```

---

## Día 4 — Orquestación con n8n

```
Continuamos el proyecto. Leé CONVENCIONES.md y los README anteriores.

Hasta ahora todo ocurría porque alguien llamaba a la API. Hoy el sistema reacciona solo: un
formulario dispara un flujo que registra la solicitud, la clasifica, consulta el reglamento,
responde por correo y deja todo asentado.

QUÉ CONSTRUIR
1. Un endpoint de entrada para solicitudes que llegan desde flujos externos. Recibe un
   contrato más simple que el interno: quien completa un formulario no conoce categorías ni
   unidades responsables. Eso lo resuelve el sistema.
2. Una entidad de bitácora que registre cada evento: lo que llegó, quién lo envió, qué se
   decidió, cuánto tardó y cuántos intentos hubo.
3. Endpoints para consultar la bitácora y los indicadores de salud de la automatización.
4. Servicios de n8n y de un servidor de correo de prueba (Mailpit) en el compose.
5. Un flujo de n8n exportado como JSON, en flujos/, listo para importar.

LAS TRES PROPIEDADES QUE LO HACEN USABLE
Automatizar es fácil. Automatizar de forma que se pueda dejar corriendo sin vigilancia
permanente exige tres cosas, y ninguna es opcional.

1. IDEMPOTENCIA. Toda automatización reintenta: el nodo HTTP reintenta, la red se cae, el
   usuario hace doble clic. Calculá una huella del cuerpo recibido y, si ya se procesó un
   evento idéntico, devolvé el mismo resultado en lugar de crear una segunda solicitud. Y
   respondé 200, no un error: el flujo externo debe poder reintentar sin miedo.

2. AUTENTICIDAD. La dirección de un webhook es pública: sin verificación, cualquiera que la
   conozca puede registrar solicitudes a nombre de terceros. Usá una firma HMAC-SHA256 con
   un secreto compartido. Tres detalles que importan:
   - La firma cubre el cuerpo EXACTO recibido. Leé el texto en crudo: deserializar y volver
     a serializar produce un texto distinto y una firma distinta.
   - La comparación debe ser de tiempo constante. Comparar cadenas con el operador habitual
     se detiene en el primer carácter distinto, y esa diferencia permite deducir la firma
     byte a byte.
   - Alterar un solo carácter después de firmar debe invalidarla.

3. TRAZABILIDAD. Registrá también los intentos rechazados por firma inválida o datos
   incorrectos: son justamente los que interesa poder revisar. Y en los indicadores, que los
   duplicados NO castiguen la tasa de éxito: un reintento correcto no es una falla.

EL ACUSE DE RECIBO
Cuando el reglamento tenga información pertinente al caso, el acuse la incluye con sus
fuentes. Cuando no, se limita a confirmar la recepción sin afirmar nada sobre el fondo del
asunto.

Ese cuerpo lo construye la API, no el flujo de n8n. Podría hacerse al revés, pero: decidir
qué se le dice a un ciudadano es lógica de negocio y pertenece al sistema; es verificable
con pruebas automatizadas, y un texto armado con expresiones dentro de un nodo no lo es; y
si mañana se cambia de orquestador, el contenido no se pierde.

n8n hace lo que sabe hacer bien: conectar. Disparar, reintentar, ramificar, enviar, avisar.

EL FLUJO
Formulario → preparar datos → llamar a la API con reintentos → ramificar según haya respaldo
normativo o no → enviar el correo correspondiente → registrar el envío en el expediente.
Y una rama de error que avise al equipo de sistemas: un proceso automatizado que falla en
silencio equivale a un trámite perdido.

Cuidado con dos cosas al configurarlo: dentro de la red de Docker el nombre del servicio es
"api", no localhost; y n8n necesita una clave de cifrado propia, porque sin ella genera una
nueva en cada arranque y las credenciales guardadas dejan de poder descifrarse.

LA INTERFAZ
Una vista de automatización con los indicadores de salud y la bitácora completa, incluidos
los rechazados. La tasa de éxito pintada según su valor, para que una caída se note sin
tener que leer números. Y un aviso visible cuando la verificación de firma está desactivada.

ANTES DE DARTE POR TERMINADO
1. Todas las pruebas en verde.
2. Enviar TRES VECES el mismo cuerpo crea UNA sola solicitud. Contá las solicitudes en la
   base para confirmarlo, no te fíes de la respuesta.
3. Con la firma exigida, una petición sin firma devuelve 401 y queda registrada.
4. Un cuerpo alterado después de firmar se rechaza.
5. Los cuatro servicios levantan y n8n alcanza la API por la red interna.
Contame qué verificaste y con qué resultado.
```

---

## Día 5 — Modelos locales con Docker y Ollama

```
Última sesión. Leé CONVENCIONES.md y los README anteriores.

Hoy el sistema completo pasa a funcionar sin salir de la institución.

LA PRUEBA DE FUEGO
Si la abstracción del día 1 está bien hecha, hoy solo hay que agregar dos clases y dos
líneas de registro de dependencias. No deberías necesitar tocar el módulo de conocimiento,
ni el agente, ni la orquestación, ni el dominio, ni el esquema de la base de datos.

Si te encontrás modificando esos módulos, pará y contame qué te obligó a hacerlo: significa
que algo quedó acoplado y ese hallazgo es más valioso que el código.

QUÉ CONSTRUIR
1. Un proveedor de lenguaje que consulte un servicio Ollama por su API nativa.
2. Un proveedor de embeddings equivalente.
3. Un servicio de Ollama en el compose, con los modelos que hagan falta.

Ambos proveedores se seleccionan con la misma variable de entorno de siempre, con el valor
"local".

DOS DETALLES QUE PARECEN MENORES Y NO LO SON
- LA VENTANA DE CONTEXTO. El valor por defecto de Ollama es corto. Si no la fijás
  explícitamente, los fragmentos que entrega el módulo de conocimiento se truncan SIN
  AVISO: el modelo responde con la mitad del contexto y nadie se entera. Es el tipo de falla
  que no produce ningún error y que solo se detecta comparando respuestas.
- LA DIMENSIÓN DE LOS VECTORES. Verificá que el modelo produzca la dimensión que espera la
  base de datos y, si no coincide, detenete con un mensaje que diga exactamente qué hacer.
  Sin esa comprobación el error aparece al insertar, con un mensaje del motor difícil de
  relacionar con la causa. Elegí un modelo de embeddings que produzca 384 dimensiones, para
  no tener que migrar el esquema.

Y un tercero: un modelo local no está "siempre disponible" como un servicio en la nube.
Puede no estar levantado o no tener el modelo descargado. Traducí cada una de esas fallas a
un error que diga qué revisar, y usá un tiempo de espera generoso: la primera consulta debe
cargar el modelo en memoria y tarda bastante más que las siguientes.

LAS CINCO DECISIONES DE SEGURIDAD
1. OLLAMA NO PUBLICA PUERTOS. No tiene autenticación: cualquiera que alcance su puerto puede
   usar el modelo, descargar otros y consumir toda la memoria del equipo. Que solo la API lo
   alcance, por una red interna. Exponerlo "para probar" es el error más común al desplegar
   un modelo local.
2. DOS REDES, UNA SIN SALIDA A INTERNET. La base de datos y el servicio de modelos viven
   solo en la interna. Aunque alguien lograra ejecutar algo dentro de esos contenedores, no
   podría sacar datos hacia afuera.
   Ojo: esto implica que el servicio de modelos NO puede descargar modelos por sí mismo.
   Resolvelo con un contenedor aparte que sí tenga salida, que deje los archivos en el
   volumen compartido y termine.
3. LÍMITES DE MEMORIA Y CPU. Un modelo sin límite puede consumir todo el equipo. Acotá
   también cuántos modelos permanecen cargados y por cuánto tiempo.
4. SIN CLAVES POR DEFECTO. Las variables sensibles no deben tener valor de respaldo: que el
   compose se niegue a arrancar si faltan. Una clave por defecto que nadie cambia es una
   clave pública. Este día, además, la firma de webhooks viene activada y n8n exige
   autenticación.
5. La API sigue sin correr como root.

LAS PRUEBAS DEL PROVEEDOR LOCAL NO DEBEN NECESITAR OLLAMA
Sustituí el transporte HTTP y verificá que el adaptador construya la petición correcta,
interprete la respuesta y traduzca cada falla a un error comprensible. Que el modelo responda
bien depende del modelo; que el adaptador esté bien escrito, no.

LA INTERFAZ
Que la barra lateral informe de forma permanente qué proveedor está respondiendo y con
cuántas dimensiones. Durante la sesión se cambia de proveedor varias veces, y tenerlo a la
vista evita atribuirle a un modelo el comportamiento de otro.

CIERRE
Actualizá el README raíz con el recorrido completo de los cinco días y lo que cada uno
aportó. La conclusión del ciclo no es cómo usar una herramienta: es cómo integrarla de modo
que pueda ser reemplazada.

ANTES DE DARTE POR TERMINADO
1. Todas las pruebas en verde, incluidas las de los cuatro días anteriores.
2. El sistema completo levanta y descarga los modelos solo la primera vez.
3. Una consulta documental devuelve una respuesta correcta y citada, generada por el modelo
   local.
4. El agente elige una herramienta, la ejecuta y responde, con el modelo local.
5. El servicio de modelos NO está publicado hacia el equipo. Comprobalo.
6. Decime cuántos archivos tuviste que modificar de los módulos de los días 2, 3 y 4.
Contame qué verificaste y con qué resultado.
```

---

## Si algo sale distinto

Es normal que el agente proponga variantes. Lo que **no** debería ceder:

| Día | La decisión que no se negocia |
|---|---|
| 1 | La abstracción de proveedores existe desde el primer día, aunque parezca prematura |
| 2 | Sin fragmentos pertinentes no se llama al modelo; y todos los proveedores, 384 dimensiones |
| 3 | La aprobación humana se aplica en el código, no en el texto del prompt |
| 4 | Idempotencia por huella del contenido, y firma sobre el cuerpo exacto |
| 5 | Ollama sin puertos publicados y sin salida a Internet |

Si el agente omite alguna, pedísela explícitamente citando esta tabla. Son las cinco ideas
que sostienen el ciclo; el resto es implementación.
