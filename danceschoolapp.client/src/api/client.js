const API_BASE = '' // Uses Vite proxy — requests to /api/* are forwarded to the backend

async function request(url, options = {}) {
    const res = await fetch(`${API_BASE}${url}`, {
        credentials: 'include',
        ...options,
        headers: {
            ...options.headers,
        },
    })

    if (!res.ok) {
        const body = await res.json().catch(() => ({}))
        const firstError = body?.errors
            ? Object.values(body.errors).flat()[0]
            : null

        throw new Error(
            firstError || body.message || body.title || `Erro ${res.status}`
        )
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
