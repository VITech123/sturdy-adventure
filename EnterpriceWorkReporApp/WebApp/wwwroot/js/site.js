/* site.js - Global JS for ProductivitySystem Web
   IE11 Safe: ES5 Only, no arrow functions, no let/const
   jQuery 3.5.1 required
*/
(function () {
    'use strict';

    // ---- Sidebar: Highlight active link based on URL ----
    function setActiveNav() {
        try {
            var path = window.location.pathname.toLowerCase();
            $('.sidebar .nav-link').each(function () {
                var href = ($(this).attr('href') || '').toLowerCase();
                if (href && href !== '/' && path.indexOf(href) === 0) {
                    $(this).addClass('active');
                } else {
                    $(this).removeClass('active');
                }
            });
        } catch (e) { /* silently fail */ }
    }

    // ---- Global Search ----
    function initGlobalSearch() {
        var input = document.getElementById('globalSearchInput');
        if (!input) return;

        $(input).on('keydown', function (e) {
            // IE11 uses e.keyCode, modern uses e.key
            var key = e.key || e.keyCode;
            if (key === 'Enter' || key === 13) {
                var query = $.trim($(this).val());
                if (query) {
                    window.location.href = '/WorkReports?search=' + encodeURIComponent(query);
                }
            }
        });
    }

    // ---- Auto-dismiss alerts after 4 seconds ----
    function initAutoDismissAlerts() {
        setTimeout(function () {
            $('.alert-auto-dismiss').fadeOut('slow', function () {
                $(this).remove();
            });
        }, 4000);
    }

    // ---- Bootstrap tooltip init (IE11 safe) ----
    function initTooltips() {
        try {
            $('[data-toggle="tooltip"]').tooltip({ trigger: 'hover' });
        } catch (e) { /* ignore if Bootstrap tooltips unavailable */ }
    }

    // ---- Confirm delete buttons ----
    function initConfirmDelete() {
        $(document).on('click', '[data-confirm-delete]', function (e) {
            var msg = $(this).attr('data-confirm-delete') || 'Are you sure you want to delete this?';
            if (!confirm(msg)) {
                e.preventDefault();
                e.stopPropagation();
            }
        });
    }

    // ---- Loading overlay for form submissions ----
    function initLoadingOverlay() {
        $(document).on('submit', 'form[data-loading]', function () {
            var btn = $(this).find('[type="submit"]');
            btn.prop('disabled', true).html('<i class="fa fa-spinner fa-spin mr-1"></i> Processing...');
        });
    }

    // ---- Run on DOM ready ----
    $(document).ready(function () {
        setActiveNav();
        initGlobalSearch();
        initAutoDismissAlerts();
        initTooltips();
        initConfirmDelete();
        initLoadingOverlay();
    });

})();
