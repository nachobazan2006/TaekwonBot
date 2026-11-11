import {browser, baseUrl, loginCookies, waitFor} from './browser-cdp.mjs';

let cdp;
try {
  ({cdp} = await browser({profileName: 'browser-openai-smoke'}));
  await cdp.send('Network.setCookies', {cookies: await loginCookies()});
  await cdp.send('Page.navigate', {url: baseUrl + '/taekwonbot'});
  await waitFor(cdp, "document.querySelector('#tb-student-select')?.options.length >= 7 && !document.querySelector('#tb-open-statistics')?.disabled");
  const usage = () => cdp.evaluate("(async()=>{const r=await fetch('/local-api/api/taekwonbot/usage');return r.ok?await r.json():{httpStatus:r.status}})()");
  const before = await usage();
  const initial = await cdp.evaluate("document.querySelectorAll('.tb-message-assistant').length");
  await cdp.evaluate("document.querySelector('#tb-open-statistics').click()");
  await waitFor(cdp, `!document.querySelector('#tb-open-statistics').disabled && document.querySelectorAll('.tb-message-assistant').length > ${initial}`, 45000);
  const last = await cdp.evaluate("[...document.querySelectorAll('.tb-message-assistant')].at(-1)?.querySelector('p')?.textContent ?? ''");
  const after = await usage();
  const ready = last.includes('La consulta está lista');
  const statisticsVisible = await cdp.evaluate("!!document.querySelector('#tb-statistics-title')");
  console.log(JSON.stringify({ready, statisticsVisible, safeMessage: last, usageBefore: before, usageAfter: after}));
  if (!ready || !statisticsVisible) process.exitCode = 1;
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
} finally {
  if (cdp) { try { await cdp.send('Browser.close'); } catch {} cdp.close(); }
}
