import React from 'react';
import OfferReportForm from './OfferReportForm.jsx';

const missing = 'No informado por la fuente';
const benefitNames = {
  PercentageDiscount: 'Descuento porcentual', FixedDiscount: 'Descuento de monto fijo',
  PromotionalPrice: 'Precio promocional', MultiBuy: 'Promoción por cantidad',
  Combo: 'Combo', Other: 'Otro beneficio', Unknown: missing,
};
const formatDate = value => value ? value.split('-').reverse().join('/') : missing;
const formatAmount = (value, currency) => value == null ? missing
  : `${currency === 'ARS' ? '$' : currency || 'Moneda no informada'} ${new Intl.NumberFormat('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)}`;
const safeSource = value => {
  try { const url = new URL(value); return ['http:', 'https:'].includes(url.protocol) ? url.href : null; }
  catch { return null; }
};

export default function OfferDetail({ offer, administrative = false }) {
  const source = safeSource(offer.sourceUrl);
  return <article className="detail">
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
        {source ? <a className="source" href={source} target="_blank" rel="noopener noreferrer" onClick={() => {
          if (!administrative) fetch(`/api/offers/${offer.id}/source-access`, { method: 'POST', keepalive: true }).catch(() => {});
        }}>Ver oferta en origen ↗</a> : <p>El enlace de origen no está disponible.</p>}
        {!administrative && <OfferReportForm key={offer.id} offerId={offer.id} />}
      </article>;
}
