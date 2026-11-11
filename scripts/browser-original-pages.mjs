import {writeFile, mkdir} from 'node:fs/promises';
import path from 'node:path';
import {browser, baseUrl, root, loginCookies, waitFor, sleep} from './browser-cdp.mjs';

const results = [];
let cdp;
try {
  ({cdp} = await browser({profileName: 'browser-original-pages'}));
  await cdp.send('Network.setCookies', {cookies: await loginCookies()});
  for (const route of ['/', '/alumnos', '/cuotas', '/examenes', '/mensajes', '/reportes', '/configuracion']) {
    await cdp.send('Page.navigate', {url: baseUrl + route});
    await waitFor(cdp, "document.readyState === 'complete' && !!document.querySelector('main')", 15000);
    await sleep(750);
    const state = await cdp.evaluate(`({path:location.pathname,login:!!document.querySelector('.login-page'),fatal:[...document.querySelectorAll('.blazor-error-ui')].some(e=>getComputedStyle(e).display!=='none'),title:document.title})`);
    if (state.path !== route || state.login || state.fatal) throw new Error(`La página ${route} no cargó correctamente.`);
    results.push({route, status: 'PASS'});
    console.log(`PASS ${route}`);
  }
} catch (error) {
  results.push({name: 'Original pages', status: 'FAIL', message: error.message});
  console.error(error.message);
  process.exitCode = 1;
} finally {
  await mkdir(path.join(root, 'artifacts', 'qa'), {recursive: true});
  await writeFile(path.join(root, 'artifacts', 'qa', 'browser-original-pages.json'), JSON.stringify({at: new Date().toISOString(), results}, null, 2));
  if (cdp) { try { await cdp.send('Browser.close'); } catch {} cdp.close(); }
}
