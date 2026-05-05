import { get, post, put, patch } from '@/api/client'
// See EndpointsMapping.md for full API reference

export function getMyStudents() {
    return get('/api/ee/students')
}

export function createStudent(body) {
    return post('/api/students', { personInfo: body })
}

export function updateStudent(id, body) {
    return put(`/api/students/${id}`, body)
}

export function deactivateStudent(id) {
    return patch(`/api/students/${id}/deactivate`)
}
