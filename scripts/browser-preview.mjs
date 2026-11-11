import {browser, baseUrl, loginCookies, waitFor} from './browser-cdp.mjs';

let cdp;
try {
  const response = await fetch(baseUrl + '/login', {signal: AbortSignal.timeout(5000)});
  if (!response.ok) throw new Error('El frontend local todavía no responde.');
  const cookies = await loginCookies();
  ({cdp} = await browser({visible: true}));
  await cdp.send('Network.setCookies', {cookies});
  await cdp.send('Page.navigate', {url: baseUrl + '/taekwonbot'});
  await waitFor(cdp, "location.pathname === '/taekwonbot' && document.readyState === 'complete'");
  await cdp.send('Page.bringToFront');
  console.log('Pestaña local abierta con sesión de desarrollo: ' + baseUrl + '/taekwonbot');
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
} finally { cdp?.close(); }
