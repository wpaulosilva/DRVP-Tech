import { get } from '@/api/client'
// See EndpointsMapping.md for full API reference

/** Parent-facing: returns active coaches with their modalities */
export function getCoachesForParent() {
    return get('/api/ee/coaches')
}

export function getCoaches() {
    return get('/api/coaches')
}
