import {mkdir, writeFile} from 'node:fs/promises';
import path from 'node:path';
import {browser, baseUrl, root, loginCookies, waitFor, sleep} from './browser-cdp.mjs';

const results = [];
let cdp;
const record = name => { results.push({name, status: 'PASS'}); console.log(`PASS ${name}`); };
async function send(message) {
  const previous = await cdp.evaluate("document.querySelectorAll('.tb-message-assistant').length");
  await cdp.evaluate(`(() => { const field=document.querySelector('#tb-message'); field.value=${JSON.stringify(message)}; field.dispatchEvent(new Event('input',{bubbles:true})); })()`);
  await waitFor(cdp, "!document.querySelector('#tb-send').disabled");
  await cdp.evaluate("document.querySelector('#tb-send').click()");
  await waitFor(cdp, `document.querySelectorAll('.tb-message-assistant').length > ${previous} && !document.querySelector('#tb-send')?.closest('[aria-busy=true]')`, 60000);
  if (await cdp.evaluate("!!document.querySelector('#tb-error')")) throw new Error('El chat mostró un error.');
}
async function getJson(route) {
  return cdp.evaluate(`(async()=>{const r=await fetch(${JSON.stringify('/local-api' + route)});if(!r.ok) throw new Error('HTTP '+r.status);return r.json()})()`);
}

try {
  ({cdp} = await browser({profileName: 'browser-conversation'}));
  await cdp.send('Network.setCookies', {cookies: await loginCookies()});
  await cdp.send('Page.navigate', {url: baseUrl + '/taekwonbot'});
  await waitFor(cdp, "!!document.querySelector('#tb-message') && !document.querySelector('.tb-conversation')?.getAttribute('aria-busy')?.includes('true')");
  const students = await getJson('/api/taekwonbot/students?query=Alex');
  const second = students.find(student => student.graduacion === 'Amarillo');
  if (students.length < 2 || !second) throw new Error('Faltan los homónimos ficticios.');
  const current = new Date();
  const countPayments = async () => (await getJson('/api/taekwonbot/payments?studentRef=' + encodeURIComponent(second.studentRef)))
    .filter(payment => payment.year === current.getFullYear() && payment.month === current.getMonth() + 1 && payment.status === 'active');
  const before = await countPayments();
  if (before.length) throw new Error('El segundo Alex ya tiene un pago vigente en el mes actual; se necesita un dato local nuevo para repetir esta prueba.');

  await send('Cargale a Alex la cuota de noviembre, pagó $13.000 por transferencia.');
  await waitFor(cdp, "document.querySelectorAll('.tb-candidates li').length >= 2");
  if (await cdp.evaluate("!!document.querySelector('#tb-operation-confirm')")) throw new Error('Se preparó un pago sin resolver el homónimo.');
  record('Nombre ambiguo exige elegir alumno');

  await send('El segundo.');
  await waitFor(cdp, "!!document.querySelector('#tb-operation-confirm')");
  let proposal = await cdp.evaluate("document.querySelector('.operation-comparison')?.textContent ?? ''");
  if (!proposal.includes('13.000') || !proposal.includes('Alex Prueba')) throw new Error('La propuesta inicial no conserva alumno e importe.');
  if ((await countPayments()).length !== 0) throw new Error('Se registró el pago antes de la confirmación.');
  record('Referencia contextual prepara pago sin escribir');

  await send('Perdón, eran $12.000.');
  await waitFor(cdp, "!!document.querySelector('#tb-operation-confirm')");
  proposal = await cdp.evaluate("document.querySelector('.operation-comparison')?.textContent ?? ''");
  if (!proposal.includes('12.000') || proposal.includes('13.000')) throw new Error('La corrección no reemplazó la propuesta anterior.');
  if ((await countPayments()).length !== 0) throw new Error('La corrección registró un pago sin confirmar.');
  record('Corrección reemplaza propuesta sin escribir');

  await cdp.evaluate("document.querySelector('#tb-operation-confirm').click()");
  await waitFor(cdp, "document.querySelector('.operation-complete')?.textContent.includes('confirmada')", 30000);
  await sleep(250);
  const after = await countPayments();
  if (after.length !== 1 || after[0].amount !== 12000) throw new Error('La confirmación no dejó un único pago corregido.');
  record('Confirmación explícita registra un solo pago corregido');
} catch (error) {
  if (cdp) {
    try {
      const pageState = await cdp.evaluate("({lastAssistant:[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent ?? '', candidates:document.querySelectorAll('.tb-candidates li').length, proposal:document.querySelector('.operation-comparison')?.textContent ?? '', error:document.querySelector('#tb-error')?.textContent ?? ''})");
      console.error(JSON.stringify({pageState}));
    } catch {}
  }
  results.push({name: 'Conversación real', status: 'FAIL', message: error.message});
  console.error(error.message);
  process.exitCode = 1;
} finally {
  await mkdir(path.join(root, 'artifacts', 'qa'), {recursive: true});
  await writeFile(path.join(root, 'artifacts', 'qa', 'browser-conversation.json'), JSON.stringify({at: new Date().toISOString(), results}, null, 2));
  if (cdp) { try { await cdp.send('Browser.close'); } catch {} cdp.close(); }
}
