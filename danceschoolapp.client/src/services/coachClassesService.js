import { get, patch } from '@/api/client'

export function getCoachValidate({ tab = 'requests', page = 1, pageSize = 20 } = {}) {
    const params = new URLSearchParams({ tab, page, pageSize })
    return get(`/api/coach/validate?${params}`)
}

export function coachAccept(classId) {
    return patch(`/api/coachclasses/${classId}/coach-respond`, { accept: true })
}

export function coachReject(classId, reason) {
    return patch(`/api/coachclasses/${classId}/coach-respond`, { accept: false, ...(reason ? { reason } : {}) })
}

export function coachValidate(classId, didTeach) {
    return patch(`/api/coachclasses/${classId}/coach-validate`, { didTeach })
}
