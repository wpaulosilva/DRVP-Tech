import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import PageCard from '../../components/common/PageCard'
import KpiCard from '../../components/common/KpiCard'
import { getStaffDashboardStats } from '../../services/dashboardService'
import '../../styles/AdminPage.css'
import '../../styles/DashboardCards.css'

const fmt = {
    date: v => v ? new Date(v).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short' }) : '—',
    time: v => v ? new Date(v).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }) : '—',
}

function StaffDashboardPage() {
    const [stats,   setStats]   = useState(null)
    const [loading, setLoading] = useState(true)
    const [error,   setError]   = useState('')

    useEffect(() => {
        getStaffDashboardStats()
            .then(setStats)
            .catch(err => setError(err.message))
            .finally(() => setLoading(false))
    }, [])

    const pendingValidation = stats?.classesPendingValidation ?? 0
    const pendingStudents   = stats?.pendingStudentRegistrations ?? 0

    return (
        <PageCard>
            <div className="admin-page-header">
                <div>
                    <h2>Painel de Direção</h2>
                    <p>Gestão completa da escola Ent&apos;Artes.</p>
                </div>
            </div>

            {error && <p className="admin-error">{error}</p>}

            {/* Action alerts — only shown when there's something needing attention */}
            {!loading && pendingValidation > 0 && (
                <div className="alert-banner alert-banner--warning">
                    <span>
                        {pendingValidation} {pendingValidation === 1 ? 'coaching aguarda' : 'coachings aguardam'} validação
                    </span>
                    <Link to="/staff/validar-aulas">Validar agora</Link>
                </div>
            )}
            {!loading && pendingStudents > 0 && (
                <div className="alert-banner alert-banner--warning">
                    <span>
                        {pendingStudents} {pendingStudents === 1 ? 'novo estudante aguarda' : 'novos estudantes aguardam'} aprovação
                    </span>
                    <Link to="/staff/validar-estudantes">Aprovar</Link>
                </div>
            )}

            {/* Row 1 — People */}
            <div className="kpi-grid">
                <KpiCard
                    label="Encarregados"
                    value={stats?.totalParents}
                    loading={loading}
                    icon="users"
                    description="Contas de EE ativas"
                    to="/staff/utilizadores"
                />
                <KpiCard
                    label="Professores"
                    value={stats?.totalCoaches}
                    loading={loading}
                    icon="students"
                    description="Professores ativos"
                    to="/staff/utilizadores"
                />
                <KpiCard
                    label="Estudantes Ativos"
                    value={stats?.totalActiveStudents}
                    loading={loading}
                    icon="students"
                    description="Alunos com inscrição válida"
                    to="/staff/validar-estudantes"
                />
                <KpiCard
                    label="Eventos Ativos"
                    value={stats?.totalActiveEvents}
                    loading={loading}
                    icon="events"
                    description="Publicados e visíveis"
                    to="/staff/eventos"
                />
            </div>

            {/* Row 2 — Classes this month */}
            <div className="kpi-grid">
                <KpiCard
                    label="Coachings Este Mês"
                    value={stats?.classesScheduledThisMonth}
                    loading={loading}
                    icon="classes"
                    description="Agendados no mês corrente"
                />
                <KpiCard
                    label="Coachings Realizados"
                    value={stats?.classesCompletedThisMonth}
                    loading={loading}
                    tone="success"
                    icon="validate"
                    description="Concluídas e validadas"
                />
                <KpiCard
                    label="Pendentes Validação"
                    value={pendingValidation}
                    loading={loading}
                    tone={pendingValidation > 0 ? 'orange' : 'default'}
                    icon="validate"
                    description="Aguardam confirmação"
                    to={pendingValidation > 0 ? '/staff/validar-aulas' : undefined}
                />
            </div>

            {/* Upcoming events */}
            <h3 className="dashboard-section-title">Próximos Eventos</h3>

            {loading ? (
                <p style={{ color: 'var(--text-3)', fontSize: '0.875rem' }}>A carregar…</p>
            ) : !stats?.upcomingEvents?.length ? (
                <p className="dashboard-empty">Sem eventos próximos.</p>
            ) : (
                <div className="dashboard-cards-grid">
                    {stats.upcomingEvents.map(event => (
                        <div key={event.eventId} className="dashboard-info-card">
                            <h4>{event.title}</h4>
                            <div className="dashboard-card-footer">
                                <span>{fmt.date(event.startDatetime)}</span>
                                <span>{fmt.time(event.startDatetime)}</span>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </PageCard>
    )
}

export default StaffDashboardPage
