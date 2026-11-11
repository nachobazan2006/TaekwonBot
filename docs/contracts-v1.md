# TaekwonBot — contrato local v1

Contrato del coordinador previo a implementación, 2026-11-10. Fuente backend: APITaekwondo `817295a1406aeac26bfdeb76036241eb4db898ee`; frontend: BlazorTaekwondo `abe21725f9ca983d1b2c27ae722b9cf8f6f1fef0`. Ramas de trabajo `feature/taekwonbot-mvp-local`. Alcance CRUD aprobado explícitamente por el usuario.

## Decisiones confirmadas
- Conservar cookies/login y los dos usuarios actuales, con igualdad de acceso y sin roles nuevos.
- Firmar la identidad frontend servidor → API; el navegador no determina el usuario ni conoce claves.
- Deuda = meses sin pago válido desde el mes de alta hasta el actual, ambos incluidos. Mostrar meses pendientes, sin inventar tarifas ni deuda monetaria.
- Incluir registrar, editar/corregir, dejar sin efecto y restaurar pagos. Cada operación requiere vista antes/después, confirmación individual, auditoría e idempotencia. Sin DELETE físico contable.
- Becas: conservar el comportamiento existente de un registro mensual de importe cero con medio Beca, como mes cubierto; no inventar tarifas ni pagos parciales.
- Período año/mes explícito, distinto de fecha de cobro. Nuevas cuotas relacionadas por AlumnoClubId. No deducir años históricos ambiguos.
- Desarrollo exclusivamente ficticio y en loopback. No enviar correos/WhatsApp reales. No abrir Seed original ni bases existentes.

## Configuración privada y aislamiento
Raíz: `C:/Users/luana/proyectos/taekwonbot`. Archivo privado nuevo `.local/settings.json`, cargado en ambos servidores antes del arranque. Claves `ApiKey`, `AdminPassword`, `TaekwonBot:SigningKey` (base64 aleatoria >=64 bytes), `TaekwonBot:WorkspaceRoot`, `TaekwonBot:LocalOnly=true`, `TaekwonBot:Provider=mock`, `TaekwonBot:HistoryRetentionDays=30`. OpenAI sin clave ni modelo predeterminado hasta F5.

SQLite nueva `.local/data/taekwonbot.db`; pruebas en `.local/tests/`. Logs/artefactos sin secretos ni PII. El lanzador genera secretos locales sin imprimirlos. No usar credenciales existentes de Azure/VPS/OpenAI. Configuración Development debe rechazar URL API fuera de localhost/127.0.0.1 y DB fuera del workspace. Puertos configurados API5087/frontend5006, comprobar antes del inicio. Sin HTTPS certificado global necesario para ensayos HTTP loopback.

## Identidad firmada entre servidores
Paquete `System.IdentityModel.Tokens.Jwt` 8.23.0 en ambos proyectos, confirmado compatible con net8.0/net10.0. No compartir DataProtection de versiones de framework distintas.

Browser → BFF Blazor `/local-api/{**path}` mediante fetch mismo origen, cookie actual, autorización por solicitud y antiforgery en escrituras. BFF admite exclusivamente rutas/métodos enumerados de la API; destino loopback fijo y sin redirecciones. HttpClient original usa BrowserBffMessageHandler para preservar servicios; primeras lecturas JS se realizan después de render interactivo.

BFF → API: `Authorization: Bearer <JWT>` y `X-Api-Key` local. HS256; issuer `TaekwonBot.Frontend.Local`; audience `TaekwonBot.Api.Local`; sub = identidad validada por cookie; exp <=60 segundos; jti aleatorio; claims `htm` (método mayúsculas), `htu` (PathAndQuery de destino), `bhash` (SHA256 bytes cuerpo, hexadecimal minúscula). API valida firma, algoritmo, issuer/audience/exp, método/ruta/hash y unicidad jti antes de ejecutar. ClockSkew <=5 segundos. Sin JWT/identidad o solo API key: 401 en TODAS rutas sensibles. Webhooks externos deshabilitados localmente. No imprimir tokens ni reenviarlos a proveedores IA.

## JSON compartido (camelCase)
Errores: HTTP400/401/403/404/409/410/429/503 con `{code,message}` saneados. Excepciones/entidades nunca salen hacia OpenAI. Cancelación del cliente no debe convertir una escritura confirmada en un reintento ciego.

`StudentChoice`: `{studentRef,displayName,graduacion,horario,registeredAt}`. studentRef es opaca, efímera y ligada al usuario/sesión. Toda PII solo API/BFF/UI local autorizada.

`Period`: `{year,month}`.

`PaymentView`: `{paymentRef,studentRef,studentName,year,month,amount,method,date,status,version}`. amount pesos enteros según modelo actual; method Efectivo/Transferencia/Beca; status active/voided. paymentRef opaca, efímera por usuario.

`DebtResult`: `{studentName,fromYear,fromMonth,toYear,toMonth,pendingMonths:[Period]}`.

`StatisticsResult`: `{year,month,students,payments,totalAmount,pendingStudents}`. Datos locales exactos; agregados enviados a OpenAI solo cuando se evita reidentificación (mínimo 5 sujetos). Los demás resultados se presentan localmente.

`HistoryMessage`: `{id,role,text,createdAt}`; role user/assistant. `HistoryPage`: `{messages:[HistoryMessage],retentionDays}`. Backend deriva dueño; nunca acepta usuario de otro cliente. Retención configurable y limpieza automática. No guardar secretos/tokens.

`PreparePaymentRequest`: `{kind,studentRef?,paymentRef?,year?,month?,amount?,method?,date?,reason?}`. kind create/edit/void/restore. Motivo requerido para edit/void/restore. Todos los campos necesarios deben ser explícitos. Edit guarda snapshot anterior y versión, y muestra cambios. Un pago activo existente para un período produce conflicto para nueva alta; corregir el existente en lugar de duplicarlo. No inventar soportes de pagos parciales.

`OperationView`: `{operationRef,kind,status,summary,expiresAt,before?,after?}`. status prepared/confirmed/cancelled/expired. Vista inmutable, vinculada al usuario y a versión de pago. Confirmar no admite modificar valores. Identificador persistido con unicidad transaccional para reintentos.

`ChatRequest`: `{text,studentRef?,year?,month?,intent?,mockScenario?}`. intent opcional students/payments/debts/statistics/help. mockScenario opcional success/error/timeout/no_students/ambiguous/duplicate, permitido solo proveedor mock. Texto libre se procesa exclusivamente local y jamás se envía a OpenAI, ni siquiera sanitizado por expresiones regulares. Si no puede normalizarse de manera segura, responder con aclaración/selector antes de invocar proveedor.

`ChatResponse`: `{state,message,provider,students?,debt?,payments?,statistics?,operation?}`. state answer/clarification/confirmation/error; provider mock/openai. UI no interpreta HTML del modelo.

## Rutas API
- GET `/api/taekwonbot/students?query=` → `[StudentChoice]`.
- POST `/api/taekwonbot/chat` → ChatResponse.
- GET `/api/taekwonbot/history` → HistoryPage; DELETE misma ruta borra solo historial propio, no pagos/auditoría.
- GET `/api/taekwonbot/debts?studentRef=` → DebtResult.
- GET `/api/taekwonbot/payments?studentRef=` → `[PaymentView]`, incluidos anulados con estado.
- GET `/api/taekwonbot/statistics?year=&month=` → StatisticsResult.
- POST `/api/taekwonbot/payments/prepare` → OperationView, sin escritura contable.
- POST `/api/taekwonbot/operations/{operationRef}/confirm` → OperationView. Transacción operación+pago+auditoría. Repetir devuelve el mismo resultado sin duplicar.
- POST `/api/taekwonbot/operations/{operationRef}/cancel` → OperationView, sin pago.
- GET `/api/taekwonbot/operations/{operationRef}` → OperationView (consultar tras timeout).
- GET `/health` → estado mínimo sin secretos ni acceso a datos privados. Única excepción pública explícita, aparte de endpoints de infraestructura necesarios que no devuelvan datos.

## Registro autorizado de herramientas IA
Solo consultas normalizadas y propuestas. Confirmar, cancelar o ejecutar escrituras no son herramientas accesibles al modelo. Nunca SQL arbitrario. Payloads construidos con DTO allowlist sin texto original, nombres, correos, IDs persistentes, mensajes crudos ni excepciones. Rechazar antes de emitir si no se puede producir ese DTO.

## Reparto de archivos
- backend_dev: únicamente APITaekwondo y tests backend. Implementar mock y contrato, identidad, datos ficticios y operaciones. No modificar docs raíz/scripts/Blazor.
- frontend_dev: únicamente BlazorTaekwondo. BFF, página, clientes/contratos y cambios mínimos de integración. No modificar backend/docs raíz/scripts.
- coordinador: documentos raíz/docs, scripts, configuración privada, integración HTTP/browser y adaptador real en coordinación explícita con backend cuando libere ese archivo.
- qa_reviewer: inspección/pruebas después de integración; sin editar código ni datos. Máximo dos subagentes simultáneos.
