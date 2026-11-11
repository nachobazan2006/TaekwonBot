# Integración OpenAI — preparación F5

Estado: primera consulta real local verificada el 2026-11-10, con datos ficticios. El mock sigue disponible al cambiar `TaekwonBot.Provider` a `mock`. No hay despliegue.

Documentación oficial consultada el 2026-11-10:
- https://developers.openai.com/api/docs/guides/function-calling
- https://developers.openai.com/api/docs/models/gpt-5.4-mini

Adaptador HTTP a Responses API, sin SDK nuevo. Modelo propuesto explícito configurable `gpt-5.4-mini-2026-03-17`; compatibilidad documentada con Function Calling y Structured Outputs. Precios estándar publicados por millón de tokens: entrada USD0.75, entrada cacheada USD0.075, salida USD4.50. Revalidar al cambiar modelo/precios. No seleccionar automáticamente otro modelo si no hay acceso.

`store:false`; herramientas con `strict:true`, objetos `additionalProperties:false` y todas las propiedades requeridas. Sin herramientas alojadas ni acceso web/SQL. El backend ejecuta únicamente consultas/propuestas permitidas; confirmar/cancelar escrituras está reservado al usuario en UI.

No enviar mensajes originales ni historial de chat, nombres, datos de alumnos, identificadores persistentes, cuerpos de error, credenciales ni campos arbitrarios. Construir cada solicitud exclusivamente desde contexto estructurado permitido. Resultados identificables y grupos pequeños permanecen locales. Capturar y verificar payloads con un HttpMessageHandler de pruebas; esto no sustituye una prueba real G15.

Presupuesto y métricas por usuario se limitan localmente antes de emitir solicitudes. Se registran solo consumo/tokens/costo, no prompts ni secretos. Cuota, timeout y errores se tratan con estado explícito y sin reintentar pagos. El usuario configuró una clave nueva fuera del chat; no se aprovecharon credenciales de otras aplicaciones.

Se activó el proveedor con un límite local de USD 0,10 por usuario/día. `node scripts/openai-live-smoke.mjs` verificó una consulta de estadísticas en el frontend local: mensaje permitido, resultado visible, 481 tokens de entrada, 53 de salida y costo calculado USD 0,00059925. No se imprimieron clave ni cuerpos HTTP salientes. El texto devuelto por el modelo está restringido a un aviso fijo; los valores de alumnos y pagos se muestran por el canal local.
