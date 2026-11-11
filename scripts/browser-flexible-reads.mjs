import {browser, baseUrl, loginCookies, waitFor} from './browser-cdp.mjs';

let cdp;
async function ask(message, expected) {
  const previous = await cdp.evaluate("document.querySelectorAll('.tb-message-assistant').length");
  await cdp.evaluate(`(() => {const field=document.querySelector('#tb-message');field.value=${JSON.stringify(message)};field.dispatchEvent(new Event('input',{bubbles:true}));})()`);
  await waitFor(cdp, "!document.querySelector('#tb-send').disabled");
  await cdp.evaluate("document.querySelector('#tb-send').click()");
  await waitFor(cdp, `document.querySelectorAll('.tb-message-assistant').length > ${previous} && ${expected}`, 120000);
  const error = await cdp.evaluate("document.querySelector('#tb-error')?.textContent ?? ''");
  if (error) throw new Error('El chat mostró un error.');
}
try {
  ({cdp} = await browser({profileName: 'browser-flexible-reads'}));
  await cdp.send('Network.setCookies', {cookies: await loginCookies()});
  await cdp.send('Page.navigate', {url: baseUrl + '/taekwonbot'});
  await waitFor(cdp, "!!document.querySelector('#tb-message') && document.querySelector('.tb-conversation')?.getAttribute('aria-busy') === 'false'");
  if (!process.argv.includes('--payments-only')) {
    const year = new Date().getFullYear();
    const monthly = await cdp.evaluate(`(async()=>{const values=[];for(let month=1;month<=12;month++){const response=await fetch('/local-api/api/taekwonbot/statistics?year=${year}&month='+month);if(!response.ok)throw Error('stats '+response.status);values.push(await response.json())}return values})()`);
    const top = monthly.reduce((best, value) => value.totalAmount > best.totalAmount ? value : best);
    const monthName = new Intl.DateTimeFormat('es-AR', {month: 'long'}).format(new Date(year, top.month - 1, 1));
    await ask('Dame el mes con mas recaudacion del año', "document.querySelector('.tb-conversation')?.getAttribute('aria-busy') === 'false'");
    const answer = await cdp.evaluate("[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent ?? ''");
    if (!answer.toLowerCase().includes(monthName) || /especific[aá].*(mes|año)/i.test(answer)) throw new Error('La comparación anual no coincide con los datos locales.');
    console.log('PASS comparación anual interpretada con datos locales');
  }
  if (!process.argv.includes('--annual-only')) {
    await ask('Mostrame los pagos de Rio', "!!document.querySelector('#tb-payments-title') && document.querySelector('.tb-conversation')?.getAttribute('aria-busy') === 'false'");
    console.log('PASS consulta libre de pagos de alumno');
  }
} catch (error) {
  if (cdp) {
    try { console.error(JSON.stringify(await cdp.evaluate("({last:[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent ?? '',error:document.querySelector('#tb-error')?.textContent ?? ''})"))); } catch {}
  }
  console.error(error.message);
  process.exitCode = 1;
} finally { if (cdp) { try { await cdp.send('Browser.close'); } catch {} cdp.close(); } }
