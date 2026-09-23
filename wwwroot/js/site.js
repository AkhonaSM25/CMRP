// CMRP front-end behaviour. Everything here is progressive enhancement: the forms still work with the server rules if this fails.
(function () {
    'use strict';

    var body = document.body;

    // Mobile sidebar
    document.querySelectorAll('[data-sidebar-open]').forEach(function (b) {
        b.addEventListener('click', function () { body.classList.add('sidebar-open'); });
    });
    document.querySelectorAll('[data-sidebar-close]').forEach(function (b) {
        b.addEventListener('click', function () { body.classList.remove('sidebar-open'); });
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') body.classList.remove('sidebar-open');
    });

    // Show/hide password
    document.querySelectorAll('[data-toggle-password]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var input = document.querySelector(btn.getAttribute('data-toggle-password'));
            if (!input) return;
            var show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            btn.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
        });
    });

    // Character counters ("0/500")
    document.querySelectorAll('[data-counter]').forEach(function (field) {
        var out = document.querySelector(field.getAttribute('data-counter'));
        if (!out) return;
        var max = field.getAttribute('maxlength') || '500';
        var update = function () { out.textContent = field.value.length + '/' + max; };
        field.addEventListener('input', update);
        update();
    });

    // Campus -> building/area cascade on the report form
    var campus = document.querySelector('[data-campus-select]');
    var area = document.querySelector('[data-area-select]');
    if (campus && area) {
        var all = Array.prototype.map.call(area.querySelectorAll('option[data-campus]'), function (o) {
            return { value: o.value, text: o.textContent, campus: o.getAttribute('data-campus') };
        });
        var chosen = area.value;

        var render = function (keep) {
            var c = campus.value;
            area.innerHTML = '';
            var placeholder = document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = c ? 'Select building/area' : 'Select a campus first';
            area.appendChild(placeholder);
            all.filter(function (o) { return o.campus === c; }).forEach(function (o) {
                var opt = document.createElement('option');
                opt.value = o.value;
                opt.textContent = o.text;
                opt.setAttribute('data-campus', o.campus);
                if (keep && o.value === keep) opt.selected = true;
                area.appendChild(opt);
            });
        };

        if (chosen) {
            var match = all.filter(function (o) { return o.value === chosen; })[0];
            if (match) campus.value = match.campus;
        }
        render(chosen);
        campus.addEventListener('change', function () { render(''); });
    }

    // Photo drop zone: preview, client-side checks (the server validates again)
    document.querySelectorAll('[data-dropzone]').forEach(function (zone) {
        var input = zone.querySelector('input[type=file]');
        var empty = zone.querySelector('.dropzone-empty');
        var filled = zone.querySelector('.dropzone-file');
        var img = zone.querySelector('.dropzone-preview');
        var nameEl = zone.querySelector('.dropzone-name');
        var clear = zone.querySelector('[data-dropzone-clear]');
        var errorEl = zone.parentElement.querySelector('[data-dropzone-error]');
        var MAX = 5 * 1024 * 1024;
        var url = null;

        var reset = function () {
            if (url) { URL.revokeObjectURL(url); url = null; }
            input.value = '';
            zone.classList.remove('has-file');
            filled.hidden = true;
            empty.hidden = false;
        };
        var fail = function (message) { if (errorEl) errorEl.textContent = message; reset(); };

        input.addEventListener('change', function () {
            if (errorEl) errorEl.textContent = '';
            var file = input.files && input.files[0];
            if (!file) { reset(); return; }
            if (!/\.(png|jpe?g)$/i.test(file.name) || !/^image\/(png|jpeg)$/.test(file.type)) {
                fail('Only PNG or JPG photos can be uploaded.');
                return;
            }
            if (file.size > MAX) { fail('The photo is larger than 5 MB. Please choose a smaller image.'); return; }
            if (url) URL.revokeObjectURL(url);
            url = URL.createObjectURL(file);
            img.src = url;
            nameEl.textContent = file.name;
            empty.hidden = true;
            filled.hidden = false;
            zone.classList.add('has-file');
        });
        if (clear) clear.addEventListener('click', function () { if (errorEl) errorEl.textContent = ''; reset(); });
        ['dragenter', 'dragover'].forEach(function (ev) {
            zone.addEventListener(ev, function () { zone.classList.add('dragover'); });
        });
        ['dragleave', 'drop'].forEach(function (ev) {
            zone.addEventListener(ev, function () { zone.classList.remove('dragover'); });
        });
    });

    // Open the "Update Incident Status" drawer when arriving from the complaints queue
    document.querySelectorAll('[data-open-on-load="true"]').forEach(function (el) {
        if (window.bootstrap && bootstrap.Offcanvas) bootstrap.Offcanvas.getOrCreateInstance(el).show();
    });

    // Stop double submits (two clicks must not create two reports)
    document.querySelectorAll('form[data-submit-lock]').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            setTimeout(function () {
                if (e.defaultPrevented) return;
                form.querySelectorAll('button[type=submit]').forEach(function (b) { b.disabled = true; });
            }, 0);
        });
    });
    window.addEventListener('pageshow', function (e) {
        if (e.persisted) document.querySelectorAll('button[type=submit]').forEach(function (b) { b.disabled = false; });
    });
})();
