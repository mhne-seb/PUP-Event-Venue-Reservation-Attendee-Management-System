// PUP Event Venue System — site.js

// Auto-dismiss alerts after 5 seconds
document.addEventListener('DOMContentLoaded', function () {
    const alerts = document.querySelectorAll('.alert-dismissible');
    alerts.forEach(alert => {
        setTimeout(() => {
            const bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            bsAlert?.close();
        }, 5000);
    });

    // Highlight active nav link
    const path = window.location.pathname.toLowerCase();
    document.querySelectorAll('.navbar .nav-link').forEach(link => {
        const href = (link.getAttribute('href') || '').toLowerCase();
        if (href && href !== '/' && path.startsWith(href)) {
            link.classList.add('active');
        }
    });

    // Image preview on file input
    document.querySelectorAll('input[type="file"][accept*="image"]').forEach(input => {
        input.addEventListener('change', function () {
            const file = this.files[0];
            if (!file) return;
            const reader = new FileReader();
            const previewId = this.dataset.preview;
            reader.onload = e => {
                let preview = previewId
                    ? document.getElementById(previewId)
                    : this.previousElementSibling?.querySelector('img');
                if (preview) { preview.src = e.target.result; preview.style.display = 'block'; }
            };
            reader.readAsDataURL(file);
        });
    });

    // Confirm delete buttons
    document.querySelectorAll('[data-confirm]').forEach(btn => {
        btn.addEventListener('click', e => {
            if (!confirm(btn.dataset.confirm)) e.preventDefault();
        });
    });

    // Set min datetime for reservation form
    const startDt = document.getElementById('startDt');
    const endDt = document.getElementById('endDt');
    if (startDt && endDt) {
        const now = new Date();
        now.setDate(now.getDate() + 1);
        const minStr = now.toISOString().slice(0, 16);
        startDt.min = minStr;
        endDt.min = minStr;

        startDt.addEventListener('change', () => {
            if (startDt.value) {
                endDt.min = startDt.value;
                if (endDt.value && endDt.value <= startDt.value) {
                    const start = new Date(startDt.value);
                    start.setHours(start.getHours() + 2);
                    endDt.value = start.toISOString().slice(0, 16);
                }
            }
        });
    }
});
