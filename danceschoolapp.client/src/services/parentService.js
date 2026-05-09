import { get } from '@/api/client'

export function getParentDashboard() {
    return get('/api/ee/dashboard')
}