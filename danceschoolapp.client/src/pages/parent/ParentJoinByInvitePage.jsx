import { useEffect, useState } from 'react'
import { useSearchParams, useNavigate } from 'react-router-dom'
import { getClassById, enrollByInvite } from '@/services/classesService'
import { getMyStudents } from '@/services/studentsService'
import Button from '@/components/common/Button'
import Select from '@/components/common/Select'
import '@/styles/AdminPage.css'

function fmtDate(iso) {
    if (!iso) return ''
    try {
        return new Date(iso).toLocaleDateString('pt-PT', {
            weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
        })
    } catch { return iso }
}

function fmtTime(iso) {
    if (!iso) return ''
    try { return new Date(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }) }
    catch { return iso }
}

const STATUS_LABEL = {
    0: 'Solicitada', 1: 'Aprovada', 2: 'Recusada',
    3: 'Cancelada', 4: 'Finalizada', 5: 'Validada', 6: 'Pendente', 7: 'Coach Aprovada',
}

function ParentJoinByInvitePage() {
    const [searchParams] = useSearchParams()
    const navigate = useNavigate()
    const classId = searchParams.get('classId')

    const [cls, setCls]                 = useState(null)
    const [loadingClass, setLoadingClass] = useState(false)
    const [classError, setClassError]   = useState('')

    const [students, setStudents]       = useState([])
    const [studentId, setStudentId]     = useState('')

    const [submitting, setSubmitting]   = useState(false)
    const [submitError, setSubmitError] = useState('')
    const [success, setSuccess]         = useState(false)

    useEffect(() => {
        if (!classId) return
        setLoadingClass(true)
        setClassError('')
        getClassById(Number(classId))
            .then(data => setCls(data))
            .catch(e => setClassError(e.message || 'Não foi possível carregar a aula.'))
            .finally(() => setLoadingClass(false))

        getMyStudents()
            .then(data => {
                const list = Array.isArray(data) ? data : (data?.items ?? data?.Items ?? [])
                setStudents(list)
            })
            .catch(() => {})
    }, [classId])

    const studentOptions = students.map(s => {
        const fn = s.FirstName ?? s.firstName ?? ''
        const ln = s.LastName  ?? s.lastName  ?? ''
        const name = (fn + ' ' + ln).trim() || (s.name ?? '')
        return { value: String(s.StudentId ?? s.studentId), label: name }
    })

    const handleSubmit = async (e) => {
        e.preventDefault()
        if (!studentId) { setSubmitError('Selecione um estudante.'); return }
        setSubmitting(true)
        setSubmitError('')
        try {
            await enrollByInvite({ classId: Number(classId), studentId: Number(studentId) })
            setSuccess(true)
        } catch (err) {
            setSubmitError(err.message || 'Erro ao inscrever estudante.')
        } finally {
            setSubmitting(false)
        }
    }

    if (!classId) {
        return (
            <section className="dashboard-page-card">
                <p style={{ color: 'var(--danger)' }}>Nenhuma aula especificada no link.</p>
            </section>
        )
    }

    return (
        <section className="dashboard-page-card">
            <div className="admin-page-header">
                <div>
                    <h2>Entrar numa Aula</h2>
                    <p>Inscreva um estudante na aula para a qual foi convidado.</p>
                </div>
                <Button variant="secondary" onClick={() => navigate('/parent/aulas')}>
                    Voltar às Aulas
                </Button>
            </div>

            {loadingClass && <p style={{ color: 'var(--text-2)' }}>A carregar aula...</p>}
            {classError  && <p style={{ color: 'var(--danger)' }}>{classError}</p>}

            {cls && !loadingClass && (
                <div style={{
                    background: 'var(--surface-2)',
                    borderRadius: '12px',
                    padding: '20px 24px',
                    marginBottom: '24px',
                    display: 'grid',
                    gap: '10px',
                }}>
                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center', flexWrap: 'wrap' }}>
                        <span style={{
                            padding: '4px 10px',
                            borderRadius: '6px',
                            fontSize: '0.8rem',
                            fontWeight: 600,
                            background: 'var(--accent-soft)',
                            color: 'var(--accent)',
                        }}>
                            {STATUS_LABEL[cls.status ?? cls.Status] ?? 'Desconhecido'}
                        </span>
                        <span style={{ color: 'var(--text-2)', fontSize: '0.875rem' }}>
                            {cls.modalityName ?? cls.ModalityName}
                        </span>
                    </div>

                    <div style={{ fontSize: '1rem', fontWeight: 600 }}>
                        {fmtDate(cls.startDatetime ?? cls.StartDatetime)}
                    </div>
                    <div style={{ color: 'var(--text-2)', fontSize: '0.875rem' }}>
                        {fmtTime(cls.startDatetime ?? cls.StartDatetime)} –{' '}
                        {fmtTime(cls.endDatetime   ?? cls.EndDatetime)}
                    </div>

                    <div style={{ display: 'flex', gap: '24px', flexWrap: 'wrap', marginTop: '4px' }}>
                        <span style={{ fontSize: '0.875rem' }}>
                            <strong>Coach:</strong> {cls.coachName ?? cls.CoachName}
                        </span>
                        <span style={{ fontSize: '0.875rem' }}>
                            <strong>Estúdio:</strong> {cls.studioName ?? cls.StudioName}
                        </span>
                        <span style={{ fontSize: '0.875rem' }}>
                            <strong>Vagas:</strong>{' '}
                            {(cls.maxParticipants ?? cls.MaxParticipants) - (cls.currentParticipants ?? cls.CurrentParticipants ?? 0)}{' '}
                            / {cls.maxParticipants ?? cls.MaxParticipants}
                        </span>
                    </div>
                </div>
            )}

            {cls && !loadingClass && !success && (
                <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '16px', maxWidth: '400px' }}>
                    <div>
                        <label htmlFor="inviteStudent" style={{ display: 'block', marginBottom: '6px', fontWeight: 500 }}>
                            Estudante *
                        </label>
                        {studentOptions.length === 0 ? (
                            <p style={{ color: 'var(--text-2)', fontSize: '0.875rem' }}>
                                Não tem estudantes disponíveis para inscrever.
                            </p>
                        ) : (
                            <Select
                                id="inviteStudent"
                                value={studentId}
                                onChange={setStudentId}
                                options={[{ value: '', label: 'Selecione um estudante' }, ...studentOptions]}
                            />
                        )}
                    </div>

                    {submitError && (
                        <p style={{ color: 'var(--danger)', fontSize: '0.875rem', margin: 0 }}>{submitError}</p>
                    )}

                    <div style={{ display: 'flex', gap: '12px' }}>
                        <Button
                            type="submit"
                            variant="primary"
                            disabled={submitting || studentOptions.length === 0}
                        >
                            {submitting ? 'A inscrever...' : 'Inscrever'}
                        </Button>
                    </div>
                </form>
            )}

            {success && (
                <div style={{
                    background: 'var(--success-bg)',
                    color: 'var(--success)',
                    borderRadius: '10px',
                    padding: '16px 20px',
                    fontWeight: 500,
                }}>
                    Inscrição realizada com sucesso! O estudante foi inscrito na aula.{' '}
                    <button
                        style={{ background: 'none', border: 'none', color: 'var(--accent)', cursor: 'pointer', textDecoration: 'underline' }}
                        onClick={() => navigate('/parent/aulas')}
                    >
                        Ver as minhas aulas →
                    </button>
                </div>
            )}
        </section>
    )
}

export default ParentJoinByInvitePage
