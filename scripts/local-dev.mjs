import {spawn, spawnSync} from 'node:child_process';
import {readFile, writeFile, mkdir, open} from 'node:fs/promises';
import {existsSync} from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const privateRoot = path.join(root, '.local');
const statePath = path.join(privateRoot, 'processes.json');
const services = [
  {name: 'api', port: 5087, project: path.join(root, 'APITaekwondo', 'TaekwondoAPI', 'TaekwondoAPI.csproj'), probe: '/health'},
  {name: 'frontend', port: 5006, project: path.join(root, 'BlazorTaekwondo', 'BlazorTaekwondo', 'BlazorTaekwondo.csproj'), probe: '/login'},
];
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const alive = pid => { try { process.kill(pid, 0); return true; } catch { return false; } };

async function portOccupied(port) {
  return new Promise(resolve => {
    const socket = net.connect({host: '127.0.0.1', port});
    socket.setTimeout(1000);
    socket.once('connect', () => { socket.destroy(); resolve(true); });
    socket.once('error', () => resolve(false));
    socket.once('timeout', () => { socket.destroy(); resolve(false); });
  });
}

async function readState() {
  try { return JSON.parse(await readFile(statePath, 'utf8')); }
  catch (error) { if (error.code === 'ENOENT') return {services: []}; throw error; }
}

async function probe(service) {
  try {
    const response = await fetch(`http://localhost:${service.port}${service.probe}`, {signal: AbortSignal.timeout(2000), redirect: 'manual'});
    return response.status === 200;
  } catch { return false; }
}

function processMetadata(pid) {
  if (!Number.isSafeInteger(pid) || pid < 1) return null;
  const query = `$p=Get-CimInstance Win32_Process -Filter 'ProcessId = ${pid}' -ErrorAction SilentlyContinue; if($p){[pscustomobject]@{Name=$p.Name;CommandLine=$p.CommandLine;Created=$p.CreationDate.ToUniversalTime().ToString('o')} | ConvertTo-Json -Compress}`;
  const result = spawnSync('powershell.exe', ['-NoProfile', '-Command', query], {encoding: 'utf8', windowsHide: true});
  if (result.status !== 0 || !result.stdout.trim()) return null;
  return JSON.parse(result.stdout);
}

async function start() {
  const configPath = path.join(privateRoot, 'settings.json');
  if (!existsSync(configPath)) throw new Error('Falta .local/settings.json; no se iniciarán servicios con configuración remota.');
  const config = JSON.parse(await readFile(configPath, 'utf8'));
  if (config.TaekwonBot?.LocalOnly !== true || path.resolve(config.TaekwonBot.WorkspaceRoot).toLowerCase() !== root.toLowerCase())
    throw new Error('La configuración no corresponde al workspace local autorizado.');
  const previous = await readState();
  if (previous.services.some(service => alive(service.pid))) throw new Error('Hay procesos registrados activos. Usá status antes de iniciar otra instancia.');
  for (const service of services) {
    if (await portOccupied(service.port)) throw new Error(`El puerto ${service.port} está ocupado; no se modificó su proceso.`);
  }
  await mkdir(path.join(privateRoot, 'logs'), {recursive: true});
  const state = {workspace: root, startedAt: new Date().toISOString(), services: []};
  for (const service of services) {
    const logPath = path.join(privateRoot, 'logs', `${service.name}.log`);
    const log = await open(logPath, 'a');
    const watchArgs = ['watch', '--non-interactive', '--project', service.project];
    watchArgs.push('--artifacts-path', path.join(root, service.name === 'api' ? 'APITaekwondo' : 'BlazorTaekwondo', '.watch-artifacts'));
    watchArgs.push('run', '--launch-profile', 'http');
    const child = spawn('dotnet.exe', watchArgs, {
      cwd: path.dirname(service.project), detached: true, windowsHide: true,
      stdio: ['ignore', log.fd, log.fd],
      env: {...process.env, ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development',
        TAEKWONBOT_WORKSPACE: root, DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER: '1', DOTNET_WATCH_SUPPRESS_EMOJIS: '1',
        DOTNET_WATCH_RESTART_ON_RUDE_EDIT: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1'},
    });
    await new Promise((resolve, reject) => { child.once('spawn', resolve); child.once('error', reject); });
    child.unref();
    await log.close();
    const metadata = processMetadata(child.pid);
    state.services.push({...service, pid: child.pid, created: metadata?.Created ?? null, logPath});
    await writeFile(statePath, JSON.stringify(state, null, 2));
  }
  for (const service of state.services) {
    let ready = false;
    for (let attempt = 0; attempt < 90; attempt++) {
      if (await probe(service)) { ready = true; break; }
      if (!alive(service.pid)) break;
      await sleep(1000);
    }
    if (!ready) throw new Error(`${service.name} todavía no responde. Revisá su log privado y node scripts/local-dev.mjs status; no se reinició automáticamente.`);
    console.log(`${service.name}: http://localhost:${service.port} (watch PID ${service.pid})`);
  }
}

async function status() {
  const state = await readState();
  for (const service of state.services) {
    console.log(`${service.name}: proceso=${alive(service.pid) ? 'activo' : 'detenido'}, HTTP=${await probe(service) ? 'listo' : 'sin respuesta'}, PID=${service.pid}, http://localhost:${service.port}`);
  }
  if (!state.services.length) console.log('Todavía no hay servidores registrados.');
}

async function stop() {
  const state = await readState();
  if (state.workspace && path.resolve(state.workspace).toLowerCase() !== root.toLowerCase()) throw new Error('Estado de otro workspace; no se detendrá ningún proceso.');
  for (const service of state.services) {
    const expected = services.find(item => item.name === service.name);
    const metadata = processMetadata(service.pid);
    if (!metadata) { console.log(`${service.name}: ya detenido.`); continue; }
    const projectMatches = expected && path.resolve(service.project).toLowerCase() === expected.project.toLowerCase()
      && metadata.CommandLine?.toLowerCase().includes(expected.project.toLowerCase());
    const createdMatches = service.created && metadata.Created === service.created;
    if (metadata.Name?.toLowerCase() !== 'dotnet.exe' || !projectMatches || !createdMatches)
      throw new Error(`El PID ${service.pid} no coincide con el servidor registrado; no se detuvo.`);
    const result = spawnSync('taskkill.exe', ['/PID', String(service.pid), '/T', '/F'], {encoding: 'utf8', windowsHide: true});
    if (result.status !== 0 && alive(service.pid)) throw new Error(`No se pudo detener ${service.name}.`);
    console.log(`${service.name}: detenido.`);
  }
  state.stoppedAt = new Date().toISOString();
  await writeFile(statePath, JSON.stringify(state, null, 2));
}

try {
  const command = process.argv[2] ?? 'status';
  if (command === 'start') await start();
  else if (command === 'status') await status();
  else if (command === 'stop') await stop();
  else throw new Error('Uso: node scripts/local-dev.mjs start|status|stop');
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}
