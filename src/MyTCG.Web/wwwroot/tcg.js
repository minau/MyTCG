window.tcgFocus = {
  trap(element) {
    element.focus();
    if (element.dataset.trapped) return;
    element.dataset.trapped = 'true';
    element.addEventListener('keydown', event => {
      if (event.key !== 'Tab') return;
      const items = [...element.querySelectorAll('button:not([disabled]),a[href],input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex="0"]')].filter(e => e.getClientRects().length);
      if (!items.length) { event.preventDefault(); return; }
      const first = items[0], last = items[items.length - 1];
      if (event.shiftKey && (document.activeElement === first || document.activeElement === element)) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && (document.activeElement === last || document.activeElement === element)) { event.preventDefault(); first.focus(); }
    });
  }
};
