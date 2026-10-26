import React, { useState } from 'react';

export default function OfferReportForm({ offerId }) {
  const [reason, setReason] = useState('');
  const [comment, setComment] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [sent, setSent] = useState(false);
  async function submit(event) {
    event.preventDefault(); setBusy(true); setMessage('');
    try {
      const response = await fetch(`/api/offers/${offerId}/reports`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ reason, comment }) });
      if (!response.ok) throw new Error('No pudimos enviar el reporte. Intentá nuevamente.');
      setSent(true); setMessage('Reporte enviado. Gracias por avisarnos. La oferta será revisada.');
    } catch (error) { setMessage(error.message); } finally { setBusy(false); }
  }
  return <section><h3>Reportar un problema</h3>{!sent && <form className="report-form" onSubmit={submit}>
    <label>Motivo<select required value={reason} onChange={event => setReason(event.target.value)}><option value="">Seleccioná un motivo</option><option value="Expired">Oferta vencida</option><option value="IncorrectData">Datos incorrectos</option></select></label>
    <label>Comentario opcional<textarea rows={3} maxLength={2000} value={comment} onChange={event => setComment(event.target.value)} /></label>
    <button disabled={busy}>{busy ? 'Enviando…' : 'Enviar reporte'}</button>
  </form>}<p role="status">{message}</p></section>;
}
