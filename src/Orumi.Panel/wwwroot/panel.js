'use strict';
(() => {
  // ------------------------------------------------------------ utilidades (todo texto se inserta como texto, nunca como HTML)
  const $ = (id) => document.getElementById(id);
  const cont = $('contenido');
  function el(tag, props = {}, ...hijos) {
    const e = document.createElement(tag);
    for (const [k, v] of Object.entries(props)) {
      if (v === null || v === undefined || v === false) continue;
      if (k === 'class') e.className = v;
      else if (k === 'onclick') e.addEventListener('click', v);
      else if (k === 'onsubmit') e.addEventListener('submit', v);
      else if (k === 'text') e.textContent = v;
      else e.setAttribute(k, v === true ? '' : v);
    }
    for (const h of hijos.flat()) if (h !== null && h !== undefined && h !== false) e.append(h.nodeType ? h : document.createTextNode(String(h)));
    return e;
  }
  const vaciar = (n) => { while (n.firstChild) n.removeChild(n.firstChild); };
  let temporizador = null;
  function avisar(texto, clase) {
    const a = $('aviso'); a.textContent = texto || ''; a.className = 'aviso ' + (clase || '');
    clearTimeout(temporizador);
    if (texto) temporizador = setTimeout(() => { a.textContent = ''; }, clase === 'mal' ? 7000 : 3500);
  }
  const hoy = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; };
  const sumarDias = (f, n) => { const [a, m, d] = f.split('-').map(Number); const x = new Date(a, m - 1, d + n); return `${x.getFullYear()}-${String(x.getMonth() + 1).padStart(2, '0')}-${String(x.getDate()).padStart(2, '0')}`; };
  const ID_REG = /^reg-\d{8}-[a-z0-9]{4}$/;
  const FORMATOS = { fecha: 'AAAA-MM-DD o AAAA-MM-DD..AAAA-MM-DD', hora: 'HH:MM', duracion: '90, 2h, 1h30', entidades: 'Nombre:tipo, Nombre:tipo', titulo: 'Texto', contexto: 'Texto', resultados: 'Texto' };

  // ------------------------------------------------------------ sesión (Google a través del GoTrue de la plataforma)
  // La sesión se comparte con la página de captura (mismo origen): las mismas claves.
  const CLAVE_SESION = 'orumi_sesion', CLAVE_AUTH = 'orumi_auth', CLAVE_API = 'orumi_panel_api';
  const leerLS = (k) => { try { return JSON.parse(localStorage.getItem(k)); } catch { return null; } };
  const escribirLS = (k, v) => { try { v === null ? localStorage.removeItem(k) : localStorage.setItem(k, JSON.stringify(v)); } catch { /* sin almacenamiento */ } };
  const sesion = () => leerLS(CLAVE_SESION);
  let urlAuth = leerLS(CLAVE_AUTH);
  let urlApi = leerLS(CLAVE_API);

  async function cargarConfig() {
    try {
      const r = await fetch('config.json'); // relativo a /panel/
      if (r.ok) { const c = await r.json(); urlAuth = c.auth; urlApi = c.api; escribirLS(CLAVE_AUTH, urlAuth); escribirLS(CLAVE_API, urlApi); }
    } catch { /* sin red: se usan las guardadas */ }
  }
  function leerFragmento() {
    if (!location.hash.includes('access_token') && !location.hash.includes('error')) return null;
    const p = new URLSearchParams(location.hash.slice(1));
    history.replaceState(null, '', location.pathname + location.search);
    if (p.get('error')) return { error: p.get('error_description') || p.get('error') };
    escribirLS(CLAVE_SESION, { access: p.get('access_token'), refresh: p.get('refresh_token'), expira: Date.now() + parseInt(p.get('expires_in') || '3600', 10) * 1000 });
    return { ok: true };
  }
  async function renovar() {
    const s = sesion();
    if (!s || !s.refresh || !urlAuth) return false;
    try {
      const r = await fetch(urlAuth + '/token?grant_type=refresh_token', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refresh_token: s.refresh }) });
      if (!r.ok) { if (r.status === 400 || r.status === 401) escribirLS(CLAVE_SESION, null); return false; }
      const d = await r.json();
      escribirLS(CLAVE_SESION, { access: d.access_token, refresh: d.refresh_token, expira: Date.now() + (d.expires_in || 3600) * 1000 });
      return true;
    } catch { return false; }
  }
  async function acceso() {
    let s = sesion();
    if (!s) return null;
    if (s.expira - 60000 < Date.now()) { await renovar(); s = sesion(); }
    return s ? s.access : null;
  }
  function entrar() {
    if (!urlAuth) { avisar('Sin conexión: no se puede iniciar sesión ahora.', 'mal'); return; }
    location.href = urlAuth + '/authorize?provider=google&redirect_to=' + encodeURIComponent(location.origin + '/panel/');
  }

  // ------------------------------------------------------------ API
  class ErrorDeApi extends Error { constructor(estado, mensaje) { super(mensaje); this.estado = estado; } }
  async function api(ruta, opciones = {}, reintento = false) {
    const token = await acceso();
    if (!token) throw new ErrorDeApi(401, 'Inicia sesión.');
    if (!urlApi) throw new ErrorDeApi(0, 'Sin conexión con el servidor.');
    const cabeceras = { Authorization: 'Bearer ' + token };
    if (opciones.cuerpo) cabeceras['Content-Type'] = 'application/json';
    let r;
    try { r = await fetch(urlApi + ruta, { method: opciones.metodo || 'GET', headers: cabeceras, body: opciones.cuerpo ? JSON.stringify(opciones.cuerpo) : undefined }); }
    catch { throw new ErrorDeApi(0, 'Sin conexión con el servidor.'); }
    if (r.status === 401 && !reintento && await renovar()) return api(ruta, opciones, true);
    const datos = await r.json().catch(() => ({}));
    if (r.ok) return datos;
    if (r.status === 401) { escribirLS(CLAVE_SESION, null); throw new ErrorDeApi(401, 'La sesión ha caducado.'); }
    if (r.status === 429) { const espera = r.headers.get('Retry-After'); throw new ErrorDeApi(429, 'Demasiadas peticiones' + (espera ? `: reintenta en ${espera} s.` : '.')); }
    throw new ErrorDeApi(r.status, datos.error || datos.mensaje || 'Error inesperado.');
  }
  const get = (ruta, params = {}) => {
    const q = new URLSearchParams(Object.entries(params).filter(([, v]) => v !== '' && v !== null && v !== undefined));
    return api(ruta + (q.toString() ? '?' + q : ''));
  };
  const post = (ruta, cuerpo) => api(ruta, { metodo: 'POST', cuerpo });

  function manejar(e) {
    if (e.estado === 401) { mostrarAcceso(); return; }
    avisar(e.message, 'mal');
  }

  // ------------------------------------------------------------ componentes
  const etiquetaCerteza = (c) => el('span', { class: 'etiqueta certeza-' + (c || 'desconocido'), text: c || 'sin certeza' });

  function itemRegistro(r) {
    return el('a', { class: 'item', href: '#/registro/' + r.id },
      el('span', { class: 'hora', text: r.hora || (r.desde && r.hasta && r.desde !== r.hasta ? r.desde.slice(5) + '→' + r.hasta.slice(5) : '·') }),
      el('span', {}, el('strong', { text: r.titulo })),
      el('span', { class: 'etiquetas fila' }, el('span', { class: 'etiqueta', text: r.clase || '?' }), r.certezaFecha ? etiquetaCerteza(r.certezaFecha) : null, r.enConflicto ? el('span', { class: 'etiqueta conflicto', text: 'conflicto' }) : null));
  }

  async function actualizarContador() {
    try {
      const e = await get('/api/estado');
      const c = $('contador');
      c.hidden = !e.preguntasPendientes; c.textContent = e.preguntasPendientes;
    } catch { /* el contador es accesorio */ }
  }

  // ------------------------------------------------------------ vista: día
  async function vistaDia(fecha) {
    fecha = /^\d{4}-\d{2}-\d{2}$/.test(fecha || '') ? fecha : hoy();
    const selector = el('input', { type: 'date', value: fecha, 'aria-label': 'Día' });
    selector.addEventListener('change', () => { if (selector.value) location.hash = '#/dia/' + selector.value; });
    const lista = el('div', {}, el('p', { class: 'vacio', text: 'Cargando…' }));
    cont.append(el('h2', { text: 'Día' }),
      el('div', { class: 'fila' },
        el('button', { type: 'button', onclick: () => { location.hash = '#/dia/' + sumarDias(fecha, -1); }, 'aria-label': 'Día anterior', text: '←' }),
        selector,
        el('button', { type: 'button', onclick: () => { location.hash = '#/dia/' + sumarDias(fecha, 1); }, 'aria-label': 'Día siguiente', text: '→' }),
        el('button', { type: 'button', onclick: () => { location.hash = '#/dia/' + hoy(); }, text: 'Hoy' })),
      el('div', { class: 'separador' }), lista);
    try {
      const p = await get('/api/registros', { desde: fecha, hasta: fecha, orden: 'asc', limite: 100 });
      vaciar(lista);
      if (!p.registros.length) lista.append(el('p', { class: 'vacio', text: 'No hay registros este día.' }));
      else lista.append(el('div', { class: 'lista' }, p.registros.map(itemRegistro)));
    } catch (e) { manejar(e); }
  }

  // ------------------------------------------------------------ vista: buscar
  async function vistaBuscar(parametros) {
    const ps = new URLSearchParams(parametros || '');
    const campo = (nombre, etiqueta, tipo = 'text') => el('label', {}, etiqueta + ' ', el('input', { name: nombre, type: tipo, value: ps.get(nombre) || '', maxlength: tipo === 'text' ? 200 : null }));
    const selClase = el('select', { name: 'clase' }, ['', 'actividad', 'evento', 'problema', 'idea', 'decision', 'observacion'].map((c) => el('option', { value: c, selected: ps.get('clase') === c, text: c || 'cualquiera' })));
    const selCerteza = el('select', { name: 'certeza' }, ['', 'dicho', 'confirmado', 'interpretado', 'hipotesis'].map((c) => el('option', { value: c, selected: ps.get('certeza') === c, text: c || 'cualquiera' })));
    const resultado = el('div', {});
    const form = el('form', { class: 'tarjeta', onsubmit: (ev) => {
      ev.preventDefault();
      const d = new FormData(form); const q = new URLSearchParams();
      for (const [k, v] of d.entries()) if (String(v).trim()) q.set(k, String(v).trim());
      location.hash = '#/buscar' + (q.toString() ? '?' + q : '');
    } },
      el('div', { class: 'fila' }, campo('q', 'Texto'), campo('entidad', 'Entidad'), campo('proyecto', 'Proyecto')),
      el('div', { class: 'fila' }, campo('desde', 'Desde', 'date'), campo('hasta', 'Hasta', 'date'),
        el('label', {}, 'Clase ', selClase), el('label', {}, 'Certeza ', selCerteza)),
      el('div', { class: 'fila' }, el('button', { type: 'submit', class: 'principal', text: 'Buscar' })));
    cont.append(el('h2', { text: 'Buscar' }), form, resultado);
    if (![...ps.keys()].length) return;
    const pagina = Math.max(1, parseInt(ps.get('pagina') || '1', 10) || 1);
    resultado.append(el('p', { class: 'vacio', text: 'Buscando…' }));
    try {
      const filtros = Object.fromEntries([...ps.entries()].filter(([k]) => k !== 'pagina'));
      const p = await get('/api/registros', { ...filtros, pagina, limite: 25 });
      vaciar(resultado);
      if (!p.registros.length) { resultado.append(el('p', { class: 'vacio', text: 'No hay resultados.' })); return; }
      resultado.append(el('p', { class: 'meta', text: `${p.total} registro(s)` }), el('div', { class: 'lista' }, p.registros.map(itemRegistro)));
      const paginas = Math.ceil(p.total / p.limite);
      if (paginas > 1) {
        const ir = (n) => { const q = new URLSearchParams(ps); q.set('pagina', n); location.hash = '#/buscar?' + q; };
        resultado.append(el('div', { class: 'fila' }, el('button', { type: 'button', disabled: pagina <= 1, onclick: () => ir(pagina - 1), text: '← Anterior' }), el('span', { class: 'meta', text: `Página ${pagina} de ${paginas}` }), el('button', { type: 'button', disabled: pagina >= paginas, onclick: () => ir(pagina + 1), text: 'Siguiente →' })));
      }
    } catch (e) { vaciar(resultado); manejar(e); }
  }

  // ------------------------------------------------------------ vista: preguntas pendientes
  async function vistaPendientes() {
    const lista = el('div', {}, el('p', { class: 'vacio', text: 'Cargando…' }));
    cont.append(el('h2', { text: 'Preguntas de Mira' }), lista);
    try {
      const preguntas = await get('/api/preguntas');
      vaciar(lista);
      if (!preguntas.length) { lista.append(el('p', { class: 'vacio', text: 'No hay preguntas pendientes.' })); return; }
      for (const p of preguntas) lista.append(tarjetaDePregunta(p));
    } catch (e) { manejar(e); }
  }

  function tarjetaDePregunta(p) {
    const conflicto = p.otros.length > 0 && p.campo !== 'coincidencia';
    const valor = el('input', { type: 'text', placeholder: conflicto ? 'Valor correcto' : p.campo === 'coincidencia' ? 'misma / distinta' : 'Tu respuesta', maxlength: 500, 'aria-label': 'Respuesta' });
    let destino = null;
    if (conflicto) destino = el('select', { 'aria-label': 'Registro al que corresponde el valor' }, [p.registro, ...p.otros].map((r) => el('option', { value: r.id, text: `${r.titulo || r.id} (${r.valorActual})` })));
    const enviar = async (cuerpo, ruta) => {
      try { const r = await post(ruta, cuerpo); avisar(r.mensaje || 'Hecho.', 'ok'); tarjeta.remove(); actualizarContador(); } catch (e) { manejar(e); }
    };
    const tarjeta = el('div', { class: 'tarjeta' },
      el('p', {}, el('strong', { text: p.texto })),
      el('p', { class: 'meta' }, 'Registro: ', el('a', { href: '#/registro/' + p.registro.id, text: p.registro.titulo || p.registro.id }), ` · campo: ${p.campo} · valor actual: ${p.registro.valorActual}`),
      p.otros.map((o) => el('p', { class: 'meta' }, 'Con: ', el('a', { href: '#/registro/' + o.id, text: o.titulo || o.id }), ` · valor actual: ${o.valorActual}`)),
      el('div', { class: 'fila' }, valor, destino,
        el('button', { type: 'button', class: 'principal', text: 'Responder', onclick: () => enviar({ valor: valor.value, registro: destino ? destino.value : null }, `/api/preguntas/${p.id}/responder`) }),
        el('button', { type: 'button', text: 'No lo recuerdo', onclick: () => enviar({ estado: 'no_recuerda' }, `/api/preguntas/${p.id}/cerrar`) }),
        el('button', { type: 'button', text: 'No es relevante', onclick: () => enviar({ estado: 'no_relevante' }, `/api/preguntas/${p.id}/cerrar`) })));
    return tarjeta;
  }

  // ------------------------------------------------------------ vista: ficha
  async function vistaRegistro(id) {
    if (!ID_REG.test(id || '')) { cont.append(el('p', { class: 'vacio', text: 'Registro no válido.' })); return; }
    const caja = el('div', {}, el('p', { class: 'vacio', text: 'Cargando…' }));
    cont.append(caja);
    try { pintarFicha(caja, await get('/api/registros/' + id)); } catch (e) { vaciar(caja); caja.append(el('p', { class: 'vacio', text: e.estado === 404 ? 'No existe ese registro.' : 'No se pudo cargar.' })); manejar(e); }
  }

  function pintarFicha(caja, f) {
    vaciar(caja);
    const recargar = () => { vaciar(caja); caja.append(el('p', { class: 'vacio', text: 'Actualizando…' })); get('/api/registros/' + f.id).then((n) => pintarFicha(caja, n)).catch(manejar); };
    const filas = f.datos.map((d) => {
      const editor = el('span', { class: 'fila' });
      const abrir = () => {
        vaciar(editor);
        const entrada = el('input', { type: 'text', value: d.desconocido || d.campo === 'duracion' ? '' : d.texto, placeholder: FORMATOS[d.campo] || 'Nuevo valor', maxlength: 500, 'aria-label': 'Nuevo valor de ' + d.campo });
        editor.append(entrada,
          el('button', { type: 'button', class: 'principal', text: 'Guardar', onclick: async () => { try { const r = await post(`/api/registros/${f.id}/corregir`, { campo: d.campo, valor: entrada.value }); avisar(r.mensaje, 'ok'); recargar(); } catch (e) { manejar(e); } } }),
          el('button', { type: 'button', text: 'Cancelar', onclick: () => vaciar(editor) }));
        entrada.focus();
      };
      const rechazable = !d.desconocido && (d.certeza === 'interpretado' || d.certeza === 'hipotesis') && d.campo !== 'entidades';
      return [
        el('dt', { text: d.campo }),
        el('dd', {}, el('span', { text: d.texto }), d.desconocido ? null : etiquetaCerteza(d.certeza),
          el('button', { type: 'button', class: 'enlace', text: 'Corregir', onclick: abrir }),
          rechazable ? el('button', { type: 'button', class: 'enlace peligro', text: 'Rechazar', onclick: async () => { try { const r = await post(`/api/registros/${f.id}/rechazar`, { campo: d.campo }); avisar(r.mensaje, 'ok'); recargar(); } catch (e) { manejar(e); } } }) : null,
          editor)];
    });
    const bloqueCaptura = (c) => el('details', {}, el('summary', { text: `Nota original ${c.id} · ${c.origen}` }), el('p', { class: 'meta', text: c.comunicadaEl }), el('pre', { class: 'texto', text: c.texto }));
    caja.append(
      el('p', {}, el('a', { href: '#/dia', text: '← Volver' })),
      el('h2', { text: f.titulo }),
      el('p', { class: 'meta', text: `${f.clase} · comunicada ${f.comunicadaEl} · ${f.id}` }),
      f.descripcion ? el('p', { text: f.descripcion }) : null,
      el('h3', { text: 'Datos' }), el('div', { class: 'tarjeta' }, el('dl', { class: 'datos' }, filas)),
      f.conflictos.length ? [el('h3', { text: 'Conflictos' }), f.conflictos.map((c) => el('p', {}, `${c.estado} · ${c.campo} con `, el('a', { href: '#/registro/' + c.con, text: c.titulo || c.con })))] : null,
      f.relaciones.length ? [el('h3', { text: 'Relaciones' }), f.relaciones.map((r) => el('p', {}, `${r.tipo} (${r.certeza}) → `, el('a', { href: '#/registro/' + r.destino, text: r.titulo || r.destino })))] : null,
      el('h3', { text: 'De dónde sale' }),
      f.fuentes.map((s) => el('div', { class: 'tarjeta' }, el('p', { class: 'meta', text: `${s.rol} · ${s.captura}` }), s.cita ? el('div', { class: 'cita', text: s.cita }) : null)),
      f.capturas.map(bloqueCaptura),
      f.rechazos.length ? [el('h3', { text: 'Interpretaciones rechazadas' }), f.rechazos.map((r) => el('p', { class: 'meta', text: `${r.campo}: ${r.valor} (era ${r.certeza_original})` }))] : null,
      f.historial.length ? [el('h3', { text: 'Historial' }), f.historial.map((h) => el('p', { class: 'meta', text: `${h.fecha} · ${h.origen} · ${h.campo}` }))] : null);
  }

  // ------------------------------------------------------------ enrutado
  function marcarMenu(vista) { document.querySelectorAll('#menu a').forEach((a) => a.classList.toggle('activa', a.dataset.vista === vista)); }

  async function enrutar() {
    vaciar(cont);
    if (!(await acceso())) { mostrarAcceso(); return; }
    $('menu').hidden = false;
    const m = (location.hash.replace(/^#/, '') || '/dia').match(/^\/([^/?]*)(?:[/?](.*))?$/) || [];
    const vista = m[1] || 'dia', resto = m[2];
    marcarMenu(vista);
    actualizarContador();
    if (vista === 'buscar') await vistaBuscar(resto || '');
    else if (vista === 'pendientes') await vistaPendientes();
    else if (vista === 'registro') await vistaRegistro(resto);
    else await vistaDia(vista === 'dia' ? resto : null);
    cont.focus({ preventScroll: true });
  }

  function mostrarAcceso(mensaje) {
    $('menu').hidden = true;
    vaciar(cont);
    cont.append(el('h2', { text: 'Acceso' }), el('p', { text: 'Inicia sesión con tu cuenta de Google para ver tus registros.' }),
      el('button', { type: 'button', class: 'principal', text: 'Entrar con Google', onclick: entrar }),
      mensaje ? el('p', { class: 'error', role: 'alert', text: mensaje }) : null);
  }

  $('salir').addEventListener('click', () => { escribirLS(CLAVE_SESION, null); mostrarAcceso(); });
  window.addEventListener('hashchange', enrutar);

  (async () => {
    const vuelta = leerFragmento();
    await cargarConfig();
    if (vuelta && vuelta.error) { mostrarAcceso('No se pudo iniciar sesión: ' + vuelta.error); return; }
    if (await acceso()) {
      try { await get('/api/estado'); } catch (e) {
        if (e.estado === 403) { escribirLS(CLAVE_SESION, null); mostrarAcceso('Esta cuenta de Google no tiene acceso a ORUMI.'); return; }
        if (e.estado === 401) { mostrarAcceso(); return; }
      }
    }
    enrutar();
  })();
})();
