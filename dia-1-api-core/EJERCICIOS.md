# Ejercicios — Día 1

Los ejercicios están ordenados por dificultad. Los tres primeros se resuelven durante la
sesión; los últimos quedan como trabajo autónomo.

Antes de empezar, deje la batería de pruebas en verde:

```powershell
.\tareas.ps1 probar
```

---

## 1. Agregar una categoría nueva

La mesa de ayuda necesita atender casos de **Biblioteca**.

1. Agregue el valor a `CategoriaSolicitud`.
2. Registre la unidad responsable en `ServicioSolicitudes.UnidadPorCategoria`.
3. Agregue las palabras clave en `ProveedorSimulado.SenalesPorCategoria`.
4. Escriba una prueba que verifique que un texto sobre préstamo de libros se clasifica
   en la categoría nueva.

> **Para discutir:** ¿en cuántos archivos hubo que tocar? ¿Es aceptable, o el diseño
> debería permitir agregar una categoría sin recompilar?

---

## 2. Reabrir una solicitud cerrada

Actualmente `Cerrada` es un estado final absoluto. Se pide que un supervisor pueda
reabrir una solicitud cerrada por error, devolviéndola a `EnProceso`.

1. Modifique `MaquinaEstados`.
2. Ajuste `Solicitud.CambiarEstado` para que la fecha de cierre se limpie al reabrir.
3. Actualice las pruebas que hoy afirman que `Cerrada` no tiene salida.

> **Para discutir:** ¿qué prueba falló primero al hacer el cambio? Ese es exactamente el
> valor de tener el flujo cubierto.

---

## 3. Endpoint de solicitudes rezagadas

Agregue `GET /api/v1/solicitudes/rezagadas`, que devuelva las solicitudes sin resolver con
más de tres días de antigüedad, ordenadas de la más antigua a la más reciente.

1. Agregue el método en `ServicioSolicitudes`.
2. Publique el endpoint en `SolicitudesEndpoints`.
3. Escriba una prueba de integración.

> **Pista:** el sembrador de datos ya genera solicitudes con distintas antigüedades.

---

## 4. Comparar el proveedor simulado contra un modelo real

Con una clave de API disponible:

1. Configure `PROVEEDOR_LENGUAJE=nube` y `NUBE_CLAVE_API` en el archivo `.env`.
2. Levante el sistema y registre las mismas cinco solicitudes con ambos proveedores.
3. Complete el cuadro:

   | Texto de la solicitud | Categoría con `simulado` | Categoría con `nube` | ¿Coinciden? |
   |-----------------------|--------------------------|----------------------|-------------|

> **Para discutir:** ¿dónde acierta el modelo y falla la regla? ¿Dónde ocurre lo
> contrario? ¿Justifica el costo y la latencia en este caso concreto?

---

## 5. Historial de cambios de estado

Hoy solo se conserva el estado actual. Se pide registrar **todo** el historial: qué
transición ocurrió, cuándo y quién la ejecutó.

1. Cree la entidad `CambioEstado` con su configuración de mapeo.
2. Regístrela desde `Solicitud.CambiarEstado`.
3. Genere la migración: `.\tareas.ps1 migracion AgregarHistorialEstados`.
4. Expóngala en `GET /api/v1/solicitudes/{id}/historial`.

> **Para discutir:** ¿por qué conviene que el historial lo escriba la entidad y no el
> servicio de aplicación?

---

## 6. Límite de peticiones por solicitante

Sin control, una persona puede registrar cientos de solicitudes. Implemente un límite de
cinco solicitudes por correo electrónico por hora, devolviendo `429 Too Many Requests`.

> **Para discutir:** ¿debe ser una regla del dominio o una preocupación de la
> infraestructura? La respuesta condiciona dónde se escribe el código.

---

## Preguntas de cierre

1. ¿Por qué el clasificador no impone su resultado por encima de lo declarado por el
   solicitante? ¿Qué pasaría si lo hiciera?
2. Si mañana el servicio de modelos cambia de proveedor, ¿qué archivos habría que tocar?
3. ¿Qué hace la aplicación si el clasificador demora treinta segundos en responder?
   ¿Y si nunca responde?
4. El proveedor simulado es determinista. ¿Por qué eso importa para las pruebas?
5. ¿Qué información se guarda hoy que permitiría auditar una clasificación equivocada
   dentro de seis meses?
