'use strict';
(() => {
  const $ = (id) => document.getElementById(id);
  const CLAVE_TOKEN = 'orumi_token';
  const texto = $('texto'), dictar = $('dictar'), enviar = $('enviar'), estado = $('estado'), pendientes = $('pendientes');
  const { Icono, Logo } = window.OrumiIconos;
  const reducido = matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ------------------------------------------------------------ almacenamiento local (cola sin conexión)
  const abrirBD = () => new Promise((res, rej) => {
    const r = indexedDB.open('orumi-captura', 1);
    r.onupgradeneeded = () => r.result.createObjectStore('cola', { keyPath: 'idEnvio' });
    r.onsuccess = () => res(r.result);
    r.onerror = () => rej(r.error);
  });
  const tx = async (modo, f) => {
    const bd = await abrirBD();
    return new Promise((res, rej) => {
      const t = bd.transaction('cola', modo);
      const out = f(t.objectStore('cola'));
      t.oncomplete = () => res(out && 'result' in out ? out.result : undefined);
      t.onerror = () => rej(t.error);
    });
  };
  const guardar = (n) => tx('readwrite', (s) => s.put(n));
  const quitar = (id) => tx('readwrite', (s) => s.delete(id));
  const todas = () => tx('readonly', (s) => s.getAll());

  // ------------------------------------------------------------ utilidades
  // ------------------------------------------------------------ sesión (Google a través del GoTrue de la plataforma)
  const CLAVE_SESION = 'orumi_sesion', CLAVE_AUTH = 'orumi_auth';
  const leerLS = (k) => { try { return JSON.parse(localStorage.getItem(k)); } catch { return null; } };
  const escribirLS = (k, v) => { try { v === null ? localStorage.removeItem(k) : localStorage.setItem(k, JSON.stringify(v)); } catch { /* sin almacenamiento */ } };
  const sesion = () => leerLS(CLAVE_SESION);
  let urlAuth = leerLS(CLAVE_AUTH);

  async function cargarConfig() {
    try {
      const r = await fetch('/api/config');
      if (r.ok) { urlAuth = (await r.json()).auth; escribirLS(CLAVE_AUTH, urlAuth); }
    } catch { /* sin red: se usa la guardada */ }
    return urlAuth;
  }

  // Al volver de Google, GoTrue deja la sesión en el fragmento de la URL.
  function leerFragmento() {
    if (!location.hash.includes('access_token') && !location.hash.includes('error')) return null;
    const p = new URLSearchParams(location.hash.slice(1));
    history.replaceState(null, '', location.pathname + location.search);
    if (p.get('error')) return { error: p.get('error_description') || p.get('error') };
    const expiraEn = parseInt(p.get('expires_in') || '3600', 10);
    escribirLS(CLAVE_SESION, { access: p.get('access_token'), refresh: p.get('refresh_token'), expira: Date.now() + expiraEn * 1000 });
    return { ok: true };
  }

  async function renovar() {
    const s = sesion();
    if (!s || !s.refresh || !urlAuth) return false;
    try {
      const r = await fetch(urlAuth + '/token?grant_type=refresh_token', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refresh_token: s.refresh }),
      });
      if (!r.ok) { if (r.status === 400 || r.status === 401) escribirLS(CLAVE_SESION, null); return false; }
      const d = await r.json();
      escribirLS(CLAVE_SESION, { access: d.access_token, refresh: d.refresh_token, expira: Date.now() + (d.expires_in || 3600) * 1000 });
      return true;
    } catch { return false; }
  }

  // Token de acceso vigente (renovándolo si hace falta) o null si hay que iniciar sesión de nuevo.
  async function accesoValido() {
    let s = sesion();
    if (!s) return null;
    if (s.expira - 60000 < Date.now()) { await renovar(); s = sesion(); }
    return s ? s.access : null;
  }

  function entrar() {
    if (!urlAuth) { $('error-acceso').textContent = 'Sin conexión: no se puede iniciar sesión ahora.'; return; }
    location.href = urlAuth + '/authorize?provider=google&redirect_to=' + encodeURIComponent(location.origin + '/');
  }
  const dispositivo = () => (matchMedia('(pointer: coarse)').matches ? 'web-movil' : 'web-escritorio');
  let temporizador = null;
  const decir = (t, clase) => {
    const a = $('aviso'); a.textContent = t || ''; a.className = 'aviso ' + (clase || '');
    clearTimeout(temporizador);
    if (t) temporizador = setTimeout(() => { a.textContent = ''; }, clase === 'mal' ? 7000 : 3500);
  };
  const mostrar = (acceso) => { $('acceso').hidden = !acceso; $('captura').hidden = acceso; $('barra').hidden = acceso; };

  async function actualizarPendientes() {
    let n = 0;
    try { n = (await todas()).length; } catch { /* sin almacenamiento local */ }
    pendientes.hidden = n === 0;
    pendientes.textContent = n ? `${n} nota(s) guardada(s) en este dispositivo, pendiente(s) de enviar.` : '';
  }

  // ------------------------------------------------------------ envío
  // Devuelve 'ok' | 'rechazada' (error de contenido; se devuelve el texto al editor) | 'acceso' | 'sin-conexion'
  async function mandar(nota, reintento = false) {
    const acceso = await accesoValido();
    if (!acceso) return { resultado: 'acceso' };
    let r;
    try {
      r = await fetch('/api/capturas', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + acceso },
        body: JSON.stringify(nota),
      });
    } catch { return { resultado: 'sin-conexion' }; }
    if (r.status === 200 || r.status === 201) return { resultado: 'ok', id: (await r.json()).id };
    if (r.status === 401 && !reintento && await renovar()) return mandar(nota, true);
    if (r.status === 401) { escribirLS(CLAVE_SESION, null); return { resultado: 'acceso' }; }
    if (r.status === 403) return { resultado: 'acceso', prohibido: true };
    if (r.status === 429) return { resultado: 'acceso', bloqueado: true };
    if (r.status === 400) return { resultado: 'rechazada', error: (await r.json().catch(() => ({}))).error || 'La nota no es válida.' };
    return { resultado: 'sin-conexion' }; // fallo del servidor o del acceso: se conserva y se reintenta
  }

  async function vaciarCola() {
    if (!sesion()) return;
    let cola = [];
    try { cola = await todas(); } catch { return; }
    for (const nota of cola.sort((a, b) => a.creada.localeCompare(b.creada))) {
      const r = await mandar(nota);
      if (r.resultado === 'ok') await quitar(nota.idEnvio);
      else if (r.resultado === 'rechazada') { await quitar(nota.idEnvio); decir('Una nota guardada no era válida y se descartó: ' + r.error, 'mal'); }
      else { if (r.resultado === 'acceso') mostrar(true); break; }
    }
    actualizarPendientes();
  }

  async function enviarNota() {
    const t = texto.value;
    if (!t.trim()) { decir('Escribe o dicta algo antes de enviar.', 'mal'); return; }
    parar();
    enviar.disabled = true;
    const nota = {
      idEnvio: crypto.randomUUID(), texto: t, voz: huboDictado, fechaAcontecimiento: $('fecha').value || null,
      etiqueta: $('etiqueta').value.trim() || null, dispositivo: dispositivo(), creada: new Date().toISOString(),
    };
    try { await guardar(nota); } catch { /* si no hay almacenamiento local se envía igualmente */ }
    const r = await mandar(nota);
    enviar.disabled = false;
    if (r.resultado === 'ok') {
      try { await quitar(nota.idEnvio); } catch { /* nada */ }
      texto.value = ''; huboDictado = false; base = '';
      decir('Nota guardada · ' + r.id, 'ok');
      confirmar();
    } else if (r.resultado === 'rechazada') {
      try { await quitar(nota.idEnvio); } catch { /* nada */ }
      decir(r.error, 'mal');
    } else if (r.resultado === 'acceso') {
      decir(r.prohibido ? 'Esta cuenta no tiene acceso a ORUMI.' : r.bloqueado ? 'Demasiados intentos. Espera unos minutos.' : 'La sesión ha caducado. La nota queda guardada en este dispositivo: inicia sesión para enviarla.', 'mal');
      texto.value = ''; if (!r.prohibido) mostrar(true);
    } else {
      texto.value = ''; huboDictado = false; base = '';
      decir('Sin conexión: la nota queda guardada en este dispositivo y se enviará sola.', 'ok');
    }
    actualizarPendientes();
  }

  // ------------------------------------------------------------ detalles visuales
  function confirmar() {
    const l = $('lienzo'); l.classList.remove('enviada'); void l.offsetWidth; l.classList.add('enviada');
    setTimeout(() => l.classList.remove('enviada'), reducido ? 0 : 950);
  }
  function marcarMic(on) {
    dictar.setAttribute('aria-pressed', on ? 'true' : 'false');
    dictar.querySelector('.etiqueta-mic').textContent = on ? 'Parar' : 'Dictar';
  }
  function iniciarInterfaz() {
    document.querySelectorAll('[data-icono]').forEach((n) => n.prepend(Icono(n.dataset.icono)));
    $('marca').append(Logo(30), Object.assign(document.createElement('span'), { textContent: 'ORUMI' }));
    $('logo-acceso').append(Logo(76));
    dictar.prepend(Object.assign(Icono('microfono'), { }));
    dictar.firstChild.classList.add('icono-mic');
    enviar.append(Icono('enviar'));
    const t = $('tema');
    const pintar = () => { while (t.firstChild) t.removeChild(t.firstChild); t.append(Icono(window.OrumiTema.actual() === 'dark' ? 'sol' : 'luna')); };
    t.addEventListener('click', () => { window.OrumiTema.alternar(); pintar(); });
    pintar();
    const cuenta = $('cuenta');
    texto.addEventListener('input', () => { cuenta.textContent = texto.value.length ? texto.value.length.toLocaleString('es-ES') + ' caracteres' : ''; });
  }

  // ------------------------------------------------------------ dictado (opcional)
  const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
  let rec = null, escuchando = false, huboDictado = false, base = '', finales = '';

  function parar() { if (rec && escuchando) rec.stop(); }

  if (SR) {
    dictar.hidden = false; $('aviso-voz').hidden = false;
    dictar.addEventListener('click', () => {
      if (escuchando) { rec.stop(); return; }
      base = texto.value; finales = '';
      rec = new SR(); rec.lang = 'es-ES'; rec.continuous = true; rec.interimResults = true;
      rec.onstart = () => { escuchando = true; huboDictado = true; marcarMic(true); decir('Escuchando…'); };
      rec.onend = () => { escuchando = false; marcarMic(false); texto.value = (base + finales).trimStart(); decir(''); };
      rec.onerror = (e) => { if (e.error !== 'no-speech' && e.error !== 'aborted') decir('El dictado falló (' + e.error + '). Puedes escribir la nota.', 'mal'); };
      rec.onresult = (e) => {
        let provisional = '';
        for (let i = e.resultIndex; i < e.results.length; i++) {
          const parte = e.results[i][0].transcript;
          if (e.results[i].isFinal) finales += (finales && !/\s$/.test(finales) ? ' ' : '') + parte.trim(); else provisional += parte;
        }
        const sep = (base && !/\s$/.test(base) ? ' ' : '');
        texto.value = base + sep + finales + (provisional ? (finales ? ' ' : '') + provisional : '');
      };
      rec.start();
    });
  }
  texto.addEventListener('input', () => { if (!escuchando) base = texto.value; });

  // ------------------------------------------------------------ arranque
  $('entrar').addEventListener('click', entrar);
  $('salir').addEventListener('click', () => { escribirLS(CLAVE_SESION, null); mostrar(true); decir(''); });
  enviar.addEventListener('click', enviarNota);
  texto.addEventListener('keydown', (e) => { if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') enviarNota(); });
  window.addEventListener('online', () => { estado.textContent = ''; vaciarCola(); });
  window.addEventListener('offline', () => { estado.textContent = 'sin conexión'; });

  iniciarInterfaz();
  (async () => {
    if ('serviceWorker' in navigator) navigator.serviceWorker.register('sw.js').catch(() => {});
    const vuelta = leerFragmento();
    await cargarConfig();
    if (vuelta && vuelta.error) $('error-acceso').textContent = 'No se pudo iniciar sesión: ' + vuelta.error;
    const logueado = !!(await accesoValido());
    mostrar(!logueado);
    if (logueado) {
      // Comprobar de entrada que la cuenta tiene acceso, para decirlo antes de que se escriba nada.
      try {
        const r = await fetch('/api/comprobar', { headers: { Authorization: 'Bearer ' + (await accesoValido()) } });
        if (r.status === 403) { escribirLS(CLAVE_SESION, null); mostrar(true); $('error-acceso').textContent = 'Esta cuenta de Google no tiene acceso a ORUMI.'; }
        else if (r.status === 401) { escribirLS(CLAVE_SESION, null); mostrar(true); }
      } catch { /* sin red: se puede escribir igualmente */ }
    }
    if (!navigator.onLine) estado.textContent = 'sin conexión';
    actualizarPendientes();
    vaciarCola();
  })();
})();
