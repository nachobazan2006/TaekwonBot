(function () {
  'use strict';

  function buildDocument(title, contentHtml) {
    return `<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>${title}</title>
  <style>
    :root { color-scheme: light; }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      padding: 24px;
      font-family: "Segoe UI", Tahoma, sans-serif;
      color: #1f2937;
      background: #ffffff;
    }
    .print-shell { display: flex; flex-direction: column; gap: 18px; }
    .print-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; border-bottom: 2px solid #102845; padding-bottom: 12px; }
    .print-title { margin: 0; font-size: 26px; color: #102845; }
    .print-meta { margin: 6px 0 0; color: #6b7280; font-size: 13px; }
    .print-summary { display: flex; flex-wrap: wrap; gap: 10px; }
    .print-pill {
      border: 1px solid #d1d5db;
      border-radius: 999px;
      padding: 7px 12px;
      font-size: 12px;
      font-weight: 700;
      color: #374151;
      background: #f9fafb;
    }
    table { width: 100%; border-collapse: collapse; font-size: 12px; }
    th, td { border: 1px solid #d1d5db; padding: 8px 10px; text-align: left; vertical-align: top; }
    th { background: #102845; color: #f9fafb; text-transform: uppercase; letter-spacing: 0.05em; font-size: 11px; }
    tbody tr:nth-child(even) td { background: #f8fafc; }
    .center { text-align: center; }
    .muted { color: #6b7280; }
    .status-paid { font-weight: 700; color: #166534; }
    .status-unpaid { font-weight: 700; color: #9f1239; }
    .amount { white-space: nowrap; font-weight: 700; }
    @page { size: auto; margin: 12mm; }
    @media print {
      body { padding: 0; }
      .print-shell { gap: 12px; }
    }
  </style>
</head>
<body>${contentHtml}</body>
</html>`;
  }

  function removePrintFrame(frame) {
    if (frame && frame.parentNode) {
      frame.parentNode.removeChild(frame);
    }
  }

  function openPrintWindow(title, contentHtml) {
    const documentHtml = buildDocument(title, contentHtml);
    const existingFrame = document.getElementById('tkw-print-frame');
    if (existingFrame) {
      removePrintFrame(existingFrame);
    }

    const frame = document.createElement('iframe');
    frame.id = 'tkw-print-frame';
    frame.setAttribute('aria-hidden', 'true');
    Object.assign(frame.style, {
      position: 'fixed',
      right: '0',
      bottom: '0',
      width: '0',
      height: '0',
      border: '0',
      visibility: 'hidden'
    });

    document.body.appendChild(frame);

    const frameWindow = frame.contentWindow;
    const frameDocument = frame.contentDocument || frameWindow?.document;

    if (!frameWindow || !frameDocument) {
      removePrintFrame(frame);
      throw new Error('No se pudo inicializar el documento de impresión.');
    }

    frameDocument.open();
    frameDocument.write(documentHtml);
    frameDocument.close();

    const triggerPrint = () => {
      frameWindow.focus();
      frameWindow.print();
      window.setTimeout(() => removePrintFrame(frame), 1000);
    };

    if (frameDocument.readyState === 'complete') {
      setTimeout(triggerPrint, 150);
    } else {
      frame.addEventListener('load', () => setTimeout(triggerPrint, 150), { once: true });
    }
  }

  window.TkdPrint = {
    printHtml(title, contentHtml) {
      openPrintWindow(title, contentHtml);
    }
  };
})();
