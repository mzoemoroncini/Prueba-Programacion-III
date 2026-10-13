import React from 'react';
import { createRoot } from 'react-dom/client';
import './styles.css';

function App() {
  return <main><h1>Centralizador de ofertas</h1><p>McDonald’s · Burger King · Mostaza</p><p>Proyecto en preparación. El catálogo todavía no está implementado.</p></main>;
}

createRoot(document.getElementById('root')).render(<App />);
