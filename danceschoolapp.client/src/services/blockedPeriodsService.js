import { get, post, put, del } from '@/api/client'

export function getBlockedPeriods({ from, to, scope } = {}) {
    const params = new URLSearchParams({ from, to })
    if (scope !== undefined && scope !== '') params.set('scope', scope)
    return get(`/api/blockedperiods?${params}`)
}

export function createBlockedPeriod(body) {
    return post('/api/blockedperiods', body)
}

export function updateBlockedPeriod(id, body) {
    return put(`/api/blockedperiods/${id}`, body)
}

export function deleteBlockedPeriod(id) {
    return del(`/api/blockedperiods/${id}`)
}
