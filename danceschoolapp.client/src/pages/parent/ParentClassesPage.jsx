// Tabs: minhas-marcacoes | marcar | grupo | validar
// Tab 1 — GET /api/ee/classes/my?from=&to=  (monthly calendar)
// Tab 2 — GET /api/modalities, GET /api/ee/coaches, GET /api/ee/classes/available-slots?from=&to=&modalityId=&coachId=
//          POST /api/coachclasses  body: { coachId, modalityId, startDatetime, endDatetime, maxParticipants, studentIds[] }
// Tab 3 — GET /api/ee/classes/open?page=&pageSize=&modalityId=
//          POST /api/participants  body: { classId, studentId }
// Tab 4 — GET /api/ee/classes/validate?page=&pageSize=
//          PATCH /api/participants/{id}/parent-validate  body: { attended: bool }
import { useEffect, useMemo, useState } from 'react'
import PageCard from '../../components/common/PageCard'
import ClassValidationCard from '../../components/common/ClassValidationCard'
import Modal from '../../components/common/Modal'
import Button from '../../components/common/Button'
import Select from '../../components/common/Select'
import {
    getMyClasses,
    getAvailableSlots,
    getOpenClasses,
    getValidateClasses,
    parentValidateParticipant,
    createClass,
    enrollInClass,
} from '../../services/classesService'
import { getModalities } from '../../services/modalitiesService'
import { getCoachesForParent } from '../../services/coachService'
import { getMyStudents } from '../../services/studentsService'
import '../../styles/AdminPage.css'
import '../../styles/ValidateClasses.css'
import '../../styles/ParentClasses.css'

// ---- Utilities ----

function isoDate(d) {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function getWeekStartDate(date) {
    const d = new Date(date)
    d.setDate(d.getDate() - d.getDay())
    d.setHours(0, 0, 0, 0)
    return d
}

function getMonthRange(date) {
    const y = date.getFullYear(), m = date.getMonth()
    return { from: isoDate(new Date(y, m, 1)), to: isoDate(new Date(y, m + 1, 0)) }
}

function getWeekRange(weekStart) {
    const end = new Date(weekStart)
    end.setDate(weekStart.getDate() + 6)
    return { from: isoDate(weekStart), to: isoDate(end) }
}

function fmtMonthLabel(date) {
    return date.toLocaleDateString('pt-PT', { month: 'long', year: 'numeric' })
}

function fmtWeekRange(weekStart) {
    const end = new Date(weekStart)
    end.setDate(weekStart.getDate() + 6)
    if (weekStart.getMonth() === end.getMonth()) {
        return `${weekStart.getDate()} – ${end.getDate()} ${weekStart.toLocaleDateString('pt-PT', { month: 'long', year: 'numeric' })}`
    }
    return `${weekStart.getDate()} ${weekStart.toLocaleDateString('pt-PT', { month: 'short' })} – ${end.getDate()} ${end.toLocaleDateString('pt-PT', { month: 'short', year: 'numeric' })}`
}

function fmtDate(iso) {
    if (!iso) return ''
    try { return new Date(iso).toLocaleDateString('pt-PT', { day: '2-digit', month: 'long', year: 'numeric' }) }
    catch { return iso }
}

function fmtDateLong(iso) {
    if (!iso) return ''
    try { return new Date(iso + 'T00:00:00').toLocaleDateString('pt-PT', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }) }
    catch { return iso }
}

function fmtTime(iso) {
    if (!iso) return ''
    try { return new Date(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }) }
    catch { return iso }
}

function normalizeItems(data) {
    if (Array.isArray(data)) return data
    if (data && Array.isArray(data.items)) return data.items
    return []
}

function studentLabel(s) {
    if (!s) return ''
    if (s.firstName) return `${s.firstName} ${s.lastName ?? s.lastNme ?? ''}`.trim()
    return s.name ?? s.Name ?? ''
}

const STATUS_LABEL = {
    0: 'Solicitada', 1: 'Aprovada', 2: 'Recusada',
    3: 'Cancelada', 4: 'Finalizada', 5: 'Validada', 6: 'Pendente', 7: 'Aprovada (Staff)',
}

function statusVariant(s) {
    if (s === 1 || s === 5) return 'confirmed'
    if (s === 2 || s === 3) return 'rejected'
    return 'pending'
}

const DAYS_PT = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb']

const TABS = [
    { id: 'minhas-marcacoes', label: 'Minhas Marcações', activeExtra: '' },
    { id: 'marcar', label: 'Criar Aula', activeExtra: '' },
    { id: 'grupo', label: 'Aulas Existentes', activeExtra: '-teal' },
    { id: 'validar', label: 'Validar Aulas', activeExtra: '-orange' },
]

const T3_PAGE_SIZE = 10

// ---- Component ----

function ParentClassesPage() {

    // ===== Shared data (modalities, coaches, students) =====
    const [modalities, setModalities] = useState([])
    const [coaches, setCoaches] = useState([])
    const [myStudents, setMyStudents] = useState([])

    useEffect(() => {
        Promise.allSettled([
            getModalities(),
            getCoachesForParent(),
            getMyStudents(),
        ]).then(([modsRes, coachesRes, studentsRes]) => {
            if (modsRes.status === 'fulfilled') setModalities(normalizeItems(modsRes.value))
            if (coachesRes.status === 'fulfilled') setCoaches(normalizeItems(coachesRes.value))
            if (studentsRes.status === 'fulfilled') setMyStudents(normalizeItems(studentsRes.value))
        })
    }, [])

    const modalityOptions = useMemo(() =>
        modalities.map(m => ({
            value: String(m.modalityId ?? m.id ?? m.ModalityId ?? ''),
            label: m.name ?? m.Name ?? '',
        })), [modalities])

    const coachOptions = useMemo(() =>
        coaches.map(c => ({
            value: String(c.coachId ?? c.id ?? c.CoachId ?? ''),
            label: c.name ?? c.Name ?? c.fullName ?? c.FullName ?? '',
        })), [coaches])

    const studentOptions = useMemo(() =>
        myStudents.map(s => ({
            value: String(s.studentId ?? s.id ?? s.StudentId ?? ''),
            label: studentLabel(s),
        })), [myStudents])

    // ===== Active tab =====
    const [activeTab, setActiveTab] = useState('minhas-marcacoes')

    // ===================================================
    // TAB 1 — Minhas Marcações (monthly calendar)
    // ===================================================
    const [t1Month, setT1Month] = useState(new Date())
    const [t1Classes, setT1Classes] = useState([])
    const [t1Loading, setT1Loading] = useState(false)
    const [t1Error, setT1Error] = useState('')
    const [t1SelectedDate, setT1SelectedDate] = useState(null)

    useEffect(() => {
        if (activeTab !== 'minhas-marcacoes') return
        let cancelled = false
        const { from, to } = getMonthRange(t1Month)
        setT1Loading(true)
        setT1Error('')
        getMyClasses({ from, to })
            .then(d => { if (!cancelled) setT1Classes(normalizeItems(d)) })
            .catch(e => { if (!cancelled) setT1Error(e.message) })
            .finally(() => { if (!cancelled) setT1Loading(false) })
        return () => { cancelled = true }
    }, [activeTab, t1Month])

    const t1ByDate = useMemo(() => {
        const map = {}
        t1Classes.forEach(c => {
            const dt = c.startDatetime ?? c.StartDatetime ?? c.date
            if (!dt) return
            const key = dt.slice(0, 10)
            ;(map[key] ||= []).push(c)
        })
        return map
    }, [t1Classes])

    // ===================================================
    // TAB 2 — Criar Aula (weekly slot calendar)
    // ===================================================
    const [t2WeekStart, setT2WeekStart] = useState(() => getWeekStartDate(new Date()))
    const [t2Modality, setT2Modality] = useState('')
    const [t2Coach, setT2Coach] = useState('')
    const [t2Slots, setT2Slots] = useState([])
    const [t2Loading, setT2Loading] = useState(false)
    const [t2Error, setT2Error] = useState('')
    const [t2SelectedDate, setT2SelectedDate] = useState(null)

    // Booking modal state
    const [bookingSlot, setBookingSlot] = useState(null)
    const [bookingModalityId, setBookingModalityId] = useState('')
    const [bookingStudentId, setBookingStudentId] = useState('')
    const [bookingMaxParts, setBookingMaxParts] = useState(1)
    const [bookingSubmitting, setBookingSubmitting] = useState(false)
    const [bookingError, setBookingError] = useState('')
    const [bookingSuccess, setBookingSuccess] = useState(false)

    useEffect(() => {
        if (activeTab !== 'marcar') return
        let cancelled = false
        const { from, to } = getWeekRange(t2WeekStart)
        setT2Loading(true)
        setT2Error('')
        const params = { from, to }
        if (t2Modality) params.modalityId = t2Modality
        if (t2Coach) params.coachId = t2Coach
        getAvailableSlots(params)
            .then(d => { if (!cancelled) setT2Slots(normalizeItems(d)) })
            .catch(e => { if (!cancelled) setT2Error(e.message) })
            .finally(() => { if (!cancelled) setT2Loading(false) })
        return () => { cancelled = true }
    }, [activeTab, t2WeekStart, t2Modality, t2Coach])

    const t2SlotsByDate = useMemo(() => {
        const map = {}
        t2Slots.forEach(s => {
            const dt = s.startDatetime ?? s.StartDatetime ?? s.start
            if (!dt) return
            const key = dt.slice(0, 10)
            ;(map[key] ||= []).push(s)
        })
        return map
    }, [t2Slots])

    const openBookingModal = (slot) => {
        setBookingSlot(slot)
        setBookingModalityId(t2Modality || (slot.modalityId ? String(slot.modalityId) : ''))
        setBookingStudentId('')
        setBookingMaxParts(1)
        setBookingError('')
        setBookingSuccess(false)
    }

    const handleBookingSubmit = async (e) => {
        e.preventDefault()
        if (!bookingStudentId) { setBookingError('Selecione um aluno.'); return }
        if (!bookingModalityId) { setBookingError('Selecione uma modalidade.'); return }
        setBookingSubmitting(true)
        setBookingError('')
        try {
            await createClass({
                coachId: bookingSlot.coachId ?? bookingSlot.CoachId ?? (t2Coach ? Number(t2Coach) : undefined),
                modalityId: Number(bookingModalityId),
                startDatetime: bookingSlot.startDatetime ?? bookingSlot.StartDatetime,
                endDatetime: bookingSlot.endDatetime ?? bookingSlot.EndDatetime,
                maxParticipants: Number(bookingMaxParts),
                studentIds: [Number(bookingStudentId)],
            })
            setBookingSuccess(true)
            // Refresh slots after a short delay so user sees the success state
            setTimeout(() => {
                setBookingSlot(null)
                const { from, to } = getWeekRange(t2WeekStart)
                const params = { from, to }
                if (t2Modality) params.modalityId = t2Modality
                if (t2Coach) params.coachId = t2Coach
                getAvailableSlots(params).then(d => setT2Slots(normalizeItems(d))).catch(() => {})
            }, 1200)
        } catch (err) {
            setBookingError(err.message)
        } finally {
            setBookingSubmitting(false)
        }
    }

    // ===================================================
    // TAB 3 — Aulas Existentes (paginated open classes)
    // ===================================================
    const [t3Modality, setT3Modality] = useState('')
    const [t3Classes, setT3Classes] = useState([])
    const [t3Total, setT3Total] = useState(0)
    const [t3Page, setT3Page] = useState(1)
    const [t3Loading, setT3Loading] = useState(false)
    const [t3Error, setT3Error] = useState('')
    const [t3Refresh, setT3Refresh] = useState(0)

    // Enroll modal state
    const [enrollTarget, setEnrollTarget] = useState(null)
    const [enrollStudentId, setEnrollStudentId] = useState('')
    const [enrollSubmitting, setEnrollSubmitting] = useState(false)
    const [enrollError, setEnrollError] = useState('')

    useEffect(() => {
        if (activeTab !== 'grupo') return
        let cancelled = false
        setT3Loading(true)
        setT3Error('')
        const params = { page: t3Page, pageSize: T3_PAGE_SIZE }
        if (t3Modality) params.modalityId = t3Modality
        getOpenClasses(params)
            .then(d => {
                if (!cancelled) {
                    setT3Classes(normalizeItems(d))
                    setT3Total(d?.totalCount ?? d?.TotalCount ?? d?.total ?? 0)
                }
            })
            .catch(e => { if (!cancelled) setT3Error(e.message) })
            .finally(() => { if (!cancelled) setT3Loading(false) })
        return () => { cancelled = true }
    }, [activeTab, t3Modality, t3Page, t3Refresh])

    const handleT3ModalityChange = (v) => {
        setT3Modality(v)
        setT3Page(1)
    }

    const handleEnrollSubmit = async (e) => {
        e.preventDefault()
        if (!enrollStudentId) { setEnrollError('Selecione um aluno.'); return }
        setEnrollSubmitting(true)
        setEnrollError('')
        try {
            await enrollInClass({
                classId: enrollTarget.classId ?? enrollTarget.ClassId ?? enrollTarget.id,
                studentId: Number(enrollStudentId),
            })
            setEnrollTarget(null)
            setT3Refresh(r => r + 1)
        } catch (err) {
            setEnrollError(err.message)
        } finally {
            setEnrollSubmitting(false)
        }
    }

    // ===================================================
    // TAB 4 — Validar Aulas
    // ===================================================
    const [t4Items, setT4Items] = useState([])
    const [t4Loading, setT4Loading] = useState(false)
    const [t4Error, setT4Error] = useState('')

    useEffect(() => {
        if (activeTab !== 'validar') return
        let cancelled = false
        setT4Loading(true)
        setT4Error('')
        getValidateClasses({ page: 1, pageSize: 20 })
            .then(d => { if (!cancelled) setT4Items(normalizeItems(d)) })
            .catch(e => { if (!cancelled) setT4Error(e.message) })
            .finally(() => { if (!cancelled) setT4Loading(false) })
        return () => { cancelled = true }
    }, [activeTab])

    const handleValidate = async (participantId, attended) => {
        try {
            await parentValidateParticipant(participantId, attended)
            setT4Items(prev => prev.map(cls => {
                const parts = cls.Participants ?? cls.participants ?? []
                if (!parts.some(p => (p.ParticipantId ?? p.participantId) === participantId)) return cls
                const updateParts = list => list?.map(p => {
                    if ((p.ParticipantId ?? p.participantId) === participantId)
                        return { ...p, ValidationStatus: attended ? 1 : 2, validationStatus: attended ? 1 : 2 }
                    return p
                })
                return { ...cls, Participants: updateParts(cls.Participants), participants: updateParts(cls.participants) }
            }))
        } catch (err) {
            alert(err.message)
        }
    }

    const t4Pending = useMemo(() =>
        t4Items.filter(cls =>
            (cls.Participants ?? cls.participants ?? []).some(p => (p.ValidationStatus ?? p.validationStatus ?? 0) === 0)
        ), [t4Items])

    const t4Done = useMemo(() =>
        t4Items.filter(cls => {
            const parts = cls.Participants ?? cls.participants ?? []
            return parts.length > 0 && parts.every(p => (p.ValidationStatus ?? p.validationStatus ?? 0) !== 0)
        }), [t4Items])

    // ===================================================
    // RENDER HELPERS
    // ===================================================

    const renderMinhasMarcacoes = () => {
        const year = t1Month.getFullYear()
        const month = t1Month.getMonth()
        const daysInMonth = new Date(year, month + 1, 0).getDate()
        const firstDow = new Date(year, month, 1).getDay()
        const dayList = t1SelectedDate ? (t1ByDate[t1SelectedDate] ?? []) : []

        return (
            <div>
                <p className="tab-description">Visualize as aulas marcadas para os seus educandos no mês.</p>
                {t1Error && <p className="admin-error">{t1Error}</p>}

                <div className="pc-card">
                    <div className="pc-cal-header">
                        <h3 className="pc-cal-title">{fmtMonthLabel(t1Month)}</h3>
                        <div className="pc-cal-nav">
                            <button
                                className="pc-cal-nav-btn"
                                onClick={() => { setT1Month(new Date(year, month - 1, 1)); setT1SelectedDate(null) }}
                                aria-label="Mês anterior"
                            >‹</button>
                            <button
                                className="pc-cal-nav-btn"
                                onClick={() => { setT1Month(new Date(year, month + 1, 1)); setT1SelectedDate(null) }}
                                aria-label="Próximo mês"
                            >›</button>
                        </div>
                    </div>

                    {t1Loading ? (
                        <div className="validate-empty"><p>Carregando...</p></div>
                    ) : (
                        <div className="pc-month-grid">
                            {DAYS_PT.map(d => <div key={d} className="pc-dow-label">{d}</div>)}
                            {Array.from({ length: firstDow }).map((_, i) => (
                                <div key={`e${i}`} className="pc-day-cell pc-day-cell--empty" />
                            ))}
                            {Array.from({ length: daysInMonth }).map((_, i) => {
                                const day = i + 1
                                const key = isoDate(new Date(year, month, day))
                                const items = t1ByDate[key] ?? []
                                const isSelected = key === t1SelectedDate
                                return (
                                    <div
                                        key={key}
                                        className={`pc-day-cell${items.length ? ' pc-day-cell--has' : ''}${isSelected ? ' pc-day-cell--selected' : ''}`}
                                        onClick={() => setT1SelectedDate(isSelected ? null : key)}
                                    >
                                        <span className="pc-day-num">{day}</span>
                                        {items.slice(0, 3).map((c, ci) => {
                                            const start = c.startDatetime ?? c.StartDatetime
                                            return (
                                                <div key={ci} className="pc-event-chip" title={`${fmtTime(start)} – ${c.modalityName ?? c.ModalityName ?? ''}`}>
                                                    {fmtTime(start)} {c.modalityName ?? c.ModalityName ?? ''}
                                                </div>
                                            )
                                        })}
                                        {items.length > 3 && <div className="pc-event-more">+{items.length - 3}</div>}
                                    </div>
                                )
                            })}
                        </div>
                    )}
                </div>

                {/* Selected day detail */}
                {t1SelectedDate && dayList.length > 0 && (
                    <div>
                        <h3 className="validate-section-heading">{fmtDateLong(t1SelectedDate)}</h3>
                        {dayList
                            .sort((a, b) => (a.startDatetime ?? a.StartDatetime ?? '').localeCompare(b.startDatetime ?? b.StartDatetime ?? ''))
                            .map((c, i) => {
                                const status = c.status ?? c.Status ?? 0
                                const start = c.startDatetime ?? c.StartDatetime
                                const end = c.endDatetime ?? c.EndDatetime
                                return (
                                    <div key={c.classId ?? c.ClassId ?? i} className="session-card">
                                        <div className="session-card-top">
                                            <h3 className="session-card-name">{c.modalityName ?? c.ModalityName ?? 'Aula'}</h3>
                                            <span className={`status-pill status-pill--${statusVariant(status)}`}>
                                                {STATUS_LABEL[status] ?? `Estado ${status}`}
                                            </span>
                                        </div>
                                        <div className="session-card-info">
                                            <span>🕐 {fmtTime(start)}{end ? ` – ${fmtTime(end)}` : ''}</span>
                                            {(c.coachName ?? c.CoachName) && <span>👨‍🏫 {c.coachName ?? c.CoachName}</span>}
                                            {(c.studioName ?? c.StudioName) && <span>📍 {c.studioName ?? c.StudioName}</span>}
                                            {(c.studentName ?? c.StudentName) && <span>👤 {c.studentName ?? c.StudentName}</span>}
                                        </div>
                                    </div>
                                )
                            })}
                    </div>
                )}

                {t1SelectedDate && dayList.length === 0 && !t1Loading && (
                    <div className="validate-empty">
                        <div className="validate-empty-icon">📅</div>
                        <h3>Sem aulas</h3>
                        <p>Nenhuma aula marcada para este dia.</p>
                    </div>
                )}

                {!t1Loading && !t1Error && t1Classes.length === 0 && !t1SelectedDate && (
                    <div className="validate-empty">
                        <div className="validate-empty-icon">📅</div>
                        <h3>Sem marcações</h3>
                        <p>Não tem aulas marcadas este mês.</p>
                    </div>
                )}
            </div>
        )
    }

    const renderCriarAula = () => {
        const weekDays = Array.from({ length: 7 }, (_, i) => {
            const d = new Date(t2WeekStart)
            d.setDate(t2WeekStart.getDate() + i)
            return d
        })
        const selectedSlots = t2SelectedDate ? (t2SlotsByDate[t2SelectedDate] ?? []) : []

        return (
            <div>
                <p className="tab-description">
                    Escolha uma modalidade e professor, selecione um horário disponível e envie o pedido de aula.
                </p>
                {t2Error && <p className="admin-error">{t2Error}</p>}

                {/* Filters */}
                <div className="pc-filter-bar">
                    <div className="pc-filter-group">
                        <label className="pc-filter-label">Modalidade</label>
                        <Select
                            value={t2Modality}
                            onChange={v => { setT2Modality(v); setT2SelectedDate(null) }}
                            options={[{ value: '', label: 'Todas' }, ...modalityOptions]}
                        />
                    </div>
                    <div className="pc-filter-group">
                        <label className="pc-filter-label">Professor</label>
                        <Select
                            value={t2Coach}
                            onChange={v => { setT2Coach(v); setT2SelectedDate(null) }}
                            options={[{ value: '', label: 'Qualquer' }, ...coachOptions]}
                        />
                    </div>
                </div>

                {/* Weekly slot calendar */}
                <div className="pc-card">
                    <div className="pc-cal-header">
                        <h3 className="pc-cal-title">{fmtWeekRange(t2WeekStart)}</h3>
                        <div className="pc-cal-nav">
                            <button
                                className="pc-cal-nav-btn"
                                onClick={() => { const d = new Date(t2WeekStart); d.setDate(d.getDate() - 7); setT2WeekStart(d); setT2SelectedDate(null) }}
                                aria-label="Semana anterior"
                            >‹</button>
                            <button
                                className="pc-cal-nav-btn"
                                onClick={() => { const d = new Date(t2WeekStart); d.setDate(d.getDate() + 7); setT2WeekStart(d); setT2SelectedDate(null) }}
                                aria-label="Próxima semana"
                            >›</button>
                        </div>
                    </div>

                    {t2Loading ? (
                        <div className="validate-empty"><p>Carregando vagas...</p></div>
                    ) : (
                        <div className="pc-week-grid">
                            {DAYS_PT.map(d => <div key={d} className="pc-dow-label">{d}</div>)}
                            {weekDays.map(date => {
                                const key = isoDate(date)
                                const daySlots = t2SlotsByDate[key] ?? []
                                const isSelected = key === t2SelectedDate
                                return (
                                    <div
                                        key={key}
                                        className={`pc-week-cell${daySlots.length ? ' pc-week-cell--has' : ' pc-week-cell--empty'}${isSelected ? ' pc-week-cell--selected' : ''}`}
                                        onClick={() => daySlots.length && setT2SelectedDate(isSelected ? null : key)}
                                        title={daySlots.length ? `${daySlots.length} vaga(s)` : 'Sem vagas'}
                                    >
                                        <span className="pc-day-num">{date.getDate()}</span>
                                        {daySlots.slice(0, 3).map((s, si) => (
                                            <div key={si} className="pc-slot-chip">
                                                {fmtTime(s.startDatetime ?? s.StartDatetime ?? s.start)}
                                            </div>
                                        ))}
                                        {daySlots.length > 3 && <div className="pc-event-more">+{daySlots.length - 3}</div>}
                                    </div>
                                )
                            })}
                        </div>
                    )}
                </div>

                {/* Selected day slot list */}
                {t2SelectedDate && selectedSlots.length > 0 && (
                    <div>
                        <h3 className="validate-section-heading">Vagas para {fmtDateLong(t2SelectedDate)}</h3>
                        {selectedSlots
                            .sort((a, b) => (a.startDatetime ?? '').localeCompare(b.startDatetime ?? ''))
                            .map((slot, i) => {
                                const start = slot.startDatetime ?? slot.StartDatetime
                                const end = slot.endDatetime ?? slot.EndDatetime
                                return (
                                    <div key={i} className="pc-slot-card">
                                        <div className="pc-slot-info">
                                            <div className="pc-slot-time">
                                                {fmtTime(start)}{end ? ` – ${fmtTime(end)}` : ''}
                                            </div>
                                            <div className="pc-slot-detail">
                                                {(slot.coachName ?? slot.CoachName) && <span>👨‍🏫 {slot.coachName ?? slot.CoachName}</span>}
                                                {(slot.studioName ?? slot.StudioName) && <span>📍 {slot.studioName ?? slot.StudioName}</span>}
                                                {(slot.modalityName ?? slot.ModalityName) && <span>💃 {slot.modalityName ?? slot.ModalityName}</span>}
                                            </div>
                                        </div>
                                        <Button variant="primary" onClick={() => openBookingModal(slot)}>
                                            Pedir Aula
                                        </Button>
                                    </div>
                                )
                            })}
                    </div>
                )}

                {t2SelectedDate && selectedSlots.length === 0 && !t2Loading && (
                    <div className="validate-empty">
                        <div className="validate-empty-icon">📅</div>
                        <h3>Sem vagas</h3>
                        <p>Nenhuma vaga disponível para este dia com os filtros selecionados.</p>
                    </div>
                )}

                {!t2Loading && !t2SelectedDate && t2Slots.length === 0 && (
                    <div className="validate-empty">
                        <div className="validate-empty-icon">🔍</div>
                        <h3>Sem vagas disponíveis</h3>
                        <p>Não há vagas nesta semana. Tente outra semana ou altere os filtros.</p>
                    </div>
                )}
            </div>
        )
    }

    const renderAulasExistentes = () => {
        const totalPages = Math.max(1, Math.ceil(t3Total / T3_PAGE_SIZE))
        return (
            <div>
                <p className="tab-description">Aulas abertas a inscrições — inscreva o seu educando diretamente.</p>
                {t3Error && <p className="admin-error">{t3Error}</p>}

                {/* Modality filter */}
                <div className="pc-filter-bar">
                    <div className="pc-filter-group">
                        <label className="pc-filter-label">Filtrar por modalidade</label>
                        <Select
                            value={t3Modality}
                            onChange={handleT3ModalityChange}
                            options={[{ value: '', label: 'Todas' }, ...modalityOptions]}
                        />
                    </div>
                </div>

                {t3Loading ? (
                    <div className="validate-empty"><p>Carregando...</p></div>
                ) : t3Classes.length === 0 ? (
                    <div className="validate-empty">
                        <div className="validate-empty-icon">🎭</div>
                        <h3>Sem aulas disponíveis</h3>
                        <p>Não há aulas abertas a inscrições neste momento.</p>
                    </div>
                ) : (
                    <>
                        {t3Classes.map((c, i) => {
                            const id = c.classId ?? c.ClassId ?? c.id ?? i
                            const enrolled = c.totalParticipants ?? c.TotalParticipants ?? (c.participants?.length ?? c.Participants?.length ?? 0)
                            const maxParts = c.maxParticipants ?? c.MaxParticipants ?? 0
                            const isFull = maxParts > 0 && enrolled >= maxParts
                            const start = c.startDatetime ?? c.StartDatetime
                            const end = c.endDatetime ?? c.EndDatetime
                            return (
                                <div key={id} className="class-card class-card--teal">
                                    <div className="class-card-header" style={{ cursor: 'default' }}>
                                        <div style={{ flex: '1 1 auto', minWidth: 0 }}>
                                            <div className="class-card-title-row">
                                                <h3 className="class-card-title">{c.modalityName ?? c.ModalityName ?? 'Aula'}</h3>
                                                <span className="class-card-capacity class-card-capacity--teal">
                                                    Alunos {enrolled}/{maxParts || '?'}
                                                </span>
                                                {isFull && <span className="status-pill status-pill--rejected">Lotado</span>}
                                            </div>
                                            <div className="class-card-info-grid">
                                                <div>
                                                    <span className="label">Data: </span>
                                                    {fmtDate(start)} · {fmtTime(start)}{end ? ` – ${fmtTime(end)}` : ''}
                                                </div>
                                                {(c.studioName ?? c.StudioName) && (
                                                    <div><span className="label">Estúdio: </span>{c.studioName ?? c.StudioName}</div>
                                                )}
                                                {(c.coachName ?? c.CoachName) && (
                                                    <div><span className="label">Coach: </span>{c.coachName ?? c.CoachName}</div>
                                                )}
                                            </div>
                                        </div>
                                        <Button
                                            variant={isFull ? 'secondary' : 'primary'}
                                            disabled={isFull}
                                            onClick={() => {
                                                if (!isFull) {
                                                    setEnrollTarget(c)
                                                    setEnrollStudentId('')
                                                    setEnrollError('')
                                                }
                                            }}
                                        >
                                            {isFull ? 'Lotado' : 'Inscrever'}
                                        </Button>
                                    </div>
                                </div>
                            )
                        })}

                        {t3Total > T3_PAGE_SIZE && (
                            <div className="pagination-row">
                                <button
                                    className="pc-cal-nav-btn"
                                    disabled={t3Page <= 1}
                                    onClick={() => setT3Page(p => p - 1)}
                                    aria-label="Página anterior"
                                >‹</button>
                                <span className="pagination-label">Página {t3Page} de {totalPages}</span>
                                <button
                                    className="pc-cal-nav-btn"
                                    disabled={t3Page >= totalPages}
                                    onClick={() => setT3Page(p => p + 1)}
                                    aria-label="Próxima página"
                                >›</button>
                            </div>
                        )}
                    </>
                )}
            </div>
        )
    }

    const renderValidarAulas = () => {
        if (t4Loading) return <div className="validate-empty"><p>Carregando...</p></div>
        if (t4Error) return <p className="admin-error">{t4Error}</p>
        if (!t4Items.length) return (
            <div className="validate-empty">
                <div className="validate-empty-icon">✓</div>
                <h3>Tudo validado</h3>
                <p>Não há aulas para validar neste momento.</p>
            </div>
        )
        return (
            <>
                {t4Pending.length > 0 && (
                    <div className="validate-warning">
                        <span className="validate-warning-icon">!</span>
                        <p>
                            <strong>Atenção:</strong> Tem {t4Pending.length} aula{t4Pending.length > 1 ? 's' : ''} aguardando
                            validação. Confirme dentro do prazo de 48 horas.
                        </p>
                    </div>
                )}

                {t4Pending.length > 0 && (
                    <>
                        <h3 className="validate-section-heading">Aguardam Validação ({t4Pending.length})</h3>
                        {t4Pending.map((cls, i) => (
                            <ClassValidationCard
                                key={cls.ClassId ?? cls.classId ?? i}
                                aula={cls}
                                tipo="professor"
                                variant="amber"
                                showParticipants
                                onConfirm={pId => handleValidate(pId, true)}
                                onReject={pId => handleValidate(pId, false)}
                            />
                        ))}
                    </>
                )}

                {t4Done.length > 0 && (
                    <>
                        <h3 className="validate-section-heading">Já Validadas ({t4Done.length})</h3>
                        {t4Done.map((cls, i) => {
                            const parts = cls.Participants ?? cls.participants ?? []
                            const allConfirmed = parts.every(p => (p.ValidationStatus ?? p.validationStatus) === 1)
                            return (
                                <div
                                    key={cls.ClassId ?? cls.classId ?? i}
                                    className={`class-card ${allConfirmed ? 'class-card--green' : 'class-card--amber'}`}
                                    style={{ opacity: 0.75 }}
                                >
                                    <div className="class-card-header" style={{ cursor: 'default' }}>
                                        <div style={{ flex: '1 1 auto', minWidth: 0 }}>
                                            <div className="class-card-title-row">
                                                <h3 className="class-card-title">{cls.ModalityName ?? cls.modalityName ?? 'Aula'}</h3>
                                                {allConfirmed
                                                    ? <span className="status-pill status-pill--confirmed">✓ Confirmada</span>
                                                    : <span className="status-pill status-pill--rejected">✗ Contestada</span>
                                                }
                                            </div>
                                            <div className="class-card-info-grid">
                                                <div>
                                                    <span className="label">Data: </span>
                                                    {fmtDate(cls.StartDatetime ?? cls.startDatetime)} · {fmtTime(cls.StartDatetime ?? cls.startDatetime)}
                                                </div>
                                                {(cls.CoachName ?? cls.coachName) && (
                                                    <div><span className="label">Coach: </span>{cls.CoachName ?? cls.coachName}</div>
                                                )}
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            )
                        })}
                    </>
                )}
            </>
        )
    }

    // ===================================================
    // MAIN RENDER
    // ===================================================

    return (
        <PageCard>
            <div className="admin-page-header">
                <div>
                    <h2>Aulas</h2>
                    <p>Gerir marcações, validações e aulas de grupo.</p>
                </div>
            </div>

            <div className="validate-tabs">
                {TABS.map(t => (
                    <button
                        key={t.id}
                        type="button"
                        className={`validate-tab${activeTab === t.id ? ` validate-tab--active${t.activeExtra}` : ''}`}
                        onClick={() => setActiveTab(t.id)}
                    >
                        {t.label}
                    </button>
                ))}
            </div>

            <div className="tab-content">
                {activeTab === 'minhas-marcacoes' && renderMinhasMarcacoes()}
                {activeTab === 'marcar' && renderCriarAula()}
                {activeTab === 'grupo' && renderAulasExistentes()}
                {activeTab === 'validar' && renderValidarAulas()}
            </div>

            {/* ====== Booking Modal (Tab 2 — Pedir Aula) ====== */}
            <Modal
                open={bookingSlot !== null}
                title="Pedir Aula"
                onClose={() => setBookingSlot(null)}
            >
                {bookingSuccess ? (
                    <div className="validate-empty" style={{ padding: '24px' }}>
                        <div className="validate-empty-icon">✓</div>
                        <h3>Pedido enviado!</h3>
                        <p>O seu pedido de aula foi submetido e aguarda aprovação.</p>
                    </div>
                ) : (
                    <form onSubmit={handleBookingSubmit} className="modal-form">
                        {bookingSlot && (
                            <div className="reject-class-summary">
                                <p>📅 {fmtDate(bookingSlot.startDatetime ?? bookingSlot.StartDatetime)} · {fmtTime(bookingSlot.startDatetime ?? bookingSlot.StartDatetime)}{(bookingSlot.endDatetime ?? bookingSlot.EndDatetime) ? ` – ${fmtTime(bookingSlot.endDatetime ?? bookingSlot.EndDatetime)}` : ''}</p>
                                {(bookingSlot.coachName ?? bookingSlot.CoachName) && <p>👨‍🏫 {bookingSlot.coachName ?? bookingSlot.CoachName}</p>}
                                {(bookingSlot.studioName ?? bookingSlot.StudioName) && <p>📍 {bookingSlot.studioName ?? bookingSlot.StudioName}</p>}
                            </div>
                        )}

                        <div className="modal-field">
                            <label className="modal-label">Modalidade *</label>
                            <Select
                                value={bookingModalityId}
                                onChange={setBookingModalityId}
                                placeholder="Selecione a modalidade"
                                options={modalityOptions}
                            />
                        </div>

                        <div className="modal-field">
                            <label className="modal-label">Aluno *</label>
                            <Select
                                value={bookingStudentId}
                                onChange={setBookingStudentId}
                                placeholder="Selecione o aluno"
                                options={studentOptions}
                            />
                        </div>

                        <div className="modal-field">
                            <label className="modal-label">Número máximo de alunos (1–8)</label>
                            <input
                                type="number"
                                className="input"
                                min={1}
                                max={8}
                                value={bookingMaxParts}
                                onChange={e => setBookingMaxParts(Number(e.target.value))}
                            />
                        </div>

                        {bookingError && <p className="admin-error">{bookingError}</p>}

                        <div className="modal-actions">
                            <Button type="button" variant="secondary" onClick={() => setBookingSlot(null)}>
                                Cancelar
                            </Button>
                            <Button type="submit" variant="primary" disabled={bookingSubmitting}>
                                {bookingSubmitting ? 'A enviar...' : 'Confirmar Pedido'}
                            </Button>
                        </div>
                    </form>
                )}
            </Modal>

            {/* ====== Enroll Modal (Tab 3 — Inscrever) ====== */}
            <Modal
                open={enrollTarget !== null}
                title="Inscrever Educando"
                onClose={() => setEnrollTarget(null)}
            >
                <form onSubmit={handleEnrollSubmit} className="modal-form">
                    {enrollTarget && (
                        <div className="reject-class-summary">
                            <p>💃 {enrollTarget.modalityName ?? enrollTarget.ModalityName ?? 'Aula'}</p>
                            <p>📅 {fmtDate(enrollTarget.startDatetime ?? enrollTarget.StartDatetime)} · {fmtTime(enrollTarget.startDatetime ?? enrollTarget.StartDatetime)}</p>
                            {(enrollTarget.coachName ?? enrollTarget.CoachName) && (
                                <p>👨‍🏫 {enrollTarget.coachName ?? enrollTarget.CoachName}</p>
                            )}
                            <p>Vagas: {enrollTarget.totalParticipants ?? enrollTarget.TotalParticipants ?? 0}/{enrollTarget.maxParticipants ?? enrollTarget.MaxParticipants ?? '?'}</p>
                        </div>
                    )}

                    <div className="modal-field">
                        <label className="modal-label">Selecionar Aluno *</label>
                        <Select
                            value={enrollStudentId}
                            onChange={setEnrollStudentId}
                            placeholder="Selecione o aluno"
                            options={studentOptions}
                        />
                    </div>

                    {enrollError && <p className="admin-error">{enrollError}</p>}

                    <div className="modal-actions">
                        <Button type="button" variant="secondary" onClick={() => setEnrollTarget(null)}>
                            Cancelar
                        </Button>
                        <Button type="submit" variant="primary" disabled={enrollSubmitting}>
                            {enrollSubmitting ? 'A inscrever...' : 'Confirmar Inscrição'}
                        </Button>
                    </div>
                </form>
            </Modal>
        </PageCard>
    )
}

export default ParentClassesPage
