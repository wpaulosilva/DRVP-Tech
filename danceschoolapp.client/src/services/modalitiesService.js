import { get, post, patch, del } from '@/api/client'
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

export function activateModality(id) {
    return patch(`/api/modalities/${id}/activate`)
}

export function assignCoach(modalityId, coachId) {
    return post(`/api/modalities/${modalityId}/coaches/${coachId}`)
}

export function removeCoach(modalityId, coachId) {
    return del(`/api/modalities/${modalityId}/coaches/${coachId}`)
}
