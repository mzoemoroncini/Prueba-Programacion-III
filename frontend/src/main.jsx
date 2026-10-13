import React from 'react';
import { createRoot } from 'react-dom/client';
import OfferBrowser from './features/offer-browsing/OfferBrowser.jsx';
import AdminPanel from './features/offer-publication/AdminPanel.jsx';
import './styles.css';

createRoot(document.getElementById('root')).render(new URLSearchParams(window.location.search).has('admin') ? <AdminPanel /> : <OfferBrowser />);
