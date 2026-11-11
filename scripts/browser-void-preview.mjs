import {browser, baseUrl, loginCookies, waitFor} from './browser-cdp.mjs';

let cdp;
try {
  ({cdp} = await browser({profileName: 'browser-void-preview'}));
  await cdp.send('Network.setCookies', {cookies: await loginCookies()});
  await cdp.send('Page.navigate', {url: baseUrl + '/taekwonbot'});
  await waitFor(cdp, "!!document.querySelector('#tb-message') && document.querySelector('.tb-conversation')?.getAttribute('aria-busy') === 'false'");
  const student = await cdp.evaluate("(async()=>{const r=await fetch('/local-api/api/taekwonbot/students?query=Sol');return (await r.json())[0]})()");
  const status = () => cdp.evaluate(`(async()=>{const r=await fetch('/local-api/api/taekwonbot/payments?studentRef=${encodeURIComponent(student.studentRef)}');return (await r.json()).find(p=>p.month===11&&p.year===2026)?.status ?? ''})()`);
  if (await status() !== 'active') throw new Error('Falta el pago ficticio activo de Sol.');
  await cdp.evaluate("(() => {const field=document.querySelector('#tb-message');field.value='Anulá el pago de Sol de noviembre por duplicado';field.dispatchEvent(new Event('input',{bubbles:true}));})()");
  await waitFor(cdp, "!document.querySelector('#tb-send').disabled");
  await cdp.evaluate("document.querySelector('#tb-send').click()");
  await waitFor(cdp, "document.querySelector('#tb-operation-title')?.textContent.includes('Dejar pago sin efecto') && !!document.querySelector('#tb-operation-confirm')", 90000);
  if (await status() !== 'active') throw new Error('La propuesta anuló el pago antes de confirmar.');
  console.log('PASS anulación propuesta sin escritura');
  await cdp.evaluate("document.querySelector('#tb-operation-cancel').click()");
  await waitFor(cdp, "[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent.includes('Cancelé la propuesta')", 30000);
  if (await status() !== 'active') throw new Error('Cancelar la propuesta cambió el pago.');
  console.log('PASS cancelación conserva el pago');
} catch (error) {
  if (cdp) {
    try { console.error(JSON.stringify(await cdp.evaluate("({last:[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent ?? '',error:document.querySelector('#tb-error')?.textContent ?? ''})"))); } catch {}
  }
  console.error(error.message);
  process.exitCode = 1;
} finally { if (cdp) { try { await cdp.send('Browser.close'); } catch {} cdp.close(); } }
