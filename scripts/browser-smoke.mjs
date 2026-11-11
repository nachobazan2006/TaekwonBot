import {writeFile, mkdir} from 'node:fs/promises';
import path from 'node:path';
import {browser, baseUrl, root, loginCookies, waitFor, sleep} from './browser-cdp.mjs';

const results = [];
let cdp;
const record = (name, evidence = {}) => { results.push({name, status: 'PASS', ...evidence}); console.log(`PASS ${name}`); };
const click = selector => cdp.evaluate(`document.querySelector(${JSON.stringify(selector)})?.click()`);
async function set(selector, value) {
  await cdp.evaluate(`(() => { const element=document.querySelector(${JSON.stringify(selector)}); if(!element) throw new Error('missing'); element.value=${JSON.stringify(String(value))}; element.dispatchEvent(new Event('change',{bubbles:true})); })()`);
  await sleep(150);
}
try {
  ({cdp} = await browser());
  await cdp.send('Network.setCookies', {cookies: await loginCookies()});
  await cdp.send('Page.navigate', {url: baseUrl + '/taekwonbot'});
  await waitFor(cdp, "document.querySelector('#tb-student-select')?.options.length >= 7 && !document.querySelector('#tb-send')?.disabled");
  record('Página interactiva autenticada y seis alumnos ficticios cargados');
  const horizontalOverflow = await cdp.evaluate('document.documentElement.scrollWidth > innerWidth');
  if (horizontalOverflow) throw new Error('La vista escritorio tiene desbordamiento horizontal.');
  record('Escritorio sin desbordamiento horizontal');
  await mkdir(path.join(root, 'artifacts', 'qa'), {recursive: true});
  const desktop = await cdp.send('Page.captureScreenshot', {format: 'png', captureBeyondViewport: false});
  await writeFile(path.join(root, 'artifacts', 'qa', 'taekwonbot-desktop.png'), Buffer.from(desktop.data, 'base64'));

  await click('#tb-open-students');
  await waitFor(cdp, "!document.querySelector('#tb-student-select')?.closest('[hidden]')");
  await cdp.evaluate("(() => { const select=document.querySelector('#tb-student-select'); select.value=select.options[1].value; select.dispatchEvent(new Event('change',{bubbles:true})); })()");
  await waitFor(cdp, "!document.querySelector('#tb-send').disabled && !!document.querySelector('.tb-current-student')");
  await click('#tb-open-debts');
  await waitFor(cdp, "!document.querySelector('#tb-send').disabled && !!document.querySelector('#tb-debt-title')");
  if (await cdp.evaluate("!!document.querySelector('#tb-error')")) throw new Error('La consulta de meses pendientes muestra un error.');
  record('Consulta de deuda e historial funcionan en navegador');

  await click('#tb-open-statistics');
  await waitFor(cdp, "!document.querySelector('#tb-send').disabled && !!document.querySelector('#tb-statistics-title')");
  record('Estadísticas del período visibles');
  const resultView = await cdp.send('Page.captureScreenshot', {format: 'png', captureBeyondViewport: false});
  await writeFile(path.join(root, 'artifacts', 'qa', 'taekwonbot-result.png'), Buffer.from(resultView.data, 'base64'));

  await set('#tb-message', '¿Qué meses debe este alumno?');
  await click('#tb-send');
  await waitFor(cdp, "!document.querySelector('#tb-send').disabled && [...document.querySelectorAll('.tb-message-user')].at(-1)?.textContent.includes('meses pendientes')", 15000);
  record('Texto libre determina la consulta de deuda localmente');

  await cdp.send('Emulation.setDeviceMetricsOverride', {width: 390, height: 844, deviceScaleFactor: 1, mobile: true});
  await sleep(500);
  if (await cdp.evaluate('document.documentElement.scrollWidth > innerWidth + 1')) throw new Error('La vista móvil tiene desbordamiento horizontal.');
  const mobile = await cdp.send('Page.captureScreenshot', {format: 'png', captureBeyondViewport: false});
  await writeFile(path.join(root, 'artifacts', 'qa', 'taekwonbot-mobile.png'), Buffer.from(mobile.data, 'base64'));
  record('Móvil 390x844 sin desbordamiento horizontal');

  await cdp.send('Emulation.clearDeviceMetricsOverride');
  await click('#tb-open-payment');
  await waitFor(cdp, "!document.querySelector('#tb-payment-prepare')?.closest('[hidden]')");
  record('Formulario de pagos aparece solo al pedirlo');
  const ownHistoryCount = await cdp.evaluate("document.querySelectorAll('.tb-message').length");
  if (ownHistoryCount < 2) throw new Error('No se ve el historial propio después de consultar.');
  await cdp.send('Network.clearBrowserCookies');
  await cdp.send('Network.setCookies', {cookies: await loginCookies(1)});
  await cdp.send('Page.navigate', {url: baseUrl + '/taekwonbot'});
  await waitFor(cdp, "document.querySelector('#tb-student-select')?.options.length >= 7 && !document.querySelector('#tb-send')?.disabled");
  if (await cdp.evaluate("document.querySelectorAll('.tb-message').length > 0")) throw new Error('El segundo usuario ve historial que no le pertenece.');
  const emptyView = await cdp.send('Page.captureScreenshot', {format: 'png', captureBeyondViewport: false});
  await writeFile(path.join(root, 'artifacts', 'qa', 'taekwonbot-empty.png'), Buffer.from(emptyView.data, 'base64'));
  record('Segundo usuario autenticado con historial separado');

  const unauthenticatedWrite = await cdp.evaluate("(async()=>{const r=await fetch('/local-api/api/taekwonbot/history',{method:'DELETE'});return r.status;})()");
  if (unauthenticatedWrite !== 400 && unauthenticatedWrite !== 403) throw new Error('BFF no rechazó escritura sin antiforgery.');
  record('BFF rechaza escritura sin antiforgery', {httpStatus: unauthenticatedWrite});
  const unsafeLogout = await cdp.evaluate("(async()=>{const r=await fetch('/auth/logout',{method:'POST'});return r.status;})()");
  if (unsafeLogout !== 400) throw new Error('Logout aceptó POST sin antiforgery.');
  record('Logout rechaza POST sin antiforgery', {httpStatus: unsafeLogout});
} catch (error) {
  results.push({name: 'Browser smoke', status: 'FAIL', message: error.message});
  console.error(error.message);
  process.exitCode = 1;
} finally {
  await mkdir(path.join(root, 'artifacts', 'qa'), {recursive: true});
  await writeFile(path.join(root, 'artifacts', 'qa', 'browser-smoke.json'), JSON.stringify({at: new Date().toISOString(), results}, null, 2));
  if (cdp) { try { await cdp.send('Browser.close'); } catch {} cdp.close(); }
}
