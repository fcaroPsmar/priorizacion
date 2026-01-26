(function () {
  const list = document.getElementById('plazas-list');
  const orderedIds = document.getElementById('orderedIds');
  if (!list || !orderedIds) return;

  let dragged = null;

  function renumber() {
    const items = Array.from(list.querySelectorAll('[data-plaza-id]'));
    items.forEach((li, idx) => {
      const badge = li.querySelector('.order-badge');
      if (badge) badge.textContent = String(idx + 1);
    });
    orderedIds.value = items.map(li => li.getAttribute('data-plaza-id')).join(',');
  }

  function isBefore(a, b) {
    if (a.parentNode !== b.parentNode) return false;
    for (let cur = a.previousSibling; cur; cur = cur.previousSibling) {
      if (cur === b) return true;
    }
    return false;
  }

  list.addEventListener('dragstart', (e) => {
    if (e.target.closest('.delete-button')) return;
    const li = e.target.closest('[data-plaza-id]');
    if (!li) return;
    dragged = li;
    li.classList.add('dragging');
    e.dataTransfer.effectAllowed = 'move';
  });

  list.addEventListener('dragend', (e) => {
    const li = e.target.closest('[data-plaza-id]');
    if (li) li.classList.remove('dragging');
    dragged = null;
    renumber();
  });

  list.addEventListener('dragover', (e) => {
    e.preventDefault();
    const li = e.target.closest('[data-plaza-id]');
    if (!li || !dragged || li === dragged) return;

    const rect = li.getBoundingClientRect();
    const next = (e.clientY - rect.top) / (rect.bottom - rect.top) > 0.5;

    if (next && li.nextSibling !== dragged) {
      list.insertBefore(dragged, li.nextSibling);
    } else if (!next && li !== dragged.nextSibling) {
      list.insertBefore(dragged, li);
    }
  });

  list.addEventListener('click', (e) => {
    const deleteButton = e.target.closest('.delete-button');
    if (!deleteButton) return;
    e.preventDefault();
    e.stopPropagation();
    const li = deleteButton.closest('[data-plaza-id]');
    if (!li) return;
    li.remove();
    renumber();
  });

  // Init
  renumber();
})();
