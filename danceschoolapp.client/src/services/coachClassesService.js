import { get, patch } from '@/api/client'

export function getCoachValidate({ tab = 'requests', page = 1, pageSize = 20 } = {}) {
    const params = new URLSearchParams({ tab, page, pageSize })
    return get(`/api/coach/validate?${params}`)
}

export function coachAccept(classId) {
    return patch(`/api/coachclasses/${classId}/coach-accept`)
}

export function coachReject(classId, reason) {
    return patch(`/api/coachclasses/${classId}/coach-reject`, reason ? { reason } : undefined)
}

export function coachValidate(classId, didTeach) {
    return patch(`/api/coachclasses/${classId}/coach-validate`, { didTeach })
}
