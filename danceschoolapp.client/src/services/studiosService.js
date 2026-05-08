import { get } from '@/api/client'
// See EndpointsMapping.md for full API reference

export function getStudios() {
    return get('/api/studios')
}
