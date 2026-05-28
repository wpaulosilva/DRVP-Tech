import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import '../../styles/LoginPage.css'
import logo from '../../assets/logo-entartes.svg'
import { resetPassword } from '@/services/authService'

function ResetPasswordPage() {
    const navigate = useNavigate()

    const token = useMemo(() => {
        return new URLSearchParams(window.location.search).get('token')
    }, [])

    const [newPassword, setNewPassword] = useState('')
    const [confirmPassword, setConfirmPassword] = useState('')
    const [error, setError] = useState('')
    const [success, setSuccess] = useState('')
    const [loading, setLoading] = useState(false)

    const handleSubmit = async (e) => {
        e.preventDefault()
        setError('')
        setSuccess('')

        if (!token) {
            setError('Token inválido ou em falta.')
            return
        }

        if (newPassword !== confirmPassword) {
            setError('As palavras-passe não coincidem.')
            return
        }

        setLoading(true)

        try {
            await resetPassword({
                token,
                newPassword,
            })

            setSuccess('Palavra-passe alterada com sucesso. A redirecionar...')

            setTimeout(() => {
                navigate('/login')
            }, 2000)
        } catch (err) {
            console.error(err)
            setError(
                err?.response?.data ||
                err?.message ||
                'Erro ao alterar a palavra-passe.'
            )
        } finally {
            setLoading(false)
        }
    }

    return (
        <div className="login-page">
            <div className="login-card">
                <img src={logo} alt="Ent'Artes" className="login-logo" />

                <h1>Alterar palavra-passe</h1>

                <form onSubmit={handleSubmit} className="login-form">
                    <div className="form-group">
                        <label htmlFor="newPassword">Nova palavra-passe</label>
                        <input
                            id="newPassword"
                            type="password"
                            placeholder="••••••••"
                            value={newPassword}
                            onChange={(e) => setNewPassword(e.target.value)}
                            required
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="confirmPassword">
                            Confirmar palavra-passe
                        </label>
                        <input
                            id="confirmPassword"
                            type="password"
                            placeholder="••••••••"
                            value={confirmPassword}
                            onChange={(e) => setConfirmPassword(e.target.value)}
                            required
                        />
                    </div>

                    <button
                        type="submit"
                        className="login-btn"
                        disabled={loading}
                    >
                        {loading ? 'A alterar...' : 'Alterar palavra-passe'}
                    </button>

                    {error && (
                        <p style={{ color: 'red', marginTop: '12px' }}>
                            {error}
                        </p>
                    )}

                    {success && (
                        <p style={{ color: 'green', marginTop: '12px' }}>
                            {success}
                        </p>
                    )}
                </form>
            </div>
        </div>
    )
}

export default ResetPasswordPage