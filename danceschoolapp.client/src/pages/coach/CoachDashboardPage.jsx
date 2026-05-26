import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import PageCard from '@/components/common/PageCard'
import KpiCard from '@/components/common/KpiCard'
import { getCoachDashboard } from '@/services/coachService'
import '@/styles/AdminPage.css'
import '@/styles/DashboardCards.css'

const fmt = {
    date: v => v ? new Date(v).toLocaleDateString('pt-PT', { weekday: 'short', day: '2-digit', month: 'short' }) : '—',
    time: v => v ? new Date(v).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }) : '—',
}

function statusPill(status) {
    // status: 1=Approved, 7=StaffApproved, etc.
    if (status === 1 || status === 7) return { label: 'Confirmado', cls: '' }
    if (status === 6)                 return { label: 'Pendente',   cls: 'dashboard-pill--pending' }
    return { label: 'Agendado', cls: '' }
}

function CoachDashboardPage() {
    const [dashboard, setDashboard] = useState(null)
    const [loading,   setLoading]   = useState(true)
    const [error,     setError]     = useState('')

    useEffect(() => {
        getCoachDashboard()
            .then(setDashboard)
            .catch(err => setError(err.message))
            .finally(() => setLoading(false))
    }, [])

    const firstName         = dashboard?.coachName?.split(' ')[0] ?? 'Professor'
    const validationsPending = dashboard?.validationsPending ?? 0

    return (
        <PageCard>
            <div className="admin-page-header">
                <div>
                    <h2>Bem-vindo, {firstName}!</h2>
                    <p>A sua agenda e actividade este mês.</p>
                </div>
                <Link to="/coach/agenda" className="btn btn-secondary btn-sm">
                    Ver agenda
                </Link>
            </div>

            {error && <p className="admin-error">{error}</p>}

            {!loading && validationsPending > 0 && (
                <div className="alert-banner alert-banner--warning">
                    <span>
                        {validationsPending} {validationsPending === 1 ? 'coaching aguarda' : 'coachings aguardam'} a sua validação
                    </span>
                    <Link to="/coach/validar-aulas">Validar</Link>
                </div>
            )}

            <div className="kpi-grid">
                <KpiCard
                    label="Coachings Dados"
                    value={dashboard?.classesTaughtThisMonth}
                    loading={loading}
                    icon="validate"
                    description="Este mês"
                    tone="success"
                />
                <KpiCard
                    label="Próximos Coachings"
                    value={dashboard?.classesUpcoming}
                    loading={loading}
                    icon="classes"
                    description="Agendadas à frente"
                    to="/coach/agenda"
                />
                <KpiCard
                    label="Validações Pendentes"
                    value={validationsPending}
                    loading={loading}
                    tone={validationsPending > 0 ? 'orange' : 'default'}
                    icon="validate"
                    description="Aguardam confirmação"
                    to={validationsPending > 0 ? '/coach/validar-aulas' : undefined}
                />
            </div>

            <h3 className="dashboard-section-title">Próximos Coachings</h3>

            {loading ? (
                <p style={{ color: 'var(--text-3)', fontSize: '0.875rem' }}>A carregar…</p>
                ) : !dashboard?.upcomingClasses?.length ? (
                <p className="dashboard-empty">Sem coachings próximos agendados.</p>
            ) : (
                <div className="dashboard-cards-grid">
                    {dashboard.upcomingClasses.map(item => {
                        const pill = statusPill(item.status)
                        return (
                            <div key={item.classId} className="dashboard-info-card">
                                <div className="dashboard-card-header">
                                    <h4>{item.modalityName}</h4>
                                    <span className={`dashboard-pill ${pill.cls}`}>{pill.label}</span>
                                </div>

                                {item.studentNames?.length > 0 && (
                                    <p>{item.studentNames.join(', ')}</p>
                                )}

                                <div className="dashboard-card-footer">
                                    <span>{fmt.date(item.startDatetime)}</span>
                                    <span>{fmt.time(item.startDatetime)}</span>
                                    {item.studioName && <span>{item.studioName}</span>}
                                </div>
                            </div>
                        )
                    })}
                </div>
            )}
        </PageCard>
    )
}

export default CoachDashboardPage
