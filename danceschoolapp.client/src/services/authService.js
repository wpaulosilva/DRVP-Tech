import { post } from '@/api/client'

export function resetPassword({ token, newPassword }) {
    return post('/api/auth/reset-password', {
        token,
        newPassword,
    })
}