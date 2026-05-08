import { get, post, put, del } from '@/api/client'

export function getMyCoachProfile() {
    return get('/api/coaches/me')
}

export function getCoachAvailability(coachId) {
    return get(`/api/coachavailability/coach/${coachId}`)
}

export function createAvailability(body) {
    return post('/api/coachavailability', body)
}

export function updateAvailability(id, body) {
    return put(`/api/coachavailability/${id}`, body)
}

export function deleteAvailability(id) {
    return del(`/api/coachavailability/${id}`)
}
