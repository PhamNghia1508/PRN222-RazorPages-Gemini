(function () {
    const rootSelector = '.js-governance-root';
    const toastHostId = 'governanceToastHost';
    let activeConfirmToast = null;

    function getRoot(target) {
        if (!target) {
            return null;
        }

        if (target.matches && target.matches(rootSelector)) {
            return target;
        }

        return target.closest ? target.closest(rootSelector) : document.querySelector(rootSelector);
    }

    function getToastHost() {
        let host = document.getElementById(toastHostId);
        if (host) {
            return host;
        }

        host = document.createElement('div');
        host.id = toastHostId;
        host.className = 'app-toast-host';
        host.setAttribute('aria-live', 'polite');
        host.setAttribute('aria-atomic', 'false');
        document.body.appendChild(host);
        return host;
    }

    function buildToast(title, message, tone, actions) {
        const toast = document.createElement('div');
        toast.className = `app-toast${tone === 'error' ? ' toast-error' : ''}${tone === 'confirm' ? ' toast-confirm' : ''}`;
        toast.setAttribute('role', tone === 'error' ? 'alert' : 'status');

        if (title) {
            const heading = document.createElement('div');
            heading.className = 'app-toast-title';
            heading.textContent = title;
            toast.appendChild(heading);
        }

        const body = document.createElement('div');
        body.className = 'app-toast-message';
        body.textContent = message;
        toast.appendChild(body);

        if (actions && actions.length > 0) {
            const footer = document.createElement('div');
            footer.className = 'app-toast-actions';

            actions.forEach((action) => {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = action.className;
                button.textContent = action.label;
                button.addEventListener('click', action.onClick);
                footer.appendChild(button);
            });

            toast.appendChild(footer);
        }

        return toast;
    }

    function removeToast(toast) {
        if (!toast || !toast.isConnected) {
            return;
        }

        toast.remove();
        if (activeConfirmToast === toast) {
            activeConfirmToast = null;
        }
    }

    function showStatusToast(messageOrOptions, options) {
        const settings = typeof messageOrOptions === 'object' && messageOrOptions !== null
            ? messageOrOptions
            : (options || {});
        const message = typeof messageOrOptions === 'object' && messageOrOptions !== null
            ? messageOrOptions.message
            : messageOrOptions;
        const host = getToastHost();
        const toast = buildToast(
            settings.title || (settings.tone === 'error' ? 'Thao tác chưa hoàn tất' : 'Đã cập nhật'),
            message || '',
            settings.tone || 'success');

        host.appendChild(toast);
        window.setTimeout(() => removeToast(toast), settings.duration || 3600);
        return toast;
    }

    function showGovernanceConfirmToast(options) {
        if (activeConfirmToast) {
            removeToast(activeConfirmToast);
        }

        const host = getToastHost();
        const toast = buildToast(options.title || 'Xác nhận thao tác', options.message, 'confirm', [
            {
                label: options.cancelLabel || 'Hủy',
                className: 'btn btn-sm btn-outline-secondary',
                onClick: () => {
                    removeToast(toast);
                    options.onCancel?.();
                }
            },
            {
                label: options.confirmLabel || options.actionLabel || 'Tiếp tục',
                className: 'btn btn-sm btn-primary',
                onClick: async () => {
                    const confirmButton = toast.querySelector('.btn-primary');
                    if (confirmButton) {
                        confirmButton.disabled = true;
                        confirmButton.textContent = options.loadingLabel || 'Đang xử lý...';
                    }

                    try {
                        await options.onConfirm();
                    } finally {
                        removeToast(toast);
                    }
                }
            }
        ]);

        activeConfirmToast = toast;
        host.appendChild(toast);
        return toast;
    }

    function setFormBusy(form, isBusy) {
        const submitButtons = form.querySelectorAll('button[type="submit"]');
        submitButtons.forEach((button) => {
            if (isBusy) {
                button.dataset.originalHtml = button.innerHTML;
                button.disabled = true;
                button.classList.add('is-loading');
                button.innerHTML = '<span>Đang xử lý...</span>';
            } else {
                button.disabled = false;
                button.classList.remove('is-loading');
                if (button.dataset.originalHtml) {
                    button.innerHTML = button.dataset.originalHtml;
                    delete button.dataset.originalHtml;
                }
            }
        });
    }

    function extractFlashMessages(root) {
        const alerts = Array.from(root.querySelectorAll('.js-governance-flash .inline-alert')).map((alert) => ({
            message: alert.textContent.replace(/\s+/g, ' ').trim(),
            tone: alert.classList.contains('inline-alert-danger') ? 'error' : 'success'
        }));

        root.querySelectorAll('.js-governance-flash').forEach((flash) => flash.remove());
        return alerts;
    }

    function parseHtml(html) {
        return new DOMParser().parseFromString(html, 'text/html');
    }

    function captureState(root) {
        const page = root.dataset.governancePage;
        const state = {
            page,
            scrollY: window.scrollY
        };

        if (page === 'accounts') {
            state.search = root.querySelector('.js-account-filter')?.value || '';
            state.role = root.querySelector('.js-account-role-filter')?.value || '';
            state.status = root.querySelector('.js-account-status-filter')?.value || '';
            state.openEditorId = root.querySelector('.governance-account-editor[open]')?.dataset.governanceEditorId || '';
        }

        return state;
    }

    function applyAccountFilters(root) {
        const searchInput = root.querySelector('.js-account-filter');
        const roleFilter = root.querySelector('.js-account-role-filter');
        const statusFilter = root.querySelector('.js-account-status-filter');
        const rows = Array.from(root.querySelectorAll('.governance-account-row'));
        const search = (searchInput?.value || '').trim().toLowerCase();
        const role = roleFilter?.value || '';
        const status = statusFilter?.value || '';

        rows.forEach((row) => {
            const matchesSearch = search === '' || (row.dataset.search || '').toLowerCase().includes(search);
            const matchesRole = role === '' || (row.dataset.roles || '').includes(`|${role}|`);
            const matchesStatus = status === '' || row.dataset.status === status;
            row.classList.toggle('is-hidden', !(matchesSearch && matchesRole && matchesStatus));
        });
    }

    function restoreState(root, state) {
        if (!state) {
            return;
        }

        if (state.page === 'accounts') {
            const searchInput = root.querySelector('.js-account-filter');
            const roleFilter = root.querySelector('.js-account-role-filter');
            const statusFilter = root.querySelector('.js-account-status-filter');

            if (searchInput) {
                searchInput.value = state.search || '';
            }

            if (roleFilter) {
                roleFilter.value = state.role || '';
            }

            if (statusFilter) {
                statusFilter.value = state.status || '';
            }

            applyAccountFilters(root);

            if (state.openEditorId) {
                const editor = root.querySelector(`.governance-account-editor[data-governance-editor-id="${state.openEditorId}"]`);
                if (editor) {
                    editor.open = true;
                }
            }
        }

        window.requestAnimationFrame(() => {
            window.scrollTo(0, state.scrollY || 0);
        });
    }

    async function submitGovernanceForm(form) {
        if (form.dataset.submitting === 'true') {
            return;
        }

        const root = getRoot(form);
        if (!root) {
            form.submit();
            return;
        }

        const state = captureState(root);
        form.dataset.submitting = 'true';
        setFormBusy(form, true);

        try {
            const response = await fetch(form.action, {
                method: (form.method || 'POST').toUpperCase(),
                body: new FormData(form),
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const html = await response.text();
            const nextDocument = parseHtml(html);
            const nextRoot = nextDocument.querySelector(rootSelector);

            if (!nextRoot) {
                window.location.assign(response.url || window.location.href);
                return;
            }

            const flashMessages = extractFlashMessages(nextRoot);
            root.replaceWith(nextRoot);

            if (response.url) {
                window.history.replaceState({}, '', response.url);
            }

            initGovernancePage(nextRoot);
            restoreState(nextRoot, state);

            if (flashMessages.length === 0) {
                showStatusToast('Đã cập nhật dữ liệu quản trị.', { tone: response.ok ? 'success' : 'error' });
            } else {
                flashMessages.forEach((flash) => showStatusToast(flash.message, { tone: flash.tone }));
            }
        } catch (_error) {
            showStatusToast('Không thể xử lý yêu cầu. Vui lòng thử lại.', { tone: 'error' });
            setFormBusy(form, false);
            delete form.dataset.submitting;
            return;
        }

        setFormBusy(form, false);
        delete form.dataset.submitting;
    }

    function wireAsyncForms(root) {
        root.querySelectorAll('form[data-governance-ajax="true"]').forEach((form) => {
            form.addEventListener('submit', (event) => {
                event.preventDefault();

                const submitButton = event.submitter || form.querySelector('button[type="submit"]');
                const actionLabel = form.dataset.confirmAction || submitButton?.textContent?.replace(/\s+/g, ' ').trim() || 'Tiếp tục';
                const title = form.dataset.confirmTitle || 'Xác nhận thao tác';
                const message = form.dataset.confirmMessage || 'Bạn có chắc muốn tiếp tục?';

                showGovernanceConfirmToast({
                    title,
                    message,
                    confirmLabel: actionLabel,
                    onConfirm: () => submitGovernanceForm(form)
                });
            });
        });
    }

    function syncCourseOptions(form) {
        const departmentPicker = form.querySelector('.js-department-picker');
        const selectedDepartmentId = departmentPicker?.value || '';
        const emptyState = form.querySelector('.js-course-empty-state');
        const emptyCopy = form.querySelector('.js-course-empty-copy-text');
        const departmentLabel = departmentPicker?.selectedOptions?.[0]?.textContent?.trim() || 'Khoa này';
        let visibleCount = 0;

        form.querySelectorAll('.js-course-option').forEach((option) => {
            const matchesDepartment = selectedDepartmentId !== '' && option.dataset.departmentId === selectedDepartmentId;
            const checkbox = option.querySelector('input[type="checkbox"]');

            if (matchesDepartment) {
                visibleCount += 1;
            }

            option.classList.toggle('is-hidden', !matchesDepartment);

            if (checkbox) {
                checkbox.disabled = !matchesDepartment;
                if (!matchesDepartment) {
                    checkbox.checked = false;
                }
            }
        });

        if (emptyState) {
            const shouldShow = selectedDepartmentId !== '' && visibleCount === 0;
            emptyState.classList.toggle('d-none', !shouldShow);

            if (shouldShow && emptyCopy) {
                emptyCopy.textContent = `${departmentLabel} chưa có môn học nào được gán. Tạo môn mới hoặc vào màn Khoa để chuyển một môn hiện có vào đúng Khoa này.`;
            }
        }
    }

    function syncCreateRole(root) {
        const roleSelect = root.querySelector('#Role');
        const createCoursePicker = root.querySelector('#createCoursePicker');
        if (!roleSelect || !createCoursePicker) {
            return;
        }

        const requiresCourses = roleSelect.value === 'Lecturer' || roleSelect.value === 'HeadLecturer';
        createCoursePicker.classList.toggle('is-muted', !requiresCourses);
        createCoursePicker.querySelectorAll('input[type="checkbox"]').forEach((input) => {
            input.disabled = !requiresCourses || input.closest('.js-course-option')?.classList.contains('is-hidden') === true;
        });
    }

    function initAccountsPage(root) {
        if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
            window.jQuery.validator.unobtrusive.parse(root);
        }

        root.querySelectorAll('.js-governance-form').forEach((form) => {
            syncCourseOptions(form);
            const picker = form.querySelector('.js-department-picker');
            if (picker) {
                picker.addEventListener('change', () => {
                    syncCourseOptions(form);
                    syncCreateRole(root);
                });
            }
        });

        const roleSelect = root.querySelector('#Role');
        if (roleSelect) {
            roleSelect.addEventListener('change', () => syncCreateRole(root));
        }
        syncCreateRole(root);

        const searchInput = root.querySelector('.js-account-filter');
        const roleFilter = root.querySelector('.js-account-role-filter');
        const statusFilter = root.querySelector('.js-account-status-filter');
        searchInput?.addEventListener('input', () => applyAccountFilters(root));
        roleFilter?.addEventListener('change', () => applyAccountFilters(root));
        statusFilter?.addEventListener('change', () => applyAccountFilters(root));
        applyAccountFilters(root);

        root.querySelectorAll('.governance-account-editor').forEach((editor) => {
            editor.addEventListener('toggle', () => {
                if (!editor.open) {
                    return;
                }

                root.querySelectorAll('.governance-account-editor[open]').forEach((otherEditor) => {
                    if (otherEditor !== editor) {
                        otherEditor.open = false;
                    }
                });
            });
        });
    }

    function initGovernancePage(target) {
        const root = getRoot(target || document);
        if (!root || root.dataset.governanceInitialized === 'true') {
            return;
        }

        root.dataset.governanceInitialized = 'true';
        wireAsyncForms(root);

        if (root.dataset.governancePage === 'accounts') {
            initAccountsPage(root);
        }
    }

    document.addEventListener('DOMContentLoaded', () => initGovernancePage(document));

    window.GovernancePage = {
        init: initGovernancePage,
        showGovernanceConfirmToast,
        showStatusToast
    };
})();
