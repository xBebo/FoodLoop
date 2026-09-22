import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// Base styles first so component CSS (imported through App) wins at equal specificity.
import './styles/globals.css'
import App from './app/App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
