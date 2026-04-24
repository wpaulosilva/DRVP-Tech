// Tabs: minhas-marcacoes | marcar | grupo | validar
// GET /api/ee/classes/my?from=&to=
// GET /api/ee/classes/available-slots?from=&to=&modalityId=&coachId=
// GET /api/ee/classes/open?page=1&pageSize=10
// GET /api/ee/classes/validate?page=1&pageSize=10
// POST /api/coachclasses  |  POST /api/participants
// PATCH /api/participants/{id}/parent-validate  body: { attended: true|false }
import { useCallback, useEffect, useMemo, useState } from 'react'
import PageCard from '../../components/common/PageCard'
import ClassValidationCard from '../../components/common/ClassValidationCard'
import { getMyClasses, getValidateClasses, parentValidateParticipant } from '../../services/classesService'
import '../../styles/AdminPage.css'
import '../../styles/ValidateClasses.css'

const TABS = [
    { id: 'minhas-marcacoes', label: 'Minhas Marcações' },
    { id: 'marcar', label: 'Criar Aula' },
    { id: 'grupo', label: 'Aulas Existentes' },
    { id: 'validar', label: 'Validar Aulas' },
]

function fmtDate(input) {
    if (!input) return ''
    try {
        const d = new Date(input)
        return d.toLocaleDateString('pt-PT', { day: '2-digit', month: 'long', year: 'numeric' })
    } catch {
        return input
    }
}

function fmtTime(input) {
    if (!input) return ''
    try {
        const d = new Date(input)
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    } catch {
        return input
    }
}

function ParentClassesPage() {
    const [activeTab, setActiveTab] = useState('minhas-marcacoes')
    const [myClasses, setMyClasses] = useState([])
    const [validateItems, setValidateItems] = useState([])
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')

    const fetchParentData = useCallback(async () => {
        setLoading(true)
        setError('')

        try {
            const [mine, toValidate] = await Promise.all([
                getMyClasses(),
                getValidateClasses({ page: 1, pageSize: 20 }),
            ])

            if (Array.isArray(mine)) {
                setMyClasses(mine)
            } else if (mine && Array.isArray(mine.items)) {
                setMyClasses(mine.items)
            } else {
                setMyClasses([])
            }

            if (Array.isArray(toValidate)) {
                setValidateItems(toValidate)
            } else if (toValidate && Array.isArray(toValidate.items)) {
                setValidateItems(toValidate.items)
            } else {
                setValidateItems([])
            }
        } catch (err) {
            setError(err.message)
        } finally {
            setLoading(false)
        }
    }, [])

    useEffect(() => {
        fetchParentData()
    }, [fetchParentData])

    const handleValidate = async (participantId, attended) => {
        try {
            await parentValidateParticipant(participantId, attended)
            // Update the participant inside its class item
            setValidateItems((prev) => prev.map((cls) => {
                const parts = cls.Participants ?? cls.participants ?? []
                if (!parts.some(p => (p.ParticipantId ?? p.participantId) === participantId)) return cls
                const updateParts = (list) => list?.map(p => {
                    if ((p.ParticipantId ?? p.participantId) === participantId)
                        return { ...p, ValidationStatus: attended ? 1 : 2, validationStatus: attended ? 1 : 2 }
                    return p
                })
                return {
                    ...cls,
                    Participants: updateParts(cls.Participants),
                    participants: updateParts(cls.participants),
                }
            }))
        } catch (err) {
            alert(err.message)
        }
    }

    // Classes where at least one of my students is still pending
    const pendingValidations = useMemo(() =>
        validateItems.filter(cls => {
            const parts = cls.Participants ?? cls.participants ?? []
            return parts.some(p => (p.ValidationStatus ?? p.validationStatus ?? 0) === 0)
        }),
        [validateItems]
    )

    // Classes where all of my students have already voted
    const doneValidations = useMemo(() =>
        validateItems.filter(cls => {
            const parts = cls.Participants ?? cls.participants ?? []
            return parts.length > 0 && parts.every(p => (p.ValidationStatus ?? p.validationStatus ?? 0) !== 0)
        }),
        [validateItems]
    )

    const tabContent = useMemo(() => {
        if (activeTab === 'minhas-marcacoes') {
            if (loading) return <div className="validate-empty"><p>Carregando...</p></div>
            if (error) return <p className="admin-error">{error}</p>
            if (!myClasses.length) return (
                <div className="validate-empty">
                    <div className="validate-empty-icon">{'\uD83D\uDCC5'}</div>
                    <h3>Sem marcações</h3>
                    <p>Não tem aulas marcadas de momento.</p>
                </div>
            )
            return myClasses.map((c, idx) => {
                const id = c.classId ?? c.id ?? idx
                const modality = c.modalityName ?? c.modality ?? c.name ?? 'Aula'
                const student = c.studentName ?? c.student?.name ?? c.student ?? ''
                const start = c.startDatetime ?? c.date ?? c.datetime
                const time = fmtTime(start)
                const date = fmtDate(start)

                return (
                    <div key={id} className="session-card">
                        <div className="session-card-top">
                            <h3 className="session-card-name">{modality}</h3>
                        </div>
                        {student && <p style={{ margin: 0, color: '#6b7280', fontSize: '0.9rem' }}>{student}</p>}
                        <div className="session-card-info">
                            <span>{date}</span>
                            <span>{time}</span>
                        </div>
                        {(c.validationDeadline || c.deadline) && (
                            <div className="validate-warning" style={{ margin: 0, padding: '10px 12px' }}>
                                <span className="validate-warning-icon">!</span>
                                <p>Prazo: {fmtDate(c.validationDeadline ?? c.deadline)}</p>
                            </div>
                        )}
                    </div>
                )
            })
        }

        if (activeTab === 'validar') {
            if (loading) return <div className="validate-empty"><p>Carregando...</p></div>
            if (error) return <p className="admin-error">{error}</p>
            if (!validateItems.length) return (
                <div className="validate-empty">
                    <div className="validate-empty-icon">{'\u2713'}</div>
                    <h3>Tudo validado</h3>
                    <p>Não há aulas para validar neste momento.</p>
                </div>
            )

            return (
                <>
                    {/* Warning banner */}
                    {pendingValidations.length > 0 && (
                        <div className="validate-warning">
                            <span className="validate-warning-icon">!</span>
                            <p>
                                <strong>Atenção:</strong> Tem {pendingValidations.length} aula{pendingValidations.length > 1 ? 's' : ''} aguardando validação.
                                Por favor, confirme se {pendingValidations.length > 1 ? 'foram realizadas' : 'foi realizada'} dentro do prazo de 48 horas.
                            </p>
                        </div>
                    )}

                    {/* Pending sessions */}
                    {pendingValidations.length > 0 && (
                        <>
                            <h3 className="validate-section-heading">
                                Aguardam Validação ({pendingValidations.length})
                            </h3>
                            {pendingValidations.map((cls, idx) => (
                                <ClassValidationCard
                                    key={cls.ClassId ?? cls.classId ?? idx}
                                    aula={cls}
                                    tipo="professor"
                                    variant="amber"
                                    showParticipants
                                    onConfirm={(pId) => handleValidate(pId, true)}
                                    onReject={(pId) => handleValidate(pId, false)}
                                />
                            ))}
                        </>
                    )}

                    {/* Already validated */}
                    {doneValidations.length > 0 && (
                        <>
                            <h3 className="validate-section-heading">
                                Já Validadas ({doneValidations.length})
                            </h3>
                            {doneValidations.map((cls, idx) => {
                                const parts = cls.Participants ?? cls.participants ?? []
                                const allConfirmed = parts.every(p => (p.ValidationStatus ?? p.validationStatus) === 1)

                                return (
                                    <div key={cls.ClassId ?? cls.classId ?? idx} className="class-card class-card--green" style={{ opacity: 0.75 }}>
                                        <div style={{ padding: '16px' }}>
                                            <div className="class-card-title-row">
                                                <h3 className="class-card-title">
                                                    {cls.ModalityName ?? cls.modalityName ?? 'Aula'}
                                                </h3>
                                                {allConfirmed
                                                    ? <span className="status-pill status-pill--confirmed">{'\u2713'} Confirmada</span>
                                                    : <span className="status-pill status-pill--rejected">{'\u2717'} Contestada</span>
                                                }
                                            </div>
                                            <div className="class-card-info-grid">
                                                <div>
                                                    <span className="label">Data: </span>
                                                    {fmtDate(cls.StartDatetime ?? cls.startDatetime)} às {fmtTime(cls.StartDatetime ?? cls.startDatetime)}
                                                </div>
                                                {(cls.CoachName ?? cls.coachName) && (
                                                    <div>
                                                        <span className="label">Coach: </span>
                                                        {cls.CoachName ?? cls.coachName}
                                                    </div>
                                                )}
                                            </div>
                                            {parts.length > 0 && (
                                                <div style={{ marginTop: '10px', display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                                    {parts.map((p, pi) => {
                                                        const vs = p.ValidationStatus ?? p.validationStatus ?? 0
                                                        return (
                                                            <div key={pi} style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.86rem', color: '#475569' }}>
                                                                <span>{p.StudentName ?? p.studentName}</span>
                                                                <span style={{ fontWeight: 600, color: vs === 1 ? '#059669' : '#dc2626' }}>
                                                                    {vs === 1 ? '\u2713 Realizada' : '\u2717 Não realizada'}
                                                                </span>
                                                            </div>
                                                        )
                                                    })}
                                                </div>
                                            )}
                                        </div>
                                    </div>
                                )
                            })}
                        </>
                    )}
                </>
            )
        }

        return (
            <div className="validate-empty">
                <p>Área em construção.</p>
            </div>
        )
    }, [activeTab, myClasses, validateItems, pendingValidations, doneValidations, loading, error])

    return (
        <PageCard>
            <div className="admin-page-header">
                <div>
                    <h2>Aulas</h2>
                    <p>Gerir marcações, validações e aulas de grupo.</p>
                </div>
            </div>

            <div className="validate-tabs">
                {TABS.map((t) => (
                    <button
                        key={t.id}
                        type="button"
                        className={`validate-tab ${t.id === activeTab ? 'validate-tab--active' : ''}`}
                        onClick={() => setActiveTab(t.id)}
                    >
                        {t.label}
                    </button>
                ))}
            </div>

            <div className="tab-content">{tabContent}</div>
        </PageCard>
    )
}

export default ParentClassesPage
