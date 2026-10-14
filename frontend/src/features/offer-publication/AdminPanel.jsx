import React, { useEffect, useState } from 'react';
import OfferDetail from '../offer-browsing/OfferDetail.jsx';

const states = { Pending: 'Pendiente de revisión', Published: 'Publicada', Disabled: 'Desactivada' };
const chains = { McDonalds: 'McDonald’s', BurgerKing: 'Burger King', Mostaza: 'Mostaza' };
async function request(path, options = {}) {
  const response = await fetch(path, { credentials: 'same-origin', ...options });
  if (!response.ok) {
    let message = response.status === 401 ? 'Usuario o contraseña incorrectos, o sesión finalizada.' : 'No se pudo completar la acción. Intentá nuevamente.';
    const problem = await response.json().catch(() => null);
    if (problem?.errors) message = Object.values(problem.errors).flat().join(' ');
    else if (problem?.title) message = problem.title;
    const error = new Error(message); error.status = response.status; throw error;
  }
  return response.status === 204 ? null : response.json();
}
async function mutate(path, body) {
  const { token } = await request('/api/admin/csrf');
  return request(path, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token }, ...(body ? { body: JSON.stringify(body) } : {}) });
}
export default function AdminPanel() {
  const [auth, setAuth] = useState('checking');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [offers, setOffers] = useState(null);
  const [selected, setSelected] = useState(null);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [capture, setCapture] = useState(null);
  const [runs, setRuns] = useState([]);
  async function checkSession() {
    setAuth('checking'); setMessage('');
    try { await request('/api/admin/session'); setAuth('signed-in'); }
    catch (error) { setAuth(error.status === 401 ? 'anonymous' : 'error'); if (error.status !== 401) setMessage(error.message); }
  }
  useEffect(() => { checkSession(); }, []);
  useEffect(() => { if (auth === 'signed-in') { loadOffers(); loadCapture(); } }, [auth]);
  function handleError(error) {
    setMessage(error.message);
    if (error.status === 401) { setAuth('anonymous'); setOffers(null); setSelected(null); }
  }
  async function loadCapture() {
    try {
      const [status, history] = await Promise.all([request('/api/admin/capture/status'), request('/api/admin/capture/runs')]);
      setCapture(status); setRuns(history);
    } catch (error) { handleError(error); }
  }
  async function readSource(offerId) {
    setBusy(true); setMessage('Leyendo fuentes. Esto puede tardar unos minutos…');
    try {
      const result = await mutate(offerId ? `/api/admin/offers/${offerId}/reread` : '/api/admin/capture/run');
      await loadOffers(); await loadCapture();
      if (offerId) setSelected(await request(`/api/admin/offers/${offerId}`));
      setMessage(result.some(run => run.status === 'Failed') ? 'La lectura terminó con errores. Revisá el registro.' : 'Lectura completada. Las ofertas nuevas quedan pendientes de revisión.');
    } catch (error) { handleError(error); } finally { setBusy(false); }
  }
  async function loadOffers() {
    setOffers(null);
    try { setOffers(await request('/api/admin/offers')); } catch (error) { handleError(error); }
  }
  async function login(event) {
    event.preventDefault(); setBusy(true); setMessage('');
    try { await mutate('/api/admin/login', { username, password }); setPassword(''); setAuth('signed-in'); }
    catch (error) { handleError(error); } finally { setBusy(false); }
  }
  async function openOffer(id) {
    setBusy(true); setMessage(''); setSelected(null);
    try { setSelected(await request(`/api/admin/offers/${id}`)); } catch (error) { handleError(error); } finally { setBusy(false); }
  }
  async function changeStatus(action) {
    setBusy(true); setMessage('');
    try {
      await mutate(`/api/admin/offers/${selected.detail.id}/${action}`);
      setSelected(await request(`/api/admin/offers/${selected.detail.id}`)); await loadOffers();
      setMessage(action === 'publish' ? 'Oferta publicada. Su disponibilidad pública depende de las fechas informadas.' : 'Oferta desactivada.');
    } catch (error) { handleError(error); } finally { setBusy(false); }
  }
  async function logout() {
    setBusy(true); setMessage('');
    try { await mutate('/api/admin/logout'); setAuth('anonymous'); setOffers(null); setSelected(null); }
    catch (error) { handleError(error); } finally { setBusy(false); }
  }
  return <main>
    <header><a href="/">← Volver al catálogo</a><h1>Administración de ofertas</h1><p>Revisá la información de origen antes de publicar.</p></header>
    {message && <p className="notice" role="status">{message}</p>}
    {auth === 'checking' && <p role="status">Comprobando sesión…</p>}
    {auth === 'error' && <button onClick={checkSession}>Reintentar</button>}
    {auth === 'anonymous' && <form className="login" onSubmit={login}>
      <label htmlFor="username">Usuario</label><input id="username" autoComplete="username" value={username} maxLength={256} required onChange={event => setUsername(event.target.value)} />
      <label htmlFor="password">Contraseña</label><input id="password" type="password" autoComplete="current-password" value={password} maxLength={1024} required onChange={event => setPassword(event.target.value)} />
      <button disabled={busy} type="submit">{busy ? 'Ingresando…' : 'Iniciar sesión'}</button>
    </form>}
    {auth === 'signed-in' && <>
      <section className="notice" aria-label="Lectura automática">
        <h2>Lectura con IA</h2>{capture?.ocrEnabled && <p>OCR de imágenes y PDF: OCR.space</p>}<p>{capture?.message || 'Consultando configuración de lectura…'}</p>
        <button disabled={busy || !capture?.configured} onClick={() => readSource(null)}>Leer fuentes ahora</button>
        {runs.length > 0 && <details><summary>Últimas lecturas y errores</summary><ul>{runs.map(run => <li key={run.id}>
          <strong>{run.sourceId}</strong> · {run.status === 'Succeeded' ? 'Completada' : run.status === 'Failed' ? 'Fallida' : 'En curso'} · {run.stage}
          {run.error && <p>{run.error}</p>}
          <p>{run.offerCount} ofertas procesadas · {new Date(/(Z|[+-]\d{2}:\d{2})$/.test(run.startedAt) ? run.startedAt : run.startedAt + 'Z').toLocaleString('es-AR')}</p>
        </li>)}</ul></details>}
      </section>
      <div className="admin-actions"><button disabled={busy} onClick={logout}>Cerrar sesión</button><button disabled={busy} onClick={loadOffers}>Actualizar listado</button></div>
      {offers === null && !message && <p role="status">Cargando publicaciones…</p>}
      {offers?.length === 0 && <p className="notice">Todavía no hay ofertas extraídas para revisar.</p>}
      {offers && <ul className="admin-list">{offers.map(offer => <li key={offer.id}><button className="offer-row" disabled={busy} onClick={() => openOffer(offer.id)}>
        <strong>{chains[offer.chain] || 'Cadena no informada'}</strong><span>{offer.summary || 'No informado por la fuente'}</span><span>{states[offer.status]}</span>
        {!offer.endsOn && <small>Sin vencimiento informado: requiere revisión</small>}
      </button></li>)}</ul>}
      {selected && <section aria-label="Revisión de la oferta">
        <p>Estado: <strong>{states[selected.status]}</strong></p><OfferDetail offer={selected.detail} />
        <div className="admin-actions">
          {selected.captureSourceId && <button disabled={busy || !capture?.configured} onClick={() => readSource(selected.detail.id)}>Volver a leer la fuente</button>}
          {selected.status !== 'Published' && <button disabled={busy} onClick={() => changeStatus('publish')}>Publicar oferta</button>}
          {selected.status !== 'Disabled' && <button className="danger" disabled={busy} onClick={() => changeStatus('disable')}>Desactivar oferta</button>}
        </div>
      </section>}
    </>}
  </main>;
}
