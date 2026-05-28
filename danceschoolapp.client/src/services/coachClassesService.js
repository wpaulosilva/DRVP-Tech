import { get, patch, post } from '@/api/client'

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
export function getCoachAgenda({ from, to } = {}) {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    return get(`/api/coach/agenda?${params}`)
}

/** GET /api/coach/students?modalityId={id} — students enrolled in a modality (for class creation picker) */
export function getStudentsByModality(modalityId) {
    return get(`/api/coach/students?modalityId=${modalityId}`)
}

/** GET /api/coachclasses/max-participants — system-configured max group size */
export function getMaxParticipants() {
    return get('/api/coachclasses/max-participants')
}

/** POST /api/coachclasses/coach-create — coach creates individual or group class
 *  Body: { modalityId, startDatetime, endDatetime, maxParticipants, studentIds[] }
 */
export function coachCreateClass(body) {
    return post('/api/coachclasses/coach-create', body)
}