import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import PageCard from '@/components/common/PageCard'
import KpiCard from '@/components/common/KpiCard'
import { getParentDashboard } from '@/services/parentService'
import '@/styles/AdminPage.css'
import '@/styles/DashboardCards.css'

const fmt = {
    date: v => v ? new Date(v).toLocaleDateString('pt-PT', { weekday: 'short', day: '2-digit', month: 'short' }) : '—',
    time: v => v ? new Date(v).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }) : '—',
}

const STATUS_LABEL = {
    0: { label: 'Pendente',    cls: 'dashboard-pill--pending' },
    1: { label: 'Confirmada',  cls: '' },
    4: { label: 'Concluída',   cls: 'dashboard-pill--pending' },
    6: { label: 'A Validar',   cls: 'dashboard-pill--pending' },
    7: { label: 'Aprovada',    cls: '' },
}

function classStatusPill(status) {
    return STATUS_LABEL[status] ?? { label: 'Agendada', cls: '' }
}

function ParentDashboardPage() {
    const [dashboard, setDashboard] = useState(null)
    const [loading,   setLoading]   = useState(true)
    const [error,     setError]     = useState('')

    useEffect(() => {
        getParentDashboard()
            .then(setDashboard)
            .catch(err => setError(err.message))
            .finally(() => setLoading(false))
    }, [])

    const firstName          = dashboard?.parentName?.split(' ')[0] ?? ''
    const awaitingValidation = dashboard?.classesAwaitingValidation ?? 0
    const upcomingCount      = dashboard?.upcomingClasses?.length ?? 0

    return (
        <PageCard>
            <div className="admin-page-header">
                <div>
                    <h2>Olá{firstName ? `, ${firstName}` : ''}!</h2>
                    <p>Acompanhe os coachings e actividades dos seus estudantes.</p>
                </div>
                <Link to="/parent/aulas" className="btn btn-primary btn-sm">
                    Marcar Coaching
                </Link>
            </div>

            {error && <p className="admin-error">{error}</p>}

            {!loading && awaitingValidation > 0 && (
                <div className="alert-banner alert-banner--warning">
                    <span>
                        {awaitingValidation} {awaitingValidation === 1 ? 'coaching aguarda' : 'coachings aguardam'} a sua confirmação
                    </span>
                    <Link to="/parent/aulas">Confirmar</Link>
                </div>
            )}

            <div className="kpi-grid">
                <KpiCard
                    label="Próximos Coachings"
                    value={upcomingCount}
                    loading={loading}
                    icon="classes"
                    description="Agendadas à frente"
                    to="/parent/aulas"
                />
                <KpiCard
                    label="Por Validar"
                    value={awaitingValidation}
                    loading={loading}
                    tone={awaitingValidation > 0 ? 'orange' : 'default'}
                    icon="validate"
                    description="Precisam da sua confirmação"
                    to={awaitingValidation > 0 ? '/parent/aulas' : undefined}
                />
            </div>

            <h3 className="dashboard-section-title">Próximos Coachings</h3>

            {loading ? (
                <p style={{ color: 'var(--text-3)', fontSize: '0.875rem' }}>A carregar…</p>
            ) : !dashboard?.upcomingClasses?.length ? (
                <div className="dashboard-empty">
                    <p>Sem coachings próximos.</p>
                    <Link to="/parent/aulas" className="btn btn-primary btn-sm" style={{ marginTop: '0.75rem' }}>
                        Marcar um coaching
                    </Link>
                </div>
            ) : (
                <div className="dashboard-cards-grid">
                    {dashboard.upcomingClasses.map(item => {
                        const pill = classStatusPill(item.status)
                        return (
                            <div key={item.classId} className="dashboard-info-card">
                                <div className="dashboard-card-header">
                                    <h4>{item.modalityName}</h4>
                                    <span className={`dashboard-pill ${pill.cls}`}>{pill.label}</span>
                                </div>

                                {item.coachName && <p>Prof. {item.coachName}</p>}
                                {item.studentName && (
                                    <p style={{ fontSize: '0.82rem', color: 'var(--text-3)', marginTop: '-0.25rem' }}>
                                        {item.studentName}
                                    </p>
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

export default ParentDashboardPage
