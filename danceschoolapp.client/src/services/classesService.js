import { get, patch, post } from '@/api/client'
// See EndpointsMapping.md for full API reference

export function getMyClasses({ from, to } = {}) {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    const qs = params.toString()
    return get(`/api/ee/classes/my${qs ? `?${qs}` : ''}`)
}

export function getAvailableSlots({ from, to, modalityId, coachId } = {}) {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    if (modalityId) params.set('modalityId', modalityId)
    if (coachId) params.set('coachId', coachId)
    const qs = params.toString()
    return get(`/api/ee/classes/available-slots${qs ? `?${qs}` : ''}`)
}

export function getOpenClasses({ page = 1, pageSize = 10, modalityId } = {}) {
    const params = new URLSearchParams({ page, pageSize })
    if (modalityId) params.set('modalityId', modalityId)
    return get(`/api/ee/classes/open?${params}`)
}

export function getValidateClasses({ page = 1, pageSize = 10 } = {}) {
    const params = new URLSearchParams({ page, pageSize })
    return get(`/api/ee/classes/validate?${params}`)
}

export function parentValidateParticipant(participantId, attended) {
    return patch(`/api/participants/${participantId}/parent-validate`, { attended })
}

/** POST /api/coachclasses — parent requests a new class on an available slot */
export function createClass(body) {
    return post('/api/coachclasses', body)
}

/** POST /api/participants — parent enrolls a student in an existing open class */
export function enrollInClass(body) {
    return post('/api/participants', body)
}
