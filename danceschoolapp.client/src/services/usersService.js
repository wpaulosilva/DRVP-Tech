import { get, post, patch } from '@/api/client'

export function getAdminUsers({ page = 1, pageSize = 20, search = '' }) {
    const params = new URLSearchParams({ page, pageSize })
    if (search) params.set('search', search)
    return get(`/api/admin/users?${params}`)
}

export function getStaffUsers({ page = 1, pageSize = 20, search = '', role = '' }) {
    const params = new URLSearchParams({ page, pageSize })
    if (search) params.set('search', search)
    if (role) params.set('role', role)
    return get(`/api/users?${params}`)
}

export function createUser({ email, username, firstRole, personInfo }) {
    return post('/api/users', {
        username: username ?? email.split('@')[0],
        email,
        firstRole,
        personInfo,
    })
}

export function activateUser(userId) {
    return patch(`/api/users/${userId}/activate`)
}

export function deactivateUser(userId) {
    return patch(`/api/users/${userId}/deactivate`)
}
