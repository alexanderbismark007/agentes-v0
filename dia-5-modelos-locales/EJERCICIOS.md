# Ejercicios — Día 5

Antes de empezar:

```powershell
copy .env.ejemplo .env    # y edite las claves
.\tareas.ps1 probar
.\tareas.ps1 levantar
```

---

## 1. Comprobar que el aislamiento funciona

1. Confirme que Ollama **no** publica puertos:

```powershell
docker compose ps
```

Debe verse `11434/tcp` **sin** el prefijo `0.0.0.0:`.

2. Verifique que la red interna no tiene salida:

```powershell
docker compose exec modelos sh -c "wget -q -O- https://example.com || echo SIN SALIDA A INTERNET"
```

3. Confirme que la API sí lo alcanza:

```powershell
docker compose exec api sh -c "wget -q -O- http://modelos:11434/api/tags"
```

> **Para discutir:** Ollama no tiene autenticación. Si hubiera publicado el puerto 11434,
> ¿qué podría hacer alguien en la misma red del equipo?

---

## 2. Medir el costo del aislamiento

El servicio de modelos no tiene salida a Internet, por lo que no puede descargar modelos
por sí mismo: lo hace un contenedor aparte que sí la tiene.

1. Elimine el volumen: `docker compose down -v`.
2. Levante de nuevo y observe la secuencia en los registros.
3. Ahora intente descargar un modelo desde el servicio aislado:

```powershell
docker compose exec modelos ollama pull llama3.2:1b
```

> **Para discutir:** falla, y debe fallar. ¿Qué se gana con esa restricción y qué se
> complica? ¿Cómo se descargaría un modelo nuevo en un servidor institucional real?

---

## 3. Comparar los tres proveedores sobre las mismas preguntas

Elija cinco preguntas y ejecútelas con `simulado`, `local` y, si tiene clave, `nube`.
Recuerde reindexar al cambiar el proveedor de embeddings.

| Pregunta | `simulado` | `local` | `nube` | Mejor similitud | Mejor respuesta |
|---|---|---|---|---|---|

Registre además latencia y tokens, que la API devuelve en cada respuesta.

> **Para discutir:** con el reglamento del ejemplo, la similitud sube notoriamente del
> proveedor simulado al local. ¿Por qué? ¿Qué captura un modelo de embeddings real que unas
> reglas de palabras clave no pueden capturar?

---

## 4. Encontrar dónde falla el modelo pequeño

Un modelo local pequeño sigue el formato de respuesta con menos rigor que uno grande. El
intérprete del agente ya absorbe varias de esas variantes, pero no todas.

1. Consulte al agente diez preguntas distintas.
2. Anote en cuántas eligió bien la herramienta y en cuántas devolvió algo inesperado.
3. Revise `Decision.Interpretar` y agregue tolerancia para el caso que haya encontrado.
4. Escriba la prueba correspondiente **con la salida real** que observó.

> Ese es exactamente el origen de las pruebas que hoy están en
> `DecisionModelosLocalesPruebas`: salidas reales de un modelo de 1.5B que rompían el
> agente.

---

## 5. Comprobar el efecto de la ventana de contexto

1. Baje `VENTANA_CONTEXTO` a `512` y reinicie.
2. Haga una consulta documental que recupere cinco fragmentos.
3. Compare la respuesta con la obtenida usando `4096`.

> **Para discutir:** con la ventana corta no aparece ningún error. La respuesta simplemente
> empeora, porque el contexto se truncó en silencio. ¿Cómo detectaría este problema en un
> sistema en producción? ¿Qué métrica lo delataría?

---

## 6. Dimensionar el servidor institucional

Con los datos que recoja del sistema en funcionamiento, estime qué haría falta para
atender a 500 estudiantes con un promedio de 200 consultas diarias.

1. Mida la latencia por consulta con `docker stats` abierto.
2. Calcule consultas por hora en el pico y cuántas concurrentes implica.
3. Determine memoria, CPU y almacenamiento necesarios.
4. Compare ese costo contra el de resolver lo mismo con un proveedor en la nube.

> **Para discutir:** ¿en qué volumen de consultas se cruzan las dos curvas de costo? ¿Cambia
> la conclusión si los datos no pueden salir de la institución por norma?

---

## 7. Repartir la carga entre proveedores

No todas las tareas necesitan el mismo modelo. Clasificar una solicitud es más simple que
redactar un reporte ejecutivo.

Diseñe un proveedor compuesto que elija la implementación según la tarea, y que use el
proveedor local como respaldo cuando el de nube no esté disponible.

1. ¿Dónde se implementaría, para no tocar ningún módulo?
2. ¿Cómo decidiría qué tarea va a cada proveedor?
3. ¿Qué pasa con los embeddings si la selección cambia entre consultas?

> **Pista para la primera:** ya existe el lugar exacto. Es el mismo archivo que se tocó hoy
> para agregar el proveedor local.

---

## Preguntas de cierre del ciclo

1. Agregar el proveedor local no obligó a tocar los módulos de los días 2, 3 y 4. ¿Qué
   decisión del día 1 lo hizo posible? ¿Qué habría costado tomarla el día 5 en su lugar?
2. Los tres proveedores producen vectores de 384 dimensiones. ¿Fue casualidad? ¿Qué habría
   pasado al migrar si el modelo local produjera 768?
3. El compose se niega a arrancar si faltan ciertas variables. ¿Por qué es preferible a
   tener valores por defecto?
4. ¿Qué información de la mesa de ayuda **no** debería salir nunca de la institución? ¿El
   sistema hoy lo garantiza?
5. Si mañana aparece un modelo mejor, ¿qué habría que cambiar en este sistema para
   adoptarlo?
6. De todo lo construido en cinco días, ¿qué parte seguirá siendo válida dentro de tres
   años, y qué parte habrá quedado obsoleta?
