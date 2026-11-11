window.TaekwonLocalApi = (() => {
    let csrfPromise;
    async function csrf() {
        if (!csrfPromise) csrfPromise = fetch('/local-api/session', { credentials: 'same-origin', cache: 'no-store' })
            .then(async response => {
                if (response.status === 401) return expired();
                if (!response.ok) throw new Error('No se pudo validar la sesión local. Recargá la página.');
                return (await response.json()).requestToken;
            }).catch(error => { csrfPromise = undefined; throw error; });
        return csrfPromise;
    }
    function expired() {
        window.location.assign('/login?returnUrl=' + encodeURIComponent(window.location.pathname));
        throw new Error('La sesión venció. Volvé a ingresar.');
    }
    async function request(method, path, body) {
        if (!/^(api\/|health(?:\?|$))/.test(path) || /[\\\r\n]/.test(path))
            throw new Error('La ruta solicitada no está habilitada.');
        const headers = { 'Accept': 'application/json' };
        if (method !== 'GET' && method !== 'HEAD') headers['X-TaekwonBot-Antiforgery'] = await csrf();
        if (body !== null && body !== undefined) headers['Content-Type'] = 'application/json; charset=utf-8';
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), 30000);
        try {
            const response = await fetch('/local-api/' + path, {
                method, headers, body: body ?? undefined, credentials: 'same-origin', cache: 'no-store',
                redirect: 'error', signal: controller.signal
            });
            if (response.status === 401) return expired();
            return { status: response.status, body: await response.text() };
        } catch (error) {
            return { status: 504, body: JSON.stringify({ code: 'STATUS_UNKNOWN', message: 'No llegó la respuesta. Consultá el estado antes de volver a confirmar.' }) };
        } finally { clearTimeout(timer); }
    }
    function focus(id) { document.getElementById(id)?.focus(); }
    function scrollConversation() {
        const messages = document.querySelector('.tb-messages');
        if (messages) messages.scrollTop = messages.scrollHeight;
    }
    return { request, focus, scrollConversation };
})();
