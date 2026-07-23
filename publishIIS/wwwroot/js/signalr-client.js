/* ============================================
   signalr-client.js
   Dùng chung cho tất cả pages
   ============================================ */

let _connection = null;

function initSignalR(groupName, handlers) {
    _connection = new signalR.HubConnectionBuilder()
        .withUrl('/orderHub')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .withServerTimeout(60000)
        .withKeepAliveInterval(15000)
        .build();

    // Đăng ký handlers
    if (handlers) {
        Object.entries(handlers).forEach(([event, fn]) => {
            _connection.on(event, fn);
        });
    }

    // Trạng thái kết nối
    _connection.onreconnecting(() => _setConnStatus('reconnecting'));
    _connection.onreconnected(async () => {
        _setConnStatus('connected');
        await _connection.invoke('JoinGroup', groupName).catch(console.error);
        showToast('Đã kết nối lại', 'success');
    });
    _connection.onclose(() => _setConnStatus('disconnected'));

    _startConnection(groupName);
}

async function _startConnection(groupName) {
    try {
        await _connection.start();
        await _connection.invoke('JoinGroup', groupName);
        _setConnStatus('connected');
    } catch (err) {
        _setConnStatus('disconnected');
        console.warn('SignalR connect failed, retrying...', err);
        setTimeout(() => _startConnection(groupName), 5000);
    }
}

function _setConnStatus(state) {
    const dot  = document.getElementById('conn-dot');
    const text = document.getElementById('conn-text');
    if (!dot || !text) return;

    dot.className = 'conn-dot';
    if (state === 'connected') {
        dot.classList.add('connected');
        text.textContent = 'Đã kết nối';
    } else if (state === 'reconnecting') {
        text.textContent = 'Đang kết nối lại...';
    } else {
        dot.classList.add('disconnected');
        text.textContent = 'Mất kết nối';
    }
}

/* ── Toast ──────────────────────────────────── */
function showToast(message, type, duration) {
    type     = type     || 'success';
    duration = duration || 3500;

    const stack = document.getElementById('toast-stack');
    if (!stack) return;

    const icons = { success: '✓', warning: '⚠', error: '✕', info: 'ℹ' };
    const toast = document.createElement('div');
    toast.className = 'toast toast-' + type;
    toast.innerHTML = '<span>' + (icons[type] || '✓') + '</span><span>' + message + '</span>';
    stack.appendChild(toast);

    setTimeout(function () {
        toast.style.transition = 'opacity 0.3s, transform 0.3s';
        toast.style.opacity    = '0';
        toast.style.transform  = 'translateX(20px)';
        setTimeout(function () { toast.remove(); }, 300);
    }, duration);
}

/* ── Helpers ────────────────────────────────── */
function fmtVND(amount) {
    return new Intl.NumberFormat('vi-VN').format(amount) + ' ₫';
}

function fmtTime(dateStr) {
    return new Date(dateStr).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
}
