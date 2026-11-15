'use strict';
(() => {
  const { Icono, Logo } = window.OrumiIconos;
  const $ = (id) => document.getElementById(id);
  const cont = $('contenido');
  const reducido = matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ------------------------------------------------------------ utilidades (todo texto se inserta como texto, nunca como HTML)
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
  const entra = (nodo, i) => { nodo.classList.add('entra'); nodo.style.setProperty('--i', Math.min(i, 14)); return nodo; };
  const ic = (n, o) => Icono(n, o);
  const boton = (texto, icono, props = {}) => el('button', { type: 'button', ...props, class: 'boton ' + (props.class || '') }, icono ? ic(icono) : null, texto ? el('span', { text: texto }) : null);

  let temporizador = null;
  function avisar(texto, clase) {
    const a = $('aviso'); a.textContent = texto || ''; a.className = 'aviso ' + (clase || '');
    clearTimeout(temporizador);
    if (texto) temporizador = setTimeout(() => { a.textContent = ''; }, clase === 'mal' ? 7000 : 3500);
  }

  const pad = (n) => String(n).padStart(2, '0');
  const iso = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  const hoy = () => iso(new Date());
  const aFecha = (s) => { const [a, m, d] = s.split('-').map(Number); return new Date(a, m - 1, d); };
  const sumarDias = (f, n) => { const d = aFecha(f); d.setDate(d.getDate() + n); return iso(d); };
  const fmtLargo = new Intl.DateTimeFormat('es-ES', { weekday: 'long', day: 'numeric', month: 'long' });
  const fmtAnio = new Intl.DateTimeFormat('es-ES', { year: 'numeric' });
  const fmtCorto = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short' });
  const fmtMomento = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  const momento = (s) => { const d = new Date(s); return isNaN(d) ? String(s) : fmtMomento.format(d); };
  const ID_REG = /^reg-\d{8}-[a-z0-9]{4}$/;

  const CAMPOS = { titulo: 'Título', fecha: 'Fecha', hora: 'Hora', duracion: 'Duración', personas: 'Personas', entidades: 'Entidades', lugares: 'Lugares', proyectos: 'Proyectos', herramientas: 'Herramientas', contexto: 'Contexto', acciones: 'Acciones', resultados: 'Resultados', problemas: 'Problemas', ideas: 'Ideas', multidia: 'Varios días' };
  // Preguntas que no son de un campo del registro: su etiqueta y el icono con que se pintan.
  const CAMPOS_EXTRA = { ciudad: ['Ciudad', 'lugares'], contraste: ['Contraste con tus hipótesis', 'hipotesis'] };
  const CLASES = { actividad: 'Actividad', evento: 'Evento', idea: 'Idea', decision: 'Decisión', problema: 'Problema', otro: 'Otro' };
  const CERTEZAS = { dicho: 'Dicho por ti', confirmado: 'Confirmado', interpretado: 'Interpretado', hipotesis: 'Hipótesis', recomendacion: 'Recomendación' };
  const FORMATOS = { fecha: 'AAAA-MM-DD o AAAA-MM-DD..AAAA-MM-DD', hora: 'HH:MM', duracion: '90, 2h, 1h30', entidades: 'Nombre:tipo, Nombre:tipo' };

  function resumen(v) {
    if (v === null || v === undefined) return 'desconocido';
    if (typeof v === 'string') return v;
    if (Array.isArray(v)) return v.map(resumen).join(', ');
    if (v.desde && v.hasta) return v.desde === v.hasta ? v.desde : `${v.desde}..${v.hasta}`;
    if (v.nombre) return v.tipo ? `${v.nombre}:${v.tipo}` : v.nombre;
    if (v.estado) return v.estado + (v.fin ? ' · fin ' + resumen(v.fin) : '');
    if (v.valor) return v.valor;
    if (v.minutos) return `${v.expresion || ''} (${v.minutos} min)`.trim();
    return v.expresion || JSON.stringify(v);
  }

  // ------------------------------------------------------------ componentes visuales
  const chipCerteza = (c) => el('span', { class: 'chip c-' + (c || 'desconocido') }, ic(c || 'desconocido'), CERTEZAS[c] || 'Sin certeza');
  const chipClase = (c) => el('span', { class: 'chip clase' }, ic(c || 'otro'), CLASES[c] || c || 'Otro');
  const claseK = (c) => 'k-' + (CLASES[c] ? c : 'otro');

  function contar(nodo, hasta) {
    if (reducido || hasta < 2) { nodo.textContent = hasta; return; }
    const t0 = performance.now();
    const paso = (t) => { const p = Math.min(1, (t - t0) / 700); nodo.textContent = Math.round(hasta * (1 - Math.pow(1 - p, 3))); if (p < 1) requestAnimationFrame(paso); };
    requestAnimationFrame(paso);
  }

  function esqueletos(n = 3) { return el('div', { class: 'lista-esqueleto' }, Array.from({ length: n }, (_, i) => entra(el('div', { class: 'esqueleto fila-e' }), i))); }
  const estadoVacio = (texto, ...extra) => el('div', { class: 'vacio' }, ic('vacio'), el('p', { text: texto }), ...extra);

  // foco luminoso que sigue al puntero (una sola escucha, con rAF)
  let pendiente = null;
  document.addEventListener('pointermove', (e) => {
    const t = e.target.closest && e.target.closest('.foco-luz');
    if (!t || pendiente) return;
    pendiente = requestAnimationFrame(() => { const r = t.getBoundingClientRect(); t.style.setProperty('--mx', (e.clientX - r.left) + 'px'); t.style.setProperty('--my', (e.clientY - r.top) + 'px'); pendiente = null; });
  }, { passive: true });

  // ------------------------------------------------------------ sesión (Google a través del GoTrue de la plataforma)
  const CLAVE_SESION = 'orumi_sesion', CLAVE_AUTH = 'orumi_auth', CLAVE_API = 'orumi_panel_api';
  const leerLS = (k) => { try { return JSON.parse(localStorage.getItem(k)); } catch { return null; } };
  const escribirLS = (k, v) => { try { v === null ? localStorage.removeItem(k) : localStorage.setItem(k, JSON.stringify(v)); } catch { /* sin almacenamiento */ } };
  const sesion = () => leerLS(CLAVE_SESION);
  let urlAuth = leerLS(CLAVE_AUTH), urlApi = leerLS(CLAVE_API);

  async function cargarConfig() {
    try {
      const r = await fetch('config.json');
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
  function manejar(e) { if (e.estado === 401) { mostrarAcceso(); return; } avisar(e.message, 'mal'); }

  // ------------------------------------------------------------ barra y navegación
  function pintarBarra() {
    const barra = $('barra');
    if (!$('marca').firstChild) $('marca').append(Logo(30), el('span', { text: 'ORUMI' }));
    const nav = (id, icono, texto) => { const a = $(id); if (!a.firstChild) a.append(ic(icono), el('span', { text: texto })); if (id === 'nav-pendientes') a.append(el('span', { class: 'contador', id: 'contador', hidden: true })); };
    nav('nav-dia', 'dia', 'Día'); nav('nav-buscar', 'buscar', 'Buscar'); nav('nav-pendientes', 'pregunta', 'Preguntas'); nav('nav-papelera', 'papelera', 'Papelera'); nav('nav-descubrimientos', 'spark', 'Descubrimientos');
    const acc = $('acciones');
    if (!acc.firstChild) {
      const tema = el('button', { type: 'button', class: 'boton fantasma icono-solo', 'aria-label': 'Cambiar tema' });
      const pintarTema = () => { vaciar(tema); tema.append(ic(window.OrumiTema.actual() === 'dark' ? 'sol' : 'luna')); };
      tema.addEventListener('click', () => { window.OrumiTema.alternar(); pintarTema(); });
      pintarTema();
      acc.append(tema,
        el('a', { href: '/', class: 'boton fantasma icono-solo', 'aria-label': 'Ir a la captura', title: 'Captura' }, ic('captura')),
        el('button', { type: 'button', class: 'boton fantasma icono-solo', 'aria-label': 'Cerrar sesión', title: 'Cerrar sesión', onclick: () => { escribirLS(CLAVE_SESION, null); mostrarAcceso(); } }, ic('salir')));
    }
    barra.hidden = false;
  }

  function moverIndicador(vista) {
    const ind = $('indicador'); const activo = document.querySelector(`#menu a[data-vista="${vista}"]`);
    document.querySelectorAll('#menu a').forEach((a) => a.classList.toggle('activa', a === activo));
    if (!activo) { ind.classList.remove('lista'); return; }
    ind.style.setProperty('--x', (activo.offsetLeft - 4) + 'px'); ind.style.setProperty('--w', activo.offsetWidth + 'px');
    requestAnimationFrame(() => ind.classList.add('lista'));
  }
  window.addEventListener('resize', () => moverIndicador(vistaActual));

  async function actualizarContador() {
    try {
      const e = await get('/api/estado'); const c = $('contador');
      if (!c) return;
      c.hidden = !e.preguntasPendientes; if (e.preguntasPendientes) contar(c, e.preguntasPendientes);
    } catch { /* accesorio */ }
  }

  // ------------------------------------------------------------ elementos de lista
  function tarjetaRegistro(r, i, conFecha) {
    const hora = r.hora || (r.desde && r.hasta && r.desde !== r.hasta ? `${fmtCorto.format(aFecha(r.desde))}` : '');
    const a = el('a', { class: `registro cristal foco-luz ${claseK(r.clase)}`, href: '#/registro/' + r.id },
      conFecha ? null : el('span', { class: 'hora', text: hora || '--:--' }),
      el('div', { class: 'cuerpo' },
        conFecha && r.desde ? el('div', { class: 'cuando', text: fmtCorto.format(aFecha(r.desde)) + (r.hora ? ' · ' + r.hora : '') }) : null,
        el('h3', { text: r.titulo }),
        el('div', { class: 'chips' }, chipClase(r.clase), r.certezaFecha ? chipCerteza(r.certezaFecha) : null, r.enConflicto ? el('span', { class: 'chip c-conflicto' }, ic('conflicto'), 'Conflicto') : null)));
    return entra(a, i);
  }

  // ------------------------------------------------------------ vista: día
  async function vistaDia(fecha) {
    fecha = /^\d{4}-\d{2}-\d{2}$/.test(fecha || '') ? fecha : hoy();
    const d = aFecha(fecha);
    const lunes = sumarDias(fecha, -((d.getDay() + 6) % 7)); const domingo = sumarDias(lunes, 6);
    const selector = el('input', { type: 'date', value: fecha, 'aria-label': 'Elegir día', class: 'selector-fecha' });
    selector.addEventListener('change', () => { if (selector.value) location.hash = '#/dia/' + selector.value; });
    const irA = (f) => () => { location.hash = '#/dia/' + f; };
    const semana = el('div', { class: 'semana', role: 'group', 'aria-label': 'Semana' });
    const lista = el('div', {}, esqueletos(3));
    const fecha_txt = fmtLargo.format(d);
    cont.append(
      el('div', { class: 'cabecera' },
        el('div', {}, el('p', { class: 'sobretitulo', text: 'Memoria del día · ' + fmtAnio.format(d) }), el('h2', { class: 'titulo' }, el('span', { class: 'grad', text: fecha_txt.charAt(0).toUpperCase() + fecha_txt.slice(1) }))),
        el('div', { class: 'fila' }, boton('', 'izq', { class: 'icono-solo', 'aria-label': 'Día anterior', onclick: irA(sumarDias(fecha, -1)) }), selector,
          boton('', 'der', { class: 'icono-solo', 'aria-label': 'Día siguiente', onclick: irA(sumarDias(fecha, 1)) }), boton('Hoy', null, { onclick: irA(hoy()) }))),
      semana, lista);

    const dias = ['L', 'M', 'X', 'J', 'V', 'S', 'D'];
    const pintarSemana = (cuentas) => {
      vaciar(semana);
      for (let i = 0; i < 7; i++) {
        const f = sumarDias(lunes, i); const n = cuentas[f] || 0;
        const b = el('button', { type: 'button', class: 'dia-sem' + (f === fecha ? ' activo' : '') + (f === hoy() ? ' hoy' : ''), 'aria-label': `${fmtLargo.format(aFecha(f))}, ${n} registros`, 'aria-pressed': f === fecha ? 'true' : 'false', onclick: irA(f) },
          el('small', { text: dias[i] }), el('b', { text: aFecha(f).getDate() }), el('span', { class: 'puntos' }, Array.from({ length: Math.min(n, 4) }, () => el('i'))));
        semana.append(entra(b, i));
      }
    };
    pintarSemana({});
    get('/api/dias', { desde: lunes, hasta: domingo }).then((r) => pintarSemana(Object.fromEntries(r.dias.map((x) => [x.fecha, x.total])))).catch(() => {});

    try {
      const p = await get('/api/registros', { desde: fecha, hasta: fecha, orden: 'asc', limite: 100 });
      vaciar(lista);
      if (!p.registros.length) {
        const vacio = estadoVacio('No hay registros este día.');
        lista.append(vacio);
        get('/api/dias', { desde: sumarDias(fecha, -90), hasta: sumarDias(fecha, -1) }).then((r) => {
          const previo = r.dias.filter((x) => x.total > 0).map((x) => x.fecha).sort().pop();
          if (previo) vacio.append(boton('Ir al último día con registros', 'izq', { onclick: irA(previo) }));
        }).catch(() => {});
        return;
      }
      const ul = el('ul', { class: 'tiempo' });
      p.registros.forEach((r, i) => { const li = el('li', { class: claseK(r.clase) }, el('span', { class: 'nodo' }, ic(r.clase || 'otro')), tarjetaRegistro(r, i)); ul.append(li); });
      lista.append(ul);
    } catch (e) { vaciar(lista); manejar(e); }
  }

  // ------------------------------------------------------------ vista: buscar
  async function vistaBuscar(parametros) {
    const ps = new URLSearchParams(parametros || '');
    const campo = (nombre, etiqueta, tipo = 'text') => el('label', {}, etiqueta, el('input', { name: nombre, type: tipo, value: ps.get(nombre) || '', maxlength: tipo === 'text' ? 200 : null }));
    const opciones = (nombre, lista) => el('select', { name: nombre }, lista.map((c) => el('option', { value: c, selected: ps.get(nombre) === c, text: c ? (CLASES[c] || CERTEZAS[c] || c) : 'Cualquiera' })));
    const resultado = el('div', { class: 'resultado' });
    const form = el('form', { class: 'buscador cristal', onsubmit: (ev) => {
      ev.preventDefault();
      const q = new URLSearchParams();
      for (const [k, v] of new FormData(form).entries()) if (String(v).trim()) q.set(k, String(v).trim());
      location.hash = '#/buscar' + (q.toString() ? '?' + q : '');
    } },
      el('div', { class: 'entrada-grande' }, ic('buscar'), el('input', { name: 'q', type: 'search', value: ps.get('q') || '', maxlength: 200, placeholder: 'Busca en tus registros: personas, lugares, temas…', 'aria-label': 'Texto a buscar' })),
      el('details', { class: 'filtros', open: ['entidad', 'proyecto', 'desde', 'hasta', 'clase', 'certeza'].some((k) => ps.get(k)) },
        el('summary', {}, ic('filtros'), 'Filtros'),
        el('div', { class: 'rejilla-filtros' }, campo('entidad', 'Entidad'), campo('proyecto', 'Proyecto'), campo('desde', 'Desde', 'date'), campo('hasta', 'Hasta', 'date'),
          el('label', {}, 'Clase', opciones('clase', ['', ...Object.keys(CLASES)])), el('label', {}, 'Certeza', opciones('certeza', ['', 'dicho', 'confirmado', 'interpretado', 'hipotesis'])))),
      el('div', { class: 'fila' }, boton('Buscar', 'buscar', { type: 'submit', class: 'principal' })));
    cont.append(el('div', { class: 'cabecera' }, el('div', {}, el('p', { class: 'sobretitulo', text: 'Todo lo que has contado' }), el('h2', { class: 'titulo' }, el('span', { class: 'grad', text: 'Buscar' })))), form, resultado);
    if (![...ps.keys()].length) { resultado.append(estadoVacio('Escribe algo o usa los filtros para empezar.')); return; }
    const pagina = Math.max(1, parseInt(ps.get('pagina') || '1', 10) || 1);
    resultado.append(esqueletos(3));
    try {
      const filtros = Object.fromEntries([...ps.entries()].filter(([k]) => k !== 'pagina'));
      const p = await get('/api/registros', { ...filtros, pagina, limite: 25 });
      vaciar(resultado);
      if (!p.registros.length) { resultado.append(estadoVacio('No hay resultados con esos criterios.')); return; }
      resultado.append(el('p', { class: 'meta', text: `${p.total} registro${p.total === 1 ? '' : 's'}` }), el('div', { class: 'tiempo-plano' }, p.registros.map((r, i) => el('div', { class: 'separador-item' }, tarjetaRegistro(r, i, true)))));
      const paginas = Math.ceil(p.total / p.limite);
      if (paginas > 1) {
        const ir = (n) => () => { const q = new URLSearchParams(ps); q.set('pagina', n); location.hash = '#/buscar?' + q; };
        resultado.append(el('div', { class: 'paginacion' }, boton('Anterior', 'izq', { disabled: pagina <= 1, onclick: ir(pagina - 1) }), el('span', { class: 'meta', text: `Página ${pagina} de ${paginas}` }), boton('Siguiente', 'der', { disabled: pagina >= paginas, onclick: ir(pagina + 1) })));
      }
    } catch (e) { vaciar(resultado); manejar(e); }
  }

  // ------------------------------------------------------------ vista: preguntas pendientes
  async function vistaPendientes() {
    const lista = el('div', {}, esqueletos(2));
    cont.append(el('div', { class: 'cabecera' }, el('div', {}, el('p', { class: 'sobretitulo', text: 'Lo que Mira necesita saber' }), el('h2', { class: 'titulo' }, el('span', { class: 'grad', text: 'Preguntas' })))), lista);
    try {
      const preguntas = await get('/api/preguntas');
      vaciar(lista);
      if (!preguntas.length) { lista.append(estadoVacio('Todo al día: no hay preguntas pendientes.')); return; }
      preguntas.forEach((p, i) => lista.append(entra(tarjetaDePregunta(p), i)));
    } catch (e) { manejar(e); }
  }

  function tarjetaDePregunta(p) {
    const conflicto = p.otros.length > 0 && p.campo !== 'coincidencia';
    const valor = el('input', { type: 'text', placeholder: conflicto ? 'Valor correcto' : p.campo === 'coincidencia' ? 'misma o distinta' : (FORMATOS[p.campo] ? FORMATOS[p.campo] : 'Tu respuesta'), maxlength: 500, 'aria-label': 'Respuesta' });
    const destino = conflicto ? el('select', { 'aria-label': 'Registro al que corresponde el valor' }, [p.registro, ...p.otros].map((r) => el('option', { value: r.id, text: `${r.titulo || r.id} (${r.valorActual})` }))) : null;
    const enviar = async (cuerpo, ruta) => {
      try {
        const r = await post(ruta, cuerpo); avisar(r.mensaje || 'Hecho.', 'ok');
        tarjeta.classList.add('sale'); setTimeout(() => { tarjeta.remove(); actualizarContador(); }, reducido ? 0 : 340);
      } catch (e) { manejar(e); }
    };
    const registroLink = (r) => el('a', { href: '#/registro/' + r.id, text: r.titulo || r.id });
    const tarjeta = el('div', { class: 'pregunta cristal foco-luz' },
      el('p', { class: 'texto-pregunta', text: p.texto }),
      el('div', { class: 'contexto' },
        el('div', {}, ic('nota'), 'Registro: ', registroLink(p.registro)),
        el('div', {}, ic(p.campo === 'coincidencia' ? 'enlace' : (CAMPOS_EXTRA[p.campo] ? CAMPOS_EXTRA[p.campo][1] : p.campo)), `Campo: ${CAMPOS[p.campo] || (CAMPOS_EXTRA[p.campo] ? CAMPOS_EXTRA[p.campo][0] : p.campo)}`, el('span', { class: 'chip c-neutro' }, 'Valor actual: ' + p.registro.valorActual)),
        p.otros.map((o) => el('div', {}, ic('enlace'), 'Con: ', registroLink(o), el('span', { class: 'chip c-neutro' }, 'Valor actual: ' + o.valorActual)))),
      el('div', { class: 'respuesta' + (conflicto ? ' conflicto' : '') }, valor, destino,
        boton('Responder', 'enviar', { class: 'principal', onclick: () => enviar({ valor: valor.value, registro: destino ? destino.value : null }, `/api/preguntas/${p.id}/responder`) }),
        el('div', { class: 'opciones' },
          boton('No lo recuerdo', 'historial', { class: 'fantasma pequeno', onclick: () => enviar({ estado: 'no_recuerda' }, `/api/preguntas/${p.id}/cerrar`) }),
          boton('No es relevante', 'cerrar', { class: 'fantasma pequeno', onclick: () => enviar({ estado: 'no_relevante' }, `/api/preguntas/${p.id}/cerrar`) }),
          boton('No sucedió', 'rechazar', { class: 'fantasma pequeno', onclick: () => enviar({ estado: 'no_sucedio' }, `/api/preguntas/${p.id}/cerrar`) }))));
    return tarjeta;
  }

  // ------------------------------------------------------------ gestión: confirmación en dos pasos, fusión y papelera
  /** Botón que pide confirmación en la misma pantalla (sin diálogos del navegador). */
  function conConfirmacion(texto, icono, clase, pregunta, ok) {
    const caja = el('span', { class: 'fila' });
    const inicial = () => { vaciar(caja); caja.append(boton(texto, icono, { class: clase, onclick: preguntar })); };
    const preguntar = () => {
      vaciar(caja);
      caja.append(el('span', { class: 'meta', text: pregunta }), boton('Confirmar', 'check', { class: 'principal pequeno', onclick: ok }),
        boton('Cancelar', 'cerrar', { class: 'fantasma pequeno', onclick: inicial }));
    };
    inicial();
    return caja;
  }

  /** Selector para fusionar otro registro en el actual: busca, elige y confirma. */
  function selectorDeFusion(f, hecho) {
    const caja = el('div', { class: 'fusion cristal' });
    const entrada = el('input', { type: 'search', placeholder: 'Busca el registro que quieres traer a este…', maxlength: 200, 'aria-label': 'Buscar registro a fusionar' });
    const resultados = el('div', { class: 'enlaces' });
    const elegido = el('div', {});
    let t = null;
    const buscar = async () => {
      vaciar(resultados);
      if (!entrada.value.trim()) return;
      try {
        const p = await get('/api/registros', { q: entrada.value.trim(), limite: 8 });
        const otros = p.registros.filter((x) => x.id !== f.id);
        if (!otros.length) { resultados.append(el('span', { class: 'meta', text: 'Sin resultados.' })); return; }
        for (const x of otros) {
          resultados.append(el('button', { type: 'button', class: 'chip c-neutro elegir', onclick: () => elegir(x) }, ic(x.clase || 'otro'), x.titulo, el('span', { class: 'meta', text: x.desde ? ' · ' + fmtCorto.format(aFecha(x.desde)) : '' })));
        }
      } catch (e) { manejar(e); }
    };
    const elegir = (x) => {
      vaciar(elegido);
      elegido.append(el('p', { class: 'meta', text: `«${x.titulo}» se unirá a este registro y quedará como fusionado. Podrás verlo en la ficha de este.` }),
        el('div', { class: 'fila' },
          boton('Fusionar', 'fusionar', { class: 'principal pequeno', onclick: async () => { try { const r2 = await post(`/api/registros/${f.id}/fusionar`, { origen: x.id }); avisar(r2.mensaje, 'ok'); hecho(); } catch (e) { manejar(e); } } }),
          boton('Cancelar', 'cerrar', { class: 'fantasma pequeno', onclick: () => vaciar(elegido) })));
    };
    entrada.addEventListener('input', () => { clearTimeout(t); t = setTimeout(buscar, 300); });
    caja.append(entrada, resultados, elegido);
    return caja;
  }

  async function vistaPapelera() {
    const lista = el('div', {}, esqueletos(2));
    cont.append(el('div', { class: 'cabecera' }, el('div', {}, el('p', { class: 'sobretitulo', text: 'Nada se pierde' }), el('h2', { class: 'titulo' }, el('span', { class: 'grad', text: 'Papelera' })))), lista);
    try {
      const p = await get('/api/papelera');
      vaciar(lista);
      if (!p.registros.length) { lista.append(estadoVacio('La papelera está vacía.')); return; }
      p.registros.forEach((r, i) => {
        const fila = el('div', { class: `registro papelera-fila cristal ${claseK(r.clase)}` },
          el('div', { class: 'cuerpo' }, el('h3', {}, el('a', { href: '#/registro/' + r.id, text: r.titulo })), el('div', { class: 'chips' }, chipClase(r.clase), el('span', { class: 'meta', text: 'Eliminado ' + momento(r.eliminadoEl) }))),
          boton('Restaurar', 'deshacer', { class: 'pequeno', onclick: async () => { try { const r2 = await post(`/api/registros/${r.id}/restaurar`, {}); avisar(r2.mensaje, 'ok'); fila.classList.add('sale'); setTimeout(() => fila.remove(), reducido ? 0 : 340); } catch (e) { manejar(e); } } }));
        lista.append(entra(fila, i));
      });
    } catch (e) { manejar(e); }
  }

  // ------------------------------------------------------------ vista: descubrimientos (revisión semanal y tus hipótesis)
  const VEREDICTOS = {
    apoyada: ['Apoyada por los datos', 'c-confirmado', 'check'],
    sin_apoyo: ['Sin apoyo en los datos', 'c-desconocido', 'rechazar'],
    insuficiente: ['Evidencia insuficiente', 'c-interpretado', 'historial'],
  };
  const REVISIONES = { confirmada: 'Lo confirmaste', matizada: 'Lo matizaste', descartada: 'Lo descartaste' };
  const FACTORES = [
    ['clima:uv_alto', 'Índice UV alto'], ['clima:lluvia', 'Lluvia'], ['clima:soleado', 'Día soleado o despejado'],
    ['registro:socializacion', 'Socializar'], ['registro:exterior>=90', 'Más de 90 min al aire libre'],
  ];
  const pct = (x) => (x === undefined || x === null || x === 'desconocido' || x === '') ? '—' : Math.round(Number(x) * 100) + ' %';
  const lista = (v) => (Array.isArray(v) ? v : []);
  const slug = (t) => t.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '').slice(0, 40);

  function tarjetaDeContraste(c) {
    const [hip, con] = c.id.split('#');
    const v = VEREDICTOS[c.veredicto] || VEREDICTOS.insuficiente;
    const r = c.resumen || {};
    const accion = async (ruta, cuerpo) => {
      try { const x = await post(`/api/hipotesis/${hip}/${ruta}`, { contraste: con, ...cuerpo }); avisar(x.mensaje || 'Hecho.', 'ok'); enrutar(); } catch (e) { manejar(e); }
    };
    const comentario = el('input', { type: 'text', maxlength: 500, placeholder: 'Cómo lo matizas, con tus palabras', 'aria-label': 'Comentario para matizar' });
    const dias = lista(c.evidencia);
    const evidencia = dias.length ? el('details', { class: 'nota-original' },
      el('summary', {}, ic('nota'), `Ver los ${dias.length} día(s) en que se basa`),
      el('div', { class: 'evidencia' }, dias.map((e) => el('div', { class: 'dia-evidencia' },
        el('strong', { text: `${fmtCorto.format(aFecha(e.fecha))} · ${e.ciudad}` }),
        el('span', { class: 'meta', text: ` · factor: ${e.factor === 'si' ? 'sí' : 'no'} · efecto: ${e.efecto} · ${e.clima}` }),
        lista(e.registros).map((id) => ID_REG.test(id) ? el('a', { href: '#/registro/' + id, text: ' ' + id }) : null),
        lista(e.citas).map((q) => el('div', { class: 'meta', text: `«${q}»` })))))) : null;
    const revision = c.revision && c.revision.estado ? el('p', { class: 'meta' }, ic('dicho'), `${REVISIONES[c.revision.estado] || c.revision.estado}` + (c.revision.comentario && c.revision.comentario !== 'ninguno' ? `: «${c.revision.comentario}»` : '.')) : null;
    return el('div', { class: 'pregunta cristal foco-luz contraste' },
      el('p', { class: 'meta', text: 'Tu hipótesis' }),
      el('p', { class: 'texto-pregunta', text: `«${c.formulacion}»` }),
      el('div', { class: 'chips' }, el('span', { class: 'chip ' + v[1] }, ic(v[2]), v[0]),
        el('span', { class: 'chip c-neutro' }, 'Factor: ' + c.factor), el('span', { class: 'chip c-neutro' }, 'Efecto: ' + String(c.efecto).replaceAll('_', ' ')),
        c.reabierto === 'true' ? el('span', { class: 'chip c-hipotesis' }, 'Reabierto: evidencia nueva') : null),
      el('p', { text: c.texto }),
      el('div', { class: 'cifras' },
        el('span', {}, el('strong', { text: `${r.efecto_con_factor ?? 0} de ${r.dias_con_factor ?? 0}` }), ` con el factor (${pct(r.tasa_con_factor)})`),
        el('span', {}, el('strong', { text: `${r.efecto_sin_factor ?? 0} de ${r.dias_sin_factor ?? 0}` }), ` sin él (${pct(r.tasa_sin_factor)})`)),
      revision, evidencia,
      el('div', { class: 'respuesta' }, comentario,
        boton('Matizar', 'editar', { class: 'pequeno', onclick: () => { if (!comentario.value.trim()) { avisar('Para matizar escribe cómo, con tus palabras.', 'mal'); return; } accion('matizar', { comentario: comentario.value.trim() }); } }),
        el('div', { class: 'opciones' },
          boton('Confirmar', 'check', { class: 'fantasma pequeno', onclick: () => accion('confirmar', {}) }),
          conConfirmacion('Descartar', 'cerrar', 'fantasma pequeno', '¿Descartar este contraste?', () => accion('descartar', {})))));
  }

  function formularioDeHipotesis() {
    const t = el('input', { type: 'text', maxlength: 400, placeholder: 'Con tus palabras: «el calor me quita el apetito»', 'aria-label': 'Tu hipótesis' });
    const f = el('select', { 'aria-label': 'Factor' }, FACTORES.map(([v, n]) => el('option', { value: v, text: n })));
    const e = el('input', { type: 'text', maxlength: 40, placeholder: 'Efecto, en pocas palabras (p. ej. sin apetito)', 'aria-label': 'Efecto' });
    const d = el('input', { type: 'text', maxlength: 300, placeholder: 'Cómo lo dirías tú cuando ocurre (para que Mira lo reconozca)', 'aria-label': 'Descripción del efecto' });
    const a = el('input', { type: 'number', min: 0, max: 14, value: 0, 'aria-label': 'Retardo desde (días)' });
    const b = el('input', { type: 'number', min: 0, max: 14, value: 0, 'aria-label': 'Retardo hasta (días)' });
    return el('details', { class: 'filtros' },
      el('summary', {}, ic('editar'), 'Declarar una hipótesis'),
      el('div', { class: 'rejilla-mas' },
        el('label', {}, 'Tu hipótesis', t), el('label', {}, 'Factor', f), el('label', {}, 'Efecto', e), el('label', {}, 'Cómo reconocerlo', d),
        el('label', {}, 'Retardo: de (días)', a), el('label', {}, 'a (días)', b),
        boton('Declarar', 'enviar', { class: 'principal', onclick: async () => {
          try {
            const r = await post('/api/hipotesis', { formulacion: t.value.trim(), factor: f.value, efecto: slug(e.value), descripcionEfecto: d.value.trim(), retardoDesde: Number(a.value), retardoHasta: Number(b.value) });
            avisar(r.mensaje || 'Hipótesis declarada.', 'ok'); enrutar();
          } catch (x) { manejar(x); }
        } })));
  }

  async function vistaDescubrimientos() {
    const caja = el('div', {}, esqueletos(2));
    cont.append(el('div', { class: 'cabecera' }, el('div', {}, el('p', { class: 'sobretitulo', text: 'Lo que dicen tus datos' }), el('h2', { class: 'titulo' }, el('span', { class: 'grad', text: 'Descubrimientos' }))),
      el('p', { class: 'meta', text: 'Mira contrasta tus hipótesis con tus registros y con el clima, una vez por semana. Describe lo observado: no indica causas ni aconseja.' })), caja);
    try {
      const d = await get('/api/descubrimientos');
      vaciar(caja);
      const r = d.revision;
      if (!r) { caja.append(estadoVacio('Aún no hay revisión semanal: Mira la genera la primera noche de cada semana.')); caja.append(formularioDeHipotesis()); return; }
      const per = r.periodo || {};
      caja.append(el('p', { class: 'meta', text: `${r.id} · del ${per.desde} al ${per.hasta} · generada ${momento(r.generada_el)}` }));
      if (r.estado === 'observacion') {
        const o = r.observacion || {};
        const hecho = Math.min(Number(o.dias_transcurridos) || 0, Number(o.dias_necesarios) || 28), total = Number(o.dias_necesarios) || 28;
        const barra = el('div', { class: 'progreso', role: 'progressbar', 'aria-valuemin': 0, 'aria-valuemax': total, 'aria-valuenow': hecho, 'aria-label': 'Etapa de observación' }, el('div', { class: 'progreso-lleno' }));
        barra.firstChild.style.setProperty('--p', Math.round(hecho / total * 100) + '%');
        caja.append(entra(el('div', { class: 'pregunta cristal' },
          el('p', { class: 'texto-pregunta', text: 'Mira está observando antes de sacar conclusiones' }),
          el('p', { text: `Lleva ${hecho} de ${total} días desde tu primer registro (${o.primer_registro}), con ${o.dias_con_registros} día(s) con registros. Hasta entonces no hay contrastes: un patrón no se afirma con evidencias aisladas.` }), barra), 0));
      } else {
        const cs = lista(r.contrastes);
        if (!cs.length) caja.append(estadoVacio('Esta semana no hay contrastes que mostrar.'));
        cs.forEach((c, i) => caja.append(entra(tarjetaDeContraste(c), i + 1)));
        if (lista(r.descartados).length) caja.append(el('p', { class: 'meta', text: `${lista(r.descartados).length} contraste(s) descartado(s) por ti, sin evidencia nueva sustancial.` }));
      }
      const sug = lista(r.sugerencias);
      if (sug.length) caja.append(el('div', { class: 'pregunta cristal' }, el('p', { class: 'texto-pregunta', text: 'Sugerencias de ajuste' }), sug.map((x) => el('p', { class: 'meta', text: x }))));
      caja.append(formularioDeHipotesis());
      const ap = r.aprendizaje || {};
      const sem = lista(ap.tendencia);
      caja.append(el('div', { class: 'pregunta cristal' },
        el('p', { class: 'texto-pregunta' }, ic('spark'), ' Qué ha aprendido Mira'),
        el('p', { class: 'meta', text: `${ap.reglas_activas ?? 0} regla(s) activa(s). ` }, el('a', { href: '#/aprendizajes', text: 'Verlas y gestionarlas' })),
        lista(ap.cambios).map((x) => el('p', { text: `${momento(x.fecha)} · ${x.accion}: ${x.descripcion}` })),
        sem.length ? el('div', { class: 'tendencia' }, sem.map((x) => el('div', { class: 'semana' },
          el('strong', { text: `${fmtCorto.format(aFecha(x.desde))} – ${fmtCorto.format(aFecha(x.hasta))}` }),
          el('span', { class: 'meta', text: `${x.formuladas} preguntas · ${x.respondidas} respondidas · ${x.no_relevantes} no relevantes · ${x.no_sucedio} no sucedió · respuesta ${pct(x.tasa_respuesta)}` })))) : null));
    } catch (e) { manejar(e); }
  }

  // ------------------------------------------------------------ vista: lo que Mira ha aprendido
  const ESTADO_APRENDIZAJE = { activa: 'Activo', restablecida: 'Restablecido por tus respuestas', desactivada: 'Desactivado por ti' };
  async function vistaAprendizajes() {
    const lista = el('div', {}, esqueletos(2));
    cont.append(el('div', { class: 'cabecera' }, el('div', {}, el('p', { class: 'sobretitulo', text: 'Cómo evoluciona Mira' }), el('h2', { class: 'titulo' }, el('span', { class: 'grad', text: 'Aprendizajes' }))),
      el('p', { class: 'meta', text: 'Mira aprende de cómo cierras sus preguntas. Nada se aplica en secreto: aquí lo ves, lo desactivas o lo borras. Nunca deja de preguntar, solo pasa esas preguntas por detrás de las demás.' })), lista);
    try {
      const d = await get('/api/aprendizajes');
      vaciar(lista);
      if (!d.aprendizajes.length) { lista.append(estadoVacio('Mira aún no ha aprendido nada de tus cierres de preguntas.')); return; }
      d.aprendizajes.forEach((a, i) => {
        const accion = async (ruta, texto) => { try { const r = await post(`/api/aprendizajes/${a.id}/${ruta}`, {}); avisar(r.mensaje || texto, 'ok'); enrutar(); } catch (e) { manejar(e); } };
        const botones = a.estado === 'activa'
          ? [boton('Desactivar', 'parar', { class: 'fantasma pequeno', onclick: () => accion('desactivar') })]
          : [boton('Activar', 'check', { class: 'fantasma pequeno', onclick: () => accion('activar') })];
        botones.push(conConfirmacion('Borrar', 'papelera', 'fantasma pequeno', '¿Borrar este aprendizaje?', () => accion('borrar')));
        lista.append(entra(el('div', { class: 'pregunta cristal foco-luz' },
          el('p', { class: 'texto-pregunta', text: a.descripcion }),
          el('div', { class: 'contexto' },
            el('div', {}, ic('spark'), ESTADO_APRENDIZAJE[a.estado] || a.estado, el('span', { class: 'chip c-neutro' }, 'Clase: ' + a.clase)),
            el('div', {}, ic('historial'), `Evidencia: ${a.evidencia.length} cierre(s) «no relevante»`)),
          el('div', { class: 'opciones' }, botones)), i));
      });
    } catch (e) { manejar(e); }
  }

  // ------------------------------------------------------------ vista: ficha
  async function vistaRegistro(id) {
    if (!ID_REG.test(id || '')) { cont.append(estadoVacio('Registro no válido.')); return; }
    const caja = el('div', {}, esqueletos(2));
    cont.append(caja);
    try { pintarFicha(caja, await get('/api/registros/' + id)); } catch (e) { vaciar(caja); caja.append(estadoVacio(e.estado === 404 ? 'No existe ese registro.' : 'No se pudo cargar.')); manejar(e); }
  }

  function pintarFicha(caja, f) {
    vaciar(caja);
    const recargar = () => get('/api/registros/' + f.id).then((n) => pintarFicha(caja, n)).catch(manejar);
    const k = claseK(f.clase);
    const accion = async (ruta, cuerpo) => { try { const r = await post(ruta, cuerpo); avisar(r.mensaje, 'ok'); await recargar(); } catch (e) { manejar(e); } };

    const activo = f.estado === 'activo';
    const filas = f.datos.map((d) => {
      const acc = el('div', { class: 'acc' });
      const fila = el('div', { class: 'dato' },
        el('div', { class: 'nombre' }, ic(d.campo), CAMPOS[d.campo] || d.campo),
        el('div', { class: 'valor' + (d.desconocido ? ' vacio-v' : '') , text: d.texto }), acc);
      const pintarAcciones = () => {
        vaciar(acc);
        if (!d.desconocido) acc.append(chipCerteza(d.certeza));
        if (!activo) return;
        acc.append(boton('', 'editar', { class: 'fantasma pequeno icono-solo', 'aria-label': 'Corregir ' + (CAMPOS[d.campo] || d.campo), title: 'Corregir', onclick: abrir }));
        if (!d.desconocido && (d.certeza === 'interpretado' || d.certeza === 'hipotesis') && d.campo !== 'entidades') {
          acc.append(boton('', 'rechazar', { class: 'fantasma pequeno icono-solo peligro', 'aria-label': 'Rechazar ' + (CAMPOS[d.campo] || d.campo), title: 'Rechazar interpretación', onclick: () => accion(`/api/registros/${f.id}/rechazar`, { campo: d.campo }) }));
        }
      };
      function abrir() {
        const entrada = el('input', { type: 'text', value: d.desconocido || d.campo === 'duracion' ? '' : d.texto, placeholder: FORMATOS[d.campo] || 'Nuevo valor', maxlength: 500, 'aria-label': 'Nuevo valor de ' + (CAMPOS[d.campo] || d.campo) });
        const editor = el('div', { class: 'editor' }, entrada,
          boton('Guardar', 'check', { class: 'principal pequeno', onclick: () => accion(`/api/registros/${f.id}/corregir`, { campo: d.campo, valor: entrada.value }) }),
          boton('Cancelar', 'cerrar', { class: 'fantasma pequeno', onclick: () => editor.remove() }));
        fila.append(editor); entrada.focus();
      }
      pintarAcciones();
      return fila;
    });

    const separador = (s) => {
      const titulo = el('input', { type: 'text', placeholder: 'Título del nuevo registro (opcional)', maxlength: 200, 'aria-label': 'Título del nuevo registro' });
      return el('div', { class: 'fila separar-fila' }, titulo,
        conConfirmacion('Separar en otro registro', 'separar', 'fantasma pequeno', 'Se creará un registro nuevo con esta nota.',
          () => accion(`/api/registros/${f.id}/separar`, { captura: s.captura, titulo: titulo.value })));
    };
    const fusionHueco = el('div', {});
    const barraDeAcciones = activo
      ? el('div', { class: 'acciones-ficha' },
        boton('Deshacer último cambio', 'deshacer', { class: 'pequeno', onclick: () => accion(`/api/registros/${f.id}/deshacer`, {}) }),
        boton('Fusionar con otro…', 'fusionar', { class: 'pequeno', onclick: () => { if (fusionHueco.firstChild) vaciar(fusionHueco); else fusionHueco.append(selectorDeFusion(f, recargar)); } }),
        conConfirmacion('Eliminar', 'papelera', 'pequeno peligro', 'Irá a la papelera; podrás restaurarlo.', () => accion(`/api/registros/${f.id}/eliminar`, {})),
        fusionHueco)
      : null;
    const banner = f.estado === 'eliminado'
      ? el('div', { class: 'banner cristal' }, ic('papelera'), el('span', { text: `Este registro está en la papelera desde ${momento(f.eliminadoEl)}.` }), boton('Restaurar', 'deshacer', { class: 'pequeno principal', onclick: () => accion(`/api/registros/${f.id}/restaurar`, {}) }))
      : f.estado === 'fusionado'
        ? el('div', { class: 'banner cristal' }, ic('fusionar'), el('span', {}, 'Este registro se fusionó en '), el('a', { href: '#/registro/' + (f.fusionadoEn && f.fusionadoEn.id), text: (f.fusionadoEn && f.fusionadoEn.titulo) || 'otro registro' }), el('span', { text: '.' }))
        : null;
    const bloqueNota = (c) => el('details', { class: 'nota-original' }, el('summary', {}, ic('nota'), `Nota original · ${momento(c.comunicadaEl)} · ${c.origen}`), el('pre', { text: c.texto }));
    const link = (id2, titulo, extra) => el('a', { class: 'chip c-neutro', href: '#/registro/' + id2 }, ic('enlace'), titulo || id2, extra || null);
    const origen = (o) => (o === 'usuario' ? 'personas' : 'spark');

    const piezas = [
      el('p', {}, boton('Volver', 'izq', { class: 'fantasma pequeno', onclick: () => history.length > 1 ? history.back() : (location.hash = '#/dia') })),
      el('div', { class: `ficha-cabecera cristal ${k}` },
        el('div', { class: 'ficha-icono' }, ic(f.clase || 'otro')),
        el('div', {}, el('h2', { text: f.titulo }),
          el('div', { class: 'chips' }, chipClase(f.clase), f.conflictos.some((c) => c.estado === 'abierto') ? el('span', { class: 'chip c-conflicto' }, ic('conflicto'), 'Conflicto abierto') : null,
            el('span', { class: 'meta', text: 'Comunicada ' + momento(f.comunicadaEl) }), el('span', { class: 'id-mono', text: f.id })))),
      banner,
      barraDeAcciones,
      f.descripcion ? el('p', { class: 'descripcion', text: f.descripcion }) : null,
      el('div', { class: 'seccion' }, 'Datos'), el('div', { class: 'datos cristal' }, filas),
      f.conflictos.length ? [el('div', { class: 'seccion' }, 'Conflictos'), el('div', { class: 'enlaces' }, f.conflictos.map((c) => link(c.con, c.titulo, el('span', { class: 'meta', text: `· ${CAMPOS[c.campo] || c.campo} · ${c.estado}` }))))] : null,
      f.relaciones.length ? [el('div', { class: 'seccion' }, 'Relaciones'), el('div', { class: 'enlaces' }, f.relaciones.map((r) => link(r.destino, r.titulo, el('span', { class: 'meta', text: `· ${r.tipo}` }))))] : null,
      el('div', { class: 'seccion' }, 'De dónde sale'),
      f.fuentes.map((s) => el('div', { class: 'cita cristal' }, el('span', { class: 'meta', text: `${s.rol} · ${s.captura}` }), s.cita ? el('blockquote', { text: s.cita }) : null,
        activo && s.rol === 'ampliacion' ? separador(s) : null)),
      f.capturas.map(bloqueNota),
      f.rechazos.length ? [el('div', { class: 'seccion' }, 'Interpretaciones rechazadas'), f.rechazos.map((r) => el('p', { class: 'meta' }, `${CAMPOS[r.campo] || r.campo}: `, el('span', { class: 'anterior', text: r.valor }), ` (era ${r.certeza_original})`))] : null,
      f.historial.length ? [el('div', { class: 'seccion' }, 'Historial'), el('ul', { class: 'historial' }, f.historial.map((h) => el('li', {}, el('span', { class: 'punto' }, ic(origen(h.origen))),
        el('div', { class: 'cambio' }, el('b', { text: h.origen === 'usuario' ? 'Tú' : 'Mira' }), ` · ${CAMPOS[h.campo] || h.campo} · ${momento(h.fecha)}`, el('br'),
          h.anterior !== undefined ? [el('span', { class: 'anterior', text: resumen(h.anterior) }), ' → '] : null, h.nuevo !== undefined ? el('span', { text: resumen(h.nuevo) }) : null))))] : null,
    ];
    caja.append(...piezas.flat(Infinity).filter(Boolean));
    caja.querySelectorAll('.ficha-cabecera, .datos, .cita').forEach((n, i) => entra(n, i));
  }

  // ------------------------------------------------------------ enrutado
  let vistaActual = '';
  async function enrutar() {
    vaciar(cont); cont.classList.remove('vista'); void cont.offsetWidth; cont.classList.add('vista');
    if (!(await acceso())) { mostrarAcceso(); return; }
    pintarBarra();
    const m = (location.hash.replace(/^#/, '') || '/dia').match(/^\/([^/?]*)(?:[/?](.*))?$/) || [];
    const vista = m[1] || 'dia', resto = m[2];
    vistaActual = vista === 'registro' ? 'dia' : vista === 'aprendizajes' ? 'descubrimientos' : vista;
    moverIndicador(vistaActual); actualizarContador();
    if (vista === 'buscar') await vistaBuscar(resto || '');
    else if (vista === 'pendientes') await vistaPendientes();
    else if (vista === 'papelera') await vistaPapelera();
    else if (vista === 'aprendizajes') await vistaAprendizajes();
    else if (vista === 'descubrimientos') await vistaDescubrimientos();
    else if (vista === 'registro') await vistaRegistro(resto);
    else await vistaDia(vista === 'dia' ? resto : null);
    window.scrollTo({ top: 0 });
  }

  function mostrarAcceso(mensaje) {
    $('barra').hidden = true; vaciar(cont);
    cont.append(el('div', { class: 'hero cristal vista' },
      Logo(76), el('h1', { class: 'grad-texto', text: 'ORUMI' }),
      el('p', { class: 'lema', text: 'Escucha, comprende y conecta.' }),
      el('p', { text: 'Tu memoria personal, ordenada. Inicia sesión con tu cuenta de Google para entrar.' }),
      boton('Entrar con Google', 'candado', { class: 'principal', onclick: entrar }),
      mensaje ? el('p', { class: 'error', role: 'alert', text: mensaje }) : null));
  }

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
