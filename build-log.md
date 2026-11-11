# TaekwonBot — registro de construcción

## 2026-11-10 — estado al cierre

- MVP local: chat probado para pagos con homónimos y corrección; lecturas de pagos, deuda, estadísticas y recaudación anual; mock probado para alta, edición, anulación y restauración con confirmación. No se usaron datos reales ni producción.
- Verificación final: backend 43/43; build frontend 0 errores/0 advertencias; API `/health` y frontend `/login` HTTP 200. API arranca desde DLL compilada; watcher API puede quedar sin respuesta tras recarga.
- Presupuesto local OpenAI: USD 1 por usuario/día. El saldo de la cuenta no se consultó. No se autoriza ni se realizó despliegue. Pendiente antes de VPS: revisión de privacidad con datos reales y confirmación de anulaciones OpenAI en navegador.

## 2026-11-10 — cierre de conversación MVP

- Lecturas de chat conectadas a datos acotados por servicios locales: alumno, pagos, meses pendientes, estadísticas mensuales y resumen de recaudación de doce meses. OpenAI redacta usando resultados validados; no recibe SQL ni padrones completos. El presupuesto de desarrollo quedó en USD 1 por usuario/día. Registro observado: USD 0,029076 conocidos y USD 0,05 retenidos por un intento incierto; el saldo de cuenta no se consultó.
- Escrituras conversacionales cubren alta/corrección de importe, edición, anulación y restauración con propuesta, cancelación o confirmación, auditoría y servicios existentes. El mock tiene cobertura de chat para las mismas operaciones de prueba.
- Verificación: backend 43/43 (`dotnet test ... --artifacts-path .test-artifacts`); frontend build 0 errores/0 advertencias. Navegador OpenAI: pago corregido tras homónimo, lectura de pagos, deudas, estadísticas, comparación anual y propuesta/cancelación de anulación. Pruebas de memoria/mock: alta, corrección, edición, anulación, restauración, deuda y estadísticas. La API y frontend contestan HTTP 200 en localhost.
- Límites: anulación por OpenAI se verificó hasta propuesta y cancelación; confirmación de ese tipo se probó mediante transporte simulado. No se hizo despliegue, acceso VPS ni pruebas con datos reales. El proceso API local se ejecuta desde DLL compilada porque el watcher no recuperó una recarga.

## 2026-11-10 — consultas abiertas y límite local / coordinador

- El aviso de presupuesto provenía del tope local de USD 0,10 por usuario/día: el registro mostraba USD 0,029076 de costo conocido y USD 0,05 retenidos por una llamada incierta. Se ajustó el tope local a USD 1 por usuario/día sin exponer claves; la API confirmó el nuevo límite y USD 0,920924 disponibles en ese momento. Esto no verifica el saldo de la cuenta OpenAI.
- Se quitó la respuesta codificada para una frase anual. OpenAI dispone ahora de una herramienta de recaudación de los doce meses y de herramientas de lectura para pagos, deudas y estadísticas mensuales; el backend valida año/alumno y limita los datos salientes. El modelo redacta las respuestas de lectura con los resultados, mientras las escrituras conservan propuesta y confirmación del servidor.
- PASS backend `dotnet test ... --artifacts-path .test-artifacts --no-restore --verbosity quiet`: 42/42. PASS navegador con OpenAI: comparación anual de recaudación contra los agregados locales y consulta de pagos de Rio; `browser-reads.mjs` verificó deuda y estadísticas mensuales. Un primer intento del script combinado de navegador agotó su espera por una condición de sincronización del propio script; se corrigió y cada consulta pasó por separado. API `/health` respondió HTTP 200.
- Pendiente del MVP conversacional: edición, anulación y restauración de pagos por chat. Esos servicios administrativos siguen disponibles en API con confirmación/auditoría, pero aún no están expuestos como herramientas conversacionales. No se usaron datos reales ni producción.

## 2026-11-10 — cierre funcional local acotado

- Estado: flujos principales locales comprobados; no se realizó despliegue ni acceso a producción. El watcher de API quedó sin respuesta tras una recarga; se recuperó ejecutando la DLL compilada de `.test-artifacts` directamente en loopback. API `/health` respondió `mode=local, provider=openai`; frontend `/login` respondió HTTP 200.
- PASS `dotnet test TaekwondoAPI.Tests/TaekwondoAPI.Tests.csproj --artifacts-path .test-artifacts --no-restore --verbosity quiet`: 41/41. PASS build frontend .NET 10: 0 errores, 0 advertencias.
- PASS `node scripts/browser-conversation.mjs`: homónimos, «el segundo», propuesta sin escritura, corrección a $12.000 sin escritura y un único pago tras confirmación. Esta prueba registró un pago solo en SQLite ficticia; repetirla para el mismo alumno/período requiere preparar datos de prueba nuevos.
- PASS `node scripts/browser-reads.mjs`: deuda de Rio y estadísticas del mes en lenguaje libre con OpenAI y resultados locales. PASS test mock en memoria: propuesta/corrección/confirmación sin escritura previa, deuda y estadísticas por chat. No se comprobó el mock en navegador con la configuración activa OpenAI.
- Pendiente para preparación de VPS/datos reales: revisión específica de privacidad de menores y filtros de secretos; verificación del mock en navegador y arranque operativo fuera del watcher de desarrollo. Ninguna autorización de despliegue fue solicitada ni usada.

## 2026-11-10 22:20 — continuación del refactor conversacional / coordinador

- Estado: `IN_PROGRESS`. API y frontend locales activos en `http://localhost:5087` y `http://localhost:5006`, ambos con watchers; `/health` informó `mode=local, provider=openai`.
- Frontend: propuesta pendiente visible durante turnos intermedios; build informado por frontend_dev con 0 errores y 0 advertencias.
- Backend: selección de homónimos, contexto con expiración, borrado en memoria, presupuesto por llamada y defensas preliminares para secretos, datos no indicados y afirmaciones falsas. Backend_dev informó 38/38 tests existentes; aún faltan tests nuevos y la prueba integral final.
- `node --check scripts/browser-conversation.mjs`: PASS. Primera ejecución real: PASS desambiguación de Alex y preparación sin escritura; FAIL corrección «Perdón, eran $12.000» (la tarjeta conservó $13.000). Segunda ejecución tras un ajuste: PASS desambiguación; FAIL porque el modelo eligió «el segundo» pero pidió confirmar la preparación sin crear `OperationView`. Ninguna de estas ejecuciones confirmó un pago.
- Revisión QA: mock de pagos no disponible desde la nueva UI solo chat; riesgo de secretos en el texto actual, datos inventados en preparación y afirmaciones de escritura del modelo. El backend está corrigiendo; no dar por cerrados estos hallazgos sin nueva comprobación.
- Próximo paso: preparar en servidor tras selección si todos los campos están validados, repetir prueba real de corrección/confirmación, cubrir faltantes y mock, probar deudas/estadísticas, actualizar documentación y estado final. Sin producción ni datos reales.

## Refactor conversacional vigente — 2026-11-10
- El usuario autorizó continuar el alcance local presentado tras el punto de control del 3 % y sustituyó la prohibición absoluta de PII: solo datos necesarios para cada tarea, con minimización y atención especial a menores. No producción, no datos reales, no secretos en prompts ni logs.
- Prioridad inmediata: validar un único flujo real con «Cargale a Alex la cuota de noviembre, pagó $13.000 por transferencia.» OpenAI debe interpretar, buscar, pedir aclaración entre homónimos y preparar una operación; ninguna escritura antes de confirmación explícita. Luego ampliar deudas, estadísticas y correcciones.
- Backend y frontend trabajan en repositorios separados. Contrato aditivo acordado: `POST /api/taekwonbot/chat` con `{text}` libre; respuesta conserva campos existentes y agrega candidatos, propuesta pendiente y señal de confirmación. Ningún cambio en producción.
- Estado: `IN_PROGRESS`; todavía no se ha probado este nuevo flujo conversacional. Las pruebas y la llamada real anteriores corresponden al MVP de palabras clave, no acreditan esta arquitectura.

## Estado actual
- Fecha: 2026-11-10
- Estado: `IN_PROGRESS`
- Etapa: F1, alcance CRUD local aprobado; fuentes correctas auditadas y copiadas
- Cambios realizados en repositorios: ramas nuevas, sin implementación todavía
- Servidores locales levantados: **ninguno**
- Conexión OpenAI API: **no configurada**
- Pruebas técnicas ejecutadas: **ninguna**

## Inventario inicial informado por el usuario (pendiente de inspección)
- Backend: https://github.com/nachobazan2006/APITaekwondo
- Frontend: https://github.com/nachobazan2006/BlazorTaekwondo
- Entorno: Codex CLI + Git Bash, Windows
- Desarrollo local con datos de prueba ficticios, UI propia y vista previa en vivo.
- API OpenAI: adaptador mock primero; se requiere intervención humana para clave/saldo.

## Advertencias y bloqueos conocidos
- No se han podido confirmar los repositorios con la conexión de GitHub (respuestas 404); puede faltar permiso de la conexión. **No afirmar que estén borrados**. Si el `git clone` local funciona, auditar desde allí.
- Decisión confirmada: el sistema tiene dos usuarios con el mismo nivel de acceso; ambos podrán usar TaekwonBot. No crear roles nuevos ni allowlists. Pendiente verificar la autenticación existente en el backend.
- Decisión cerrada: mantener búsqueda/selección local y referencias efímeras para evitar envío de datos personales a OpenAI. No reabrir la discusión salvo dificultad técnica demostrada en auditoría.
- No se han inspeccionado SDK, archivos .csproj, esquema de datos ni flujo financiero real.
- No usar SSH ni acceder al VPS productivo en esta fase.

## Autorizaciones
- Alcance CRUD local propuesto: **aprobado por el usuario: «Apruebo, continua»**, tras inventario cerrado presentado en el chat. Ver `docs/contracts-v1.md`.
- Acceso a producción: **no autorizado**.
- Despliegue: **no autorizado**.
- Uso de saldo/clave OpenAI: **pendiente de configuración del usuario**.

## Plantilla para nuevas entradas
### AAAA-MM-DD HH:MM — fase X / agente
- Estado anterior -> nuevo:
- Archivos o servicios tocados:
- Alcance de permiso que los autoriza:
- Cambios concretos:
- Comandos y pruebas ejecutados (PASS/FAIL/NOT_RUN):
- URL local del frontend/API (si existe):
- Evidencia y riesgos:
- Bloqueos `BLOCKED_NEEDS_USER`:
- Siguiente acción:

> No escribir 'funciona' o 'verificado' sin registro reproducible. No volcar claves ni datos reales al log.

### 2026-11-10 16:30 — integración local y QA / coordinador
- Estado: `MOCK_READY`; G15 `BLOCKED_NEEDS_USER` por clave y saldo de OpenAI propios, aún sin llamadas reales. No se tocó producción.
- Implementación: API local con identidad firmada, SQLite ficticia, migración, pagos con preparación/confirmación/auditoría, consultas e historial; frontend con BFF, antiforgery, página TaekwonBot y adaptación de Cuotas. Detalles y comandos en `docs/local-mvp.md`.
- QA independiente revisó autenticación, privacidad y rutas antiguas. Se corrigieron sus tres hallazgos: antiforgery en login/logout, interpretación local de consultas libres, y conteo de homónimos sin ficha médica. No quedaron defectos críticos observados en la revisión de código local.
- PASS backend: `dotnet test TaekwondoAPI.Tests/TaekwondoAPI.Tests.csproj --artifacts-path .test-artifacts` → 38/38. Incluye migración sin cambios pendientes, autenticación, replay, privacidad con canarios, concurrencia e idempotencia.
- PASS frontend: build .NET 10 con `UseAppHost=false`, 0 errores y 0 advertencias con watcher detenido. Un build simultáneo con el watcher falló por bloqueo de DLL; se repitió detenido y pasó.
- PASS navegador: `browser-smoke.mjs` → 9/9, incluyendo chat libre, dos usuarios separados, antiforgery, escritorio y móvil; `browser-payments.mjs` → 6/6 (alta propuesta/cancelar/confirmar, editar, anular, restaurar); `browser-original-pages.mjs` → 7/7 rutas originales cargadas. JSON y capturas en `artifacts/qa/`.
- Seguridad de dependencias: `dotnet list package --vulnerable --include-transitive` detectó SQLitePCLRaw.lib.e_sqlite3 2.1.6, GHSA-2m69-gcr7-jv3q, severidad alta. Se añadió SQLitePCLRaw.bundle_e_sqlite3 2.1.14 compatible con net8; análisis repetido en API y frontend sin vulnerabilidades informadas por los orígenes actuales. Tests backend volvieron a pasar.
- Entorno: el control de aplicaciones de Windows bloqueó la DLL en `bin/Debug/net8.0` tras el cambio de paquete; la DLL en `.test-artifacts` sí se ejecuta. `scripts/local-dev.mjs` usa esa ruta para el watcher API. Ambos servicios responden en `localhost:5087` y `localhost:5006`; la pestaña Chrome local permanece abierta.
- Pendiente: prueba real OpenAI G15 con clave y saldo configurados fuera del chat; no declarar Goal completo hasta entonces. No hay pruebas sobre datos reales ni despliegue.

### 2026-11-10 — rediseño de la interfaz / coordinador
- Referencia visual del usuario: `Downloads/TaekwonBot Chat Interface.png`. La página TaekwonBot quedó centrada en un chat amplio, con saludo, accesos rápidos y compositor visible; búsqueda y formulario de pagos aparecen solo cuando se piden. Los resultados de consultas se muestran dentro de la conversación. Se preservó la confirmación antes/después de pagos.
- El mock ahora responde con un resumen concreto de alumnos, pagos, deudas o estadísticas; el historial presenta intenciones normalizadas con texto legible. La acción de meses pendientes abre el selector si falta alumno y continúa tras seleccionarlo.
- Entorno local: se separaron las salidas de los watchers en `.watch-artifacts` porque Windows bloqueó las DLL nuevas en `bin` estándar. Frontend y API responden en localhost. Se abrió la pestaña local autenticada.
- PASS: build frontend 0 advertencias/0 errores; tests backend 38/38; smoke final navegador 10/10 (escritorio, móvil, consulta, historial, formulario de pago bajo demanda y antiforgery). Capturas en `artifacts/qa/taekwonbot-empty.png`, `taekwonbot-result.png`, `taekwonbot-mobile.png`.
- Pendiente inalterado: OpenAI real G15 requiere clave y saldo del usuario; proveedor activo de la vista local sigue siendo mock.

### 2026-11-10 — F0/F1 / coordinador
- Auditoría local/remota de código ejecutada; los originales Downloads y source/repos permanecen intactos.
- Acceso SSH excepcional autorizado por usuario solo para metadatos: API activa mapea el mismo inode que el DLL auditado; SourceLink APITaekwondo/817295a1406aeac26bfdeb76036241eb4db898ee, .NET8. No se consultaron datos/secretos del VPS ni se modificó producción.
- GitHub leído con sesión local autorizada exclusivamente para los dos repositorios. API branch ABRIL-DIECIOCHO coincide con DLL. Front branch ABRIL-DIECIOCHO/abe21725f9ca983d1b2c27ae722b9cf8f6f1fef0; CSS Cuotas publicado Azure coincide tras quitar scope generado/whitespace. Esto no verifica ensamblado frontend completo.
- Fuentes nuevas en workspace; ambas ramas feature/taekwonbot-mvp-local, status limpio tras clone. Sparse checkout excluye seed original y DB/perfiles/archivos .env.
- Stack: backend net8 EF Core SQLite8.0.10; frontend net10 InteractiveServer. SDK10.0.203 y runtimes8/10 disponibles. HTTP configurados5087/5006; NOT_RUN.
- Decisiones usuario: identidad firmada sin roles; meses pendientes desde alta al actual inclusive; edición/corrección de pagos incluida, con confirmación/auditoría.
- PASS: lectura de instrucciones, auditoría Git/código, identificación backend desplegado, copia/ramas aisladas. NOT_RUN: builds/tests/servidores/UI/OpenAI.
- Próximo paso: F1 local ficticio, autenticación y contratos del documento compartido. No instalaciones globales ni acceso adicional a producción.

### 2026-11-10 — primera prueba real OpenAI / coordinador
- El usuario guardó una clave en `.local/settings.json` fuera del chat; se comprobó solo su presencia, sin imprimirla. Se activó `TaekwonBot.Provider=openai`, `OpenAi.Enabled=true`, modelo `gpt-5.4-mini-2026-03-17` y presupuesto local de USD 0,10 por usuario/día.
- PASS: API `/health` respondió `mode=local, provider=openai`; `node scripts/openai-live-smoke.mjs` hizo una consulta lógica real de estadísticas sobre la SQLite ficticia y mostró el resultado en TaekwonBot. El registro local pasó de 0 a 1 intento, con 481 tokens de entrada, 53 de salida, costo calculado USD 0,00059925 y ninguna reserva pendiente.
- Las pruebas backend anteriores verifican con transporte simulado el esquema saliente y canarios de privacidad. La consulta real usó solo intención, año y mes; los datos identificables se renderizan localmente. No se imprimieron clave ni cuerpos HTTP salientes. No hubo reintentos ni cambios en producción.
- La API y el frontend quedaron activos en localhost; pestaña local reabierta. El proveedor devuelve un aviso fijo mientras el detalle se muestra por el canal local.
