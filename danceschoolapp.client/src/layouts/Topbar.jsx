import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import NotificationButton from '../components/NotificationButton'

function Topbar() {
    const { user, logout } = useAuth()
    const navigate = useNavigate()

    const firstName = user?.firstName ?? user?.FirstName ?? ''
    const lastName  = user?.lastName  ?? user?.LastName  ?? ''
    const fullName  = [firstName, lastName].filter(Boolean).join(' ')
    const username  = fullName || user?.username || user?.Username || 'Utilizador'

    const handleLogout = async () => {
        navigate('/')
        await logout()
    }

    return (
        <header className="dashboard-topbar">
            <div className="dashboard-brand">
                <div>
                    <h1>Ent&apos;Artes</h1>
                    <span>Escola de Dança</span>
                </div>
            </div>

            <div className="dashboard-topbar-right">
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                    <div className="dashboard-user-pill">
                        <div className="dashboard-user-avatar">
                            {username.charAt(0).toUpperCase()}
                        </div>
                        <span>{username}</span>
                    </div>

                    <NotificationButton />

                    <button
                        className="dashboard-logout-btn"
                        onClick={handleLogout}
                    >
                        Sair
                    </button>
                </div>
            </div>
        </header>
    )
}

export default Topbar