import { useState } from 'react'
import '../../styles/LoginPage.css'
import logo from '../../assets/logo-entartes.svg'
import { post } from '../../api/client'

function ForgotPasswordPage() {
    const [email, setEmail] = useState('')
    const [message, setMessage] = useState('')
    const [error, setError] = useState('')
    const [loading, setLoading] = useState(false)

    const handleSubmit = async (e) => {
        e.preventDefault()
        setError('')
        setMessage('')
        setLoading(true)

        try {
            await post('/api/auth/forgot-password', { email })

            setMessage(
                'Vais receber um link no email para redefinir a palavra-passe.'
            )
        } catch (err) {
            console.error(err)
            setError(err.message || 'Erro ao enviar email.')
        } finally {
            setLoading(false)
        }
    }

    return (
        <div className="login-page">
            <div className="login-card">
                <img src={logo} alt="Ent'Artes" className="login-logo" />

                <h1>Recuperar palavra-passe</h1>

                <form onSubmit={handleSubmit} className="login-form">
                    <div className="form-group">
                        <label htmlFor="email">Email</label>
                        <input
                            id="email"
                            type="email"
                            placeholder="email@exemplo.com"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            required
                        />
                    </div>

                    <button
                        type="submit"
                        className="login-btn"
                        disabled={loading}
                    >
                        {loading ? 'A enviar...' : 'Enviar email'}
                    </button>

                    {message && (
                        <p style={{ color: 'green', marginTop: '12px' }}>
                            {message}
                        </p>
                    )}

                    {error && (
                        <p style={{ color: 'red', marginTop: '12px' }}>
                            {error}
                        </p>
                    )}
                </form>
            </div>
        </div>
    )
}

export default ForgotPasswordPage