import { get, patch } from '@/api/client'

export function getValidateClasses({ tab = 'requested', page = 1, pageSize = 15 } = {}) {
    const params = new URLSearchParams({ tab, page, pageSize })
    return get(`/api/staff/validate-classes?${params}`)
}

export function staffApprove(classId) {
    return patch(`/api/coachclasses/${classId}/staff-approve`)
}

export function staffReject(classId, reason) {
    return patch(`/api/coachclasses/${classId}/staff-reject`, reason ? { reason } : undefined)
}

export function staffValidate(classId) {
    return patch(`/api/coachclasses/${classId}/staff-validate`)
}

export function cancelClass(classId) {
    return patch(`/api/coachclasses/${classId}/cancel`)
}

export default {
    getValidateClasses,
    staffApprove,
    staffReject,
    staffValidate,
    cancelClass,
}
