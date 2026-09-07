# Cómo poner en marcha el proyecto

Dos caminos. El primero si usa un agente de programación (Claude Code, Cursor, Copilot,
Gemini CLI); el segundo si prefiere hacerlo a mano.

---

## Opción A — Con un agente

Copie el bloque completo y péguelo en su agente. **Cambie la línea del día** por la sesión
que corresponda.

```
Necesito que dejes corriendo en mi máquina un proyecto de clase y me confirmes que funciona.

REPOSITORIO
https://github.com/alexanderbismark007/agentes-v0

QUÉ ES
Un sistema de mesa de ayuda universitaria: API en .NET 8, interfaz web en Vue 3 y
PostgreSQL, todo en contenedores. El repositorio tiene cinco carpetas, de
dia-1-api-core a dia-5-modelos-locales. Cada carpeta es el proyecto completo hasta
ese día y se ejecuta por sí sola; no dependen entre ellas.

QUÉ QUIERO LEVANTAR
dia-1-api-core

PASOS
1. Verifica que estén instalados y funcionando: Docker Desktop (debe estar abierto y
   con el motor corriendo), .NET SDK 8.0 o superior, y Git. Si falta alguno, dímelo
   con el enlace de descarga y detente ahí; no intentes instalarlo por tu cuenta.
2. Clona el repositorio por HTTPS en la carpeta donde estamos.
3. Entra a la carpeta del día que indiqué arriba.
4. Copia el archivo .env.ejemplo a .env.
   Si es el día 5: además, en .env reemplaza cada valor que diga "cambie..." por un
   valor propio cualquiera, y pon MODELO_LENGUAJE=qwen2.5:1.5b porque es más liviano.
5. Ejecuta: docker compose up --build -d
   La primera vez tarda varios minutos: compila la aplicación y descarga imágenes. El
   día 5 además descarga modelos de lenguaje, que pesan alrededor de 1 GB.
6. Espera a que todos los contenedores estén levantados y sanos antes de verificar.

CÓMO SÉ QUE FUNCIONA
Comprueba, en este orden, y dime el resultado de cada uno:
- http://localhost:8080 devuelve la interfaz web
- http://localhost:8080/salud responde un JSON con estado "activo"
- http://localhost:8080/swagger abre la documentación de la API
- http://localhost:8080/api/v1/solicitudes devuelve un listado con datos de ejemplo
En los días 4 y 5, además: http://localhost:5678 (n8n) y http://localhost:8025 (correo).

REGLAS
- No modifiques el código fuente del proyecto. Si algo falla, diagnostica la causa y
  explícamela; no lo "arregles" por tu cuenta ni cambies archivos del repositorio.
- No cambies los puertos salvo que estén ocupados. Si tienes que cambiarlos, avísame
  cuáles usaste.
- Si un comando tarda, espera; no lo canceles ni lo reintentes en paralelo.
- Háblame en español y explícame lo que vas haciendo en términos simples.

AL TERMINAR
Dime las direcciones donde quedó corriendo y el comando para detenerlo todo.
```

---

## Opción B — A mano

Requiere **Docker Desktop abierto**, **.NET SDK 8.0 o superior** y **Git**.

```powershell
git clone https://github.com/alexanderbismark007/agentes-v0.git
cd agentes-v0\dia-1-api-core

copy .env.ejemplo .env
.\tareas.ps1 levantar
```

La primera vez tarda varios minutos. Al terminar, abra **http://localhost:8080**.

Para detenerlo:

```powershell
.\tareas.ps1 bajar
```

Para ver todas las tareas disponibles:

```powershell
.\tareas.ps1
```

### Si va a levantar el día 5

Ese día exige claves propias: el sistema **se niega a arrancar** si el archivo `.env`
todavía tiene los valores de ejemplo. Es deliberado, y es uno de los temas de la sesión.

Abra `.env` y reemplace cada valor que diga `cambie...` por uno propio. Si su equipo tiene
poca memoria, cambie también:

```
MODELO_LENGUAJE=qwen2.5:1.5b
```

---

## Problemas frecuentes

**`docker: command not found` o "Cannot connect to the Docker daemon".**
Docker Desktop no está abierto. Ábralo, espere a que el ícono deje de animarse y repita.

**"port is already allocated" o "puerto 8080 en uso".**
Otro programa está usando ese puerto. Edite `.env` y cambie `PUERTO_API=8080` por otro
número, por ejemplo `8090`. Luego use esa dirección en el navegador.

**El navegador muestra una página en blanco.**
La aplicación todavía está iniciando. Espere medio minuto y recargue. Si sigue igual:
`docker compose logs api`.

**El día 5 no arranca y dice "required variable ... is missing a value".**
Falta completar el archivo `.env`. Vea la sección anterior.

**La primera consulta del día 5 tarda muchísimo.**
Es normal: el modelo se carga en memoria la primera vez. Las siguientes son rápidas.

**Quiero empezar de cero.**
`.\tareas.ps1 reiniciar` borra los datos y vuelve a levantar.

---

## Qué mirar una vez que esté corriendo

| Vista | Desde | Qué observar |
|---|---|---|
| Tablero | Día 1 | Indicadores. Fíjese en las solicitudes rezagadas |
| Solicitudes | Día 1 | Abra una: la categoría sugerida, su confianza y quién la produjo |
| Conocimiento | Día 2 | Pregunte por un plazo: la respuesta cita el artículo y su similitud |
| Agente | Día 3 | La traza paso a paso, y las acciones retenidas sin autorización |
| Automatización | Día 4 | La bitácora, incluidos los intentos rechazados |

Cada carpeta tiene su propio `README.md` con la explicación de la sesión y un
`EJERCICIOS.md` con el trabajo propuesto.
