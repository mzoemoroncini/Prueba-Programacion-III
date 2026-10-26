import React, { useEffect, useState } from 'react';
import OfferDetail from './OfferDetail.jsx';

const missing = 'No informado por la fuente';
const selectedId = () => new URLSearchParams(window.location.search).get('offer');

export default function OfferBrowser() {
  const [id, setId] = useState(selectedId);
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState({ status: 'loading', data: null });
  useEffect(() => {
    const sync = () => setId(selectedId());
    window.addEventListener('popstate', sync);
    return () => window.removeEventListener('popstate', sync);
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    setState({ status: 'loading', data: null });
    const path = id ? `/api/offers/${encodeURIComponent(id)}` : '/api/offers';
    fetch(path, { signal: controller.signal })
      .then(async response => {
        if (controller.signal.aborted) return;
        if (response.status === 404) { setState({ status: 'not-found', data: null }); return; }
        if (!response.ok) throw new Error('No se pudo consultar');
        const data = await response.json();
        if (controller.signal.aborted) return;
        setState({ status: 'ready', data });
      })
      .catch(error => { if (error.name !== 'AbortError' && !controller.signal.aborted) setState({ status: 'error', data: null }); });
    return () => controller.abort();
  }, [id, attempt]);
  function navigate(nextId) {
    const url = new URL(window.location.href);
    if (nextId) url.searchParams.set('offer', nextId); else url.searchParams.delete('offer');
    window.history.pushState({}, '', url);
    setId(nextId);
    window.scrollTo(0, 0);
  }
  const offer = id && state.status === 'ready' ? state.data : null;
  return <main>
    <header className="hero"><p className="eyebrow">MENOS BÚSQUEDA. MÁS CLARIDAD.</p><h1>Ofertas en un solo lugar</h1><p>Descubrí los beneficios de tus cadenas favoritas, revisá sus condiciones y accedé a la publicación original.</p><div className="chains" aria-label="Cadenas disponibles"><span>McDonald’s</span><span>Burger King</span><span>Mostaza</span></div></header>
    {id && <button className="back" onClick={() => navigate(null)}>← Volver a las ofertas</button>}
    <section aria-label="Catálogo de promociones" aria-live="polite" aria-busy={state.status === 'loading'}>
      {state.status === 'loading' && <p className="notice">Cargando ofertas…</p>}
      {state.status === 'error' && <div className="notice"><h2>No pudimos consultar las ofertas</h2><p>Intentá nuevamente en unos momentos.</p><button onClick={() => setAttempt(value => value + 1)}>Reintentar</button></div>}
      {state.status === 'not-found' && <div className="notice"><h2>Esta oferta no está disponible</h2><p>Puede haber vencido o haber sido retirada.</p></div>}
      {!id && state.status === 'ready' && (state.data.length === 0
        ? <div className="notice"><h2>No hay ofertas disponibles</h2><p>Las promociones aparecerán aquí cuando estén publicadas.</p></div>
        : <ul className="offers">{state.data.map(item => <li key={item.id}><article><div className="card-top"><p className="brand">{item.chain}</p><span className="offer-tag">Publicada</span></div><h2>{item.summary || missing}</h2><button onClick={() => navigate(item.id)}>Ver oferta →</button></article></li>)}</ul>)}
      {offer && <OfferDetail offer={offer} />}
    </section>
  </main>;
}
