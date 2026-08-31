# Ciclo de Charlas Especializadas en Inteligencia Artificial Aplicada a la Ingeniería de Software

Repositorio de código del ciclo de charlas dictado en la **Carrera de Ingeniería de
Sistemas de la Universidad Pública de El Alto**.

**Docente:** Msc. Lic. Alexander Bismark Benitez Antelo
**Gestión:** 2026 · Cinco sesiones de 90 minutos

---

## Un solo sistema, construido en cinco días

Este repositorio no contiene cinco ejemplos sueltos. Contiene **un mismo producto** al que
cada sesión le agrega un módulo: la **Mesa de Ayuda Institucional**, un sistema de registro
y atención de solicitudes de la comunidad universitaria.

| Día | Carpeta | Módulo que se agrega | Producto de la sesión |
|-----|---------|----------------------|-----------------------|
| 1 | [`dia-1-api-core/`](dia-1-api-core/) | API REST, dominio y pruebas | API funcional con validaciones, documentación y pruebas |
| 2 | [`dia-2-rag/`](dia-2-rag/) | Conocimiento documental | Asistente que responde sobre el reglamento citando la fuente |
| 3 | [`dia-3-agente/`](dia-3-agente/) | Agente con herramientas | Agente que consulta datos y genera un reporte ejecutivo |
| 4 | [`dia-4-orquestacion/`](dia-4-orquestacion/) | Automatización de procesos | Flujo que clasifica, consulta, responde por correo y registra |
| 5 | [`dia-5-modelos-locales/`](dia-5-modelos-locales/) | Inferencia local | Sistema completo corriendo con un modelo propio |

Cada carpeta contiene el **proyecto completo hasta ese día** y se ejecuta por sí sola. Quien
falte a una sesión puede incorporarse a la siguiente sin quedar atrás; y quien quiera ver
qué cambió entre un día y otro, puede comparar las dos carpetas.

---

## El hilo conductor

Desde el día 1 el sistema no habla directamente con ningún servicio de modelos. Habla con
una interfaz propia:

```csharp
public interface IProveedorLenguaje
{
    string Nombre { get; }
    Task<RespuestaLenguaje> CompletarAsync(PeticionLenguaje peticion, CancellationToken cancelacion = default);
}
```

Los módulos de los días 2, 3 y 4 se programan contra esa interfaz. El día 5 se agrega una
implementación nueva para modelos locales y **el sistema entero pasa a funcionar sin
servicios externos cambiando una variable de entorno**, sin modificar una línea de los
módulos anteriores.

Esa es la idea que sostiene el ciclo: no se trata de aprender a usar una herramienta, sino
de aprender a integrarla de modo que pueda ser reemplazada.

---

## Requisitos

| Herramienta | Versión | Se usa en |
|-------------|---------|-----------|
| .NET SDK | 8.0 o superior | Todos los días |
| Docker Desktop | Reciente | Todos los días |
| Git | Reciente | Todos los días |
| Visual Studio Code o Visual Studio | — | Todos los días |

Verificación:

```powershell
dotnet --version
docker --version
git --version
```

Todo lo demás (PostgreSQL, n8n, Ollama) se levanta en contenedores. No hace falta instalar
nada más en la máquina.

---

## Cómo empezar

```powershell
git clone git@github.com:alexanderbismark007/agentes-v0.git
cd agentes-v0\dia-1-api-core
.\tareas.ps1 levantar
```

Al terminar, la API queda disponible en http://localhost:8080/swagger.

Cada carpeta incluye su propio `README.md` con las instrucciones de la sesión y un archivo
`EJERCICIOS.md` con el trabajo propuesto.

---

## Sin credenciales y sin conexión

Los proyectos funcionan **sin claves de API y sin Internet**. El proveedor por defecto
(`simulado`) aplica reglas locales deterministas y permite ejecutar el sistema completo y
toda la batería de pruebas en cualquier máquina.

Las credenciales, cuando se usen, van en un archivo `.env` que **no se versiona**. Cada
proyecto incluye un `.env.ejemplo` como plantilla.

---

## Estado de cada proyecto

Cada carpeta compila, se levanta con un comando y trae su batería de pruebas en verde.

| Día | Pruebas | Servicios que levanta |
|-----|---------|-----------------------|
| 1 | 48 | API, PostgreSQL |
| 2 | 79 | API, PostgreSQL con pgvector |
| 3 | 107 | API, PostgreSQL con pgvector |
| 4 | 134 | API, PostgreSQL, n8n, correo de prueba |
| 5 | 150 | API, PostgreSQL, n8n, correo, servicio de modelos local |

Las pruebas corren **sin credenciales, sin Internet y sin Docker**: el proveedor `simulado`
resuelve localmente todo lo que en producción resolvería un modelo.

---

## Convenciones del repositorio

- El código, los comentarios y la documentación están en español.
- Cada proyecto se levanta con un solo comando: `.\tareas.ps1 levantar`.
- Cada proyecto trae pruebas automatizadas que corren sin dependencias externas.
- La regla de dependencia es siempre en una dirección: **Módulos → Dominio → Núcleo**.
- Ningún módulo conoce a otro módulo. Se comunican a través del núcleo.

---

## Estructura común de cada día

```
dia-N-nombre/
├── README.md              Guía de la sesión
├── EJERCICIOS.md          Trabajo propuesto
├── tareas.ps1             Comandos frecuentes del proyecto
├── docker-compose.yml     Servicios necesarios
├── .env.ejemplo           Plantilla de configuración
├── src/                   Código de la aplicación
└── tests/                 Pruebas automatizadas
```

---

## Licencia

MIT. Ver [LICENSE](LICENSE).
