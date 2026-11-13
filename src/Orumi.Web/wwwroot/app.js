'use strict';
(() => {
  const $ = (id) => document.getElementById(id);
  const CLAVE_TOKEN = 'orumi_token';
  const texto = $('texto'), dictar = $('dictar'), enviar = $('enviar'), mensaje = $('mensaje'), estado = $('estado'), pendientes = $('pendientes');

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
  const token = () => { try { return localStorage.getItem(CLAVE_TOKEN); } catch { return null; } };
  const dispositivo = () => (matchMedia('(pointer: coarse)').matches ? 'web-movil' : 'web-escritorio');
  const decir = (t, clase) => { mensaje.textContent = t; mensaje.className = 'mensaje ' + (clase || ''); };
  const mostrar = (acceso) => { $('acceso').hidden = !acceso; $('captura').hidden = acceso; };

  async function actualizarPendientes() {
    let n = 0;
    try { n = (await todas()).length; } catch { /* sin almacenamiento local */ }
    pendientes.hidden = n === 0;
    pendientes.textContent = n ? `${n} nota(s) guardada(s) en este dispositivo, pendiente(s) de enviar.` : '';
  }

  // ------------------------------------------------------------ envío
  // Devuelve 'ok' | 'rechazada' (error de contenido; se devuelve el texto al editor) | 'acceso' | 'sin-conexion'
  async function mandar(nota) {
    let r;
    try {
      r = await fetch('/api/capturas', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token() },
        body: JSON.stringify(nota),
      });
    } catch { return { resultado: 'sin-conexion' }; }
    if (r.status === 200 || r.status === 201) return { resultado: 'ok', id: (await r.json()).id };
    if (r.status === 401 || r.status === 429) return { resultado: 'acceso', bloqueado: r.status === 429 };
    if (r.status === 400) return { resultado: 'rechazada', error: (await r.json().catch(() => ({}))).error || 'La nota no es válida.' };
    return { resultado: 'sin-conexion' }; // fallo del servidor: se conserva y se reintenta
  }

  async function vaciarCola() {
    if (!token()) return;
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
      decir('Guardada: ' + r.id, 'ok');
    } else if (r.resultado === 'rechazada') {
      try { await quitar(nota.idEnvio); } catch { /* nada */ }
      decir(r.error, 'mal');
    } else if (r.resultado === 'acceso') {
      decir(r.bloqueado ? 'Demasiados intentos. Espera unos minutos.' : 'El token no es válido. La nota queda guardada en este dispositivo.', 'mal');
      texto.value = ''; mostrar(true);
    } else {
      texto.value = ''; huboDictado = false; base = '';
      decir('Sin conexión: la nota queda guardada en este dispositivo y se enviará sola.', 'ok');
    }
    actualizarPendientes();
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
      rec.onstart = () => { escuchando = true; huboDictado = true; dictar.setAttribute('aria-pressed', 'true'); dictar.textContent = '⏹ Parar'; decir('Escuchando…'); };
      rec.onend = () => { escuchando = false; dictar.setAttribute('aria-pressed', 'false'); dictar.textContent = '🎙 Dictar'; texto.value = (base + finales).trimStart(); decir(''); };
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
  $('guardar-token').addEventListener('click', async () => {
    const valor = $('token').value.trim();
    if (!valor) return;
    try { localStorage.setItem(CLAVE_TOKEN, valor); } catch { $('error-acceso').textContent = 'Este navegador no permite guardar el token.'; return; }
    let r;
    try { r = await fetch('/api/comprobar', { headers: { Authorization: 'Bearer ' + valor } }); } catch { $('error-acceso').textContent = 'Sin conexión: no se puede comprobar el token.'; return; }
    if (r.ok) { $('token').value = ''; $('error-acceso').textContent = ''; mostrar(false); decir(''); vaciarCola(); }
    else { localStorage.removeItem(CLAVE_TOKEN); $('error-acceso').textContent = r.status === 429 ? 'Demasiados intentos. Espera unos minutos.' : 'Token no válido.'; }
  });
  $('cambiar-token').addEventListener('click', () => { try { localStorage.removeItem(CLAVE_TOKEN); } catch { /* nada */ } mostrar(true); });
  enviar.addEventListener('click', enviarNota);
  texto.addEventListener('keydown', (e) => { if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') enviarNota(); });
  window.addEventListener('online', () => { estado.textContent = ''; vaciarCola(); });
  window.addEventListener('offline', () => { estado.textContent = 'sin conexión'; });

  if ('serviceWorker' in navigator) navigator.serviceWorker.register('sw.js').catch(() => {});
  mostrar(!token());
  if (!navigator.onLine) estado.textContent = 'sin conexión';
  actualizarPendientes();
  vaciarCola();
})();
