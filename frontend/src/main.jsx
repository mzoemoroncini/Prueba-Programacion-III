import React from 'react';
import { createRoot } from 'react-dom/client';
import OfferBrowser from './features/offer-browsing/OfferBrowser.jsx';
import AdminPanel from './features/offer-publication/AdminPanel.jsx';
import './styles.css';

const administrative = new URLSearchParams(window.location.search).has('admin');
createRoot(document.getElementById('root')).render(<>
  <a className="skip-link" href="#content">Saltar al contenido</a>
  <div className="topbar"><a href="/" className="site-brand"><span className="brand-mark" aria-hidden="true">%</span><span>Ofertas claras<small>Centralizador de Ofertas y Descuentos con IA</small></span></a>
    <nav aria-label="Navegación principal"><a href="/" aria-current={!administrative ? 'page' : undefined}>Catálogo</a><a href="?admin=1" aria-current={administrative ? 'page' : undefined}>Administración</a></nav>
  </div>
  <div id="content">{administrative ? <AdminPanel /> : <OfferBrowser />}</div>
  <footer>Proyecto académico · McDonald’s, Burger King y Mostaza<br/>La disponibilidad y el canje dependen de cada comercio. Consultá siempre la fuente original.</footer>
</>);
