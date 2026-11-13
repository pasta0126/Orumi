// Tema claro/oscuro: se aplica antes del primer pintado (se carga en <head>). Sin elección guardada manda el sistema.
(() => {
  const CLAVE = 'orumi_tema';
  const raiz = document.documentElement;
  const guardado = () => { try { const t = localStorage.getItem(CLAVE); return t === 'light' || t === 'dark' ? t : null; } catch { return null; } };
  const efectivo = () => guardado() || (matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark');
  const aplicar = () => { const t = guardado(); if (t) raiz.setAttribute('data-tema', t); else raiz.removeAttribute('data-tema'); };
  aplicar();
  window.OrumiTema = {
    actual: efectivo,
    alternar() {
      const siguiente = efectivo() === 'dark' ? 'light' : 'dark';
      try { localStorage.setItem(CLAVE, siguiente); } catch { /* sin almacenamiento */ }
      aplicar();
      return siguiente;
    },
  };
})();
