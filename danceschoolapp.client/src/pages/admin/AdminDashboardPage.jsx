import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import PageCard from '../../components/common/PageCard'
import KpiCard from '../../components/common/KpiCard'
import { getAdminDashboardStats } from '../../services/dashboardService'
import '../../styles/AdminPage.css'

function AdminDashboardPage() {
    const [stats,   setStats]   = useState(null)
    const [loading, setLoading] = useState(true)
    const [error,   setError]   = useState('')

    useEffect(() => {
        getAdminDashboardStats()
            .then(setStats)
            .catch(err => setError(err.message))
            .finally(() => setLoading(false))
    }, [])

    const inactive = stats
        ? stats.totalStaffAccounts - stats.activeStaffAccounts
        : null

    return (
        <PageCard>
            <div className="admin-page-header">
                <div>
                    <h2>Painel de Administração</h2>
                    <p>Gestão de contas de Direção e configurações do sistema.</p>
                </div>
                <Link to="/admin/utilizadores" className="btn btn-primary btn-sm">
                    Gerir Contas
                </Link>
            </div>

            {error && <p className="admin-error">{error}</p>}

            <div className="kpi-grid">
                <KpiCard
                    label="Contas de Direção"
                    value={stats?.totalStaffAccounts}
                    loading={loading}
                    icon="direction"
                    description="Total de contas criadas"
                    to="/admin/utilizadores"
                />
                <KpiCard
                    label="Contas Ativas"
                    value={stats?.activeStaffAccounts}
                    loading={loading}
                    tone="success"
                    icon="users"
                    description="Com acesso ao sistema"
                    to="/admin/utilizadores"
                />
                <KpiCard
                    label="Contas Inativas"
                    value={inactive}
                    loading={loading}
                    tone={inactive > 0 ? 'orange' : 'default'}
                    icon="blocked"
                    description="Acesso desativado"
                    to="/admin/utilizadores"
                />
            </div>

            <div className="dashboard-section-title" style={{ marginTop: '2rem' }}>
                Ações rápidas
            </div>

            <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                <Link to="/admin/utilizadores" className="btn btn-secondary">
                    Ver todas as contas
                </Link>
            </div>
        </PageCard>
    )
}

export default AdminDashboardPage
