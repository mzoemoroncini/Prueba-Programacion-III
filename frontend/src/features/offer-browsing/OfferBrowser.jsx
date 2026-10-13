import React, { useEffect, useState } from 'react';

const missing = 'No informado por la fuente';
const benefitNames = {
  PercentageDiscount: 'Descuento porcentual', FixedDiscount: 'Descuento de monto fijo',
  PromotionalPrice: 'Precio promocional', MultiBuy: 'Promoción por cantidad',
  Combo: 'Combo', Other: 'Otro beneficio', Unknown: missing,
};
const selectedId = () => new URLSearchParams(window.location.search).get('offer');
const formatDate = value => value ? value.split('-').reverse().join('/') : missing;
const formatAmount = (value, currency) => value == null ? missing
  : `${currency === 'ARS' ? '$' : currency || 'Moneda no informada'} ${new Intl.NumberFormat('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)}`;
const safeSource = value => {
  try { const url = new URL(value); return ['http:', 'https:'].includes(url.protocol) ? url.href : null; }
  catch { return null; }
};

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
  const source = offer && safeSource(offer.sourceUrl);
  return <main>
    <header><p className="eyebrow">McDonald’s · Burger King · Mostaza</p><h1>Ofertas en un solo lugar</h1><p>Consultá las promociones y sus condiciones en la fuente original.</p></header>
    {id && <button className="back" onClick={() => navigate(null)}>← Volver a las ofertas</button>}
    <section aria-live="polite" aria-busy={state.status === 'loading'}>
      {state.status === 'loading' && <p className="notice">Cargando ofertas…</p>}
      {state.status === 'error' && <div className="notice"><h2>No pudimos consultar las ofertas</h2><p>Intentá nuevamente en unos momentos.</p><button onClick={() => setAttempt(value => value + 1)}>Reintentar</button></div>}
      {state.status === 'not-found' && <div className="notice"><h2>Esta oferta no está disponible</h2><p>Puede haber vencido o haber sido retirada.</p></div>}
      {!id && state.status === 'ready' && (state.data.length === 0
        ? <div className="notice"><h2>No hay ofertas disponibles</h2><p>Las promociones aparecerán aquí cuando estén publicadas.</p></div>
        : <ul className="offers">{state.data.map(item => <li key={item.id}><article><p className="brand">{item.chain}</p><h2>{item.summary || missing}</h2><button onClick={() => navigate(item.id)}>Ver oferta →</button></article></li>)}</ul>)}
      {offer && <article className="detail">
        <p className="brand">{offer.chain}</p><h2>{offer.summary || missing}</h2>
        <h3>Beneficios</h3>
        {offer.benefits.length === 0 ? <p>{missing}</p> : offer.benefits.map((benefit, index) => <section className="benefit" key={index}>
          <h4>{benefitNames[benefit.type] || 'Otro beneficio'}</h4>
          <p>{benefit.description || benefit.originalDescription || missing}</p>
          {benefit.percentage != null && <p>Descuento: {new Intl.NumberFormat('es-AR').format(benefit.percentage)} %</p>}
          {benefit.discountAmount != null && <p>Monto de descuento: {formatAmount(benefit.discountAmount, benefit.currency)}</p>}
          {benefit.promotionalPrice != null && <p>Precio promocional: {formatAmount(benefit.promotionalPrice, benefit.currency)}</p>}
          {benefit.purchaseQuantity != null && <p>Cantidad a comprar: {benefit.purchaseQuantity}</p>}
          {benefit.receivedQuantity != null && <p>Cantidad a recibir: {benefit.receivedQuantity}</p>}
          <p>Requisitos: {benefit.requirements || missing}</p>
        </section>)}
        <h3>Vigencia</h3><p>Desde: {formatDate(offer.startsOn)}</p><p>Hasta: {formatDate(offer.endsOn)}</p>
        <h3>Condiciones</h3><p className="preserve">{offer.conditions || missing}</p>
        <h3>Código de descuento</h3>
        {offer.codeStatus === 'Available' && offer.code ? <code className="code">{offer.code}</code>
          : <p>{offer.codeStatus === 'NotRequired' ? 'Esta oferta no incluye código' : missing}</p>}
        {source ? <a className="source" href={source} target="_blank" rel="noopener noreferrer">Ver oferta en origen ↗</a> : <p>El enlace de origen no está disponible.</p>}
      </article>}
    </section>
  </main>;
}
