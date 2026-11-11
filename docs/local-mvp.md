# TaekwonBot local — estado y uso

Estado al 2026-11-10: **IN_PROGRESS**, con una primera consulta real de OpenAI verificada localmente. Todo el trabajo está en dos clones y ramas `feature/taekwonbot-mvp-local`; no hubo push, despliegue, migración ni escritura en producción. Falta cerrar la revisión final de los criterios del Goal.

## Vista local

- Frontend: <http://localhost:5006/taekwonbot>. La ventana de Chrome de desarrollo ya se abrió con una sesión local; el login normal se conserva para cualquier ventana nueva.
- API: <http://localhost:5087/health>.
- Desde este directorio: `node scripts/local-dev.mjs status`, `node scripts/local-dev.mjs start`, `node scripts/local-dev.mjs stop`.
- Para abrir de nuevo una pestaña local autenticada: `node scripts/browser-preview.mjs`. El script lee la contraseña ficticia desde `.local/settings.json` sin mostrarla ni pasarla por argumentos.
- Los dos servicios usan `dotnet watch` con carpetas `.watch-artifacts` separadas porque el control de aplicaciones de este Windows bloqueó DLL nuevas del directorio `bin` estándar.

## Alcance implementado

- API .NET 8 y Blazor InteractiveServer .NET 10 conectados a una SQLite **ficticia** bajo `.local/data/`; seis alumnos de prueba, incluidos homónimos. No se leyó el Seed original ni una base real.
- Se conserva el login existente de dos usuarios. El frontend firma la identidad de cada petición a la API; la API exige firma, clave local, sesión, método, ruta, cuerpo y nonce de un solo uso. El BFF exige cookie y antiforgery en escrituras; login y logout también lo validan.
- Búsqueda y selección de alumnos, pagos, meses sin pago desde el alta hasta el mes actual inclusive, estadísticas, chat de consultas de lectura e historial separado por usuario con retención de 30 días y borrado propio.
- Alta, edición, anulación y restauración de pagos mediante propuesta con antes/después, motivo en correcciones, confirmación o cancelación explícita, auditoría, idempotencia y control de concurrencia. Las escrituras antiguas de cuotas se rechazan para impedir saltarse la confirmación.
- Proveedor OpenAI activo tras configurar una clave fuera del chat. El mock sigue disponible. El adaptador solo admite intenciones estructuradas de lectura y agregados desidentificados; no envía texto libre, nombres, identificadores persistentes ni datos de alumnos. Tiene límite de gasto local por usuario y registro de consumo sin prompts. El modelo devuelve un aviso fijo y el detalle se renderiza localmente.

## Pruebas reproducibles

Ejecutadas en el workspace local:

```powershell
cd C:\Users\luana\proyectos\taekwonbot\APITaekwondo
dotnet test TaekwondoAPI.Tests\TaekwondoAPI.Tests.csproj --artifacts-path .test-artifacts
dotnet list TaekwondoAPI\TaekwondoAPI.csproj package --vulnerable --include-transitive

cd C:\Users\luana\proyectos\taekwonbot\BlazorTaekwondo
dotnet build BlazorTaekwondo\BlazorTaekwondo.csproj --no-restore -p:UseAppHost=false
dotnet list BlazorTaekwondo\BlazorTaekwondo.csproj package --vulnerable --include-transitive

cd C:\Users\luana\proyectos\taekwonbot
node scripts/browser-smoke.mjs
node scripts/browser-original-pages.mjs
```

Resultado: 38/38 tests backend, build frontend 0 errores/0 advertencias cuando el watcher está detenido, 10/10 controles de navegador, 7/7 rutas originales y un flujo completo de alta/cancelación/edición/anulación/restauración de pago (6/6). La prueba de pagos modifica únicamente la SQLite ficticia y ya se ejecutó; repetirla sin preparar otro período produce el conflicto de duplicado esperado. `node scripts/openai-live-smoke.mjs` verificó una consulta real de estadísticas ficticias, con costo calculado USD 0,00059925 y límite local USD 0,10 por usuario/día. Los resultados locales están en `artifacts/qa/*.json`, y hay capturas de escritorio y móvil. El análisis NuGet de ambos proyectos no informó paquetes vulnerables después de actualizar `SQLitePCLRaw.bundle_e_sqlite3` a 2.1.14; antes se detectó la [alerta GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) en la versión transitiva 2.1.6. Este análisis no equivale a una auditoría exhaustiva de seguridad.

## Prueba real G15

La clave se configuró **fuera del chat** en `.local/settings.json`, sin usar credenciales de otras aplicaciones. Están activos `TaekwonBot.Provider = "openai"`, `TaekwonBot.OpenAi.Enabled = true` y `TaekwonBot.OpenAi.Model = "gpt-5.4-mini-2026-03-17"`. Tras reiniciar, una consulta real de estadísticas ficticias pasó; el registro local anotó 481 tokens de entrada, 53 de salida y costo calculado USD 0,00059925. Las pruebas backend verifican con transporte simulado los payloads desidentificados y canarios de privacidad. La consulta real no imprimió el cuerpo HTTP saliente; la garantía se basa en el constructor cerrado de solicitudes y esas pruebas. El costo y la disponibilidad del modelo pueden cambiar.

## Archivos principales

- Backend: `APITaekwondo/TaekwondoAPI/Program.cs`, `Authentication/`, `Controllers/TaekwonBotController.cs`, `Services/TaekwonBot/`, `Data/LocalDatabase.cs`, `Migrations/20261110010000_AddTaekwonBotLocal.cs`, `TaekwondoAPI.Tests/`.
- Frontend: `BlazorTaekwondo/BlazorTaekwondo/Program.cs`, `Endpoints/LocalApiEndpoints.cs`, `Security/LocalApiIdentitySigner.cs`, `Components/Pages/TaekwonBot.razor`, `Components/Pages/Cuotas.razor`, `Components/Shared/PaymentOperationCard.razor`, `Services/TaekwonBotService.cs`.
- Coordinación: `docs/contracts-v1.md`, `docs/openai-integration.md`, `scripts/`, `artifacts/qa/`, `build-log.md`.

Limitaciones conocidas: el frontend publicado en Azure solo se identificó por coincidencia de CSS, no se verificó el commit completo del ensamblado; los importes de deuda no se calculan porque no existe una tarifa histórica definida; el historial conserva la intención normalizada y la respuesta segura, no el texto original libre. No se probó con datos reales ni se desplegó.
