import {browser, baseUrl, loginCookies, waitFor} from './browser-cdp.mjs';

let cdp;
async function ask(message, expected) {
  const prior = await cdp.evaluate("document.querySelectorAll('.tb-message-assistant').length");
  await cdp.evaluate(`(() => { const field=document.querySelector('#tb-message'); field.value=${JSON.stringify(message)}; field.dispatchEvent(new Event('input',{bubbles:true})); })()`);
  await waitFor(cdp, "!document.querySelector('#tb-send').disabled");
  await cdp.evaluate("document.querySelector('#tb-send').click()");
  await waitFor(cdp, `document.querySelectorAll('.tb-message-assistant').length > ${prior} && !!document.querySelector(${JSON.stringify(expected)})`, 60000);
  if (await cdp.evaluate("!!document.querySelector('#tb-error')")) throw new Error('El chat mostró un error.');
}
try {
  ({cdp} = await browser({profileName: 'browser-reads'}));
  await cdp.send('Network.setCookies', {cookies: await loginCookies()});
  await cdp.send('Page.navigate', {url: baseUrl + '/taekwonbot'});
  await waitFor(cdp, "!!document.querySelector('#tb-message') && document.querySelector('.tb-conversation')?.getAttribute('aria-busy') === 'false'");
  await ask('¿Qué meses debe Rio?', '#tb-debt-title');
  console.log('PASS deuda en lenguaje libre');
  await ask('Mostrame las estadísticas de este mes.', '#tb-statistics-title');
  console.log('PASS estadísticas en lenguaje libre');
} catch (error) {
  if (cdp) {
    try { console.error(JSON.stringify(await cdp.evaluate("({lastAssistant:[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent ?? '', error:document.querySelector('#tb-error')?.textContent ?? ''})"))); } catch {}
  }
  console.error(error.message);
  process.exitCode = 1;
} finally {
  if (cdp) { try { await cdp.send('Browser.close'); } catch {} cdp.close(); }
}
