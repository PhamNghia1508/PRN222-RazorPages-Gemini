(() => {
    'use strict';

    const wrapper = document.getElementById('assigned-course-list');
    if (!wrapper || !window.signalR) {
        return;
    }

    const refreshUrl = wrapper.dataset.refreshUrl;
    if (!refreshUrl) {
        console.warn('[CourseRealtime] Missing partial refresh URL.');
        return;
    }

    let refreshController = null;

    /**
     * Fetches the server-rendered course list partial for the current signed-in user
     * and swaps only the course list DOM. The server still enforces authorization
     * and course ownership; the client never trusts SignalR payload data for HTML.
     */
    async function refreshAssignedCourses(reason) {
        if (refreshController) {
            refreshController.abort();
        }

        refreshController = new AbortController();

        try {
            wrapper.setAttribute('aria-busy', 'true');

            const response = await fetch(refreshUrl, {
                method: 'GET',
                credentials: 'same-origin',
                cache: 'no-store',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'text/html',
                },
                signal: refreshController.signal,
            });

            if (!response.ok) {
                throw new Error(`Partial refresh failed with HTTP ${response.status}.`);
            }

            const html = await response.text();
            wrapper.innerHTML = html;
            wrapper.dataset.lastRefreshReason = reason ?? 'signalr';
        } catch (error) {
            if (error.name === 'AbortError') {
                return;
            }

            console.error('[CourseRealtime] Could not refresh assigned courses.', error);
        } finally {
            wrapper.removeAttribute('aria-busy');
        }
    }

    function startConnection() {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/admin')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on('SubjectCreated', () => refreshAssignedCourses('subject-created'));
        connection.on('SubjectUpdated', () => refreshAssignedCourses('subject-updated'));
        connection.on('SubjectDeleted', () => refreshAssignedCourses('subject-deleted'));
        connection.on('UserAssignmentsUpdated', () => refreshAssignedCourses('assignment-updated'));

        connection.start()
            .catch(error => {
                console.error('[CourseRealtime] Could not connect to /hubs/admin.', error);
            });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startConnection, { once: true });
    } else {
        startConnection();
    }
})();
