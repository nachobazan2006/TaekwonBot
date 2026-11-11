# TaekwonBot — instrucciones de inicio corregidas

**Entorno:** Windows + Git Bash + Codex CLI. **Objetivo:** MVP local, frontend Blazor visible en el navegador, API .NET y base de datos ficticia. **No acceder al VPS ni modificar producción.**

## 1. Verificar herramientas

Abrí Git Bash y ejecutá:

```bash
node --version
git --version
codex --version
dotnet --info
```

- Si `codex` no está instalado o necesita actualizarse: `npm install -g @openai/codex@latest`.
- Para usar `/goal`, la versión de Codex debe soportar Goals (documentado desde la 0.128.0).
- Si `dotnet` no está instalado, instalá el **SDK de .NET** que indiquen los proyectos (`TargetFramework` de sus `.csproj`). No adivines la versión.
- Si Codex no está autenticado, ejecutá `codex login` y completá el inicio de sesión con ChatGPT. **No hace falta cargar saldo en OpenAI API para usar Codex con tu cuenta ChatGPT**, pero el proveedor real de TaekwonBot sí necesitará una API key y facturación aparte.

## 2. Crear el espacio de trabajo

```bash
mkdir -p ~/proyectos/taekwonbot
cd ~/proyectos/taekwonbot
```

Copiá aquí los documentos y la carpeta **oculta** `.codex` del kit. No pongas todo dentro de un subdirectorio `taekwonbot-kit`; los archivos deben quedar exactamente así:

```text
taekwonbot/
├── AGENTS.md
├── PLANS.md
├── GOALS.md
├── build-log.md
└── .codex/
    ├── config.toml
    └── agents/
        ├── backend-dev.toml
        ├── frontend-dev.toml
        └── qa-reviewer.toml
```

Verificá la estructura (la opción `-a` muestra carpetas ocultas):

```bash
pwd
ls -la
ls -la .codex
ls -la .codex/agents
```

**Importante:** los archivos deben conservar sus extensiones reales (`AGENTS.md`, no `AGENTS.md.txt`). Si descargaste los archivos por separado, creá `.codex/agents` y ubicá allí los tres `.toml` de agentes.

## 3. Clonar los dos repositorios

Desde `~/proyectos/taekwonbot`, ejecutá:

```bash
git clone https://github.com/nachobazan2006/APITaekwondo.git
git clone https://github.com/nachobazan2006/BlazorTaekwondo.git
```

Comprobá:

```bash
ls -la APITaekwondo
ls -la BlazorTaekwondo
git -C APITaekwondo status --short
git -C BlazorTaekwondo status --short
```

Si aparece un error 404 o de autenticación, verificá que tenés acceso a los repositorios con tu cuenta de GitHub. No uses repositorios sustitutos. **No supongas que la rama predeterminada coincide con producción**: la auditoría debe identificar las versiones correctas antes de crear ramas de trabajo.

## 4. Confirmar la configuración de agentes

La raíz `.codex/config.toml` debe incluir:

```toml
[agents]
enabled = true
max_concurrent_threads_per_session = 2
```

Los tres agentes ya se definen en `.codex/agents/*.toml`; no tenés que crearlos manualmente desde un menú. La detección de agentes personalizados depende de que Codex cargue la configuración del proyecto confiable. Aceptá el proyecto como confiable **solo si reconocés sus archivos**. Si no aparecen los agentes, revisá la ruta y el estado de confianza antes de avanzar.

## 5. Iniciar Codex desde la raíz

En Git Bash, parado en `~/proyectos/taekwonbot`:

```bash
codex --sandbox workspace-write --ask-for-approval on-request
```

Si ya iniciás Codex normalmente con `codex`, también podés hacerlo así. **No uses modos sin sandbox o que omitan todas las confirmaciones.**

**Primer mensaje (auditoría solamente):**

```text
Leé AGENTS.md, PLANS.md, GOALS.md y build-log.md.
Revisá APITaekwondo y BlazorTaekwondo en modo estrictamente de lectura.
Identificá arquitectura, versiones de .NET, proyectos de arranque, puertos,
autenticación, conexiones a bases de datos, rutas y dependencias.
Comprobá qué commit/ramas corresponden a producción; no presupongas que HEAD es producción.
Indicá exactamente cómo levantar frontend Blazor y backend en localhost y
cómo disponer de una base local con datos totalmente ficticios.
Usá subagentes backend_dev y frontend_dev únicamente para inspecciones de lectura,
si están disponibles, y consolidá un único informe.
ANTES de crear, editar o borrar cualquier archivo o dato, instalar dependencias,
crear ramas, ejecutar migraciones o levantar servicios con efectos de escritura,
presentame un inventario cerrado de acciones CRUD locales y pedime UNA autorización
de alcance. No toques el VPS, no uses secretos ni datos reales. No hagas cambios aún.
```

Revisá el informe. La aprobación única define el alcance de trabajo autorizado por vos, **pero Codex puede igualmente mostrar confirmaciones técnicas adicionales** por sus propias políticas de seguridad.

## 6. Preparar la vista en vivo (después de autorizar el alcance)

Una vez hecha la auditoría, seguí **los comandos y rutas concretos que detectó Codex** en los `.csproj` y archivos de configuración. No podemos fijar de antemano el directorio de arranque ni el puerto.

Normalmente usarás **tres ventanas de Git Bash**, desde el workspace:

**Terminal A — API:**

```bash
cd ~/proyectos/taekwonbot/APITaekwondo
# Luego, dentro del proyecto ejecutable identificado por la auditoría:
dotnet watch run
```

**Terminal B — frontend Blazor:**

```bash
cd ~/proyectos/taekwonbot/BlazorTaekwondo
# Luego, dentro del proyecto ejecutable identificado por la auditoría:
dotnet watch run
```

**Terminal C — Codex:** permanecé en `~/proyectos/taekwonbot` y continuá con el Goal.

Abrí en el navegador la dirección `http://localhost:PUERTO` o `https://localhost:PUERTO` **que imprima Blazor**. `dotnet watch` puede aplicar Hot Reload en cambios compatibles; otros cambios requieren reiniciar. Si la solución usa Blazor WebAssembly u otro esquema de hospedaje, respetá el comando de inicio real detectado.

El frontend y la API deben apuntar a una **base ficticia local o proveedor mock**, nunca a la base de producción. No publiques puertos al exterior.

## 7. Dar autorización de trabajo y activar el Goal

Después de leer la auditoría, aprobá el conjunto concreto de operaciones locales, por ejemplo: creación de ramas de desarrollo, modificaciones de código, compilaciones, archivos de configuración de ejemplo, creación de base ficticia, pruebas, operaciones de pago ficticias y ejecución de los servidores locales. Excluí expresamente: SSH al VPS, producción, datos reales, push/merge, publicación y credenciales.

En Codex, tras aprobarlo, escribí:

```text
Ahora podés avanzar dentro del alcance local que aprobé. Leé GOALS.md y
PLANS.md. Actuá como coordinador. Delegá backend a backend_dev, interfaz
Blazor a frontend_dev y revisión independiente a qa_reviewer cuando
corresponda; no dejes que dos agentes editen el mismo archivo. Primero
asegurá el arranque local de API y frontend con datos ficticios y mostrámelas
URLs del navegador. Implementá el proveedor mock antes de conectar OpenAI.
Actualizá build-log.md con pruebas y bloqueos reales.
```

Si tu versión admite Goals, podés activar el objetivo persistente mediante:

```text
/goal Implementar TaekwonBot MVP de acuerdo con GOALS.md y PLANS.md, sin
salir del alcance local autorizado. Mantener el frontend Blazor y la API
funcionando en localhost, con datos ficticios. Verificar compilación,
pruebas, confirmación antes de pagos y anulaciones, privacidad e historial
separado para los dos usuarios existentes. Iterar ante fallos verificables.
Cuando una prueba real requiera OPENAI_API_KEY, saldo, credenciales o una
aprobación fuera de alcance, registrar BLOCKED_NEEDS_USER en build-log.md y
detener el trabajo dependiente. No declarar MVP completo sin evidencia.
No usar SSH, ni escribir en producción, ni hacer push o despliegue.
```

`/goal` se escribe **dentro de Codex**, no en Git Bash. Para revisar agentes, probá `/subagents` en Codex CLI. Si una función no aparece, comprobá la versión y el soporte de tu instalación; no continúes suponiendo que está activa.

## 8. Cuando OpenAI pida saldo o API key

El modo mock debe seguir funcionando sin conexión a OpenAI. Cuando Codex necesite probar la integración real, debe **detener esa tarea** y decirte qué variable de entorno o mecanismo de secretos espera. Configurá el secreto vos en el backend local; **nunca** lo pegues en el chat de Codex, en el frontend, en un archivo subido al repo ni aquí. No imprimir secretos en registros.

Codex CLI puede usar tu sesión de ChatGPT para desarrollar código. Eso es **independiente** de la facturación y clave API que utilizará TaekwonBot en ejecución.

## 9. Si cerrás Codex o se interrumpe

Guardá el avance de los repositorios y `build-log.md`. Para volver a Codex desde la raíz del workspace:

```bash
cd ~/proyectos/taekwonbot
codex resume --last
```

Si tu versión admite `/goal`, consultá el Goal y usá `/goal resume` cuando estuviera pausado. Volvé a iniciar las terminales API y Blazor si los procesos dejaron de ejecutarse. No presupongas que una sesión de Codex mantiene servidores vivos después de cerrar la terminal o reiniciar Windows.

## Referencias

- Configuración de subagentes: https://developers.openai.com/codex/multi-agent/
- Referencia de configuración: https://developers.openai.com/codex/config-reference/
- Goals: https://developers.openai.com/cookbook/examples/codex/using_goals_in_codex
- CLI: https://learn.chatgpt.com/docs/cli/reference
