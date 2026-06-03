import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import '../../styles/LoginPage.css'
import logo from '../../assets/logo-entartes.svg'
import { useAuth } from '../../context/useAuth'
import Icon from '../../components/ui/Icon'
import { API_BASE } from '../../api/client'

function mapLoginError(serverMessage) {
    if (!serverMessage) return 'Ocorreu um erro. Tente novamente.'

    const msg = serverMessage.toLowerCase()

    if (msg.includes('inactive') || msg.includes('account is inactive')) {
        return 'Esta conta está desativada. Contacte a direção da escola.'
    }

    if (msg.includes('invalid credentials') || msg.includes('invalid') || msg.includes('credentials')) {
        return 'Email ou palavra-passe incorretos.'
    }

    return 'Ocorreu um erro ao iniciar sessão. Tente novamente.'
}

function LoginPage() {
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')
    const [showPassword, setShowPassword] = useState(false)
    const [error, setError] = useState('')
    const [loading, setLoading] = useState(false)

    const { refreshSession } = useAuth()
    const navigate = useNavigate()

    const handleSubmit = async (e) => {
        e.preventDefault()
        setError('')
        setLoading(true)

        try {
            const response = await fetch(`${API_BASE}/api/auth/login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ email, password }),
            })

            if (!response.ok) {
                let serverMsg = ''

                try {
                    const text = await response.text()
                    serverMsg = JSON.parse(text)
                } catch {
                    serverMsg = ''
                }

                throw new Error(serverMsg)
            }

            const data = await response.json()
            await refreshSession()

            localStorage.setItem('user', JSON.stringify({
                id: data.userId,
                username: data.username,
                roles: data.roles,
            }))

            const roleNames = (data.roles || []).map((r) => r.toLowerCase())

            if (roleNames.includes('admin')) navigate('/admin')
            else if (roleNames.includes('staff')) navigate('/staff')
            else if (roleNames.includes('coach')) navigate('/coach')
            else if (roleNames.includes('parent')) navigate('/parent')
            else navigate('/')
        } catch (err) {
            setError(mapLoginError(err.message))
        } finally {
            setLoading(false)
        }
    }

    return (
        <div className="login-page">
            <div className="login-card">
                <img src={logo} alt="Ent'Artes" className="login-logo" />

                <h1>Iniciar sessão</h1>
                <p className="login-subtitle">Portal de gestão Ent&apos;Artes</p>

                <form onSubmit={handleSubmit} className="login-form" noValidate>
                    <div className="form-group">
                        <label htmlFor="email">Email</label>
                        <input
                            id="email"
                            type="email"
                            placeholder="nome@exemplo.com"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            required
                            autoComplete="email"
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="password">Palavra-passe</label>

                        <div className="password-field">
                            <input
                                id="password"
                                type={showPassword ? 'text' : 'password'}
                                placeholder="••••••••"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                required
                                autoComplete="current-password"
                            />

                            <button
                                type="button"
                                className="password-toggle"
                                onClick={() => setShowPassword((prev) => !prev)}
                                aria-label={showPassword ? 'Ocultar palavra-passe' : 'Mostrar palavra-passe'}
                            >
                                <Icon name={showPassword ? 'eyeOff' : 'eye'} size={18} />
                            </button>
                        </div>
                    </div>

                    <div className="login-links">
                        <Link to="/forgot-password">Esqueceste-te da palavra-passe?</Link>
                    </div>

                    <button type="submit" className="login-btn" disabled={loading}>
                        {loading ? 'A entrar…' : 'Entrar'}
                    </button>

                    {error && (
                        <div className="login-error" role="alert">
                            <Icon name="alert" size={16} style={{ flexShrink: 0, marginTop: 1 }} />
                            {error}
                        </div>
                    )}
                </form>
            </div>
        </div>
    )
}

export default LoginPage