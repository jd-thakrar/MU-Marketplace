(function () {
    var form = document.getElementById('filterForm');
    var grid = document.getElementById('productGrid');
    if (!form || !grid) return;          // not the marketplace page

    var pageInput = form.querySelector('input[name="Page"]');
    var timer = null;

    function load() {
        var params = new URLSearchParams(new FormData(form)).toString();

        fetch(form.getAttribute('data-search-url') + '?' + params, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            credentials: 'same-origin'
        })
        .then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.text();
        })
        .then(function (html) {
            grid.innerHTML = html;
            history.replaceState(null, '', form.getAttribute('action') + '?' + params);
        })
        .catch(function () {
            grid.innerHTML = '<p class="mp-empty">Could not load items. Please try again.</p>';
        });
    }

    // Any change to the filter goes back to page 1, after a short pause while typing
    form.addEventListener('input', function () {
        pageInput.value = 1;
        clearTimeout(timer);
        timer = setTimeout(load, 300);
    });

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        pageInput.value = 1;
        load();
    });

    // Pager links inside the grid
    grid.addEventListener('click', function (e) {
        var link = e.target.closest('[data-page]');
        if (!link) return;
        e.preventDefault();
        pageInput.value = link.getAttribute('data-page');
        load();
    });
})();
