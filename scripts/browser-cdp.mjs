import {spawn} from 'node:child_process';
import {readFile, mkdir} from 'node:fs/promises';
import {existsSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

export const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const baseUrl = 'http://localhost:5006';
export const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

export class Cdp {
  constructor(socket) {
    this.socket = socket; this.nextId = 0; this.pending = new Map();
    socket.addEventListener('message', event => {
      const data = JSON.parse(event.data);
      if (!data.id) return;
      const pending = this.pending.get(data.id);
      if (!pending) return;
      this.pending.delete(data.id); clearTimeout(pending.timer);
      if (data.error) pending.reject(new Error(`Falló CDP ${pending.method}.`));
      else pending.resolve(data.result);
    });
    socket.addEventListener('close', () => {
      for (const pending of this.pending.values()) { clearTimeout(pending.timer); pending.reject(new Error('El navegador se desconectó.')); }
      this.pending.clear();
    });
  }
  static async connect(url) {
    const socket = new WebSocket(url);
    await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, {once: true}); socket.addEventListener('error', () => reject(new Error('No se pudo conectar al navegador local.')), {once: true}); });
    return new Cdp(socket);
  }
  send(method, params = {}) {
    const id = ++this.nextId;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { this.pending.delete(id); reject(new Error(`Tiempo de espera CDP ${method}.`)); }, 15000);
      this.pending.set(id, {resolve, reject, timer, method});
      this.socket.send(JSON.stringify({id, method, params}));
    });
  }
  async evaluate(expression) {
    const response = await this.send('Runtime.evaluate', {expression, returnByValue: true, awaitPromise: true});
    if (response.exceptionDetails) throw new Error('Falló una comprobación dentro del navegador local.');
    return response.result?.value;
  }
  close() { this.socket.close(); }
}

export async function localCredentials() {
  const settings = JSON.parse(await readFile(path.join(root, '.local', 'settings.json'), 'utf8'));
  const source = await readFile(path.join(root, 'BlazorTaekwondo', 'BlazorTaekwondo', 'Program.cs'), 'utf8');
  const users = [...new Set([...source.matchAll(/string\.Equals\(username,\s*"([^"\r\n]+)"/g)].map(match => match[1]))];
  if (users.length !== 2 || !settings.AdminPassword) throw new Error('No se pudieron identificar los dos usuarios y la contraseña local.');
  return {users, password: settings.AdminPassword};
}

export async function loginCookies(userIndex = 0) {
  const {users, password} = await localCredentials();
  const loginPage = await fetch(baseUrl + '/login', {signal: AbortSignal.timeout(15000)});
  if (!loginPage.ok) throw new Error('No se pudo abrir el ingreso local.');
  const markup = await loginPage.text();
  const tokenInput = markup.match(/<input\b[^>]*name="__RequestVerificationToken"[^>]*>/i)?.[0];
  const requestToken = tokenInput?.match(/\bvalue="([^"]+)"/i)?.[1];
  const antiforgeryCookies = loginPage.headers.getSetCookie().map(header => header.split(';')[0]);
  if (!requestToken || !antiforgeryCookies.length) throw new Error('El ingreso local no entregó antiforgery.');
  const response = await fetch(baseUrl + '/auth/login', {
    method: 'POST', redirect: 'manual', headers: {'Content-Type': 'application/x-www-form-urlencoded', Cookie: antiforgeryCookies.join('; ')},
    body: new URLSearchParams({username: users[userIndex], password, returnUrl: '/taekwonbot', __RequestVerificationToken: requestToken}), signal: AbortSignal.timeout(15000),
  });
  const setCookies = response.headers.getSetCookie();
  if (![302, 303].includes(response.status) || !setCookies.length || response.headers.get('location')?.includes('error='))
    throw new Error('No se pudo iniciar la sesión ficticia local.');
  return [...antiforgeryCookies, ...setCookies].map(header => {
    const [pair, ...attributes] = header.split(';');
    const equals = pair.indexOf('=');
    return {name: pair.slice(0, equals), value: pair.slice(equals + 1), url: baseUrl,
      httpOnly: attributes.some(value => value.trim().toLowerCase() === 'httponly'), sameSite: 'Lax'};
  });
}

export async function browser({visible = false, profileName = visible ? 'browser-preview' : 'browser-tests'} = {}) {
  const executable = ['C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe'].find(existsSync);
  if (!executable) throw new Error('No se encontró Chrome o Edge instalado.');
  const profile = path.join(root, '.local', profileName);
  await mkdir(profile, {recursive: true});
  const portFile = path.join(profile, 'DevToolsActivePort');
  let port;
  if (existsSync(portFile)) {
    const candidate = Number((await readFile(portFile, 'utf8')).split(/\r?\n/)[0]);
    try { if ((await fetch(`http://127.0.0.1:${candidate}/json/version`, {signal: AbortSignal.timeout(1000)})).ok) port = candidate; } catch {}
  }
  if (!port) {
    const args = [`--user-data-dir=${profile}`, '--remote-debugging-address=127.0.0.1', '--remote-debugging-port=0',
      '--no-first-run', '--no-default-browser-check', '--disable-default-apps', '--window-size=1440,1000', '--new-window', 'about:blank'];
    if (!visible) args.unshift('--headless=new', '--disable-gpu');
    const process = spawn(executable, args, {detached: true, windowsHide: !visible, stdio: 'ignore'});
    await new Promise((resolve, reject) => { process.once('spawn', resolve); process.once('error', () => reject(new Error('No se pudo abrir el navegador.'))); });
    process.unref();
    for (let attempt = 0; attempt < 60; attempt++) {
      await sleep(250);
      try {
        const candidate = Number((await readFile(portFile, 'utf8')).split(/\r?\n/)[0]);
        if ((await fetch(`http://127.0.0.1:${candidate}/json/version`, {signal: AbortSignal.timeout(1000)})).ok) { port = candidate; break; }
      } catch {}
    }
    if (!port) throw new Error('El navegador no abrió su puerto de control local.');
  }
  const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, {method: 'PUT'})).json();
  const cdp = await Cdp.connect(target.webSocketDebuggerUrl);
  await cdp.send('Page.enable');
  await cdp.send('Runtime.enable');
  await cdp.send('Network.enable');
  await cdp.send('Network.setBlockedURLs', {urls: ['*azurewebsites.net*', '*187.77.230.196*', '*atleticoallende.xyz*', '*api.twilio.com*', '*api.openai.com*']});
  return {cdp, target, port};
}

export async function waitFor(cdp, expression, timeoutMs = 30000) {
  const until = Date.now() + timeoutMs;
  while (Date.now() < until) {
    try { if (await cdp.evaluate(expression)) return; } catch {}
    await sleep(250);
  }
  throw new Error('La vista local no alcanzó el estado esperado.');
}
