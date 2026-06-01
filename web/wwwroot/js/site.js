// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// =========================================================
//  Toast – Global notifikation
//  Brug: showToast('Besked her')               → success
//        showToast('Noget gik galt', 'error')   → fejl
//        showToast('Vær opmærksom', 'warning')  → advarsel
//        showToast('Info til dig', 'info')       → info
//  Lukker automatisk efter 3 sekunder.
// =========================================================
(function () {
    let container = null;

    function getContainer() {
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container';
            document.body.appendChild(container);
        }
        return container;
    }

    const ICONS = {
        success: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>`,
        error:   `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>`,
        warning: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>`,
        info:    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>`
    };

    window.showToast = function (message, type) {
        type = type || 'success';
        const c = getContainer();

        const toast = document.createElement('div');
        toast.className = 'toast toast-' + type;
        toast.innerHTML =
            '<span class="toast-icon">' + (ICONS[type] || ICONS.info) + '</span>' +
            '<span class="toast-msg">' + message + '</span>' +
            '<button class="toast-close" aria-label="Luk">&#x2715;</button>';

        c.appendChild(toast);

        // Trigger animation
        requestAnimationFrame(() => toast.classList.add('toast-show'));

        const dismiss = () => {
            toast.classList.remove('toast-show');
            toast.addEventListener('transitionend', () => toast.remove(), { once: true });
        };

        toast.querySelector('.toast-close').addEventListener('click', dismiss);

        setTimeout(dismiss, 3000);
    };
})();

/* ── Topbar dropdown (···) + Rediger + Slet opskrift ──────────────────── */
(function () {
    const csrfToken = () => document.querySelector('meta[name="csrf-token"]')?.content ?? '';

    // ── Dropdown toggle ──────────────────────────────────────────────
    document.addEventListener('click', function (e) {
        // Åbn/luk dropdown
        const menuBtn = e.target.closest('.js-topbar-menu-open');
        if (menuBtn) {
            const dd = menuBtn.closest('.topbar-more-wrap').querySelector('.js-topbar-dropdown');
            const isOpen = dd.style.display !== 'none';
            // Luk alle andre
            document.querySelectorAll('.js-topbar-dropdown').forEach(d => d.style.display = 'none');
            dd.style.display = isOpen ? 'none' : 'block';
            return;
        }
        // Klik udenfor lukker
        if (!e.target.closest('.topbar-more-wrap')) {
            document.querySelectorAll('.js-topbar-dropdown').forEach(d => d.style.display = 'none');
        }
    });

    // ── PRINT ────────────────────────────────────────────────────────
    document.addEventListener('click', function (e) {
        if (!e.target.closest('.js-print-recipe')) return;
        document.querySelectorAll('.js-topbar-dropdown').forEach(d => d.style.display = 'none');
        window.print();
    });

    // ── DOWNLOAD PDF ─────────────────────────────────────────────────
    document.addEventListener('click', function (e) {
        const link = e.target.closest('.js-download-pdf');
        if (!link) return;
        e.preventDefault();
        document.querySelectorAll('.js-topbar-dropdown').forEach(d => d.style.display = 'none');

        // Sæt loading-tilstand på knappen
        const svgEl   = link.querySelector('svg');
        const spinner = `<svg class="pdf-spinner" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-6.219-8.56"/></svg>`;
        const origSvg = svgEl ? svgEl.outerHTML : '';
        if (svgEl) svgEl.outerHTML = spinner;
        const textNode = [...link.childNodes].find(n => n.nodeType === 3 && n.textContent.trim());
        const origText = textNode ? textNode.textContent : '';
        if (textNode) textNode.textContent = ' Henter...';
        link.style.pointerEvents = 'none';
        link.style.opacity = '0.65';

        // Trigger download via skjult iframe, så vi kan nulstille knappen bagefter
        const url   = link.href;
        const iframe = document.createElement('iframe');
        iframe.style.display = 'none';
        document.body.appendChild(iframe);

        const reset = () => {
            // Gendan knappen
            const newSpinner = link.querySelector('.pdf-spinner');
            if (newSpinner && origSvg) newSpinner.outerHTML = origSvg;
            const tn = [...link.childNodes].find(n => n.nodeType === 3 && n.textContent.trim());
            if (tn) tn.textContent = origText;
            link.style.pointerEvents = '';
            link.style.opacity = '';
            iframe.remove();
        };

        iframe.onload = reset;
        // Fallback: nulstil efter 15 sek hvis onload ikke fyrer
        setTimeout(reset, 15000);
        iframe.src = url;
    });

    // ── REDIGER ──────────────────────────────────────────────────────
    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.js-edit-recipe');
        if (!btn) return;

        // Luk dropdown
        document.querySelectorAll('.js-topbar-dropdown').forEach(d => d.style.display = 'none');

        const id     = btn.dataset.id;
        const modal  = document.querySelector('.cr-modal');
        const bkdrop = document.querySelector('.cr-backdrop');
        if (!modal) return;

        // Sæt edit-mode flag
        modal.dataset.editId = id;
        modal.querySelector('.js-cr-modal-title').textContent = 'Rediger opskrift';

        // Hent data
        let data;
        try {
            const res = await fetch(`/Recipes/GetRecipeData?id=${id}`);
            data = await res.json();
        } catch {
            showToast('Kunne ikke hente opskrift', 'error');
            return;
        }

        // Reset og udfyld modal
        const cr = window._crHelpers;
        if (cr) cr.reset(modal);

        modal.querySelector('.js-cr-title').value    = data.title;
        modal.querySelector('.js-cr-prep').value     = data.prepTimeMinutes;
        modal.querySelector('.js-cr-cook').value     = data.cookTimeMinutes;
        modal.querySelector('.js-cr-servings').value = data.servings;
        modal.querySelector('.js-cr-notes').value    = data.notes ?? '';

        // Kategori
        const catVal  = modal.querySelector('.js-cr-cat-value');
        const catIcon = modal.querySelector('.js-cr-caticon-value');
        const catLbl  = modal.querySelector('.js-cr-cat-label');
        const catImg  = modal.querySelector('.js-cr-cat-icon-img');
        if (data.category) {
            catVal.value  = data.category;
            catIcon.value = data.categoryIcon || 'cookie';
            catLbl.textContent = data.category;
            catLbl.classList.add('selected');
            catImg.src = `/icons/${data.categoryIcon || 'cookie'}.svg`;
        }

        // Sværhedsgrad
        modal.querySelectorAll('.cr-diff-chip').forEach(c => {
            c.classList.toggle('active', c.dataset.value === data.difficulty);
        });

        // Ingredienser
        const ingList = modal.querySelector('.js-cr-ing-list');
        ingList.innerHTML = '';
        (data.ingredients || []).forEach(ing => {
            if (cr) cr.addIngRow(ingList, ing.amount, ing.unit, ing.name);
        });

        // Trin
        const stepList = modal.querySelector('.js-cr-step-list');
        stepList.innerHTML = '';
        (data.steps || []).forEach(text => {
            if (cr) cr.addStepRow(stepList, text);
        });

        // Billede preview
        if (data.imagePath) {
            const preview = modal.querySelector('.js-cr-image-preview');
            preview.src = data.imagePath;
            preview.style.display = 'block';
            modal.querySelector('.cr-image-placeholder-icon').style.display = 'none';
            modal.querySelector('.cr-image-placeholder-text').style.display = 'none';
        }

        bkdrop.classList.add('is-open');
        modal.classList.add('is-open');
        modal.querySelector('.js-cr-title').focus();
    });

    // ── SLET – åbn bekræftigelse ─────────────────────────────────────
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('.js-delete-recipe-open');
        if (!btn) return;
        document.querySelectorAll('.js-topbar-dropdown').forEach(d => d.style.display = 'none');
        const overlay = document.querySelector('.js-del-recipe-overlay');
        if (!overlay) return;
        overlay.querySelector('.js-del-recipe-name').textContent = btn.dataset.title;
        overlay.dataset.id = btn.dataset.id;
        overlay.classList.add('is-open');
    });

    // ── SLET – annuller ──────────────────────────────────────────────
    document.addEventListener('click', function (e) {
        if (e.target.closest('.js-del-recipe-cancel')) {
            const overlay = document.querySelector('.js-del-recipe-overlay');
            if (overlay) overlay.classList.remove('is-open');
        }
    });

    // ── SLET – bekræft ───────────────────────────────────────────────
    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.js-del-recipe-confirm');
        if (!btn) return;
        const overlay = document.querySelector('.js-del-recipe-overlay');
        const id = overlay?.dataset.id;
        if (!id) return;

        btn.disabled = true;
        try {
            const res  = await fetch('/Recipes/DeleteRecipe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded',
                           'RequestVerificationToken': csrfToken() },
                body: `id=${id}`
            });
            const data = await res.json();
            if (data.ok) {
                showToast('Opskrift slettet', 'success');
                overlay.classList.remove('is-open');
                setTimeout(() => window.location.href = '/Recipes', 600);
            } else {
                showToast(data.error || 'Sletning fejlede', 'error');
                btn.disabled = false;
            }
        } catch {
            showToast('Netværksfejl', 'error');
            btn.disabled = false;
        }
    });
})();

/* ── Favorit toggle ────────────────────────────────────────────────────── */
(function () {
    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.js-toggle-fav');
        if (!btn) return;

        const id = btn.dataset.id;

        // Lille bounce-animation
        btn.classList.add('animating');
        setTimeout(() => btn.classList.remove('animating'), 200);

        try {
            const res  = await fetch(`/Recipes/ToggleFavorite`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded',
                           'RequestVerificationToken': document.querySelector('meta[name="csrf-token"]')?.content ?? '' },
                body: `id=${id}`
            });
            const data = await res.json();
            if (data.ok) {
                btn.classList.toggle('fav', data.isFavorite);
                // Opdater stjerne i opskriftslisten hvis den er synlig
                const listItem = document.querySelector(`.recipe-list-item[href*="/Details/${id}"], .recipe-list-item[href*="/Details/${id}"]`);
                if (listItem) {
                    let star = listItem.querySelector('.star-btn');
                    if (data.isFavorite && !star) {
                        star = document.createElement('span');
                        star.className = 'star-btn fav';
                        star.innerHTML = '<img src="/icons/star.svg" alt="Favorit" />';
                        listItem.querySelector('.recipe-list-actions').prepend(star);
                    } else if (!data.isFavorite && star) {
                        star.remove();
                    }
                }
                showToast(data.isFavorite ? 'Tilføjet til favoritter' : 'Fjernet fra favoritter', 'success');
            }
        } catch {
            showToast('Kunne ikke opdatere favorit', 'error');
        }
    });
})();

/* ── Expand / collapse detail panel (Skalér-knap) ─────────────────────── */
(function () {
    document.addEventListener('click', function (e) {
        // Toggle expand via Skalér-knap
        const btn = e.target.closest('.js-expand-detail');
        if (btn) {
            const shell    = document.querySelector('.app-shell');
            const label    = btn.querySelector('.js-expand-label');
            const expanded = shell.classList.toggle('detail-expanded');
            if (label) label.textContent = expanded ? 'Skjul' : 'Skalér';
            return;
        }

        // Klik på Tilbage mens expanded: kollaps uden navigation
        const backBtn = e.target.closest('.back-btn');
        if (backBtn) {
            const shell = document.querySelector('.app-shell');
            if (shell && shell.classList.contains('detail-expanded')) {
                e.preventDefault();
                const label = document.querySelector('.js-expand-label');
                shell.classList.remove('detail-expanded');
                if (label) label.textContent = 'Skalér';
            }
        }
    });
})();

/* ── Opret ny opskrift – modal (cr-*) ──────────────────────────────── */
(function () {
    // ── Hjælpefunktioner ────────────────────────────────────────────
    function svgArrow(dir) {
        // dir: 'up' | 'down'
        const d = dir === 'up' ? 'M6 10 L10 5 L14 10' : 'M6 5 L10 10 L14 5';
        return `<svg viewBox="0 0 20 15" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="${d}"/></svg>`;
    }

    function reindexSteps(list) {
        list.querySelectorAll('.cr-step-row').forEach((row, i) => {
            const num = row.querySelector('.cr-step-num');
            if (num) num.textContent = i + 1;
        });
    }

    function updateSortBtns(list) {
        const rows = list.querySelectorAll(':scope > .cr-ing-row, :scope > .cr-step-row');
        rows.forEach((row, i) => {
            const up   = row.querySelector('.cr-sort-up');
            const down = row.querySelector('.cr-sort-down');
            if (up)   up.disabled   = (i === 0);
            if (down) down.disabled = (i === rows.length - 1);
        });
    }

    function makeRowActions(list, isStep) {
        const wrap = document.createElement('div');
        wrap.className = 'cr-row-actions';

        const up = document.createElement('button');
        up.type = 'button';
        up.className = 'cr-sort-btn cr-sort-up';
        up.title = 'Flyt op';
        up.innerHTML = svgArrow('up');

        const down = document.createElement('button');
        down.type = 'button';
        down.className = 'cr-sort-btn cr-sort-down';
        down.title = 'Flyt ned';
        down.innerHTML = svgArrow('down');

        const del = document.createElement('button');
        del.type = 'button';
        del.className = 'cr-del-btn';
        del.title = 'Slet';
        del.innerHTML = '<img src="/icons/trash.svg" alt="Slet" />';

        up.addEventListener('click', () => {
            const row  = up.closest('.cr-ing-row, .cr-step-row');
            const prev = row.previousElementSibling;
            if (prev) list.insertBefore(row, prev);
            updateSortBtns(list);
            if (isStep) reindexSteps(list);
        });
        down.addEventListener('click', () => {
            const row  = down.closest('.cr-ing-row, .cr-step-row');
            const next = row.nextElementSibling;
            if (next) list.insertBefore(next, row);
            updateSortBtns(list);
            if (isStep) reindexSteps(list);
        });
        del.addEventListener('click', () => {
            del.closest('.cr-ing-row, .cr-step-row').remove();
            updateSortBtns(list);
            if (isStep) reindexSteps(list);
        });

        wrap.appendChild(up);
        wrap.appendChild(down);
        wrap.appendChild(del);
        return wrap;
    }

    function addIngRow(list, amount = '', unit = '', name = '') {
        const row = document.createElement('div');
        row.className = 'cr-ing-row';
        row.innerHTML = `
            <input class="cr-ing-input" type="text" placeholder="fx. 200" name="ingAmount" value="${amount}" />
            <input class="cr-ing-input" type="text" placeholder="fx. g" name="ingUnit" value="${unit}" list="ing-units-list" autocomplete="off" />
            <input class="cr-ing-input" type="text" placeholder="fx. mel" name="ingName" value="${name}" list="ing-names-list" autocomplete="off" />`;
        row.appendChild(makeRowActions(list, false));
        list.appendChild(row);
        updateSortBtns(list);
        if (!amount && !unit && !name) row.querySelector('input').focus();
    }

    function addStepRow(list, initialText = '') {
        const idx  = list.querySelectorAll('.cr-step-row').length + 1;
        const row  = document.createElement('div');
        row.className = 'cr-step-row';
        row.innerHTML = `<div class="cr-step-num">${idx}</div>
            <textarea class="cr-step-textarea" name="stepText" rows="2" placeholder="Beskriv trinnet…"></textarea>`;
        row.querySelector('textarea').value = initialText;
        row.appendChild(makeRowActions(list, true));

        // Auto-resize textarea
        row.querySelector('textarea').addEventListener('input', function () {
            this.style.height = 'auto';
            this.style.height = this.scrollHeight + 'px';
        });

        list.appendChild(row);
        updateSortBtns(list);
        if (!initialText) row.querySelector('textarea').focus();
    }

    // ── Kategori dropdown ────────────────────────────────────────────
    function initCatDropdown(modal) {
        const chosen    = modal.querySelector('.js-cr-cat-chosen');
        const dropdown  = modal.querySelector('.js-cr-cat-dropdown');
        const listEl    = modal.querySelector('.js-cr-cat-list');
        const searchEl  = modal.querySelector('.js-cr-cat-search');
        const valInput  = modal.querySelector('.js-cr-cat-value');
        const iconInput = modal.querySelector('.js-cr-caticon-value');
        const labelEl   = modal.querySelector('.js-cr-cat-label');
        const iconImg   = modal.querySelector('.js-cr-cat-icon-img');

        let cats = [];
        let loaded = false;

        async function loadCats() {
            if (loaded) return;
            loaded = true;
            try {
                const res  = await fetch('/Recipes/GetCategories');
                cats = await res.json();
                renderCats(cats);
            } catch {
                listEl.innerHTML = '<div class="cr-cat-loading">Kunne ikke hente kategorier</div>';
            }
        }

        function renderCats(list) {
            listEl.innerHTML = list.length === 0
                ? '<div class="cr-cat-loading">Ingen kategorier</div>'
                : list.map(c =>
                    `<div class="cr-cat-option" data-name="${c.name}" data-icon="${c.icon}">
                        <img src="/icons/${c.icon}.svg" alt="" />
                        <span>${c.name}</span>
                    </div>`
                ).join('');
            listEl.querySelectorAll('.cr-cat-option').forEach(opt => {
                opt.addEventListener('click', () => {
                    valInput.value   = opt.dataset.name;
                    iconInput.value  = opt.dataset.icon;
                    labelEl.textContent = opt.dataset.name;
                    labelEl.classList.add('selected');
                    iconImg.src = `/icons/${opt.dataset.icon}.svg`;
                    dropdown.style.display = 'none';
                });
            });
        }

        chosen.addEventListener('click', async (e) => {
            e.stopPropagation();
            const open = dropdown.style.display !== 'none';
            dropdown.style.display = open ? 'none' : 'flex';
            if (!open) { await loadCats(); searchEl.focus(); }
        });

        searchEl.addEventListener('input', () => {
            const q = searchEl.value.toLowerCase();
            renderCats(cats.filter(c => c.name.toLowerCase().includes(q)));
        });

        document.addEventListener('click', (e) => {
            if (!modal.querySelector('.cr-cat-select').contains(e.target))
                dropdown.style.display = 'none';
        });
    }

    // ── Billede ──────────────────────────────────────────────────────
    function initImagePicker(modal) {
        const area    = modal.querySelector('.js-cr-image-area');
        const input   = modal.querySelector('.js-cr-image-input');
        const preview = modal.querySelector('.js-cr-image-preview');
        const placeholderIcon = area.querySelector('.cr-image-placeholder-icon');
        const placeholderText = area.querySelector('.cr-image-placeholder-text');

        area.addEventListener('click', () => input.click());
        input.addEventListener('change', () => {
            const file = input.files[0];
            if (!file) return;
            const url = URL.createObjectURL(file);
            // Åbn beskæringsværktøj
            window._imageCropper.open(url, (croppedBlob) => {
                modal._croppedImageBlob = croppedBlob;
                const blobUrl = URL.createObjectURL(croppedBlob);
                preview.src = blobUrl;
                preview.style.display = 'block';
                placeholderIcon.style.display = 'none';
                placeholderText.style.display = 'none';
            });
        });
    }

    // ── Gem ─────────────────────────────────────────────────────────
    async function saveRecipe(modal) {
        const title = modal.querySelector('.js-cr-title').value.trim();
        if (!title) {
            modal.querySelector('.js-cr-title').focus();
            showToast('Titel er påkrævet', 'warning');
            return;
        }

        const editId   = modal.dataset.editId || '';
        const isEdit   = !!editId;
        const endpoint = isEdit ? '/Recipes/UpdateRecipe' : '/Recipes/Create';

        const diff = modal.querySelector('.cr-diff-chip.active')?.dataset.value || 'Middel';
        const formData = new FormData();
        if (isEdit) formData.append('id', editId);
        formData.append('title',           title);
        formData.append('category',        modal.querySelector('.js-cr-cat-value').value);
        formData.append('categoryIcon',    modal.querySelector('.js-cr-caticon-value').value);
        formData.append('prepTimeMinutes', modal.querySelector('.js-cr-prep').value);
        formData.append('cookTimeMinutes', modal.querySelector('.js-cr-cook').value);
        formData.append('servings',        modal.querySelector('.js-cr-servings').value);
        formData.append('difficulty',      diff);
        formData.append('notes',           modal.querySelector('.js-cr-notes').value);

        const croppedBlob = modal._croppedImageBlob;
        const imageFile   = modal.querySelector('.js-cr-image-input').files[0];
        if (croppedBlob) {
            formData.append('image', croppedBlob, 'billede.webp');
        } else if (imageFile) {
            formData.append('image', imageFile);
        }

        modal.querySelectorAll('.cr-ing-row').forEach(row => {
            formData.append('ingAmount', row.querySelector('[name="ingAmount"]').value);
            formData.append('ingUnit',   row.querySelector('[name="ingUnit"]').value);
            formData.append('ingName',   row.querySelector('[name="ingName"]').value);
        });
        modal.querySelectorAll('.cr-step-row textarea').forEach(ta => {
            formData.append('stepText', ta.value);
        });

        const saveBtn = modal.querySelector('.js-cr-save');
        saveBtn.disabled = true;

        try {
            const res  = await fetch(endpoint, { method: 'POST', body: formData });
            const data = await res.json();
            if (data.ok) {
                showToast(isEdit ? 'Opskrift opdateret!' : 'Opskrift gemt!', 'success');
                closeModal();
                window.location.href = `/Recipes/Details/${data.id}`;
            } else {
                showToast(data.error || 'Noget gik galt', 'error');
            }
        } catch {
            showToast('Netværksfejl – prøv igen', 'error');
        } finally {
            saveBtn.disabled = false;
        }
    }

    // ── Reset ────────────────────────────────────────────────────────
    function resetModal(modal) {
        modal.querySelector('.js-cr-title').value     = '';
        modal.querySelector('.js-cr-prep').value      = '0';
        modal.querySelector('.js-cr-cook').value      = '0';
        modal.querySelector('.js-cr-servings').value  = '4';
        modal.querySelector('.js-cr-notes').value     = '';

        // Category
        modal.querySelector('.js-cr-cat-value').value    = '';
        modal.querySelector('.js-cr-caticon-value').value = 'cookie';
        modal.querySelector('.js-cr-cat-label').textContent = 'Vælg kategori';
        modal.querySelector('.js-cr-cat-label').classList.remove('selected');
        modal.querySelector('.js-cr-cat-icon-img').src = '/icons/cookie.svg';
        modal.querySelector('.js-cr-cat-dropdown').style.display = 'none';

        // Difficulty
        modal.querySelectorAll('.cr-diff-chip').forEach(c => {
            c.classList.toggle('active', c.dataset.value === 'Middel');
        });

        // Image
        const imgInput   = modal.querySelector('.js-cr-image-input');
        const imgPreview = modal.querySelector('.js-cr-image-preview');
        imgInput.value   = '';
        imgPreview.style.display = 'none';
        imgPreview.src   = '';
        modal._croppedImageBlob = null;
        modal.querySelector('.cr-image-placeholder-icon').style.display = '';
        modal.querySelector('.cr-image-placeholder-text').style.display = '';

        // Ingredient + step rows
        modal.querySelector('.js-cr-ing-list').innerHTML  = '';
        modal.querySelector('.js-cr-step-list').innerHTML = '';
    }

    // ── Ingredient suggestions (datalist) ───────────────────────────
    let suggestionsLoaded = false;
    async function loadIngredientSuggestions() {
        if (suggestionsLoaded) return;
        suggestionsLoaded = true;
        try {
            const res  = await fetch('/Recipes/GetIngredientSuggestions');
            const data = await res.json();
            const unitsList  = document.getElementById('ing-units-list');
            const namesList  = document.getElementById('ing-names-list');
            if (unitsList) unitsList.innerHTML = data.units.map(u => `<option value="${u}">`).join('');
            if (namesList) namesList.innerHTML = data.names.map(n => `<option value="${n}">`).join('');
        } catch { /* silent – suggestions are a nicety */ }
    }

    // ── Open / Close ─────────────────────────────────────────────────
    const backdrop = document.querySelector('.cr-backdrop');
    const modal    = document.querySelector('.cr-modal');
    if (!modal || !backdrop) return;

    function openModal() {
        delete modal.dataset.editId;
        modal.querySelector('.js-cr-modal-title').textContent = 'Ny opskrift';
        resetModal(modal);
        loadIngredientSuggestions();
        addIngRow(modal.querySelector('.js-cr-ing-list'));
        addStepRow(modal.querySelector('.js-cr-step-list'));
        backdrop.classList.add('is-open');
        modal.classList.add('is-open');
        modal.querySelector('.js-cr-title').focus();
    }

    function closeModal() {
        backdrop.classList.remove('is-open');
        modal.classList.remove('is-open');
    }

    // Eksponér helpers til edit-flow
    window._crHelpers = {
        reset:      (m) => resetModal(m),
        addIngRow:  (list, amount, unit, name) => addIngRow(list, amount, unit, name),
        addStepRow: (list, text) => addStepRow(list, text),
    };

    // Initialiser sub-modules
    initCatDropdown(modal);
    initImagePicker(modal);

    // Difficulty chips
    modal.querySelectorAll('.cr-diff-chip').forEach(chip => {
        chip.addEventListener('click', () => {
            modal.querySelectorAll('.cr-diff-chip').forEach(c => c.classList.remove('active'));
            chip.classList.add('active');
        });
    });

    // Add-row buttons
    modal.querySelector('.js-cr-add-ing').addEventListener('click',  () => addIngRow(modal.querySelector('.js-cr-ing-list')));
    modal.querySelector('.js-cr-add-step').addEventListener('click', () => addStepRow(modal.querySelector('.js-cr-step-list')));

    // Save
    modal.querySelector('.js-cr-save').addEventListener('click', () => saveRecipe(modal));

    // Close
    modal.querySelector('.js-cr-close').addEventListener('click', closeModal);
    backdrop.addEventListener('click', closeModal);

    // Open from "+" button in list panel
    document.addEventListener('click', e => {
        if (e.target.closest('.js-cr-open')) openModal();
    });
})();

(function () {
    function initRecipeList(panel) {
        const searchInput  = panel.querySelector('.js-recipe-search');
        const filterBtn    = panel.querySelector('.js-filter-open');
        const backdrop     = panel.querySelector('.filter-backdrop');
        const drawer       = panel.querySelector('.filter-drawer');

        if (!searchInput || !filterBtn || !backdrop || !drawer) return;

        const closeBtn     = drawer.querySelector('.js-filter-close');
        const clearBtn     = drawer.querySelector('.js-filter-clear');
        const applyBtn     = drawer.querySelector('.js-filter-apply');
        const countBadge   = drawer.querySelector('.js-filter-count');
        const footerCount  = panel.querySelector('.js-recipe-count');
        const allItems     = () => Array.from(panel.querySelectorAll('.recipe-list-item'));

        // ── State ────────────────────────────────────────────────
        const STORAGE_KEY = 'recipeFilter';

        function loadState() {
            try {
                const raw = sessionStorage.getItem(STORAGE_KEY);
                if (!raw) return;
                const s = JSON.parse(raw);
                activeCats  = new Set(s.cats  || []);
                activeDiffs = new Set(s.diffs || []);
                if (s.search) searchInput.value = s.search;

                // Gendan checkboxes og chips
                drawer.querySelectorAll('.filter-cat-cb').forEach(cb => {
                    cb.checked = activeCats.has(cb.value);
                });
                drawer.querySelectorAll('.filter-chip').forEach(chip => {
                    chip.classList.toggle('active', activeDiffs.has(chip.dataset.value));
                });
            } catch { /* ignore */ }
        }

        function saveState() {
            sessionStorage.setItem(STORAGE_KEY, JSON.stringify({
                cats:   [...activeCats],
                diffs:  [...activeDiffs],
                search: searchInput.value,
            }));
        }

        let activeCats  = new Set();
        let activeDiffs = new Set();

        // ── Helpers ──────────────────────────────────────────────
        function matches(item) {
            const q    = searchInput.value.trim().toLowerCase();
            const title = item.dataset.title || '';
            const cat   = item.dataset.category || '';
            const diff  = item.dataset.difficulty || '';

            if (q && !title.includes(q) && !cat.toLowerCase().includes(q)) return false;
            if (activeCats.size  && !activeCats.has(cat))   return false;
            if (activeDiffs.size && !activeDiffs.has(diff)) return false;
            return true;
        }

        function applyFilters(hide) {
            let visible = 0;
            allItems().forEach(item => {
                const show = matches(item);
                if (hide) {
                    item.style.display = show ? '' : 'none';
                }
                if (show) visible++;
            });
            if (countBadge) countBadge.textContent = visible;
            if (footerCount) footerCount.textContent = visible;
            return visible;
        }

        function updateFilterActiveState() {
            const hasFilter = activeCats.size > 0 || activeDiffs.size > 0;
            filterBtn.classList.toggle('filter-active', hasFilter);
        }

        // ── Search (live) ────────────────────────────────────────
        searchInput.addEventListener('input', () => { applyFilters(true); saveState(); });

        // ── Drawer open / close ───────────────────────────────────
        function openDrawer() {
            applyFilters(false); // update count without hiding yet
            drawer.classList.add('is-open');
            backdrop.classList.add('is-open');
        }
        function closeDrawer() {
            drawer.classList.remove('is-open');
            backdrop.classList.remove('is-open');
        }

        filterBtn.addEventListener('click', openDrawer);
        if (closeBtn) closeBtn.addEventListener('click', closeDrawer);
        backdrop.addEventListener('click', closeDrawer);

        // ── Category checkboxes ───────────────────────────────────
        drawer.querySelectorAll('.filter-cat-cb').forEach(cb => {
            cb.addEventListener('change', () => {
                if (cb.checked) activeCats.add(cb.value);
                else activeCats.delete(cb.value);
                applyFilters(false);
                saveState();
            });
        });

        // ── Difficulty chips ─────────────────────────────────────
        drawer.querySelectorAll('.filter-chip').forEach(chip => {
            chip.addEventListener('click', () => {
                chip.classList.toggle('active');
                const v = chip.dataset.value;
                if (chip.classList.contains('active')) activeDiffs.add(v);
                else activeDiffs.delete(v);
                applyFilters(false);
                saveState();
            });
        });

        // ── Ryd alle ─────────────────────────────────────────────
        if (clearBtn) {
            clearBtn.addEventListener('click', () => {
                activeCats.clear();
                activeDiffs.clear();
                searchInput.value = '';
                drawer.querySelectorAll('.filter-cat-cb').forEach(cb => cb.checked = false);
                drawer.querySelectorAll('.filter-chip').forEach(c => c.classList.remove('active'));
                sessionStorage.removeItem(STORAGE_KEY);
                saveState();
                applyFilters(true);
                updateFilterActiveState();
                closeDrawer();
            });
        }

        // ── Vis resultater
        if (applyBtn) {
            applyBtn.addEventListener('click', () => {
                saveState();
                applyFilters(true);
                updateFilterActiveState();
                closeDrawer();
            });
        }

        // Gendan gemt filter og anvend
        loadState();
        applyFilters(true);
        updateFilterActiveState();
    }

    function setup() {
        document.querySelectorAll('.list-panel').forEach(initRecipeList);
    }

            if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', setup);
        } else {
            setup();
        }
    })();

    /* ── Besk\u00e6r billede – 16:9 crop-v\u00e6rkt\u00f8j ───────────────────────────────── */
    (function () {
        const ASPECT = 16 / 9;
        const MAX_OUTPUT_W = 1600;

        const overlay   = document.querySelector('.js-crop-overlay');
        const canvasEl  = document.querySelector('.js-crop-canvas');
        const cropBox   = document.querySelector('.js-crop-box');
        const wrap      = document.querySelector('.js-crop-canvas-wrap');
        const cancelBtn = document.querySelector('.js-crop-cancel');
        const confirmBtn = document.querySelector('.js-crop-confirm');

        if (!overlay || !canvasEl) return;

        let img, imgW, imgH, canvasRect;
        let boxX, boxY, boxW, boxH;
        let doneCb = null;

        // Beregn startposition: maksimal 16:9 boks centreret
        function initBox() {
            canvasRect = canvasEl.getBoundingClientRect();
            const cw = canvasEl.offsetWidth;
            const ch = canvasEl.offsetHeight;
            if (cw / ch > ASPECT) {
                boxH = ch * 0.9;
                boxW = boxH * ASPECT;
            } else {
                boxW = cw * 0.9;
                boxH = boxW / ASPECT;
            }
            boxX = (cw - boxW) / 2;
            boxY = (ch - boxH) / 2;
            renderBox();
        }

        function renderBox() {
            const offX = canvasEl.offsetLeft;
            const offY = canvasEl.offsetTop;
            cropBox.style.left   = (offX + boxX) + 'px';
            cropBox.style.top    = (offY + boxY) + 'px';
            cropBox.style.width  = boxW + 'px';
            cropBox.style.height = boxH + 'px';
        }

        function clamp(val, min, max) { return Math.max(min, Math.min(max, val)); }

        // Drag move
        let dragMode = null; // 'move' | 'nw'|'ne'|'sw'|'se'
        let dragStart = null;
        let boxAtDragStart = null;

        function onPointerDown(e) {
            e.preventDefault();
            canvasRect = canvasEl.getBoundingClientRect();
            const handle = e.target.closest('.crop-handle');
            if (handle) {
                if (handle.classList.contains('js-crop-nw')) dragMode = 'nw';
                else if (handle.classList.contains('js-crop-ne')) dragMode = 'ne';
                else if (handle.classList.contains('js-crop-sw')) dragMode = 'sw';
                else if (handle.classList.contains('js-crop-se')) dragMode = 'se';
            } else if (e.target.closest('.js-crop-box')) {
                dragMode = 'move';
            } else {
                return;
            }
            dragStart = { x: e.clientX, y: e.clientY };
            boxAtDragStart = { x: boxX, y: boxY, w: boxW, h: boxH };
            window.addEventListener('pointermove', onPointerMove);
            window.addEventListener('pointerup', onPointerUp, { once: true });
        }

        function onPointerMove(e) {
            if (!dragMode) return;
            const cw = canvasEl.offsetWidth;
            const ch = canvasEl.offsetHeight;
            const dx = e.clientX - dragStart.x;
            const dy = e.clientY - dragStart.y;
            const MIN_W = 60;

            if (dragMode === 'move') {
                boxX = clamp(boxAtDragStart.x + dx, 0, cw - boxW);
                boxY = clamp(boxAtDragStart.y + dy, 0, ch - boxH);
            } else {
                let nx = boxAtDragStart.x, ny = boxAtDragStart.y;
                let nw = boxAtDragStart.w, nh = boxAtDragStart.h;

                if (dragMode === 'se') {
                    nw = Math.max(MIN_W, boxAtDragStart.w + dx);
                    nh = Math.max(MIN_W / ASPECT, boxAtDragStart.h + dy);
                } else if (dragMode === 'sw') {
                    nw = Math.max(MIN_W, boxAtDragStart.w - dx);
                    nh = Math.max(MIN_W / ASPECT, boxAtDragStart.h + dy);
                    nx = boxAtDragStart.x + boxAtDragStart.w - nw;
                } else if (dragMode === 'ne') {
                    nw = Math.max(MIN_W, boxAtDragStart.w + dx);
                    nh = Math.max(MIN_W / ASPECT, boxAtDragStart.h - dy);
                    ny = boxAtDragStart.y + boxAtDragStart.h - nh;
                } else if (dragMode === 'nw') {
                    nw = Math.max(MIN_W, boxAtDragStart.w - dx);
                    nh = Math.max(MIN_W / ASPECT, boxAtDragStart.h - dy);
                    nx = boxAtDragStart.x + boxAtDragStart.w - nw;
                    ny = boxAtDragStart.y + boxAtDragStart.h - nh;
                }

                // Klæm til canvas
                nx = clamp(nx, 0, cw - MIN_W);
                ny = clamp(ny, 0, ch - (MIN_W / ASPECT));
                nw = clamp(nw, MIN_W, cw - nx);
                nh = clamp(nh, MIN_W / ASPECT, ch - ny);

                boxX = nx; boxY = ny; boxW = nw; boxH = nh;
            }
            renderBox();
        }

        function onPointerUp() {
            dragMode = null;
            window.removeEventListener('pointermove', onPointerMove);
        }

        // \u00c5bn dialog
        function open(srcUrl, callback) {
            doneCb = callback;
            img = new Image();
            img.onload = () => {
                imgW = img.naturalWidth;
                imgH = img.naturalHeight;

                // S\u00e6t canvas-dimensioner svarende til billedet men begr\u00e6nset
                const maxW = wrap.clientWidth  || 640;
                const maxH = Math.min(window.innerHeight * 0.6, 480);
                let cw = imgW, ch = imgH;
                if (cw > maxW) { ch = ch * (maxW / cw); cw = maxW; }
                if (ch > maxH) { cw = cw * (maxH / ch); ch = maxH; }
                canvasEl.width  = Math.round(cw);
                canvasEl.height = Math.round(ch);

                const ctx = canvasEl.getContext('2d');
                ctx.drawImage(img, 0, 0, canvasEl.width, canvasEl.height);

                overlay.style.display = 'flex';
                requestAnimationFrame(initBox);
            };
            img.src = srcUrl;
        }

        function close() {
            overlay.style.display = 'none';
            doneCb = null;
        }

        // Bekræft: crop til blob
        confirmBtn.addEventListener('click', () => {
            // boxX/Y/W/H er i CSS display-koordinater (offsetWidth/Height)
            // Scale fra display-koordinater til originalbilledets koordinater
            const displayW = canvasEl.offsetWidth;
            const displayH = canvasEl.offsetHeight;
            const scaleX = imgW / displayW;
            const scaleY = imgH / displayH;

            const srcX = Math.round(boxX * scaleX);
            const srcY = Math.round(boxY * scaleY);
            const srcW = Math.round(boxW * scaleX);
            const srcH = Math.round(boxH * scaleY);

            const outW = Math.min(srcW, MAX_OUTPUT_W);
            const outH = Math.round(outW * (srcH / srcW));

            const out = document.createElement('canvas');
            out.width  = outW;
            out.height = outH;
            out.getContext('2d').drawImage(img, srcX, srcY, srcW, srcH, 0, 0, outW, outH);

            out.toBlob(blob => {
                const cb = doneCb;
                close();
                if (cb && blob) cb(blob);
            }, 'image/webp', 0.92);
        });

        cancelBtn.addEventListener('click', close);

        // Pointer events p\u00e5 wrap (ikke direkte p\u00e5 canvas for at f\u00e5 korrekte koordinater)
        wrap.addEventListener('pointerdown', onPointerDown);

        // Ekspon\u00e9r
        window._imageCropper = { open };
    })();
