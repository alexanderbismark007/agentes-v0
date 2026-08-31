# Ejercicios — Día 2

Antes de empezar, deje la batería de pruebas en verde:

```powershell
.\tareas.ps1 probar
```

---

## 1. Medir el efecto del tamaño de fragmento

El parámetro que más afecta la calidad de las respuestas es el tamaño de corte.

1. Elija cinco preguntas cuya respuesta esté en el reglamento.
2. Ejecute el sistema con `TamanoFragmento` en 300, 900 y 2000, reindexando cada vez
   (`.\tareas.ps1 reiniciar`).
3. Complete el cuadro:

   | Pregunta | Similitud con 300 | Similitud con 900 | Similitud con 2000 | ¿Cuál respondió mejor? |
   |----------|-------------------|-------------------|--------------------|------------------------|

> **Para discutir:** ¿por qué un fragmento más grande no siempre da mejor resultado?

---

## 2. Encontrar el umbral que rompe el sistema

`SimilitudMinima` decide cuándo el sistema admite no saber.

1. Bájelo a `0.01` y pregunte algo que no esté en el reglamento.
2. Súbalo a `0.9` y pregunte algo que sí esté.
3. Anote qué falla en cada caso.

> **Para discutir:** ¿qué error es más grave en una mesa de ayuda institucional: responder
> "no sé" cuando la información existía, o responder algo incorrecto con aparente
> seguridad? La respuesta define hacia qué lado conviene equivocarse al elegir el umbral.

---

## 3. Corregir la referencia de los fragmentos a caballo

Un fragmento que arranca con el solapamiento del artículo anterior queda etiquetado con un
solo artículo, aunque su texto provenga de dos. Compruébelo consultando algo del final de
un artículo y observando la referencia devuelta.

1. Modifique `Fragmentador` para que un fragmento pueda declarar más de una referencia, o
   para que la referencia se determine por el contenido dominante y no por el inicio.
2. Ajuste las pruebas de `FragmentadorPruebas`.

> **Para discutir:** ¿cuánta precisión en la cita justifica cuánta complejidad añadida?

---

## 4. Agregar un documento propio

1. Coloque un PDF institucional real en la carpeta `documentos/`.
2. Levante el sistema y verifique cuántos fragmentos generó.
3. Formule cinco preguntas y evalúe las respuestas.

> Si genera cero fragmentos, es un PDF escaneado sin capa de texto. Ese hallazgo es en sí
> mismo parte del ejercicio: no todo documento institucional es indexable tal como está.

---

## 5. Comparar el proveedor simulado contra embeddings reales

El proveedor `simulado` compara vocabulario, no significado. Un modelo real captura
sinónimos.

1. Configure `PROVEEDOR_EMBEDDINGS=nube` con su clave y reindexe.
2. Pregunte usando palabras que **no** aparecen en el reglamento. Por ejemplo, "¿cuánto
   demora un papel de calificaciones?" en lugar de "certificado de notas".
3. Compare la similitud obtenida con cada proveedor.

> **Para discutir:** ¿en qué preguntas el modelo real marca una diferencia clara? ¿Cuánto
> vale esa diferencia frente a su costo y su latencia?

---

## 6. Registrar cada consulta para auditoría

Hoy las consultas no dejan rastro. Se pide guardar cada una con su pregunta, los fragmentos
recuperados, la respuesta y la similitud obtenida.

1. Cree la entidad `ConsultaRegistrada` y su configuración.
2. Regístrela desde `ServicioConsultas`.
3. Exponga `GET /api/v1/conocimiento/consultas/historial`.

> **Para discutir:** ¿qué pregunta institucional se puede responder con ese historial que
> hoy no se puede? Por ejemplo: ¿qué consultan los estudiantes y el reglamento no cubre?

---

## 7. Reindexación explícita

Cambiar de proveedor de embeddings invalida los vectores guardados. Hoy eso se resuelve
documento por documento.

Implemente `POST /api/v1/conocimiento/reindexar`, que reprocese todos los documentos con el
proveedor activo e informe cuántos se actualizaron.

---

## Preguntas de cierre

1. ¿Por qué el sistema no llama al modelo cuando no recupera fragmentos pertinentes? ¿Qué
   ganaría y qué perdería si lo llamara igual?
2. Un documento marcado como `Interno` no aparece en consultas públicas. ¿Por qué el filtro
   debe aplicarse dentro de la búsqueda y no sobre la respuesta ya generada?
3. Si cambia el proveedor de embeddings y **no** reindexa, ¿qué respuestas obtendría y por
   qué?
4. ¿Qué pasaría si dos documentos se contradicen entre sí? ¿Cómo lo detectaría el sistema
   actual? ¿Cómo debería comportarse?
5. El sistema devuelve la similitud de cada fuente. ¿Qué decisión operativa tomaría usted
   con una respuesta respaldada por una similitud de 0.18?
