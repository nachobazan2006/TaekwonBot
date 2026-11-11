/**
 * Reportes UI
 * Microinteracciones progresivas para el tablero de estadisticas.
 */

(function () {
  'use strict';

  const observed = new WeakSet();

  function formatValue(type, value) {
    if (type === 'currency') {
      return '$' + Math.round(value).toLocaleString('es-AR');
    }

    return Math.round(value).toLocaleString('es-AR');
  }

  function animateCounter(el) {
    const raw = el.dataset.countValue;
    if (!raw) return;

    const value = Number(raw);
    if (Number.isNaN(value)) return;

    const type = el.dataset.countType || 'number';
    const key = `${el.dataset.animateKey || 'default'}:${raw}:${type}`;
    if (el.dataset.lastAnimatedKey === key) return;
    el.dataset.lastAnimatedKey = key;

    const duration = 700;
    const start = performance.now();

    const step = (now) => {
      const progress = Math.min((now - start) / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      el.textContent = formatValue(type, value * eased);
      if (progress < 1) {
        requestAnimationFrame(step);
      } else {
        el.textContent = formatValue(type, value);
      }
    };

    requestAnimationFrame(step);
  }

  function initCounters(root) {
    root.querySelectorAll('.js-reportes-counter').forEach(animateCounter);
  }

  function initReveal(root) {
    if (!('IntersectionObserver' in window)) {
      root.querySelectorAll('.rep-kpi-card, .rep-insight-card, .rep-chart-panel').forEach((el) => {
        el.classList.add('is-visible');
      });
      return;
    }

    const io = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
        }
      });
    }, { threshold: 0.16 });

    root.querySelectorAll('.rep-kpi-card, .rep-insight-card, .rep-chart-panel').forEach((el) => {
      if (observed.has(el)) return;
      observed.add(el);
      io.observe(el);
    });
  }

  function pulsePeriodBanner(root) {
    const banner = root.querySelector('.rep-period-banner');
    if (!banner) return;
    banner.classList.remove('is-pulsing');
    void banner.offsetWidth;
    banner.classList.add('is-pulsing');
  }

  function initSelectFeedback(root) {
    const select = root.querySelector('.rep-period-select');
    if (!select || select.dataset.feedbackInit) return;

    select.dataset.feedbackInit = 'true';
    select.addEventListener('change', () => {
      pulsePeriodBanner(root);
    });
  }

  function refresh() {
    const root = document.querySelector('.reportes-page');
    if (!root) return;

    initCounters(root);
    initReveal(root);
    initSelectFeedback(root);
  }

  window.ReportesUI = { refresh };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', refresh);
  } else {
    refresh();
  }
})();
