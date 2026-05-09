import { useEffect, useState } from 'react'
import PageCard from '@/components/common/PageCard'
import KpiCard from '@/components/common/KpiCard'
import { getCoachDashboard } from '@/services/coachService'
import '@/styles/AdminPage.css'
import '@/styles/DashboardCards.css'

function CoachDashboardPage() {
    const [dashboard, setDashboard] = useState(null)
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')

    useEffect(() => {
        const fetchDashboard = async () => {
            try {
                const data = await getCoachDashboard()
                setDashboard(data)
            } catch (err) {
                setError(err.message)
            } finally {
                setLoading(false)
            }
        }

        fetchDashboard()
    }, [])

    const firstName = dashboard?.coachName?.split(' ')[0] ?? 'Professor'

    const formatDate = (value) =>
        value
            ? new Date(value).toLocaleDateString('pt-PT', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
            })
            : '—'

    const formatTime = (value) =>
        value
            ? new Date(value).toLocaleTimeString('pt-PT', {
                hour: '2-digit',
                minute: '2-digit',
            })
            : '—'

    return (
        <PageCard>
            <h2>Bem-vindo, Prof. {firstName}!</h2>
            <p>Gerir a sua disponibilidade e agenda.</p>

            {error && <p className="admin-error">{error}</p>}

            <div className="kpi-grid">
                <KpiCard
                    label="Aulas Dadas Este Mês"
                    value={dashboard?.classesTaughtThisMonth}
                    loading={loading}
                />

                <KpiCard
                    label="Aulas que Faltam"
                    value={dashboard?.classesUpcoming}
                    loading={loading}
                />

                <KpiCard
                    label="Validações Pendentes"
                    value={dashboard?.validationsPending}
                    loading={loading}
                    tone="orange"
                />
            </div>

            <h3 className="dashboard-section-title">Próximas Aulas</h3>

            {loading ? (
                <p>A carregar...</p>
            ) : !dashboard?.upcomingClasses?.length ? (
                <p className="table-empty">Sem próximas aulas.</p>
            ) : (
                <div className="dashboard-cards-grid">
                    {dashboard.upcomingClasses.map((item) => (
                        <div key={item.classId} className="dashboard-info-card">
                            <h4>{item.modalityName}</h4>

                            <p>
                                {item.studentNames?.length
                                    ? item.studentNames.join(', ')
                                    : '—'}
                            </p>

                            <div className="dashboard-card-footer">
                                <span>{formatDate(item.startDatetime)}</span>
                                <span>{formatTime(item.startDatetime)}</span>
                                <span>{item.studioName || '—'}</span>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </PageCard>
    )
}

export default CoachDashboardPage