import { useEffect, useState } from 'react'
import PageCard from '@/components/common/PageCard'
import KpiCard from '@/components/common/KpiCard'
import { getParentDashboard } from '@/services/parentService'
import '@/styles/AdminPage.css'
import '@/styles/DashboardCards.css'

function ParentDashboardPage() {
    const [dashboard, setDashboard] = useState(null)
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')

    useEffect(() => {
        const fetchDashboard = async () => {
            try {
                const data = await getParentDashboard()
                setDashboard(data)
            } catch (err) {
                setError(err.message)
            } finally {
                setLoading(false)
            }
        }

        fetchDashboard()
    }, [])

    const firstName = dashboard?.parentName?.split(' ')[0] ?? ''

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

    const getStatusLabel = (status) => {
        if (status === 1) return 'Confirmada'
        if (status === 0) return 'Pendente'
        return 'Agendada'
    }

    return (
        <PageCard>
            <h2>Bem-vindo, {firstName || 'Encarregado'}!</h2>
            <p>Gerir aulas e reservas dos seus estudantes.</p>

            {error && <p className="admin-error">{error}</p>}

            <div className="kpi-grid">
                <KpiCard
                    label="Próximas Aulas"
                    value={dashboard?.upcomingClasses?.length ?? 0}
                    loading={loading}
                />

                <KpiCard
                    label="Aulas por Validar"
                    value={dashboard?.classesAwaitingValidation}
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
                            <div className="dashboard-card-header">
                                <h4>{item.modalityName}</h4>

                                <span className="dashboard-pill">
                                    {getStatusLabel(item.status)}
                                </span>
                            </div>

                            <p>{item.coachName || '—'}</p>

                            <p>
                                <strong>Estudante:</strong> {item.studentName || '—'}
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

export default ParentDashboardPage