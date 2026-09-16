import React from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './index.css';
import "./i18n";
import { UiFeedbackProvider } from './UiFeedback';

const container = document.getElementById('root')!;
const root = createRoot(container);
root.render(<UiFeedbackProvider><App /></UiFeedbackProvider>);
