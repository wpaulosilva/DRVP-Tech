import { useEffect, useMemo, useState } from 'react'
import { AuthContext } from './AuthContext'
import { API_BASE } from '../api/client'

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null)
    const [loading, setLoading] = useState(true)

    const refreshSession = async () => {
        try {
            // Try the existing access token first (no DB hit)
            let response = await fetch(`${API_BASE}/api/auth/me`, {
                method: 'GET',
                credentials: 'include',
            })

            if (response.status === 401) {
                // Access token expired — try the refresh token
                const refreshRes = await fetch(`${API_BASE}/api/auth/refresh`, {
                    method: 'POST',
                    credentials: 'include',
                })
                if (!refreshRes.ok) {
                    setUser(null)
                    return
                }
                const data = await refreshRes.json()
                setUser(data)
                return
            }

            if (!response.ok) {
                setUser(null)
                return
            }

            const data = await response.json()
            setUser(data)
        } catch (error) {
            console.error('Erro ao restaurar sessão:', error)
            setUser(null)
        } finally {
            setLoading(false)
        }
    }

    useEffect(() => {
        refreshSession()
    }, [])

    // Force logout when the API client fires auth:expired (refresh token invalid)
    useEffect(() => {
        const handler = () => setUser(null)
        window.addEventListener('auth:expired', handler)
        return () => window.removeEventListener('auth:expired', handler)
    }, [])

    const logout = async () => {
        try {
            await fetch(`${API_BASE}/api/auth/logout`, {
                method: 'POST',
                credentials: 'include',
            })
        } catch (error) {
            console.error('Erro no logout:', error)
        } finally {
            setUser(null)
            localStorage.removeItem('user')
        }
    }

    const value = useMemo(() => ({
        user,
        loading,
        logout,
        refreshSession,
        isAuthenticated: !!user,
    }), [user, loading])

    return (
        <AuthContext.Provider value={value}>
            {children}
        </AuthContext.Provider>
    )
}
