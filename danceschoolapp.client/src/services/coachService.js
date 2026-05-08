import { get } from '@/api/client'
// See EndpointsMapping.md for full API reference

export function getCoachesForParent() {
    return get('/api/ee/coaches')
}

export function getCoaches() {
    return get('/api/coaches')
}

export function getCoachMe() {
    return get('/api/coaches/me')
}

export function getCoachDashboard() {
    return get('/api/coach/dashboard')
}   