import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import NotificationButton from '../components/NotificationButton'
import ThemeToggle from '../components/ui/ThemeToggle'
import Icon from '../components/ui/Icon'

function Topbar() {
    const { user, logout } = useAuth()
    const navigate = useNavigate()

    const firstName = user?.firstName ?? user?.FirstName ?? ''
    const lastName  = user?.lastName  ?? user?.LastName  ?? ''
    const fullName  = [firstName, lastName].filter(Boolean).join(' ')
    const displayName = fullName || user?.username || user?.Username || 'Utilizador'
    const initials = displayName
        .split(' ')
        .slice(0, 2)
        .map(w => w.charAt(0).toUpperCase())
        .join('')

    const handleLogout = async () => {
        navigate('/')
        await logout()
    }

    return (
        <header className="dashboard-topbar">
            <div className="dashboard-brand">
                <div className="dashboard-brand-dot" />
                <div>
                    <h1>Ent&apos;Artes</h1>
                    <span>Escola de Dança</span>
                </div>
            </div>

            <div className="dashboard-topbar-right">
                <ThemeToggle />

                <NotificationButton />

                <div className="dashboard-user-pill">
                    <div className="dashboard-user-avatar" aria-hidden="true">
                        {initials || '?'}
                    </div>
                    <span className="dashboard-user-name">{displayName}</span>
                </div>

                <button
                    className="dashboard-logout-btn"
                    onClick={handleLogout}
                    title="Terminar sessão"
                >
                    <Icon name="logout" size={15} />
                    Sair
                </button>
            </div>
        </header>
    )
}

export default Topbar
