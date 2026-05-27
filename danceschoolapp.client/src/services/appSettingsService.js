import { get, patch } from '@/api/client'

export function getAppSettings() {
    return get('/api/appsettings')
}

export function updateAppSetting(key, value) {
    return patch(`/api/appsettings/${key}`, { value: String(value) })
}