import { get, patch } from '@/api/client'

export function getValidateClasses({ tab = 'requested', page = 1, pageSize = 15 } = {}) {
    const params = new URLSearchParams({ tab, page, pageSize })
    return get(`/api/staff/validate-classes?${params}`)
}

export function staffApprove(classId) {
    return patch(`/api/coachclasses/${classId}/staff-approve`)
}

export function staffReject(classId) {
    return patch(`/api/coachclasses/${classId}/staff-reject`)
}

export function staffValidate(classId, didTeach) {
    return patch(`/api/coachclasses/${classId}/staff-validate`, { didTeach })
}

export default {
    getValidateClasses,
    staffApprove,
    staffReject,
    staffValidate,
}
