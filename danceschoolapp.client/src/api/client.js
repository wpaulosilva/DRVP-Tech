const API_BASE = '' // Uses Vite proxy — requests to /api/* are forwarded to the backend

// Deduplicates concurrent refresh calls: if multiple requests fail with 401
// at the same time, only one refresh call is made and all waiters share it.
let refreshPromise = null

function tryRefresh() {
    if (!refreshPromise) {
        refreshPromise = fetch('/api/auth/refresh', {
            method: 'POST',
            credentials: 'include',
        }).finally(() => { refreshPromise = null })
    }
    return refreshPromise
}

async function request(url, options = {}, retry = true) {
    const res = await fetch(`${API_BASE}${url}`, {
        credentials: 'include',
        ...options,
        headers: { ...options.headers },
    })

    if (res.status === 401 && retry && url !== '/api/auth/refresh' && url !== '/api/auth/login') {
        const refreshRes = await tryRefresh()
        if (refreshRes.ok) return request(url, options, false)
        // Refresh failed (user deactivated, token expired, etc.) — force re-login
        window.dispatchEvent(new CustomEvent('auth:expired'))
        throw new Error('Sessão expirada. Por favor faça login novamente.')
    }

    if (!res.ok) {
        const body = await res.json().catch(() => ({}))
        const firstError = body?.errors
            ? Object.values(body.errors).flat()[0]
            : null
        throw new Error(firstError || body.message || body.title || `Erro ${res.status}`)
    }

    const text = await res.text()
    return text ? JSON.parse(text) : null
}

export function get(url) {
    return request(url)
}

export function post(url, body) {
    return request(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    })
}

export function put(url, body) {
    return request(url, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    })
}

export function patch(url, body) {
    return request(url, {
        method: 'PATCH',
        headers: body ? { 'Content-Type': 'application/json' } : {},
        body: body ? JSON.stringify(body) : undefined,
    })
}

export function del(url) {
    return request(url, { method: 'DELETE' })
}

// Multipart file upload — browser sets Content-Type with boundary automatically
export function upload(url, formData) {
    return request(url, { method: 'POST', body: formData })
}
