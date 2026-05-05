import { get, post, patch } from '@/api/client'

export function getMyStudents() {
    return get('/api/ee/students')
}

export function createStudent(body) {
    return post('/api/students', body)
}

export function updateStudent(id, body) {
    return patch(`/api/students/${id}`, body)
}

export function getStudent(id) {
    return get(`/api/students/${id}`)
}

export function getValidateStudents({ status = 'pending', page = 1, pageSize = 10 } = {}) {
    const params = new URLSearchParams({ status, page, pageSize })
    return get(`/api/staff/validate-students?${params}`)
}

export function acceptStudent(id) {
    return patch(`/api/students/${id}/accept`)
}

export function rejectStudent(id, reason) {
    return patch(`/api/students/${id}/reject`, { reason })
}

export function deactivateStudent(id) {
    return patch(`/api/students/${id}/deactivate`)
}

export function activateStudent(id) {
    return patch(`/api/students/${id}/activate`)
}