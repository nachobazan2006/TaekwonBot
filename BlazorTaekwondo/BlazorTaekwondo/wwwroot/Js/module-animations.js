(function () {
  'use strict';

  const observed = new WeakSet();
  let io;

  function formatValue(type, value) {
    if (type === 'currency') {
      return '$' + Math.round(value).toLocaleString('es-AR');
    }

    if (type === 'percent') {
      return Math.round(value).toLocaleString('es-AR') + '%';
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

  function ensureObserver() {
    if (io || !('IntersectionObserver' in window)) return io;

    io = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
          io.unobserve(entry.target);
        }
      });
    }, { threshold: 0.14 });

    return io;
  }

  function initReveal(root) {
    const items = root.querySelectorAll('.ui-reveal');
    if (!items.length) return;

    if (!('IntersectionObserver' in window)) {
      items.forEach((el) => {
        el.classList.add('ui-reveal-ready', 'is-visible');
      });
      return;
    }

    const observer = ensureObserver();
    items.forEach((el) => {
      el.classList.add('ui-reveal-ready');
      if (observed.has(el)) return;
      observed.add(el);
      observer.observe(el);
    });
  }

  function initCounters(root) {
    root.querySelectorAll('.js-module-counter').forEach(animateCounter);
  }

  function refresh() {
    document.querySelectorAll('.inicio-page, .alumnos-page, .cuotas-page, .examenes-page').forEach((root) => {
      initReveal(root);
      initCounters(root);
    });
  }

  window.ModuleAnimations = { refresh };

  function observeDOM() {
    const observer = new MutationObserver(() => {
      refresh();
    });

    observer.observe(document.body, { childList: true, subtree: true });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
      refresh();
      observeDOM();
    });
  } else {
    refresh();
    observeDOM();
  }
})();
