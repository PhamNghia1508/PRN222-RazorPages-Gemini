/**
 * admin-realtime.js
 * ─────────────────────────────────────────────────────────────────────────────
 * Kết nối SignalR Hub tại /hubs/admin và xử lý realtime events:
 *   • SubjectCreated / SubjectUpdated / SubjectDeleted  → cập nhật Course grid
 *   • UserCreated / UserAssignmentsUpdated              → hiển thị toast thông báo
 *
 * Mỗi event đều:
 *   1. Hiển thị toast notification (slide-in từ dưới bên phải)
 *   2. Cập nhật DOM trực tiếp nếu đang ở đúng trang (Course/Index hoặc Admin/Accounts)
 *
 * Design tokens sử dụng CSS variables từ site.css (Warm Sand & Ink system).
 */

(() => {
    'use strict';

    // ─── Toast Container ──────────────────────────────────────────────────────

    const TOAST_DURATION_MS = 5000;

    function ensureToastContainer() {
        let container = document.getElementById('signalr-toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'signalr-toast-container';
            container.setAttribute('aria-live', 'polite');
            container.setAttribute('aria-atomic', 'false');
            container.style.cssText = `
                position: fixed;
                bottom: 24px;
                right: 24px;
                z-index: 9999;
                display: flex;
                flex-direction: column-reverse;
                gap: 10px;
                max-width: 360px;
                pointer-events: none;
            `;
            document.body.appendChild(container);
        }
        return container;
    }

    /**
     * Hiển thị một toast notification.
     * @param {string} message  - Nội dung thông báo
     * @param {'success'|'info'|'warning'|'danger'} type - Loại thông báo
     */
    function showToast(message, type = 'info') {
        const container = ensureToastContainer();

        const icons = {
            success: 'bi-check-circle',
            info:    'bi-info-circle',
            warning: 'bi-exclamation-triangle',
            danger:  'bi-x-circle',
        };

        const colorMap = {
            success: {
                bg:     'var(--status-success-bg)',
                text:   'var(--status-success-text)',
                border: 'rgba(90, 122, 90, 0.25)',
            },
            info: {
                bg:     'var(--status-info-bg)',
                text:   'var(--status-info-text)',
                border: 'rgba(58, 90, 122, 0.25)',
            },
            warning: {
                bg:     'var(--status-warning-bg)',
                text:   'var(--status-warning-text)',
                border: 'rgba(154, 112, 32, 0.25)',
            },
            danger: {
                bg:     'var(--status-danger-bg)',
                text:   'var(--status-danger-text)',
                border: 'rgba(138, 58, 42, 0.25)',
            },
        };

        const colors = colorMap[type] ?? colorMap.info;
        const iconClass = icons[type] ?? icons.info;

        const toast = document.createElement('div');
        toast.setAttribute('role', 'status');
        toast.style.cssText = `
            display: flex;
            align-items: flex-start;
            gap: 10px;
            padding: 14px 16px;
            background: ${colors.bg};
            color: ${colors.text};
            border: 1px solid ${colors.border};
            border-radius: 10px;
            box-shadow: 0 6px 24px rgba(28, 20, 9, 0.12);
            font-family: var(--font-body);
            font-size: 0.88rem;
            font-weight: 500;
            line-height: 1.5;
            pointer-events: all;
            cursor: default;
            opacity: 0;
            transform: translateY(12px);
            transition: opacity 0.25s ease, transform 0.25s ease;
            max-width: 360px;
            word-break: break-word;
        `;

        toast.innerHTML = `
            <i class="bi ${iconClass}" style="font-size: 1.05rem; flex-shrink: 0; margin-top: 1px;"></i>
            <span style="flex: 1;">${message}</span>
            <button style="background:none;border:none;padding:0;cursor:pointer;color:inherit;opacity:0.6;font-size:1rem;line-height:1;flex-shrink:0;"
                    aria-label="Đóng thông báo"
                    onclick="this.parentElement.remove()">
                <i class="bi bi-x"></i>
            </button>
        `;

        container.prepend(toast);

        // Animate in
        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                toast.style.opacity = '1';
                toast.style.transform = 'translateY(0)';
            });
        });

        // Auto dismiss
        const dismissTimer = setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateY(8px)';
            toast.addEventListener('transitionend', () => toast.remove(), { once: true });
        }, TOAST_DURATION_MS);

        // Cancel auto-dismiss on manual close
        toast.querySelector('button').addEventListener('click', () => clearTimeout(dismissTimer));
    }

    // ─── DOM Helpers ─────────────────────────────────────────────────────────

    /** Trả về ngày giờ định dạng dd/MM/yyyy HH:mm từ ISO string */
    function formatDateTime(isoString) {
        if (!isoString) return '';
        try {
            const d = new Date(isoString);
            const pad = (n) => String(n).padStart(2, '0');
            return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
        } catch {
            return '';
        }
    }

    /**
     * Tạo HTML cho một course card mới.
     * Cấu trúc phải khớp với Course/Index.cshtml article.course-card.
     */
    function buildCourseCardHtml(course) {
        const isAdmin = document.getElementById('courseGrid')?.dataset?.isAdmin === 'true';
        const desc = course.description?.trim() || 'Chưa có mô tả cho môn học này.';
        const docCount = course.documentCount ?? 0;
        const createdAt = formatDateTime(course.createdAt);

        const adminActions = isAdmin ? `
            <a href="/Course/Edit/${course.id}" class="btn btn-sm btn-outline-primary">
                <i class="bi bi-pencil"></i> Sửa
            </a>
            <button type="button"
                    class="btn btn-sm btn-outline-danger"
                    disabled
                    title="Xóa hết tài liệu trước khi xóa môn học."
                    data-bs-toggle="modal"
                    data-bs-target="#deleteModal-${course.id}">
                <i class="bi bi-trash"></i> Xóa
            </button>
        ` : '';

        return `
            <article class="course-card signalr-new-item" id="course-card-${course.id}" data-course-id="${course.id}">
                <div class="course-card-head">
                    <div class="course-icon">
                        <i class="bi bi-journal-richtext"></i>
                    </div>
                    <div>
                        <h3>${escapeHtml(course.name)}</h3>
                        <p>${escapeHtml(desc)}</p>
                    </div>
                </div>
                <div class="course-meta">
                    <span><i class="bi bi-file-earmark-text"></i> ${docCount} tài liệu</span>
                    <span><i class="bi bi-clock"></i> ${createdAt}</span>
                </div>
                <div class="course-actions">
                    <a href="/Document?courseId=${course.id}" class="btn btn-sm btn-outline-primary">
                        <i class="bi bi-database"></i> Tài liệu
                    </a>
                    ${adminActions}
                </div>
            </article>
        `;
    }

    /** Escape HTML để tránh XSS khi inject dữ liệu từ server vào DOM */
    function getCurrentUserContext() {
        return {
            userId: document.body?.dataset?.currentUserId ?? '',
            role: document.body?.dataset?.currentUserRole ?? '',
        };
    }

    function getCurrentController() {
        return (document.body?.dataset?.currentController ?? '').toLowerCase();
    }

    function isCourseScopedRole(role) {
        return role === 'HeadLecturer' || role === 'Lecturer';
    }

    function isCurrentUserAffected(data) {
        const currentUser = getCurrentUserContext();
        return Boolean(currentUser.userId)
            && currentUser.userId === (data?.userId ?? '')
            && isCourseScopedRole(currentUser.role);
    }

    function getCourseWorkspacePanel() {
        if (!document.querySelector('.course-summary-grid')) return null;

        return document.getElementById('courseGrid')?.closest('section')
            ?? document.querySelector('.workspace-panel');
    }

    let courseRefreshController = null;
    let documentRefreshController = null;
    let dashboardRefreshController = null;

    async function refreshPartialContainer(wrapper, refreshUrl, controllerSlot, reason) {
        if (!wrapper || !refreshUrl) {
            return false;
        }

        if (controllerSlot.current) {
            controllerSlot.current.abort();
        }

        controllerSlot.current = new AbortController();

        try {
            wrapper.setAttribute('aria-busy', 'true');
            wrapper.classList.add('signalr-refreshing');

            const response = await fetch(refreshUrl, {
                method: 'GET',
                credentials: 'same-origin',
                cache: 'no-store',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'text/html',
                },
                signal: controllerSlot.current.signal,
            });

            if (!response.ok) {
                throw new Error(`Partial refresh failed with HTTP ${response.status}.`);
            }

            wrapper.innerHTML = await response.text();
            wrapper.dataset.lastRefreshReason = reason ?? 'signalr';
            return true;
        } catch (error) {
            if (error.name === 'AbortError') {
                return true;
            }

            console.warn('[SignalR] Could not refresh partial container.', error);
            return false;
        } finally {
            wrapper.removeAttribute('aria-busy');
            wrapper.classList.remove('signalr-refreshing');
        }
    }

    async function refreshHtmlFragmentFromPage(wrapper, refreshUrl, fragmentSelector, controllerSlot, reason) {
        if (!wrapper || !refreshUrl || !fragmentSelector) {
            return false;
        }

        if (controllerSlot.current) {
            controllerSlot.current.abort();
        }

        controllerSlot.current = new AbortController();

        try {
            wrapper.setAttribute('aria-busy', 'true');
            wrapper.classList.add('signalr-refreshing');

            const response = await fetch(refreshUrl, {
                method: 'GET',
                credentials: 'same-origin',
                cache: 'no-store',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'text/html',
                },
                signal: controllerSlot.current.signal,
            });

            if (!response.ok) {
                throw new Error(`HTML fragment refresh failed with HTTP ${response.status}.`);
            }

            const parser = new DOMParser();
            const refreshedDocument = parser.parseFromString(await response.text(), 'text/html');
            const refreshedFragment = refreshedDocument.querySelector(fragmentSelector);
            if (!refreshedFragment) {
                throw new Error(`HTML fragment ${fragmentSelector} was not found.`);
            }

            wrapper.innerHTML = refreshedFragment.innerHTML;
            wrapper.dataset.lastRefreshReason = reason ?? 'signalr';
            return true;
        } catch (error) {
            if (error.name === 'AbortError') {
                return true;
            }

            console.warn('[SignalR] Could not refresh HTML fragment.', error);
            return false;
        } finally {
            wrapper.removeAttribute('aria-busy');
            wrapper.classList.remove('signalr-refreshing');
        }
    }
    function renderAssignedCourseGrid(courses) {
        const panel = getCourseWorkspacePanel();
        if (!panel) return;

        const normalizedCourses = Array.isArray(courses) ? courses : [];
        const currentGrid = document.getElementById('courseGrid');
        const emptyState = panel.querySelector('.empty-state');

        if (normalizedCourses.length === 0) {
            currentGrid?.remove();
            if (emptyState) {
                emptyState.style.display = '';
            } else {
                panel.insertAdjacentHTML('beforeend', `
                    <div class="empty-state">
                        <i class="bi bi-journal-plus"></i>
                        <h3>Ch\u01b0a c\u00f3 m\u00f4n h\u1ecdc n\u00e0o</h3>
                        <p>B\u1ea1n ch\u01b0a \u0111\u01b0\u1ee3c Admin ph\u00e2n c\u00f4ng m\u00f4n h\u1ecdc.</p>
                    </div>
                `);
            }
            updateCourseSummary(normalizedCourses);
            return;
        }

        emptyState?.remove();

        const gridHtml = `
            <div class="course-grid" id="courseGrid" data-is-admin="false">
                ${normalizedCourses.map(buildCourseCardHtml).join('')}
            </div>
        `;

        if (currentGrid) {
            currentGrid.outerHTML = gridHtml;
        } else {
            panel.insertAdjacentHTML('beforeend', gridHtml);
        }

        const grid = document.getElementById('courseGrid');
        if (grid) {
            grid.style.opacity = '0';
            grid.style.transform = 'translateY(-6px)';
            grid.style.transition = 'opacity 0.25s ease, transform 0.25s ease';
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    grid.style.opacity = '1';
                    grid.style.transform = 'translateY(0)';
                });
            });
        }

        updateCourseSummary(normalizedCourses);
    }

    async function refreshCourseWorkspace(data) {
        renderAssignedCourseGrid(data?.courses);
    }

    async function refreshDocumentWorkspace(message) {
        const wrapper = document.getElementById('documentRealtimeWorkspace');
        const refreshUrl = wrapper?.dataset?.refreshUrl;
        const refreshed = await refreshPartialContainer(
            wrapper,
            refreshUrl,
            { get current() { return documentRefreshController; }, set current(value) { documentRefreshController = value; } },
            'document-workspace-updated');

        showToast(
            refreshed
                ? (message ?? 'Ph\u1ea1m vi t\u00e0i li\u1ec7u \u0111\u00e3 \u0111\u01b0\u1ee3c c\u1eadp nh\u1eadt theo ph\u00e2n c\u00f4ng m\u1edbi.')
                : 'Ph\u00e2n c\u00f4ng \u0111\u00e3 thay \u0111\u1ed5i, nh\u01b0ng v\u00f9ng t\u00e0i li\u1ec7u ch\u01b0a th\u1ec3 t\u1ef1 c\u1eadp nh\u1eadt.',
            refreshed ? 'success' : 'warning');
    }

    async function refreshDashboardWorkspaceFromServer(reason) {
        const wrapper = document.getElementById('dashboardRealtimeWorkspace');
        const refreshUrl = wrapper?.dataset?.refreshUrl ?? window.location.pathname;
        const refreshed = await refreshHtmlFragmentFromPage(
            wrapper,
            refreshUrl,
            '#dashboardRealtimeWorkspace',
            { get current() { return dashboardRefreshController; }, set current(value) { dashboardRefreshController = value; } },
            reason);

        showToast(
            refreshed
                ? 'Tổng quan đã tải lại theo phạm vi môn học mới.'
                : 'Phân công đã thay đổi, nhưng Tổng quan chưa thể tự cập nhật.',
            refreshed ? 'success' : 'warning');
    }

    function refreshAssignmentScopedView(data) {
        const currentController = getCurrentController();

        if (currentController === 'course') {
            refreshCourseWorkspace(data);
            return;
        }

        if (currentController === 'document') {
            refreshDocumentWorkspace();
            return;
        }

        if (currentController === 'home') {
            refreshDashboardWorkspaceFromServer('assignment-updated');
            return;
        }

        if (
            currentController === 'chat'
            || currentController === 'evaluation'
            || currentController === 'finetune'
            || currentController === 'testsetgenerator'
        ) {
            showToast('Ph\u00e2n c\u00f4ng m\u00f4n h\u1ecdc \u0111\u00e3 thay \u0111\u1ed5i. N\u1ed9i dung trang n\u00e0y s\u1ebd d\u00f9ng ph\u1ea1m vi m\u1edbi khi b\u1ea1n thao t\u00e1c ti\u1ebfp.', 'info');
        }
    }
    function getPayloadValue(data, camelName, pascalName) {
        if (!data) return undefined;
        if (Object.prototype.hasOwnProperty.call(data, camelName)) {
            return data[camelName];
        }
        return data[pascalName];
    }

    function normalizeDocumentStatus(status) {
        return (status ?? '').toString().trim().toLowerCase();
    }

    function normalizeDocumentAction(action) {
        return (action ?? '').toString().trim().toLowerCase();
    }

    function isIndexedDocumentStatus(status) {
        const normalized = normalizeDocumentStatus(status);
        return normalized === 'indexed' || normalized === 'completed';
    }

    function parseDashboardNumber(value) {
        const parsed = parseInt((value ?? '').toString().replace(/[^0-9-]/g, ''), 10);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function readDashboardMetric(wrapper, selector) {
        return parseDashboardNumber(wrapper?.querySelector(selector)?.textContent);
    }

    function writeDashboardMetric(wrapper, selector, value) {
        const element = wrapper?.querySelector(selector);
        if (!element) return 0;
        const nextValue = Math.max(0, value);
        element.textContent = nextValue.toString();
        return nextValue;
    }

    function changeDashboardMetric(wrapper, selector, delta) {
        if (!delta) return readDashboardMetric(wrapper, selector);
        const current = readDashboardMetric(wrapper, selector);
        return writeDashboardMetric(wrapper, selector, current + delta);
    }

    function getDocumentNotificationValue(data, name) {
        return getPayloadValue(data, name, name.charAt(0).toUpperCase() + name.slice(1));
    }

    function getDashboardRowByDocumentId(tbody, documentId) {
        if (!tbody || documentId === undefined || documentId === null) return null;
        const id = documentId.toString();
        return Array.from(tbody.querySelectorAll('[data-document-id]'))
            .find(row => row.dataset.documentId === id) ?? null;
    }

    function dashboardStatusText(status) {
        switch (normalizeDocumentStatus(status)) {
            case 'indexed':
            case 'completed':
                return '\u0110\u00e3 index';
            case 'processing':
                return '\u0110ang x\u1eed l\u00fd';
            case 'failed':
                return 'L\u1ed7i';
            case 'uploaded':
                return 'Ch\u1edd x\u1eed l\u00fd';
            default:
                return status || 'Ch\u01b0a r\u00f5';
        }
    }

    function dashboardStatusClass(status) {
        switch (normalizeDocumentStatus(status)) {
            case 'indexed':
            case 'completed':
                return 'status-indexed';
            case 'processing':
                return 'status-processing';
            case 'failed':
                return 'status-failed';
            default:
                return 'status-uploaded';
        }
    }

    function formatDashboardDate(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return '';
        }

        const parts = new Intl.DateTimeFormat('vi-VN', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
            hour12: false,
        }).formatToParts(date).reduce((acc, part) => {
            acc[part.type] = part.value;
            return acc;
        }, {});

        return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}`;
    }

    function ensureDashboardRecentTable(wrapper) {
        let tbody = wrapper?.querySelector('[data-dashboard-recent-documents]');
        if (tbody) return tbody;

        const panel = wrapper?.querySelector('[data-dashboard-recent-panel]');
        if (!panel) return null;

        const tableHtml = `
            <div class="table-responsive table-shell">
                <table class="table ops-table">
                    <thead>
                        <tr>
                            <th>File</th>
                            <th>M\u00f4n h\u1ecdc</th>
                            <th>Chunks</th>
                            <th>Tr\u1ea1ng th\u00e1i</th>
                            <th>Ng\u00e0y n\u1ea1p</th>
                        </tr>
                    </thead>
                    <tbody data-dashboard-recent-documents></tbody>
                </table>
            </div>
        `;

        const emptyState = panel.querySelector('[data-dashboard-empty-documents]');
        if (emptyState) {
            emptyState.outerHTML = tableHtml;
        } else {
            panel.insertAdjacentHTML('beforeend', tableHtml);
        }

        return panel.querySelector('[data-dashboard-recent-documents]');
    }

    function buildDashboardDocumentRow(data) {
        const documentId = getDocumentNotificationValue(data, 'documentId');
        const courseId = getDocumentNotificationValue(data, 'courseId');
        const fileName = getDocumentNotificationValue(data, 'fileName') ?? 'T\u00e0i li\u1ec7u m\u1edbi';
        const status = getDocumentNotificationValue(data, 'status') ?? 'Uploaded';
        const chunkCount = parseDashboardNumber(getDocumentNotificationValue(data, 'chunkCount'));
        const occurredAt = getDocumentNotificationValue(data, 'occurredAt');
        const courseLabel = getDocumentNotificationValue(data, 'courseName') ?? (courseId ? `M\u00f4n #${courseId}` : '\u0110ang c\u1eadp nh\u1eadt');
        const detailsUrl = documentId ? `/Document/Details/${encodeURIComponent(documentId)}` : '/Document/Index';

        return `
            <tr data-document-id="${escapeHtml(documentId ?? '')}" data-document-status="${escapeHtml(status)}" data-document-chunk-count="${chunkCount}">
                <td><a href="${escapeHtml(detailsUrl)}">${escapeHtml(fileName)}</a></td>
                <td>${escapeHtml(courseLabel)}</td>
                <td class="text-mono" data-document-chunk-cell>${chunkCount}</td>
                <td><span class="status-badge ${dashboardStatusClass(status)}" data-document-status-badge>${dashboardStatusText(status)}</span></td>
                <td data-document-date-cell>${escapeHtml(formatDashboardDate(occurredAt))}</td>
            </tr>
        `;
    }

    function updateDashboardRecentRow(row, data, status, chunkCount) {
        const documentId = getDocumentNotificationValue(data, 'documentId');
        const fileName = getDocumentNotificationValue(data, 'fileName');
        const courseName = getDocumentNotificationValue(data, 'courseName');
        const occurredAt = getDocumentNotificationValue(data, 'occurredAt');

        row.dataset.documentId = documentId?.toString() ?? row.dataset.documentId ?? '';
        row.dataset.documentStatus = status;
        row.dataset.documentChunkCount = chunkCount.toString();

        const fileLink = row.querySelector('td:first-child a');
        if (fileLink && fileName) {
            fileLink.textContent = fileName;
        }

        const courseCell = row.querySelector('td:nth-child(2)');
        if (courseCell && courseName) {
            courseCell.textContent = courseName;
        }

        const chunkCell = row.querySelector('[data-document-chunk-cell]');
        if (chunkCell) {
            chunkCell.textContent = chunkCount.toString();
        }

        const badge = row.querySelector('[data-document-status-badge]');
        if (badge) {
            badge.className = `status-badge ${dashboardStatusClass(status)}`;
            badge.textContent = dashboardStatusText(status);
        }

        const dateCell = row.querySelector('[data-document-date-cell]');
        const formattedDate = formatDashboardDate(occurredAt);
        if (dateCell && formattedDate) {
            dateCell.textContent = formattedDate;
        }
    }

    function trimDashboardRecentRows(tbody) {
        Array.from(tbody?.querySelectorAll('[data-document-id]') ?? [])
            .slice(5)
            .forEach(row => row.remove());
    }

    function renderDashboardEmptyState(wrapper) {
        const panel = wrapper?.querySelector('[data-dashboard-recent-panel]');
        const tableShell = panel?.querySelector('.table-shell');
        if (!panel || !tableShell) return;

        tableShell.outerHTML = `
            <div class="empty-state compact" data-dashboard-empty-documents>
                <i class="bi bi-database"></i>
                <h3>Ch\u01b0a c\u00f3 t\u00e0i li\u1ec7u n\u00e0o</h3>
                <p>Kho tri th\u1ee9c ch\u01b0a c\u00f3 d\u1eef li\u1ec7u ho\u1eb7c t\u00e0i li\u1ec7u v\u1eeba \u0111\u01b0\u1ee3c x\u00f3a.</p>
            </div>
        `;
    }

    function refreshDashboardStatusHints(wrapper) {
        const indexedDocuments = readDashboardMetric(wrapper, '[data-dashboard-indexed-documents]');
        const missingAlert = wrapper?.querySelector('[data-dashboard-missing-documents-alert]');
        if (missingAlert) {
            missingAlert.hidden = indexedDocuments > 0;
        }

        const evaluationStatus = wrapper?.querySelector('[data-dashboard-evaluation-status]');
        if (evaluationStatus) {
            evaluationStatus.textContent = indexedDocuments > 0 ? 'S\u1eb5n s\u00e0ng' : 'Thi\u1ebfu d\u1eef li\u1ec7u';
        }
    }

    function refreshDashboardMetrics(wrapper, data, oldStatus, oldChunkCount, rowAlreadyExisted) {
        const action = normalizeDocumentAction(getDocumentNotificationValue(data, 'action'));
        const status = getDocumentNotificationValue(data, 'status') ?? oldStatus ?? '';
        const chunkCount = parseDashboardNumber(getDocumentNotificationValue(data, 'chunkCount'));
        const wasIndexed = isIndexedDocumentStatus(oldStatus);
        const isIndexed = isIndexedDocumentStatus(status);

        if (action === 'uploaded' && !rowAlreadyExisted) {
            changeDashboardMetric(wrapper, '[data-dashboard-total-documents]', 1);
        }

        if (action === 'deleted') {
            changeDashboardMetric(wrapper, '[data-dashboard-total-documents]', -1);
            if (wasIndexed || isIndexed) {
                changeDashboardMetric(wrapper, '[data-dashboard-indexed-documents]', -1);
                changeDashboardMetric(wrapper, '[data-dashboard-indexed-chunks]', -(oldChunkCount || chunkCount));
            }
            refreshDashboardStatusHints(wrapper);
            return;
        }

        if (!wasIndexed && isIndexed) {
            changeDashboardMetric(wrapper, '[data-dashboard-indexed-documents]', 1);
        } else if (wasIndexed && !isIndexed) {
            changeDashboardMetric(wrapper, '[data-dashboard-indexed-documents]', -1);
        }

        if (isIndexed) {
            changeDashboardMetric(wrapper, '[data-dashboard-indexed-chunks]', chunkCount - (wasIndexed ? oldChunkCount : 0));
        } else if (wasIndexed) {
            changeDashboardMetric(wrapper, '[data-dashboard-indexed-chunks]', -oldChunkCount);
        }

        refreshDashboardStatusHints(wrapper);
    }

    function updateDashboardDocumentRow(wrapper, data) {
        const action = normalizeDocumentAction(getDocumentNotificationValue(data, 'action'));
        const documentId = getDocumentNotificationValue(data, 'documentId');
        const status = getDocumentNotificationValue(data, 'status') ?? 'Uploaded';
        const chunkCount = parseDashboardNumber(getDocumentNotificationValue(data, 'chunkCount'));
        const tbody = ensureDashboardRecentTable(wrapper);
        const existingRow = getDashboardRowByDocumentId(tbody, documentId);
        const oldStatus = existingRow?.dataset.documentStatus ?? '';
        const oldChunkCount = parseDashboardNumber(existingRow?.dataset.documentChunkCount);

        refreshDashboardMetrics(wrapper, data, oldStatus, oldChunkCount, Boolean(existingRow));

        if (action === 'deleted') {
            existingRow?.remove();
            if (tbody && !tbody.querySelector('[data-document-id]')) {
                renderDashboardEmptyState(wrapper);
            }
            return;
        }

        let row = existingRow;
        if (!row && tbody) {
            tbody.insertAdjacentHTML('afterbegin', buildDashboardDocumentRow(data));
            row = getDashboardRowByDocumentId(tbody, documentId);
        }

        if (row) {
            updateDashboardRecentRow(row, data, status, chunkCount);
            row.classList.add('signalr-row-updated');
            setTimeout(() => row.classList.remove('signalr-row-updated'), 1800);
        }

        trimDashboardRecentRows(tbody);
    }

    function refreshDashboardWorkspace(data) {
        const wrapper = document.getElementById('dashboardRealtimeWorkspace');
        if (!wrapper) return;

        updateDashboardDocumentRow(wrapper, data);
        wrapper.dataset.lastRefreshReason = 'document-changed';

        showToast('T\u1ed5ng quan \u0111\u00e3 c\u1eadp nh\u1eadt d\u1eef li\u1ec7u t\u00e0i li\u1ec7u.', 'success');
    }
    function updateCourseSummary(courses) {
        const summaryValues = document.querySelectorAll('.course-summary-grid .course-summary-card strong');
        if (summaryValues.length < 3) return;

        summaryValues[0].textContent = courses.length;
        summaryValues[1].textContent = courses.filter(course => (course.documentCount ?? 0) > 0).length;
        summaryValues[2].textContent = courses.reduce((sum, course) => sum + (course.documentCount ?? 0), 0);
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // ─── Course (Subject) DOM Handlers ───────────────────────────────────────

    function onSubjectCreated(data) {
        showToast(`Môn học <strong>${escapeHtml(data.name)}</strong> vừa được thêm.`, 'success');

        const grid = document.getElementById('courseGrid');
        if (!grid) return; // Không phải trang Course/Index

        // Xóa empty-state nếu có
        const emptyState = grid.closest('section')?.querySelector('.empty-state');
        if (emptyState) {
            emptyState.remove();
            // Tạo grid nếu empty-state bao trùm cả section
            grid.style.display = '';
        }

        // Chèn card mới vào đầu grid (mới nhất lên trên)
        const existing = grid.querySelector(`#course-card-${data.id}`);
        if (!existing) {
            grid.insertAdjacentHTML('afterbegin', buildCourseCardHtml(data));

            // Animate in
            const newCard = grid.querySelector(`#course-card-${data.id}`);
            if (newCard) {
                newCard.style.opacity = '0';
                newCard.style.transform = 'translateY(-10px)';
                newCard.style.transition = 'opacity 0.35s ease, transform 0.35s ease';
                requestAnimationFrame(() => {
                    requestAnimationFrame(() => {
                        newCard.style.opacity = '1';
                        newCard.style.transform = 'translateY(0)';
                    });
                });
            }

            // Cập nhật bộ đếm "Tổng môn học"
            updateCourseCounters(1, 0);
        }
    }

    function onSubjectUpdated(data) {
        showToast(`Môn học <strong>${escapeHtml(data.name)}</strong> vừa được cập nhật.`, 'info');

        const card = document.getElementById(`course-card-${data.id}`);
        if (!card) return;

        // Cập nhật tên và mô tả
        const titleEl = card.querySelector('h3');
        const descEl  = card.querySelector('p');
        if (titleEl) titleEl.textContent = data.name;
        if (descEl)  descEl.textContent  = data.description?.trim() || 'Chưa có mô tả cho môn học này.';

        // Highlight để người dùng nhận ra card đã thay đổi
        card.style.transition = 'outline 0s, box-shadow 0.3s ease';
        card.style.boxShadow = '0 0 0 2px var(--status-info-text)';
        setTimeout(() => { card.style.boxShadow = ''; }, 2000);
    }

    function onSubjectDeleted(data) {
        showToast('Một môn học vừa bị xóa khỏi hệ thống.', 'warning');

        const card = document.getElementById(`course-card-${data.id}`);
        if (!card) return;

        // Animate out rồi remove
        card.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
        card.style.opacity = '0';
        card.style.transform = 'scale(0.95)';
        card.addEventListener('transitionend', () => {
            card.remove();
            updateCourseCounters(-1, 0);
        }, { once: true });
    }

    /** Cập nhật counter badges ở course-summary-grid */
    function updateCourseCounters(deltaCourses, deltaDocs) {
        const summaryCards = document.querySelectorAll('.course-summary-grid .course-summary-card strong');
        if (summaryCards.length >= 1 && deltaCourses !== 0) {
            const current = parseInt(summaryCards[0].textContent, 10);
            if (!isNaN(current)) summaryCards[0].textContent = current + deltaCourses;
        }
    }

    // ─── User (Account) DOM Handlers ─────────────────────────────────────────

    function onUserCreated(data) {
        showToast(
            `Tài khoản <strong>${escapeHtml(data.email)}</strong> (${escapeHtml(data.roleDisplayName)}) vừa được tạo.`,
            'success'
        );

        const list = document.getElementById('accountList');
        if (!list) return; // Không phải trang Admin/Accounts

        // Tạo row mới (skeleton đơn giản, không có form phân công)
        const row = document.createElement('article');
        row.className = 'admin-account-row signalr-new-item';
        row.id = `account-row-${data.userId}`;
        row.dataset.userId = data.userId;
        row.innerHTML = `
            <div class="admin-account-avatar">
                <i class="bi bi-person"></i>
            </div>
            <div>
                <strong>${escapeHtml(data.email)}</strong>
                <div class="admin-role-list">
                    <span class="admin-role-badge admin-role-${escapeHtml(data.role.toLowerCase())}">
                        ${escapeHtml(data.roleDisplayName)}
                    </span>
                </div>
            </div>
        `;
        row.style.opacity = '0';
        row.style.transform = 'translateX(-10px)';
        row.style.transition = 'opacity 0.35s ease, transform 0.35s ease';
        list.prepend(row);

        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                row.style.opacity = '1';
                row.style.transform = 'translateX(0)';
            });
        });

        // Cập nhật bộ đếm trong summary-grid
        updateAccountCounter(data.role, 1);
    }

    function onUserAssignmentsUpdated(data) {
        showToast(
            `Phân công môn học của <strong>${escapeHtml(data.email ?? '')}</strong> vừa được cập nhật.`,
            'info'
        );

        // Highlight account row nếu đang ở trang Accounts
        if (isCurrentUserAffected(data)) {
            refreshAssignmentScopedView(data);
        }

        const row = document.getElementById(`account-row-${data.userId}`);
        if (!row) return;
        row.style.transition = 'box-shadow 0.3s ease';
        row.style.boxShadow = '0 0 0 2px var(--status-info-text)';
        setTimeout(() => { row.style.boxShadow = ''; }, 2000);
    }


    function onDocumentChanged(data) {
        const currentController = getCurrentController();

        if (currentController === 'home') {
            refreshDashboardWorkspace(data);
            return;
        }

        if (currentController === 'document') {
            refreshDocumentWorkspace('Kho t\u00e0i li\u1ec7u \u0111\u00e3 \u0111\u01b0\u1ee3c c\u1eadp nh\u1eadt.');
            return;
        }

        if (currentController === 'course') {
            const wrapper = document.getElementById('assigned-course-list');
            const refreshUrl = wrapper?.dataset?.refreshUrl;
            if (wrapper && refreshUrl) {
                refreshPartialContainer(
                    wrapper,
                    refreshUrl,
                    { get current() { return courseRefreshController; }, set current(value) { courseRefreshController = value; } },
                    'document-changed'
                ).then(refreshed => {
                    const action = normalizeDocumentAction(getDocumentNotificationValue(data, 'action'));
                    const fileName = getDocumentNotificationValue(data, 'fileName') ?? 'T\u00e0i li\u1ec7u';
                    const toastMsg = action === 'deleted'
                        ? `T\u00e0i li\u1ec7u <strong>${escapeHtml(fileName)}</strong> \u0111\u00e3 \u0111\u01b0\u1ee3c x\u00f3a kh\u1ecfi m\u00f4n h\u1ecdc.`
                        : `T\u00e0i li\u1ec7u <strong>${escapeHtml(fileName)}</strong> \u0111\u00e3 \u0111\u01b0\u1ee3c c\u1eadp nh\u1eadt.`;
                    showToast(toastMsg, refreshed ? 'success' : 'info');
                });
            }
            return;
        }

        if (
            currentController === 'chat'
            || currentController === 'evaluation'
            || currentController === 'finetune'
            || currentController === 'testsetgenerator'
        ) {
            showToast('Kho tri th\u1ee9c v\u1eeba \u0111\u01b0\u1ee3c c\u1eadp nh\u1eadt. Trang s\u1ebd d\u00f9ng d\u1eef li\u1ec7u m\u1edbi khi b\u1ea1n thao t\u00e1c ti\u1ebfp.', 'info');
        }
    }    /** Cập nhật counter ở admin-summary-grid */
    function updateAccountCounter(role, delta) {
        const summaryCards = document.querySelectorAll('.admin-summary-grid .course-summary-card');
        const roleIndex = {
            'Admin':         0,
            'HeadLecturer':  1,
            'Lecturer':      2,
        };
        const idx = roleIndex[role];
        if (idx === undefined || !summaryCards[idx]) return;
        const strong = summaryCards[idx].querySelector('strong');
        if (!strong) return;
        const current = parseInt(strong.textContent, 10);
        if (!isNaN(current)) strong.textContent = current + delta;
    }

    // ─── SignalR Connection ───────────────────────────────────────────────────

    function startConnection() {
        if (!window.signalR) {
            console.warn('[SignalR] Client library is not loaded; realtime updates are disabled.');
            return;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/admin')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // Đăng ký handlers
        connection.on('SubjectCreated',          onSubjectCreated);
        connection.on('SubjectUpdated',          onSubjectUpdated);
        connection.on('SubjectDeleted',          onSubjectDeleted);
        connection.on('UserCreated',             onUserCreated);
        connection.on('UserAssignmentsUpdated',  onUserAssignmentsUpdated);
        connection.on('DocumentChanged',         onDocumentChanged);

        // Reconnect lifecycle
        connection.onreconnecting(() => {
            console.debug('[SignalR] Đang kết nối lại...');
        });
        connection.onreconnected(() => {
            console.debug('[SignalR] Kết nối lại thành công.');
        });
        connection.onclose((err) => {
            if (err) console.warn('[SignalR] Kết nối đóng với lỗi:', err);
        });

        connection.start()
            .then(() => console.debug('[SignalR] Đã kết nối tới /hubs/admin'))
            .catch((err) => console.warn('[SignalR] Không thể kết nối:', err));
    }

    // Khởi động sau khi DOM sẵn sàng
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startConnection);
    } else {
        startConnection();
    }

    // ─── Toast CSS inject (không cần file CSS riêng) ──────────────────────────
    // Thêm animation class cho item mới (dùng cho signalr-new-item)
    const style = document.createElement('style');
    style.textContent = `
        .admin-account-list .signalr-new-item,
        .course-grid .signalr-new-item,
        .table-shell .signalr-row-updated {
            outline: 2px solid transparent;
        }
    `;
    document.head.appendChild(style);
})();
