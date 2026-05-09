import { get, post, patch } from '@/api/client'
// See EndpointsMapping.md for full API reference

export function getStudios() {
    return get('/api/studios')
}

export function getStudio(id) {
    return get(`/api/studios/${id}`)
}

export function createStudio(body) {
    return post('/api/studios', body)
}

export function updateStudio(id, body) {
    return patch(`/api/studios/${id}`, body)
}

export function deactivateStudio(id) {
    return patch(`/api/studios/${id}/deactivate`)
}

export function activateStudio(id) {
    return patch(`/api/studios/${id}/activate`)
}
