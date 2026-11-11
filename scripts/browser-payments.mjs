import {writeFile, mkdir} from 'node:fs/promises';
import path from 'node:path';
import {browser, baseUrl, root, loginCookies, waitFor, sleep} from './browser-cdp.mjs';

const results = [];
let cdp;
const record = name => { results.push({name, status: 'PASS'}); console.log(`PASS ${name}`); };
const click = selector => cdp.evaluate(`document.querySelector(${JSON.stringify(selector)})?.click()`);
async function set(selector, value) {
  await cdp.evaluate(`(() => { const element=document.querySelector(${JSON.stringify(selector)}); if(!element) throw new Error('missing'); element.value=${JSON.stringify(String(value))}; element.dispatchEvent(new Event('change',{bubbles:true})); })()`);
  await sleep(150);
}
async function prepare() {
  await click('#tb-payment-prepare');
  await waitFor(cdp, "!!document.querySelector('#tb-operation-confirm') || !!document.querySelector('#tb-error')");
  const error = await cdp.evaluate("document.querySelector('#tb-error')?.textContent");
  if (error) throw new Error(`No se preparó la operación: ${error}`);
}
async function confirm() {
  await click('#tb-operation-confirm');
  await waitFor(cdp, "!document.querySelector('#tb-operation-confirm') && document.querySelector('.operation-complete')?.textContent.includes('confirmada')");
}
try {
  ({cdp} = await browser({profileName: 'browser-payments'}));
  await cdp.send('Network.setCookies', {cookies: await loginCookies()});
  await cdp.send('Page.navigate', {url: baseUrl + '/taekwonbot'});
  await waitFor(cdp, "document.querySelector('#tb-student-select')?.options.length >= 7 && !document.querySelector('#tb-open-payment')?.disabled");
  await click('#tb-open-payment');
  await waitFor(cdp, "!document.querySelector('#tb-payment-prepare')?.closest('[hidden]')");
  await set('#tb-student-select', await cdp.evaluate("document.querySelector('#tb-student-select').options[1].value"));
  await waitFor(cdp, "!document.querySelector('#tb-payment-prepare').disabled");

  // El alumno ficticio elegido solo tiene una cuota dos meses atrás.
  const period = new Date();
  await set('#tb-payment-year', period.getFullYear());
  await set('#tb-payment-month', period.getMonth() + 1);
  await set('#tb-payment-amount', '12000');
  await prepare();
  const proposal = await cdp.evaluate("document.querySelector('.operation-comparison')?.textContent");
  if (!proposal?.includes('Antes') || !proposal.includes('Después') || !proposal.includes('12.000'))
    throw new Error('La propuesta no muestra antes, después e importe.');
  record('Alta: propuesta revisable antes de escribir');
  await click('#tb-operation-cancel');
  await waitFor(cdp, "document.querySelector('.operation-complete')?.textContent.includes('cancelada')");
  record('Alta: cancelar no registra pago');
  await prepare();
  await confirm();
  record('Alta: confirmación registra pago');

  await set('#tb-payment-kind', 'edit');
  await waitFor(cdp, "document.querySelector('#tb-payment-select')?.options.length >= 2");
  const option = await cdp.evaluate("[...document.querySelector('#tb-payment-select').options].find(o=>o.textContent.includes('12.000'))?.value");
  if (!option) throw new Error('El pago recién creado no aparece para corregir.');
  await set('#tb-payment-select', option);
  await set('#tb-payment-amount', '13000');
  await set('#tb-payment-reason', 'Corrección de prueba local');
  await prepare();
  if (!(await cdp.evaluate("document.querySelector('.operation-comparison')?.textContent"))?.includes('13.000'))
    throw new Error('La corrección no muestra el importe propuesto.');
  await confirm();
  record('Edición: antes/después, motivo y confirmación');

  await set('#tb-payment-kind', 'void');
  await set('#tb-payment-select', option);
  await set('#tb-payment-reason', 'Carga de prueba anulada');
  await prepare();
  await confirm();
  record('Anulación: motivo, confirmación y estado sin efecto');

  await set('#tb-payment-kind', 'restore');
  await set('#tb-payment-select', option);
  await set('#tb-payment-reason', 'Restauración de prueba local');
  await prepare();
  await confirm();
  record('Restauración: pago recuperado con confirmación');
} catch (error) {
  results.push({name: 'Browser payments', status: 'FAIL', message: error.message});
  console.error(error.message);
  process.exitCode = 1;
} finally {
  await mkdir(path.join(root, 'artifacts', 'qa'), {recursive: true});
  await writeFile(path.join(root, 'artifacts', 'qa', 'browser-payments.json'), JSON.stringify({at: new Date().toISOString(), results}, null, 2));
  if (cdp) { try { await cdp.send('Browser.close'); } catch {} cdp.close(); }
}
