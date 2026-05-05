import { get, post, patch } from '@/api/client'

export function getStudentsByParent(parentId) {
    return get(`/api/students/parent/${parentId}`)
}

export function createStudent(data) {
    return post('/api/students', data)
}

export function updateStudent(studentId, data) {
    return patch(`/api/students/${studentId}`, data)
}

export function getValidateStudents({ status = 'pending', page = 1, pageSize = 10 } = {}) {
    const params = new URLSearchParams({ status, page, pageSize })
    return get(`/api/staff/validate-students?${params}`)
}

export function acceptStudent(studentId) {
    return patch(`/api/students/${studentId}/accept`)
}

export function rejectStudent(studentId, reason) {
    return patch(`/api/students/${studentId}/reject`, { reason })
}

export function getStudent(studentId) {
    return get(`/api/students/${studentId}`)
}