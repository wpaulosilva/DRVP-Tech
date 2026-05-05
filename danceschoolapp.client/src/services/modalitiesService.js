import { get, post, patch } from '@/api/client'
// See EndpointsMapping.md for full API reference

export function getModalities() {
    return get('/api/modalities')
}

export function createModality(body) {
    return post('/api/modalities', body)
}

export function updateModality(id, body) {
    return patch(`/api/modalities/${id}`, body)
}

export function deactivateModality(id) {
    return patch(`/api/modalities/${id}/deactivate`)
}
