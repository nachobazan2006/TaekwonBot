/**
 * TAEKWONDO VILLA ALLENDE — Alumnos UI
 * Microinteracciones progresivas. Sin dependencias externas.
 */

(function () {
  'use strict';

  // ── Ripple effect en botones ──────────────────────────────────
  function addRipple(e) {
    const btn = e.currentTarget;
    const existing = btn.querySelector('.ripple');
    if (existing) existing.remove();

    const rect = btn.getBoundingClientRect();
    const size = Math.max(rect.width, rect.height) * 1.5;
    const x = e.clientX - rect.left - size / 2;
    const y = e.clientY - rect.top - size / 2;

    const ripple = document.createElement('span');
    ripple.className = 'ripple';
    Object.assign(ripple.style, {
      width:        size + 'px',
      height:       size + 'px',
      left:         x + 'px',
      top:          y + 'px',
      position:     'absolute',
      borderRadius: '50%',
      background:   'rgba(255,255,255,0.22)',
      transform:    'scale(0)',
      animation:    'rippleAnim 0.5s ease-out forwards',
      pointerEvents:'none',
    });

    btn.appendChild(ripple);
    setTimeout(() => ripple.remove(), 600);
  }

  // ── Inyectar keyframe ripple ──────────────────────────────────
  function injectRippleStyle() {
    if (document.getElementById('ripple-style')) return;
    const style = document.createElement('style');
    style.id = 'ripple-style';
    style.textContent = '@keyframes rippleAnim { to { transform: scale(1); opacity: 0; } }';
    document.head.appendChild(style);
  }

  // ── Ripple en botones primarios ───────────────────────────────
  function initRipples() {
    document.querySelectorAll('.btn-primary, .btn-danger, .btn-edit').forEach(btn => {
      if (btn.dataset.ripple) return;
      btn.dataset.ripple = 'true';
      btn.style.position = 'relative';
      btn.style.overflow = 'hidden';
      btn.addEventListener('click', addRipple);
    });
  }

  // ── Feedback visual en filas de tabla ────────────────────────
  function initTableRowFeedback() {
    document.querySelectorAll('.alumno-table tbody tr').forEach(row => {
      if (row.dataset.fbInit) return;
      row.dataset.fbInit = 'true';
      row.addEventListener('click', function () {
        this.style.transition = 'background 0.1s';
        this.style.background = 'rgba(158,42,43,0.08)';
        setTimeout(() => { this.style.background = ''; }, 160);
      });
    });
  }

  // ── Tooltips en botones deshabilitados ───────────────────────
  function initDisabledTooltips() {
    const tips = {
      '.btn-danger': 'Seleccioná un alumno primero',
      '.btn-edit':   'Seleccioná un alumno primero',
    };
    Object.entries(tips).forEach(([sel, msg]) => {
      document.querySelectorAll(sel).forEach(btn => {
        if (btn.dataset.tipInit) return;
        btn.dataset.tipInit = 'true';
        btn.title = msg;
      });
    });
  }

  // ── Animación de entrada en filas nuevas ─────────────────────
  function animateNewRows() {
    const rows = document.querySelectorAll('.alumno-table tbody tr:not([data-animated])');
    rows.forEach((row, i) => {
      row.dataset.animated = 'true';
      row.style.opacity = '0';
      row.style.transform = 'translateX(-8px)';
      row.style.transition = 'opacity 0.28s ease ' + (i * 28) + 'ms, transform 0.28s ease ' + (i * 28) + 'ms';
      requestAnimationFrame(() => {
        requestAnimationFrame(() => {
          row.style.opacity = '1';
          row.style.transform = 'translateX(0)';
        });
      });
    });
  }

  // ── Focus label highlight ─────────────────────────────────────
  function initInputFeedback() {
    document.querySelectorAll('.form-input, .form-select, .search-input').forEach(el => {
      if (el.dataset.inputInit) return;
      el.dataset.inputInit = 'true';
      el.addEventListener('focus', function () {
        const label = this.closest('.form-group')?.querySelector('label');
        if (label) label.style.color = '#9E2A2B';
      });
      el.addEventListener('blur', function () {
        const label = this.closest('.form-group')?.querySelector('label');
        if (label) label.style.color = '';
      });
    });
  }

  // ── Contador animado (API pública) ────────────────────────────
  function animateCounter(el, target) {
    if (!el) return;
    let current = 0;
    const step = Math.ceil(target / 20);
    const timer = setInterval(() => {
      current = Math.min(current + step, target);
      el.textContent = current;
      if (current >= target) clearInterval(timer);
    }, 30);
  }

  // ── API pública para Blazor ───────────────────────────────────
  window.AlumnosUI = {
    animateCounter,
    refresh() {
      initRipples();
      initTableRowFeedback();
      initDisabledTooltips();
      animateNewRows();
      initInputFeedback();
    }
  };

  // ── MutationObserver — re-init tras re-render de Blazor ───────
  function observeDOM() {
    const target = document.querySelector('.alumnos-page') || document.body;
    const observer = new MutationObserver(() => {
      window.AlumnosUI.refresh();
    });
    observer.observe(target, { childList: true, subtree: true });
  }

  // ── Init ──────────────────────────────────────────────────────
  function init() {
    injectRippleStyle();
    window.AlumnosUI.refresh();
    observeDOM();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

})();
