import { get, post, patch } from '@/api/client'

export function getAdminUsers({
    page = 1,
    pageSize = 7,
    search = '',
    sortBy = '',
    sortDir = 'asc',
}) {
    const params = new URLSearchParams({ page, pageSize })

    if (search) params.set('search', search)
    if (sortBy) params.set('sortBy', sortBy)
    if (sortDir) params.set('sortDir', sortDir)

    return get(`/api/admin/users?${params}`)
}

export function getStaffUsers({
    page = 1,
    pageSize = 7,
    search = '',
    role = '',
    sortBy = '',
    sortDir = 'asc',
}) {
    const params = new URLSearchParams({ page, pageSize })

    if (search) params.set('search', search)
    if (role) params.set('role', role)
    if (sortBy) params.set('sortBy', sortBy)
    if (sortDir) params.set('sortDir', sortDir)

    return get(`/api/staff?${params}`)
}

export function getDirectionUsers({
    page = 1,
    pageSize = 7,
    search = '',
    sortBy = '',
    sortDir = 'asc',
}) {
    const params = new URLSearchParams({ page, pageSize })

    if (search) params.set('search', search)
    if (sortBy) params.set('sortBy', sortBy)
    if (sortDir) params.set('sortDir', sortDir)

    return get(`/api/staff?${params}`)
}

export function getCoaches({
    page = 1,
    pageSize = 7,
    search = '',
    sortBy = '',
    sortDir = 'asc',
}) {
    const params = new URLSearchParams({ page, pageSize })

    if (search) params.set('search', search)
    if (sortBy) params.set('sortBy', sortBy)
    if (sortDir) params.set('sortDir', sortDir)

    return get(`/api/coaches?${params}`)
}

export function getParents({
    page = 1,
    pageSize = 7,
    search = '',
    sortBy = '',
    sortDir = 'asc',
}) {
    const params = new URLSearchParams({ page, pageSize })

    if (search) params.set('search', search)
    if (sortBy) params.set('sortBy', sortBy)
    if (sortDir) params.set('sortDir', sortDir)

    return get(`/api/parents?${params}`)
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
