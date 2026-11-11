import {readFile} from 'node:fs/promises';
import path from 'node:path';
import {Cdp, root, waitFor} from './browser-cdp.mjs';

let cdp;
try {
  const port = Number((await readFile(path.join(root, '.local', 'browser-preview', 'DevToolsActivePort'), 'utf8')).split(/\r?\n/)[0]);
  const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
  const page = targets.find(target => target.type === 'page' && target.url.startsWith('http://localhost:5006/taekwonbot'));
  if (!page) throw new Error('No está abierta la pestaña autenticada de TaekwonBot.');
  cdp = await Cdp.connect(page.webSocketDebuggerUrl);
  await cdp.send('Runtime.enable');
  if (process.argv.includes('--inspect')) {
    const answer = await cdp.evaluate("[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent ?? ''");
    const usage = await cdp.evaluate("(async()=>{const r=await fetch('/local-api/api/taekwonbot/usage');if(!r.ok)return {status:r.status};const v=await r.json();return {dailyBudgetUsd:v.dailyBudgetUsd,knownCostUsd:v.knownCostUsd,heldUsd:v.heldUsd,remainingUsd:v.remainingUsd}})()");
    console.log(JSON.stringify({answer, usage}));
    process.exit(0);
  }
  await cdp.evaluate("(() => {const field=document.querySelector('#tb-message'); field.value='Dame el mes con mas recaudacion del año'; field.dispatchEvent(new Event('input',{bubbles:true}));})()");
  await waitFor(cdp, "!document.querySelector('#tb-send').disabled");
  await cdp.evaluate("document.querySelector('#tb-send').click()");
  await waitFor(cdp, "[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent?.includes('mes con más recaudación') && document.querySelector('.tb-conversation')?.getAttribute('aria-busy') === 'false'", 60000);
  const answer = await cdp.evaluate("[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent ?? ''");
  if (!answer.includes('mes con más recaudación') || answer.includes('Qué año y mes')) throw new Error('La respuesta no resolvió el máximo anual.');
  await cdp.send('Page.bringToFront');
  console.log('PASS consulta anual en la pestaña visible: ' + answer);
} catch (error) {
  if (cdp) {
    try {
      console.error(JSON.stringify(await cdp.evaluate("({busy:document.querySelector('.tb-conversation')?.getAttribute('aria-busy'),last:[...document.querySelectorAll('.tb-message-assistant p')].at(-1)?.textContent ?? '',error:document.querySelector('#tb-error')?.textContent ?? '',draft:document.querySelector('#tb-message')?.value ?? ''})")));
    } catch {}
  }
  console.error(error.message);
  process.exitCode = 1;
} finally { cdp?.close(); }
