// Iconos propios de ORUMI: trazo de 1,75 sobre rejilla de 24, extremos redondeados. Se construyen con createElementNS (sin inyectar HTML).
// Cada icono es una lista de primitivas: ['p', d] trazo, ['c', cx, cy, r] círculo, ['r', x, y, w, h, rx] rectángulo,
// ['l', x1, y1, x2, y2] línea, ['pl', puntos] polilínea.
(() => {
  const NS = 'http://www.w3.org/2000/svg';
  const D = {
    // navegación y acciones
    dia: [['r', 3, 4.5, 18, 16.5, 3], ['l', 3, 9.5, 21, 9.5], ['l', 8, 2.5, 8, 6.5], ['l', 16, 2.5, 16, 6.5]],
    buscar: [['c', 11, 11, 6.5], ['l', 20, 20, 15.8, 15.8]],
    pregunta: [['p', 'M21 12a8.5 8.5 0 0 1-12.2 7.6L3.5 21l1.4-4.6A8.5 8.5 0 1 1 21 12z'], ['p', 'M9.6 9.5a2.5 2.5 0 0 1 4.8 1c0 1.7-2.4 2.1-2.4 3.5'], ['l', 12, 17.2, 12.01, 17.2]],
    captura: [['p', 'M12 20h9'], ['p', 'M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z']],
    editar: [['p', 'M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z']],
    salir: [['p', 'M9 21H5.5A2.5 2.5 0 0 1 3 18.5v-13A2.5 2.5 0 0 1 5.5 3H9'], ['pl', '16 17 21 12 16 7'], ['l', 21, 12, 9, 12]],
    izq: [['pl', '14.5 5.5 8 12 14.5 18.5']],
    der: [['pl', '9.5 5.5 16 12 9.5 18.5']],
    abajo: [['pl', '6 9.5 12 16 18 9.5']],
    check: [['pl', '4.5 12.5 9.5 17.5 19.5 6.5']],
    cerrar: [['l', 6, 6, 18, 18], ['l', 18, 6, 6, 18]],
    rechazar: [['c', 12, 12, 9], ['l', 5.6, 5.6, 18.4, 18.4]],
    sol: [['c', 12, 12, 4], ['l', 12, 3, 12, 5], ['l', 12, 19, 12, 21], ['l', 3, 12, 5, 12], ['l', 19, 12, 21, 12], ['l', 5.6, 5.6, 7, 7], ['l', 17, 17, 18.4, 18.4], ['l', 5.6, 18.4, 7, 17], ['l', 17, 7, 18.4, 5.6]],
    luna: [['p', 'M20.5 14.2A8.5 8.5 0 1 1 9.8 3.5a6.8 6.8 0 0 0 10.7 10.7z']],
    enlace: [['p', 'M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1'], ['p', 'M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1']],
    nota: [['p', 'M14 3H7.5A2.5 2.5 0 0 0 5 5.5v13A2.5 2.5 0 0 0 7.5 21h9a2.5 2.5 0 0 0 2.5-2.5V8l-5-5z'], ['pl', '14 3 14 8 19 8'], ['l', 9, 13, 15, 13], ['l', 9, 17, 13, 17]],
    enviar: [['p', 'M21 3L10.5 13.5'], ['p', 'M21 3l-6.5 18-4-8.5L2 8.5 21 3z']],
    microfono: [['r', 9, 3, 6, 11, 3], ['p', 'M5.5 11.5a6.5 6.5 0 0 0 13 0'], ['l', 12, 18, 12, 21], ['l', 9, 21, 15, 21]],
    parar: [['r', 7, 7, 10, 10, 2.5]],
    filtros: [['l', 4, 7, 20, 7], ['l', 4, 17, 20, 17], ['c', 9, 7, 2.2], ['c', 15, 17, 2.2]],
    historial: [['p', 'M3.5 12a8.5 8.5 0 1 0 2.5-6'], ['pl', '3.5 4 3.5 8.5 8 8.5'], ['pl', '12 8 12 12.5 15 14']],
    vacio: [['p', 'M3 13l2.5-7.5A2 2 0 0 1 7.4 4h9.2a2 2 0 0 1 1.9 1.5L21 13v5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-5z'], ['p', 'M3 13h5l1.5 2.5h5L16 13h5']],
    candado: [['r', 5, 11, 14, 10, 2.5], ['p', 'M8 11V8a4 4 0 0 1 8 0v3'], ['l', 12, 15, 12, 17]],
    spark: [['p', 'M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8L12 3z']],
    // clases de registro
    actividad: [['pl', '3 12 7 12 10 5 14 19 17 12 21 12']],
    evento: [['p', 'M12 3.2l2.6 5.5 6 .8-4.4 4.2 1.1 6-5.3-2.9-5.3 2.9 1.1-6L3.4 9.5l6-.8L12 3.2z']],
    idea: [['p', 'M9 18h6'], ['p', 'M10 21h4'], ['p', 'M12 3a6 6 0 0 0-3.6 10.8c.8.7 1.1 1.4 1.1 2.2h5c0-.8.3-1.5 1.1-2.2A6 6 0 0 0 12 3z']],
    decision: [['p', 'M12 21v-7.5'], ['p', 'M12 13.5L6 7'], ['p', 'M12 13.5L18 7'], ['pl', '6 11.5 6 7 10.5 7'], ['pl', '18 11.5 18 7 13.5 7']],
    problema: [['p', 'M10.3 4.2L2.8 17.2A2 2 0 0 0 4.5 20.2h15a2 2 0 0 0 1.7-3L13.7 4.2a2 2 0 0 0-3.4 0z'], ['l', 12, 9.5, 12, 13.5], ['l', 12, 17, 12.01, 17]],
    otro: [['c', 12, 12, 8.5], ['c', 12, 12, 1.2]],
    // certezas
    dicho: [['l', 5, 10, 5, 14], ['l', 8.5, 7, 8.5, 17], ['l', 12, 4, 12, 20], ['l', 15.5, 8, 15.5, 16], ['l', 19, 10, 19, 14]],
    confirmado: [['c', 12, 12, 9], ['pl', '8 12.5 11 15.5 16.5 9']],
    interpretado: [['p', 'M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12z'], ['c', 12, 12, 2.8]],
    hipotesis: [['p', 'M9.5 3h5'], ['p', 'M10.5 3v6.2L5 19a1.7 1.7 0 0 0 1.5 2.5h11A1.7 1.7 0 0 0 19 19l-5.5-9.8V3']],
    recomendacion: [['c', 12, 12, 9], ['p', 'M15.8 8.2l-2.2 5.4-5.4 2.2 2.2-5.4 5.4-2.2z']],
    conflicto: [['c', 12, 12, 9], ['l', 12, 7.5, 12, 12.5], ['l', 12, 16, 12.01, 16]],
    desconocido: [['c', 12, 12, 9], ['p', 'M9.6 9.6a2.5 2.5 0 0 1 4.8 1c0 1.7-2.4 2.1-2.4 3.4'], ['l', 12, 17, 12.01, 17]],
    // campos
    fecha: [['r', 3, 4.5, 18, 16.5, 3], ['l', 3, 9.5, 21, 9.5], ['l', 8, 2.5, 8, 6.5], ['l', 16, 2.5, 16, 6.5]],
    hora: [['c', 12, 12, 9], ['pl', '12 7 12 12 15.5 14']],
    duracion: [['p', 'M6 3h12'], ['p', 'M6 21h12'], ['p', 'M7 3c0 5 3 6 5 9-2 3-5 4-5 9'], ['p', 'M17 3c0 5-3 6-5 9 2 3 5 4 5 9']],
    personas: [['c', 12, 8, 3.8], ['p', 'M4.5 20.5a7.5 7.5 0 0 1 15 0']],
    entidades: [['r', 5, 3, 14, 18, 2], ['l', 9, 8, 10, 8], ['l', 14, 8, 15, 8], ['l', 9, 12, 10, 12], ['l', 14, 12, 15, 12], ['p', 'M10 21v-4h4v4']],
    lugares: [['p', 'M12 21s-6.5-5.6-6.5-11a6.5 6.5 0 0 1 13 0c0 5.400-6.500 11-6.500 11z'], ['c', 12, 10, 2.4]],
    proyectos: [['p', 'M3 7a2 2 0 0 1 2-2h4l2 2.5h8A2 2 0 0 1 21 9.5V17a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7z']],
    herramientas: [['p', 'M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z']],
    contexto: [['p', 'M12 3.5l9 4.8-9 4.8-9-4.8 9-4.8z'], ['pl', '3 12.5 12 17.3 21 12.5'], ['pl', '3 16.7 12 21.5 21 16.7']],
    acciones: [['p', 'M13 2.5L4.5 13.5H12l-1 8 8.5-11H12l1-8z']],
    resultados: [['l', 5, 21, 5, 4], ['p', 'M5 4.5h11l-2 4 2 4H5']],
    problemas: [['p', 'M10.3 4.2L2.8 17.2A2 2 0 0 0 4.5 20.2h15a2 2 0 0 0 1.7-3L13.7 4.2a2 2 0 0 0-3.4 0z'], ['l', 12, 9.5, 12, 13.5], ['l', 12, 17, 12.01, 17]],
    ideas: [['p', 'M9 18h6'], ['p', 'M10 21h4'], ['p', 'M12 3a6 6 0 0 0-3.6 10.8c.8.7 1.1 1.4 1.1 2.2h5c0-.8.3-1.5 1.1-2.2A6 6 0 0 0 12 3z']],
    titulo: [['p', 'M4 7V5h16v2'], ['l', 12, 5, 12, 19], ['l', 9, 19, 15, 19]],
    multidia: [['r', 3, 4.5, 18, 16.5, 3], ['l', 3, 9.5, 21, 9.5], ['l', 8, 14, 16, 14]],
  };

  function el(tag, attrs) {
    const e = document.createElementNS(NS, tag);
    for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, v);
    return e;
  }

  /** Icono por nombre. Es decorativo (oculto a los lectores) salvo que se dé <c>etiqueta</c>. */
  function Icono(nombre, { tam, clase, etiqueta } = {}) {
    const svg = el('svg', { viewBox: '0 0 24 24', class: 'icono' + (clase ? ' ' + clase : '') });
    if (tam) { svg.setAttribute('width', tam); svg.setAttribute('height', tam); }
    if (etiqueta) { svg.setAttribute('role', 'img'); svg.setAttribute('aria-label', etiqueta); } else { svg.setAttribute('aria-hidden', 'true'); }
    for (const [tipo, ...a] of D[nombre] || D.otro) {
      if (tipo === 'p') svg.append(el('path', { d: a[0] }));
      else if (tipo === 'c') svg.append(el('circle', { cx: a[0], cy: a[1], r: a[2] }));
      else if (tipo === 'r') svg.append(el('rect', { x: a[0], y: a[1], width: a[2], height: a[3], rx: a[4] }));
      else if (tipo === 'l') svg.append(el('line', { x1: a[0], y1: a[1], x2: a[2], y2: a[3] }));
      else if (tipo === 'pl') svg.append(el('polyline', { points: a[0] }));
    }
    return svg;
  }

  let n = 0;
  /** Logotipo animado: dos anillos que giran en sentidos opuestos alrededor de un núcleo que late. */
  function Logo(tam = 40) {
    const id = 'lg' + (++n);
    const svg = el('svg', { viewBox: '0 0 48 48', width: tam, height: tam, class: 'logo', role: 'img', 'aria-label': 'ORUMI' });
    const defs = el('defs', {});
    const g = el('linearGradient', { id, x1: '0', y1: '0', x2: '1', y2: '1' });
    g.append(el('stop', { offset: '0', 'stop-color': 'var(--acento-1)' }), el('stop', { offset: '1', 'stop-color': 'var(--acento-2)' }));
    defs.append(g);
    const aro = (r, extra) => el('circle', { cx: 24, cy: 24, r, fill: 'none', stroke: `url(#${id})`, 'stroke-width': 2.4, 'stroke-linecap': 'round', ...extra });
    svg.append(defs,
      aro(20, { class: 'anillo anillo-a', 'stroke-dasharray': '86 40' }),
      aro(12.5, { class: 'anillo anillo-b', 'stroke-dasharray': '38 41', opacity: '.8' }),
      el('circle', { cx: 24, cy: 24, r: 4.2, fill: `url(#${id})`, class: 'nucleo' }));
    return svg;
  }

  window.OrumiIconos = { Icono, Logo, nombres: Object.keys(D) };
})();
