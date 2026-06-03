import { get, patch } from '@/api/client'

export function getValidateClasses({ tab = 'requested', page = 1, pageSize = 15 } = {}) {
    const params = new URLSearchParams({ tab, page, pageSize })
    return get(`/api/staff/validate-classes?${params}`)
}

export function staffApprove(classId) {
    return (perParticipantPrice) => patch(`/api/coachclasses/${classId}/staff-respond`, { approve: true, ...(perParticipantPrice ? { perParticipantPrice } : {}) })
}

export function staffReject(classId, reason) {
    return patch(`/api/coachclasses/${classId}/staff-respond`, { approve: false, ...(reason ? { reason } : {}) })
}

export function staffValidate(classId, confirmed = true, perParticipantPrice) {
    return patch(`/api/coachclasses/${classId}/staff-validate`, { confirmed, ...(perParticipantPrice ? { perParticipantPrice } : {}) })
}

export function cancelClass(classId) {
    return patch(`/api/coachclasses/${classId}/cancel`)
}

export function updateClassDetails(classId, body) {
    return patch(`/api/coachclasses/${classId}/update-details`, body)
}

export function getDefaultPrice(date) {
    const params = new URLSearchParams()
    if (date) params.set('date', date)
    return get(`/api/coachclasses/default-price?${params}`)
}

export function getAgenda({ from, to, studioId } = {}) {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    if (studioId) params.set('studioId', studioId)
    return get(`/api/staff/agenda?${params}`)
}

export default {
    getValidateClasses,
    staffApprove,
    staffReject,
    staffValidate,
    cancelClass,
    getAgenda,
    getDefaultPrice,
}
