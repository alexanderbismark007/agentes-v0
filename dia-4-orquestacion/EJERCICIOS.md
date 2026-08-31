# Ejercicios — Día 4

Antes de empezar, deje la batería de pruebas en verde y los servicios arriba:

```powershell
.\tareas.ps1 probar
.\tareas.ps1 levantar
```

---

## 1. Romper la idempotencia a propósito

1. Envíe tres veces el mismo cuerpo y verifique que solo se crea una solicitud.
2. Ahora agregue al cuerpo un campo con la marca de tiempo del envío y repita.
3. Cuente las solicitudes creadas.

> **Para discutir:** un campo que cambia en cada envío destruye la idempotencia sin que
> nadie lo note. ¿Sobre qué debería calcularse la huella: sobre todo el cuerpo, o solo
> sobre los campos que identifican el trámite?

---

## 2. Activar la firma y adaptar el flujo

1. Ponga `EXIGIR_FIRMA=true` y un secreto propio en el archivo `.env`.
2. Reinicie y compruebe que `curl` sin firma recibe `401`.
3. Agregue en n8n un nodo **Code** que calcule la firma antes del nodo HTTP:

```javascript
const crypto = require('crypto');
const cuerpo = JSON.stringify($json);
const firma = crypto.createHmac('sha256', 'SU-SECRETO').update(cuerpo).digest('hex');
return [{ json: { cuerpo, firma } }];
```

4. Envíe el cuerpo **exactamente** como se firmó.

> **Para discutir:** si el nodo HTTP vuelve a serializar el objeto, la firma falla. ¿Por
> qué? Ese detalle es la causa más común de fallo al integrar webhooks firmados.

---

## 3. Provocar una falla y comprobar que alguien se entera

1. Detenga la API: `docker compose stop api`.
2. Envíe el formulario desde n8n.
3. Observe los tres reintentos, la rama de error y el correo en Mailpit.
4. Levante la API y revise la bitácora.

> **Para discutir:** ¿cuánto habría tardado el equipo en enterarse sin la rama de error?

---

## 4. Derivar según la categoría

Modifique el flujo para que, además del correo al solicitante, se notifique a la unidad
responsable según la categoría que devuelve la API.

1. Agregue un nodo **Switch** sobre el campo `categoria`.
2. Configure un destinatario distinto por rama.

> **Para discutir:** ¿conviene que los destinatarios estén en el flujo o en la API? Aplique
> el mismo criterio que se usó para decidir dónde se arma el cuerpo del correo.

---

## 5. Escalar las solicitudes rezagadas

Cree un flujo nuevo, disparado por **Schedule Trigger** cada mañana, que:

1. Consulte `GET /api/v1/orquestacion/salud` y `GET /api/v1/solicitudes?estado=Recibida`.
2. Genere el reporte ejecutivo con `POST /api/v1/agente/reportes/ejecutivo`.
3. Lo envíe por correo a la dirección de carrera.

> Este ejercicio une los cuatro días: el reporte del día 3, sobre los datos del día 1,
> disparado por el flujo del día 4.

---

## 6. Detectar que la automatización se detuvo

Hoy `salud` informa el último evento, pero nadie lo mira.

Cree un flujo que cada hora consulte ese endpoint y avise si no hubo eventos en las últimas
24 horas o si la tasa de éxito bajó del 90 %.

> **Para discutir:** ¿qué otras señales indicarían que un flujo dejó de funcionar sin
> haber fallado nunca?

---

## 7. Cola de reintentos con retroceso

Hoy, si la API está caída, n8n reintenta tres veces y desiste.

Diseñe (no hace falta implementarlo completo) un esquema donde los eventos fallidos queden
en cola y se reintenten con esperas crecientes.

1. ¿Dónde viviría la cola?
2. ¿Cómo evitaría que un evento se reintente para siempre?
3. ¿Cómo garantizaría que el reintento no duplique el trámite?

> **Pista para la tercera:** ya está resuelto. ¿Por qué?

---

## Preguntas de cierre

1. ¿Por qué un evento duplicado responde `200` y no un error?
2. ¿Por qué el endpoint lee el cuerpo como texto en crudo en lugar de recibirlo
   deserializado?
3. ¿Por qué la comparación de firmas usa una función de tiempo constante?
4. Los intentos rechazados por firma inválida también se registran. ¿Para qué sirve ese
   registro?
5. La tasa de éxito ignora los duplicados. ¿Es correcto? ¿Qué mediría el indicador si los
   contara como fallas?
6. ¿Qué parte de este flujo **no** debería delegarse nunca a la configuración de n8n?
