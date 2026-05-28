import { get, patch, post } from '@/api/client'
// See EndpointsMapping.md for full API reference

export function getMyClasses({ from, to } = {}) {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    const qs = params.toString()
    return get(`/api/ee/classes/my${qs ? `?${qs}` : ''}`)
}

/** Returns all classes where any of this parent's students are enrolled */
export function getClassesByParent(parentUserId) {
    return get(`/api/coachclasses/parent/${parentUserId}`)
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

export function getOpenClasses({ from, to, modalityId } = {}) {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to)   params.set('to', to)
    if (modalityId) params.set('modalityId', modalityId)
    const qs = params.toString()
    return get(`/api/ee/classes/open${qs ? `?${qs}` : ''}`)
}

export function getValidateClasses({ page = 1, pageSize = 10 } = {}) {
    const params = new URLSearchParams({ page, pageSize })
    return get(`/api/ee/classes/validate?${params}`)
}

export function parentValidateParticipant(participantId, attended) {
    return patch(`/api/participants/${participantId}/parent-validate`, { attended })
}

/** POST /api/coachclasses — parent requests an individual class on an available slot
 *  Body: { coachId, modalityId, startDatetime, endDatetime, studentId }
 */
export function parentCreateClass(body) {
    return post('/api/coachclasses', body)
}

/** PATCH /api/participants/{id}/parent-approve-enrollment — parent approves/rejects enrollment in a coach-created class */
export function approveEnrollment(participantId, approve) {
    return patch(`/api/participants/${participantId}/parent-approve-enrollment`, { approve })
}

/** POST /api/participants — parent enrolls a student in an existing open class */
export function enrollInClass(body) {
    return post('/api/participants', body)
}

/** POST /api/participants/invite-join — enroll via invite link (Requested/CoachApproved/Approved) */
export function enrollByInvite(body) {
    return post('/api/participants/invite-join', body)
}

/** GET /api/coachclasses/join-class-status — returns { enabled: bool } */
export function getJoinClassStatus() {
    return get('/api/coachclasses/join-class-status')
}

/** GET /api/coachclasses/{id} — fetch a single class by id */
export function getClassById(id) {
    return get(`/api/coachclasses/${id}`)
}
