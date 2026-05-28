// Tabs: minhas-marcacoes | marcar | grupo | inscricoes | validar
// Tab 1 — GET /api/coachclasses/parent/{userId}  (all classes for parent's students)
// Tab 2 — GET /api/ee/classes/available-slots?from=&to=&modalityId=&coachId=
//          Response: DaySlotResponse[] → [{ Date:"YYYY-MM-DD", Slots:[{ CoachId, CoachName, StartTime, EndTime, ModalityIds, ModalityNames }] }]
//          POST /api/coachclasses body: { coachId, modalityId, startDatetime, endDatetime, studentId }  (individual, no maxParticipants)
// Tab 3 — GET /api/ee/classes/open?page=1&pageSize=50
//          Response: PagedResult<OpenClassItem> → { Items:[...], TotalCount }
//          POST /api/participants body: { classId, studentId }
// Tab 4 — Coach-created enrollment approvals
//          GET /api/coachclasses/{id} for each CoachCreated+Requested class → ClassParticipantSummary[]
//          PATCH /api/participants/{id}/parent-approve-enrollment body: { approve: bool }
// Tab 5 — GET /api/ee/classes/validate  (PagedResult<ParentValidateItem>)
//          PATCH /api/participants/{id}/parent-validate body: { attended: bool }
import { useEffect, useMemo, useState } from 'react'
import PageCard from '../../components/common/PageCard'
import ClassValidationCard from '../../components/common/ClassValidationCard'
import Modal from '../../components/common/Modal'
import Button from '../../components/common/Button'
import Select from '../../components/common/Select'
import MonthCalendar, { isoDate, getMonthRange, fmtDateLong } from '../../components/common/MonthCalendar'
import {
    getClassesByParent,
    getAvailableSlots,
    getOpenClasses,
    getValidateClasses,
    parentValidateParticipant,
    parentCreateClass,
    enrollInClass,
    enrollByInvite,
    getJoinClassStatus,
    getClassById,
    approveEnrollment,
} from '../../services/classesService'
import { getModalities } from '../../services/modalitiesService'
import { getCoachesForParent } from '../../services/coachService'
import { getMyStudents } from '../../services/studentsService'
import { useAuth } from '../../context/useAuth'
import '../../styles/AdminPage.css'
import '../../styles/ValidateClasses.css'
import '../../styles/ParentClasses.css'

// ---- Utilities ----

function fmtDate(iso) {
    if (!iso) return ''
    try { return new Date(iso).toLocaleDateString('pt-PT', { day: '2-digit', month: 'long', year: 'numeric' }) }
    catch { return iso }
}

function fmtTime(iso) {
    if (!iso) return ''
    try { return new Date(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' }) }
    catch { return iso }
}

// Converts "09:00:00" (TimeOnly) → "09:00"
function fmtTime24(t) {
    return t ? t.slice(0, 5) : ''
}

// Handles both PascalCase (Items) and camelCase (items) from PagedResult<T>
function normalizeItems(data) {
    if (Array.isArray(data)) return data
    if (data && Array.isArray(data.Items)) return data.Items
    if (data && Array.isArray(data.items)) return data.items
    return []
}

function studentLabel(s) {
    const fn = s.FirstName ?? s.firstName ?? ''
    const ln = s.LastName ?? s.lastName ?? ''
    if (fn || ln) return `${fn} ${ln}`.trim()
    return s.Name ?? s.name ?? ''
}

// ---- Constants ----

const STATUS_LABEL = {
    0: 'Solicitada', 1: 'Aprovada', 2: 'Recusada',
    3: 'Cancelada', 4: 'Finalizada', 5: 'Validada', 6: 'Pendente', 7: 'Staff Aprovada',
}

// Chip colors per status (for calendar chips in Tab 1) — use CSS variables
const STATUS_CHIP = {
    0: { background: 'var(--warning-bg)', color: 'var(--warning)' },
    1: { background: 'var(--accent-soft)', color: 'var(--accent)' },
    2: { background: 'var(--danger-bg)', color: 'var(--danger)' },
    3: { background: 'var(--surface-2)', color: 'var(--text-2)' },
    4: { background: 'var(--success-bg)', color: 'var(--success)' },
    5: { background: 'var(--success-bg)', color: 'var(--success)' },
    6: { background: 'var(--warning-bg)', color: 'var(--warning)' },
    7: { background: 'var(--accent-soft)', color: 'var(--accent)' },
}

function statusCardClass(s) {
    if (s === 0) return 'class-card--amber'
    if (s === 2 || s === 3) return 'class-card--contested'
    if (s === 4 || s === 5) return 'class-card--green'
    if (s === 6) return 'class-card--orange'
    return ''  // default purple
}

const TABS = [
    { id: 'minhas-marcacoes', label: 'Minhas Marcações',     activeExtra: '' },
    { id: 'marcar',           label: 'Criar Coaching',       activeExtra: '' },
    { id: 'grupo',            label: 'Coachings Existentes', activeExtra: '-teal' },
    { id: 'inscricoes',       label: 'Inscrições',           activeExtra: '-orange' },
    { id: 'validar',          label: 'Validar Coachings',    activeExtra: '-orange' },
]

// ParentEnrollmentStatus enum (mirrors backend)
const ENROLLMENT_STATUS = { NotRequired: 0, Pending: 1, Approved: 2, Rejected: 3 }

// ---- Component ----

function ParentClassesPage() {
    const { user } = useAuth()

    // ===== Shared data (modalities, coaches, students) =====
    const [modalities, setModalities] = useState([])
    const [coaches, setCoaches]       = useState([])
    const [myStudents, setMyStudents] = useState([])
    const [joinEnabled, setJoinEnabled] = useState(true)

    useEffect(() => {
        Promise.allSettled([
            getModalities(),
            getCoachesForParent(),
            getMyStudents(),
            getJoinClassStatus(),
        ]).then(([modsRes, coachesRes, studentsRes, joinRes]) => {
            if (modsRes.status === 'fulfilled')     setModalities(normalizeItems(modsRes.value))
            if (coachesRes.status === 'fulfilled')  setCoaches(normalizeItems(coachesRes.value))
            if (studentsRes.status === 'fulfilled') setMyStudents(normalizeItems(studentsRes.value))
            if (joinRes.status === 'fulfilled')     setJoinEnabled(joinRes.value?.enabled ?? joinRes.value?.Enabled ?? true)
        })
    }, [])

    // CoachAvailableResponse: { CoachId, Name, Modalities: [{ ModalityId, Name }] }
    const modalityOptions = useMemo(() =>
        modalities.map(m => ({
            value: String(m.ModalityId ?? m.modalityId ?? ''),
            label: m.Name ?? m.name ?? '',
        })), [modalities])

    const coachOptions = useMemo(() =>
        coaches.map(c => ({
            value: String(c.CoachId ?? c.coachId ?? ''),
            label: c.Name ?? c.name ?? '',
        })), [coaches])

    const studentOptions = useMemo(() =>
        myStudents.map(s => ({
            value: String(s.StudentId ?? s.studentId ?? ''),
            label: studentLabel(s),
        })), [myStudents])

    // ===== Active tab =====
    const [activeTab, setActiveTab] = useState('minhas-marcacoes')

    const prevMonth = (setter) => setter(prev => {
        const d = new Date(prev)
        d.setDate(1)
        d.setMonth(d.getMonth() - 1)
        return d
    })
    const nextMonth = (setter) => setter(prev => {
        const d = new Date(prev)
        d.setDate(1)
        d.setMonth(d.getMonth() + 1)
        return d
    })

    // ===================================================
    // TAB 1 — Minhas Marcações (monthly calendar)
    // Uses GET /api/coachclasses/parent/{id} → CoachClassListResponse[]
    // { ClassId, StartDatetime, EndDatetime, ModalityName, CoachName, StudioName,
    //   Status (int), MaxParticipants, CurrentParticipants, CreatedAt }
    // All classes for parent's students are fetched once; month filter is client-side.
    // ===================================================
    const [t1Month, setT1Month]               = useState(new Date())
    const [t1AllClasses, setT1AllClasses]     = useState([])   // full dataset
    const [t1Loading, setT1Loading]           = useState(false)
    const [t1Error, setT1Error]               = useState('')
    const [t1SelectedDate, setT1SelectedDate] = useState(null)

    // Invite modal (Tab 1 — add student to existing class)
    const [inviteTarget, setInviteTarget]       = useState(null)
    const [inviteStudentId, setInviteStudentId] = useState('')
    const [inviteSubmitting, setInviteSubmitting] = useState(false)
    const [inviteError, setInviteError]         = useState('')

    useEffect(() => {
        if (activeTab !== 'minhas-marcacoes' || !user?.userId) return
        let cancelled = false
        setT1Loading(true); setT1Error('')
        getClassesByParent(user.userId)
            .then(d => { if (!cancelled) setT1AllClasses(normalizeItems(d)) })
            .catch(e => { if (!cancelled) setT1Error(e.message) })
            .finally(() => { if (!cancelled) setT1Loading(false) })
        return () => { cancelled = true }
    }, [activeTab, user?.userId])

    // Group ALL classes by date key "YYYY-MM-DD" (no month filter).
    // The calendar only renders days of the current month, so entries outside the
    // visible month stay in the map but are never looked up — no wasted rendering.
    const t1ByDate = useMemo(() => {
        const map = {}
        t1AllClasses.forEach(c => {
            const key = (c.StartDatetime ?? c.startDatetime ?? '').slice(0, 10)
            if (key) (map[key] ||= []).push(c)
        })
        return map
    }, [t1AllClasses])

    // Classes visible in the currently displayed month (for the detail list + empty state)
    const t1Classes = useMemo(() => {
        const { from, to } = getMonthRange(t1Month)
        return t1AllClasses.filter(c => {
            const d = (c.StartDatetime ?? c.startDatetime ?? '').slice(0, 10)
            return d >= from && d <= to
        })
    }, [t1AllClasses, t1Month])

    // Next upcoming class (for hint when current month is empty)
    const t1NextClass = useMemo(() => {
        const today = new Date().toISOString().slice(0, 10)
        return [...t1AllClasses]
            .filter(c => (c.StartDatetime ?? c.startDatetime ?? '').slice(0, 10) >= today)
            .sort((a, b) => (a.StartDatetime ?? a.startDatetime ?? '').localeCompare(b.StartDatetime ?? b.startDatetime ?? ''))[0] ?? null
    }, [t1AllClasses])

    // ===================================================
    // TAB 2 — Criar Aula (monthly slot calendar)
    // DaySlotResponse[]: [{ Date:"YYYY-MM-DD", Slots:[{ CoachId, CoachName, StartTime, EndTime, ModalityIds, ModalityNames }] }]
    // ===================================================
    const [t2Month, setT2Month]                   = useState(new Date())
    const [t2Modality, setT2Modality]             = useState('')
    const [t2Coach, setT2Coach]                   = useState('')
    const [t2SlotsByDate, setT2SlotsByDate]       = useState({})
    const [t2Loading, setT2Loading]               = useState(false)
    const [t2Error, setT2Error]                   = useState('')
    const [t2SelectedDate, setT2SelectedDate]     = useState(null)

    // Booking modal
    const [bookingSlot, setBookingSlot]           = useState(null)   // CoachSlot
    const [bookingDate, setBookingDate]           = useState('')     // "YYYY-MM-DD"
    const [bookingModalityId, setBookingModalityId] = useState('')
    const [bookingStudentId, setBookingStudentId]   = useState('')
    const [bookingStartTime, setBookingStartTime]   = useState('')  // "HH:MM"
    const [bookingEndTime, setBookingEndTime]       = useState('')  // "HH:MM"
    const [bookingSubmitting, setBookingSubmitting] = useState(false)
    const [bookingError, setBookingError]           = useState('')
    const [bookingSuccess, setBookingSuccess]       = useState(false)

    useEffect(() => {
        if (activeTab !== 'marcar') return
        let cancelled = false
        const { from, to } = getMonthRange(t2Month)
        setT2Loading(true); setT2Error(''); setT2SlotsByDate({})
        const params = { from, to }
        if (t2Modality) params.modalityId = t2Modality
        if (t2Coach)    params.coachId    = t2Coach
        getAvailableSlots(params)
            .then(data => {
                if (cancelled) return
                // data is null (204) or DaySlotResponse[]
                // DaySlotResponse: { Date: "YYYY-MM-DD", Slots: [...] } — PascalCase
                const byDate = {}
                if (Array.isArray(data)) {
                    for (const day of data) {
                        const key   = day.Date ?? day.date
                        const slots = day.Slots ?? day.slots ?? []
                        if (key) byDate[key] = slots
                    }
                }
                setT2SlotsByDate(byDate)
            })
            .catch(e => { if (!cancelled) setT2Error(e.message) })
            .finally(() => { if (!cancelled) setT2Loading(false) })
        return () => { cancelled = true }
    }, [activeTab, t2Month, t2Modality, t2Coach])

    const openBookingModal = (slot, date) => {
        // CoachSlot: { CoachId, CoachName, StartTime, EndTime, ModalityIds, ModalityNames }
        setBookingSlot(slot)
        setBookingDate(date)
        const firstModalityId = (slot.ModalityIds ?? slot.modalityIds ?? [])[0]
        setBookingModalityId(firstModalityId ? String(firstModalityId) : '')
        setBookingStudentId('')
        setBookingStartTime(fmtTime24(slot.StartTime ?? slot.startTime))
        setBookingEndTime(fmtTime24(slot.EndTime ?? slot.endTime))
        setBookingError('')
        setBookingSuccess(false)
    }

    // Modality options filtered to just what the selected slot's coach offers
    const bookingModalityOptions = useMemo(() => {
        if (!bookingSlot) return modalityOptions
        const names = bookingSlot.ModalityNames ?? bookingSlot.modalityNames ?? []
        const ids   = bookingSlot.ModalityIds   ?? bookingSlot.modalityIds   ?? []
        if (names.length === 0) return modalityOptions
        return names.map((name, i) => ({ value: String(ids[i] ?? ''), label: name }))
    }, [bookingSlot, modalityOptions])

    const handleBookingSubmit = async (e) => {
        e.preventDefault()
        if (!bookingStudentId)   { setBookingError('Selecione um aluno.'); return }
        if (!bookingModalityId)  { setBookingError('Selecione uma modalidade.'); return }
        if (!bookingStartTime)   { setBookingError('Defina a hora de início.'); return }
        if (!bookingEndTime)     { setBookingError('Defina a hora de fim.'); return }
        if (bookingEndTime <= bookingStartTime) { setBookingError('A hora de fim deve ser depois da hora de início.'); return }
        setBookingSubmitting(true); setBookingError('')
        try {
            const coachId = bookingSlot.CoachId ?? bookingSlot.coachId
            await parentCreateClass({
                coachId,
                modalityId:    Number(bookingModalityId),
                startDatetime: `${bookingDate}T${bookingStartTime}:00`,
                endDatetime:   `${bookingDate}T${bookingEndTime}:00`,
                studentId:     Number(bookingStudentId),
            })
            setBookingSuccess(true)
            setTimeout(() => {
                setBookingSlot(null)
                // Re-fetch slots for the current month
                const { from, to } = getMonthRange(t2Month)
                const params = { from, to }
                if (t2Modality) params.modalityId = t2Modality
                if (t2Coach)    params.coachId    = t2Coach
                getAvailableSlots(params)
                    .then(data => {
                        const byDate = {}
                        if (Array.isArray(data)) {
                            for (const day of data) {
                                const key = day.Date ?? day.date
                                if (key) byDate[key] = day.Slots ?? day.slots ?? []
                            }
                        }
                        setT2SlotsByDate(byDate)
                    })
                    .catch(() => {})
            }, 1400)
        } catch (err) {
            setBookingError(err.message)
        } finally {
            setBookingSubmitting(false)
        }
    }

    // ===================================================
    // TAB 3 — Aulas Existentes (monthly open-classes calendar)
    // PagedResult<OpenClassItem>: { Items:[{ ClassId, StartDatetime, EndDatetime, ModalityName, CoachName, StudioName, CurrentParticipants, MaxParticipants, SpotsAvailable }], TotalCount }
    // ===================================================
    const [t3Month, setT3Month]               = useState(new Date())
    const [t3Modality, setT3Modality]         = useState('')
    const [t3Classes, setT3Classes]           = useState([])
    const [t3Loading, setT3Loading]           = useState(false)
    const [t3Error, setT3Error]               = useState('')
    const [t3SelectedDate, setT3SelectedDate] = useState(null)
    const [t3Refresh, setT3Refresh]           = useState(0)
    const [t3ExpandedCard, setT3ExpandedCard] = useState(null)

    // Enroll modal
    const [enrollTarget, setEnrollTarget]     = useState(null)
    const [enrollStudentId, setEnrollStudentId] = useState('')
    const [enrollSubmitting, setEnrollSubmitting] = useState(false)
    const [enrollError, setEnrollError]       = useState('')

    useEffect(() => {
        if (activeTab !== 'grupo') return
        let cancelled = false
        setT3Loading(true); setT3Error('')
        const { from, to } = getMonthRange(t3Month)
        const params = { from, to }
        if (t3Modality) params.modalityId = t3Modality
        getOpenClasses(params)
            .then(data => {
                if (cancelled) return
                setT3Classes(normalizeItems(data))
            })
            .catch(e => { if (!cancelled) setT3Error(e.message) })
            .finally(() => { if (!cancelled) setT3Loading(false) })
        return () => { cancelled = true }
    }, [activeTab, t3Month, t3Modality, t3Refresh])

    // Group open classes by date
    const t3ByDate = useMemo(() => {
        const map = {}
        t3Classes.forEach(c => {
            const key = (c.StartDatetime ?? c.startDatetime ?? '').slice(0, 10)
            if (key) (map[key] ||= []).push(c)
        })
        return map
    }, [t3Classes])

    const openEnrollModal = (cls) => {
        setEnrollTarget(cls)
        setEnrollStudentId('')
        setEnrollError('')
    }

    const handleEnrollSubmit = async (e) => {
        e.preventDefault()
        if (!enrollStudentId) { setEnrollError('Selecione um aluno.'); return }
        setEnrollSubmitting(true); setEnrollError('')
        try {
            await enrollInClass({
                classId:   enrollTarget.ClassId ?? enrollTarget.classId,
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
    // PagedResult<ParentValidateItem>: { Items:[{ ClassId, ModalityName, StartDatetime, CoachName, ExpiresAt, Participants:[{ ParticipantId, StudentName, ValidationStatus }] }] }
    // ===================================================
    const [t4Items, setT4Items]   = useState([])
    const [t4Loading, setT4Loading] = useState(false)
    const [t4Error, setT4Error]   = useState('')

    useEffect(() => {
        if (activeTab !== 'validar') return
        let cancelled = false
        setT4Loading(true); setT4Error('')
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
                const update = list => list?.map(p =>
                    (p.ParticipantId ?? p.participantId) === participantId
                        ? { ...p, ValidationStatus: attended ? 1 : 2, validationStatus: attended ? 1 : 2 }
                        : p
                )
                return { ...cls, Participants: update(cls.Participants), participants: update(cls.participants) }
            }))
        } catch (err) {
            alert(err.message)
        }
    }

    const handleInviteSubmit = async (e) => {
        e.preventDefault()
        if (!inviteStudentId) { setInviteError('Selecione um aluno.'); return }
        setInviteSubmitting(true); setInviteError('')
        try {
            await enrollByInvite({
                classId:   inviteTarget.ClassId ?? inviteTarget.classId,
                studentId: Number(inviteStudentId),
            })
            setInviteTarget(null)
            // Refresh the parent's class list
            getClassesByParent(user.userId)
                .then(d => setT1AllClasses(normalizeItems(d)))
                .catch(() => {})
        } catch (err) {
            setInviteError(err.message)
        } finally {
            setInviteSubmitting(false)
        }
    }

    // ===================================================
    // TAB 4 — Inscrições (coach-created enrollment approvals)
    // For each CoachCreated+Requested class, load detail to get participant IDs.
    // Filter participants where student belongs to this parent + status=Pending(1).
    // ===================================================
    const [t5Items, setT5Items]     = useState([])  // [{ classId, modalityName, coachName, start, end, participantId, studentName, studentId }]
    const [t5Loading, setT5Loading] = useState(false)
    const [t5Error, setT5Error]     = useState('')

    const loadEnrollments = async () => {
        if (!user?.userId) return
        setT5Loading(true); setT5Error('')
        try {
            // Use the already-loaded class list; if not loaded yet, fetch it
            let classes = t1AllClasses
            if (classes.length === 0) {
                const data = await getClassesByParent(user.userId)
                classes = normalizeItems(data)
                setT1AllClasses(classes)
            }
            const myStudentIds = new Set(myStudents.map(s => s.StudentId ?? s.studentId))
            const coachCreatedRequested = classes.filter(c =>
                (c.ClassOrigin ?? c.classOrigin) === 1 &&
                (c.Status ?? c.status) === 0
            )
            const results = []
            await Promise.all(coachCreatedRequested.map(async cls => {
                try {
                    const detail = await getClassById(cls.ClassId ?? cls.classId)
                    const participants = detail?.Participants ?? detail?.participants ?? []
                    for (const p of participants) {
                        const sId = p.StudentId ?? p.studentId
                        const enrollStatus = p.ParentEnrollmentStatus ?? p.parentEnrollmentStatus ?? 0
                        if (myStudentIds.has(sId) && enrollStatus === ENROLLMENT_STATUS.Pending) {
                            results.push({
                                classId:       cls.ClassId ?? cls.classId,
                                modalityName:  cls.ModalityName ?? cls.modalityName ?? '',
                                coachName:     cls.CoachName ?? cls.coachName ?? '',
                                start:         cls.StartDatetime ?? cls.startDatetime,
                                end:           cls.EndDatetime ?? cls.endDatetime,
                                participantId: p.ParticipantId ?? p.participantId,
                                studentId:     sId,
                                studentName:   p.StudentName ?? p.studentName ?? '',
                            })
                        }
                    }
                } catch { /* ignore individual class errors */ }
            }))
            setT5Items(results)
        } catch (e) {
            setT5Error(e.message)
        } finally {
            setT5Loading(false)
        }
    }

    useEffect(() => {
        if (activeTab === 'inscricoes') loadEnrollments()
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [activeTab])

    const handleEnrollmentApprove = async (participantId, approve) => {
        try {
            await approveEnrollment(participantId, approve)
            setT5Items(prev => prev.filter(i => i.participantId !== participantId))
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
    // RENDER — TAB 1
    // ===================================================

    const renderMinhasMarcacoes = () => {
        const dayList = t1SelectedDate ? (t1ByDate[t1SelectedDate] ?? []) : []
        const totalClasses = t1Classes.length
        const hasAnyData   = t1AllClasses.length > 0

        const renderDay = (key, dayNum) => {
            const items = t1ByDate[key] ?? []
            const isSelected = key === t1SelectedDate
            return (
                <div
                    key={key}
                    className={`pc-day-cell${items.length ? ' pc-day-cell--has' : ''}${isSelected ? ' pc-day-cell--selected' : ''}`}
                    onClick={() => items.length && setT1SelectedDate(isSelected ? null : key)}
                >
                    <span className="pc-day-num">{dayNum}</span>
                    {items.slice(0, 2).map((c, ci) => {
                        const status = c.Status ?? c.status ?? 0
                        return (
                            <div key={ci} className="pc-event-chip" style={STATUS_CHIP[status] ?? STATUS_CHIP[0]}>
                                {fmtTime(c.StartDatetime ?? c.startDatetime)} {c.ModalityName ?? c.modalityName}
                            </div>
                        )
                    })}
                    {items.length > 2 && <div className="pc-event-more">+{items.length - 2}</div>}
                </div>
            )
        }

        return (
            <div>
                <p className="tab-description">Visualize os coachings marcados para os seus educandos no mês.</p>
                {t1Error && <p className="admin-error">{t1Error}</p>}

                <MonthCalendar
                    month={t1Month}
                    onPrev={() => { prevMonth(setT1Month); setT1SelectedDate(null) }}
                    onNext={() => { nextMonth(setT1Month); setT1SelectedDate(null) }}
                    renderDay={renderDay}
                    loading={t1Loading}
                />

                {/* Selected day detail */}
                {t1SelectedDate && dayList.length > 0 && (
                    <div>
                        <h3 className="validate-section-heading">{fmtDateLong(t1SelectedDate)}</h3>
                        {dayList
                            .sort((a, b) => (a.StartDatetime ?? '').localeCompare(b.StartDatetime ?? ''))
                            .map((c, i) => {
                                const status = c.Status ?? c.status ?? 0
                                const modalityName = c.ModalityName ?? c.modalityName ?? 'Aula'
                                const coachName    = c.CoachName   ?? c.coachName
                                const studioName   = c.StudioName  ?? c.studioName
                                const start        = c.StartDatetime ?? c.startDatetime
                                const end          = c.EndDatetime   ?? c.endDatetime
                                const studentNames     = c.StudentNames ?? c.studentNames ?? []
                                const maxParts         = c.MaxParticipants ?? c.maxParticipants ?? 0
                                const currentParts     = c.CurrentParticipants ?? c.currentParticipants ?? 0
                                const modalityId       = c.ModalityId ?? c.modalityId ?? 0
                                const isFull           = currentParts >= maxParts && maxParts > 0
                                const canInvite        = !isFull && (status === 0 || status === 1 || status === 7)
                                // Students assigned to this class's modality, not yet enrolled
                                const invitableStudents = myStudents.filter(s => {
                                    const sIds = s.ModalityIds ?? s.modalityIds ?? []
                                    return sIds.includes(modalityId)
                                })

                                return (
                                    <div key={c.ClassId ?? c.classId ?? i} className={`class-card ${statusCardClass(status)}`}>
                                        <div className="class-card-header" style={{ cursor: 'default' }}>
                                            <div style={{ flex: '1 1 auto', minWidth: 0 }}>
                                                <div className="class-card-title-row">
                                                    <h3 className="class-card-title">{modalityName}</h3>
                                                    <span className="status-pill" style={STATUS_CHIP[status] ?? {}}>
                                                        {STATUS_LABEL[status] ?? `Estado ${status}`}
                                                    </span>
                                                </div>
                                                <div className="class-card-info-grid">
                                                    <div>
                                                        <span className="label">Horário: </span>
                                                        {fmtTime(start)} – {fmtTime(end)}
                                                    </div>
                                                    {coachName  && <div><span className="label">Coach: </span>{coachName}</div>}
                                                    {studioName && <div><span className="label">Estúdio: </span>{studioName}</div>}
                                                    {maxParts > 0 && (
                                                        <div>
                                                            <span className="label">Vagas: </span>
                                                            {currentParts}/{maxParts}
                                                        </div>
                                                    )}
                                                </div>
                                                {studentNames.length > 0 && (
                                                    <div style={{ marginTop: '6px', fontSize: '0.875rem' }}>
                                                        <span className="label">Alunos: </span>
                                                        {studentNames.join(', ')}
                                                    </div>
                                                )}
                                            </div>
                                            {canInvite && invitableStudents.length > 0 && (
                                                <Button
                                                    variant="secondary"
                                                    onClick={() => {
                                                        setInviteTarget(c)
                                                        setInviteStudentId('')
                                                        setInviteError('')
                                                    }}
                                                >
                                                    + Aluno
                                                </Button>
                                            )}
                                        </div>
                                    </div>
                                )
                            })}
                    </div>
                )}

                {t1SelectedDate && dayList.length === 0 && !t1Loading && (
                    <div className="validate-empty">
                        <div className="validate-empty-icon">📅</div>
                        <h3>Sem coachings</h3>
                        <p>Nenhum coaching marcado para este dia.</p>
                    </div>
                )}

                {/* No classes at all */}
                {!t1Loading && !t1Error && !hasAnyData && !t1SelectedDate && (
                    <div className="validate-empty">
                        <div className="validate-empty-icon">📅</div>
                        <h3>Sem marcações</h3>
                        <p>Não tem coachings marcados. Crie um coaching no separador "Criar Coaching".</p>
                    </div>
                )}

                {/* Has classes but none this month → hint with next class */}
                {!t1Loading && !t1Error && hasAnyData && totalClasses === 0 && !t1SelectedDate && (
                    <div className="validate-empty" style={{ padding: '20px' }}>
                        <p style={{ color: '#6b7280', marginBottom: '8px' }}>
                            Não tem aulas marcadas este mês.
                        {t1AllClasses.length > 0 && ` Tem ${t1AllClasses.length} coaching${t1AllClasses.length > 1 ? 's' : ''} noutros meses.`}
                        </p>
                        {t1NextClass && (
                            <p style={{ color: 'var(--accent)', fontWeight: 600, fontSize: '0.92rem' }}>
                                Próximo coaching: {fmtDateLong((t1NextClass.StartDatetime ?? t1NextClass.startDatetime ?? '').slice(0, 10))} às {fmtTime(t1NextClass.StartDatetime ?? t1NextClass.startDatetime)} — {t1NextClass.ModalityName ?? t1NextClass.modalityName}
                            </p>
                        )}
                    </div>
                )}
            </div>
        )
    }

    // ===================================================
    // RENDER — TAB 2
    // ===================================================

    const renderCriarAula = () => {
        const selectedSlots = t2SelectedDate ? (t2SlotsByDate[t2SelectedDate] ?? []) : []
        const hasAnySlots = Object.keys(t2SlotsByDate).length > 0

        const todayIso = isoDate(new Date())

        const renderDay = (key, dayNum) => {
            const slots = key < todayIso ? [] : (t2SlotsByDate[key] ?? [])
            const isSelected = key === t2SelectedDate
            return (
                <div
                    key={key}
                    className={`pc-day-cell${slots.length ? ' pc-day-cell--has-slot' : ''}${isSelected ? ' pc-day-cell--selected' : ''}${key < todayIso ? ' pc-day-cell--past' : ''}`}
                    onClick={() => slots.length && setT2SelectedDate(isSelected ? null : key)}
                >
                    <span className="pc-day-num">{dayNum}</span>
                    {slots.slice(0, 2).map((s, si) => (
                        <div key={si} className="pc-slot-chip">
                            {fmtTime24(s.StartTime ?? s.startTime)} – {fmtTime24(s.EndTime ?? s.endTime)}
                        </div>
                    ))}
                    {slots.length > 2 && <div className="pc-event-more">+{slots.length - 2}</div>}
                </div>
            )
        }

        return (
            <div>
                <p className="tab-description">
                    Filtre por modalidade ou professor, clique num dia com vagas disponíveis e envie o pedido de coaching.
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

                <MonthCalendar
                    month={t2Month}
                    onPrev={() => { prevMonth(setT2Month); setT2SelectedDate(null) }}
                    onNext={() => { nextMonth(setT2Month); setT2SelectedDate(null) }}
                    renderDay={renderDay}
                    loading={t2Loading}
                />

                {/* Selected day slot cards */}
                {t2SelectedDate && selectedSlots.length > 0 && (
                    <div>
                        <h3 className="validate-section-heading">Vagas para {fmtDateLong(t2SelectedDate)}</h3>
                        {selectedSlots
                            .sort((a, b) => (a.StartTime ?? a.startTime ?? '').localeCompare(b.StartTime ?? b.startTime ?? ''))
                            .map((slot, i) => {
                                const coachName = slot.CoachName ?? slot.coachName ?? ''
                                const modNames  = (slot.ModalityNames ?? slot.modalityNames ?? []).join(', ')
                                const start     = fmtTime24(slot.StartTime ?? slot.startTime)
                                const end       = fmtTime24(slot.EndTime   ?? slot.endTime)
                                return (
                                    <div key={i} className="class-card class-card--green">
                                        <div className="class-card-header" style={{ cursor: 'default' }}>
                                            <div style={{ flex: '1 1 auto', minWidth: 0 }}>
                                                <div className="class-card-title-row">
                                                    <h3 className="class-card-title">{coachName || 'Horário disponível'}</h3>
                                                    <span className="class-card-capacity" style={{ background: '#d1fae5', color: '#065f46' }}>
                                                        {start} – {end}
                                                    </span>
                                                </div>
                                                <div className="class-card-info-grid">
                                                    {coachName && <div><span className="label">Professor: </span>{coachName}</div>}
                                                    {modNames  && <div><span className="label">Modalidades: </span>{modNames}</div>}
                                                </div>
                                            </div>
                        <Button variant="primary" onClick={() => openBookingModal(slot, t2SelectedDate)}>
                                                Pedir Coaching
                                            </Button>
                                        </div>
                                    </div>
                                )
                            })}
                    </div>
                )}

                {!t2Loading && !hasAnySlots && (
                    <div className="validate-empty">
                        <div className="validate-empty-icon">🔍</div>
                        <h3>Sem vagas disponíveis</h3>
                        <p>Não há vagas neste mês. Tente outro mês ou altere os filtros.</p>
                    </div>
                )}

                {!t2Loading && hasAnySlots && !t2SelectedDate && (
                    <div className="validate-empty" style={{ padding: '20px' }}>
                        <p style={{ color: '#6b7280' }}>Clique num dia assinalado a verde para ver os horários disponíveis.</p>
                    </div>
                )}
            </div>
        )
    }

    // ===================================================
    // RENDER — TAB 3
    // ===================================================

    const renderAulasExistentes = () => {
        if (!joinEnabled) {
            return (
                <div className="validate-empty">
                    <div className="validate-empty-icon">🔒</div>
                    <h3>Inscrições desativadas</h3>
                    <p>A funcionalidade de inscrição em aulas existentes está temporariamente desativada.</p>
                </div>
            )
        }

        const selectedClasses = t3SelectedDate ? (t3ByDate[t3SelectedDate] ?? []) : []
        const hasAnyClasses   = Object.keys(t3ByDate).length > 0

        const renderDay = (key, dayNum) => {
            const classes    = t3ByDate[key] ?? []
            const isSelected = key === t3SelectedDate
            return (
                <div
                    key={key}
                    className={`pc-day-cell${classes.length ? ' pc-day-cell--has-open' : ''}${isSelected ? ' pc-day-cell--selected' : ''}`}
                    onClick={() => classes.length && setT3SelectedDate(isSelected ? null : key)}
                >
                    <span className="pc-day-num">{dayNum}</span>
                    {classes.slice(0, 2).map((c, ci) => (
                        <div key={ci} className="pc-slot-chip" style={{ background: '#ccfbf1', color: '#0f766e' }}>
                            {fmtTime(c.StartDatetime ?? c.startDatetime)} {c.ModalityName ?? c.modalityName}
                        </div>
                    ))}
                    {classes.length > 2 && <div className="pc-event-more">+{classes.length - 2}</div>}
                </div>
            )
        }

        return (
            <div>
                <p className="tab-description">Coachings abertos a inscrições — clique num dia para ver os coachings disponíveis.</p>
                {t3Error && <p className="admin-error">{t3Error}</p>}

                {/* Modality filter */}
                <div className="pc-filter-bar">
                    <div className="pc-filter-group">
                        <label className="pc-filter-label">Filtrar por modalidade</label>
                        <Select
                            value={t3Modality}
                            onChange={v => { setT3Modality(v); setT3SelectedDate(null) }}
                            options={[{ value: '', label: 'Todas' }, ...modalityOptions]}
                        />
                    </div>
                </div>

                <MonthCalendar
                    month={t3Month}
                    onPrev={() => { prevMonth(setT3Month); setT3SelectedDate(null); setT3ExpandedCard(null) }}
                    onNext={() => { nextMonth(setT3Month); setT3SelectedDate(null); setT3ExpandedCard(null) }}
                    renderDay={renderDay}
                    loading={t3Loading}
                />

                {/* Selected day class cards */}
                {t3SelectedDate && selectedClasses.length > 0 && (
                    <div>
                        <h3 className="validate-section-heading">Coachings para {fmtDateLong(t3SelectedDate)}</h3>
                        {selectedClasses
                            .sort((a, b) => (a.StartDatetime ?? a.startDatetime ?? '').localeCompare(b.StartDatetime ?? b.startDatetime ?? ''))
                            .map((c, i) => {
                                const classId        = c.ClassId        ?? c.classId        ?? i
                                const modalityName   = c.ModalityName   ?? c.modalityName   ?? 'Aula'
                                const coachName      = c.CoachName      ?? c.coachName
                                const studioName     = c.StudioName     ?? c.studioName
                                const start          = c.StartDatetime  ?? c.startDatetime
                                const end            = c.EndDatetime    ?? c.endDatetime
                                const spotsAvailable = c.SpotsAvailable ?? c.spotsAvailable ?? 0
                                const maxParts       = c.MaxParticipants ?? c.maxParticipants ?? 0
                                const enrolled       = c.EnrolledStudents ?? c.enrolledStudents ?? []
                                const isFull         = spotsAvailable <= 0
                                const isExpanded     = t3ExpandedCard === classId

                                return (
                                    <div key={classId} className="class-card class-card--teal">
                                        <div className="class-card-header" style={{ cursor: 'default' }}>
                                            <div style={{ flex: '1 1 auto', minWidth: 0 }}>
                                                <div className="class-card-title-row">
                                                    <h3 className="class-card-title">{modalityName}</h3>
                                                    <span className="class-card-capacity class-card-capacity--teal">
                                                        Vagas {spotsAvailable}/{maxParts}
                                                    </span>
                                                    {isFull && <span className="status-pill status-pill--rejected">Lotado</span>}
                                                </div>
                                                <div className="class-card-info-grid">
                                                    <div>
                                                        <span className="label">Horário: </span>
                                                        {fmtTime(start)} – {fmtTime(end)}
                                                    </div>
                                                    {coachName  && <div><span className="label">Coach: </span>{coachName}</div>}
                                                    {studioName && <div><span className="label">Estúdio: </span>{studioName}</div>}
                                                </div>

                                                {/* Enrolled students dropdown */}
                                                {enrolled.length > 0 && (
                                                    <div style={{ marginTop: '8px' }}>
                                                        <button
                                                            type="button"
                                                            className="pc-students-toggle"
                                                            onClick={() => setT3ExpandedCard(isExpanded ? null : classId)}
                                                        >
                                                            {isExpanded ? '▲' : '▼'} {enrolled.length} aluno{enrolled.length !== 1 ? 's' : ''} inscrito{enrolled.length !== 1 ? 's' : ''}
                                                        </button>
                                                        {isExpanded && (
                                                            <ul className="pc-students-list">
                                                                {enrolled.map((name, ni) => (
                                                                    <li key={ni}>{name}</li>
                                                                ))}
                                                            </ul>
                                                        )}
                                                    </div>
                                                )}
                                            </div>
                                            <Button
                                                variant={isFull ? 'secondary' : 'primary'}
                                                disabled={isFull}
                                                onClick={() => !isFull && openEnrollModal(c)}
                                            >
                                                {isFull ? 'Lotado' : 'Inscrever'}
                                            </Button>
                                        </div>
                                    </div>
                                )
                            })}
                    </div>
                )}

                {!t3Loading && !hasAnyClasses && (
                    <div className="validate-empty">
                        <div className="validate-empty-icon">🎭</div>
                        <h3>Sem aulas disponíveis</h3>
                        <p>Não há aulas abertas a inscrições neste mês.</p>
                    </div>
                )}

                {!t3Loading && hasAnyClasses && !t3SelectedDate && (
                    <div className="validate-empty" style={{ padding: '20px' }}>
                        <p style={{ color: '#6b7280' }}>Clique num dia assinalado a verde-azulado para ver as aulas disponíveis.</p>
                    </div>
                )}
            </div>
        )
    }

    // ===================================================
    // RENDER — TAB 4 — Inscrições
    // ===================================================

    const renderInscricoes = () => {
        if (t5Loading) return <div className="validate-empty"><p>Carregando...</p></div>
        if (t5Error)   return <p className="admin-error">{t5Error}</p>
        if (t5Items.length === 0) return (
            <div className="validate-empty">
                <div className="validate-empty-icon">✓</div>
                <h3>Sem inscrições pendentes</h3>
                <p>Não há convites de aulas aguardando a sua resposta.</p>
            </div>
        )
        return (
            <div>
                <p className="tab-description">
                    O professor criou aulas com os seus educandos. Aprove ou rejeite a inscrição de cada aluno.
                </p>
                <div className="validate-warning">
                    <span className="validate-warning-icon">!</span>
                    <p>
                        <strong>Atenção:</strong> Tem {t5Items.length} inscrição{t5Items.length > 1 ? 'ões' : ''} pendente{t5Items.length > 1 ? 's' : ''}.
                    </p>
                </div>
                {t5Items.map(item => (
                    <div key={item.participantId} className="class-card class-card--amber">
                        <div className="class-card-header" style={{ cursor: 'default' }}>
                            <div style={{ flex: '1 1 auto', minWidth: 0 }}>
                                <div className="class-card-title-row">
                                    <h3 className="class-card-title">{item.modalityName}</h3>
                                    <span className="status-pill" style={STATUS_CHIP[0]}>Aguarda resposta</span>
                                </div>
                                <div className="class-card-info-grid">
                                    <div>
                                        <span className="label">Data: </span>
                                        {fmtDate(item.start)} · {fmtTime(item.start)} – {fmtTime(item.end)}
                                    </div>
                                    {item.coachName && <div><span className="label">Coach: </span>{item.coachName}</div>}
                                    <div><span className="label">Aluno: </span>{item.studentName}</div>
                                </div>
                            </div>
                            <div style={{ display: 'flex', gap: '8px', flexShrink: 0 }}>
                                <Button
                                    variant="secondary"
                                    onClick={() => handleEnrollmentApprove(item.participantId, false)}
                                >
                                    Rejeitar
                                </Button>
                                <Button
                                    variant="primary"
                                    onClick={() => handleEnrollmentApprove(item.participantId, true)}
                                >
                                    Aceitar
                                </Button>
                            </div>
                        </div>
                    </div>
                ))}
            </div>
        )
    }

    // ===================================================
    // RENDER — TAB 5
    // ===================================================

    const renderValidarAulas = () => {
        if (t4Loading) return <div className="validate-empty"><p>Carregando...</p></div>
        if (t4Error)   return <p className="admin-error">{t4Error}</p>
        if (!t4Items.length) return (
            <div className="validate-empty">
                <div className="validate-empty-icon">✓</div>
                <h3>Tudo validado</h3>
                <p>Não há coachings para validar neste momento.</p>
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
                            const parts        = cls.Participants ?? cls.participants ?? []
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
                    <h2>Coachings</h2>
                    <p>Gerir marcações, validações e coachings de grupo.</p>
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
                {activeTab === 'marcar'           && renderCriarAula()}
                {activeTab === 'grupo'            && renderAulasExistentes()}
                {activeTab === 'inscricoes'       && renderInscricoes()}
                {activeTab === 'validar'          && renderValidarAulas()}
            </div>

            {/* ====== Booking Modal (Tab 2 — Pedir Aula) ====== */}
            <Modal
                open={bookingSlot !== null}
                title="Pedir Coaching"
                onClose={() => setBookingSlot(null)}
            >
                {bookingSuccess ? (
                    <div className="validate-empty" style={{ padding: '24px' }}>
                        <div className="validate-empty-icon">✓</div>
                        <h3>Pedido enviado!</h3>
                        <p>O seu pedido de coaching foi submetido e aguarda aprovação da direção.</p>
                    </div>
                ) : (
                    <form onSubmit={handleBookingSubmit} className="modal-form">
                        {bookingSlot && (
                            <div className="reject-class-summary">
                                <div>📅 {fmtDateLong(bookingDate)}</div>
                                {(bookingSlot.CoachName ?? bookingSlot.coachName) && (
                                    <div>👨‍🏫 {bookingSlot.CoachName ?? bookingSlot.coachName}</div>
                                )}
                                <div>🕐 Janela disponível: {fmtTime24(bookingSlot.StartTime ?? bookingSlot.startTime)} – {fmtTime24(bookingSlot.EndTime ?? bookingSlot.endTime)}</div>
                            </div>
                        )}

                        <div className="modal-field">
                            <label className="modal-label">Modalidade *</label>
                            <Select
                                value={bookingModalityId}
                                onChange={setBookingModalityId}
                                placeholder="Selecione a modalidade"
                                options={bookingModalityOptions}
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

                        <div className="pc-time-row">
                            <div className="modal-field">
                                <label className="modal-label">Hora de início *</label>
                                <input
                                    type="time"
                                    className="input"
                                    value={bookingStartTime}
                                    onChange={e => setBookingStartTime(e.target.value)}
                                    required
                                />
                            </div>
                            <div className="modal-field">
                                <label className="modal-label">Hora de fim *</label>
                                <input
                                    type="time"
                                    className="input"
                                    value={bookingEndTime}
                                    onChange={e => setBookingEndTime(e.target.value)}
                                    required
                                />
                            </div>
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

            {/* ====== Invite Modal (Tab 1 — Adicionar Aluno) ====== */}
            <Modal
                open={inviteTarget !== null}
                title="Adicionar Aluno ao Coaching"
                onClose={() => setInviteTarget(null)}
            >
                <form onSubmit={handleInviteSubmit} className="modal-form">
                    {inviteTarget && (() => {
                        const modalityId = inviteTarget.ModalityId ?? inviteTarget.modalityId ?? 0
                        const filteredStudents = myStudents
                            .filter(s => (s.ModalityIds ?? s.modalityIds ?? []).includes(modalityId))
                            .map(s => ({ value: String(s.StudentId ?? s.studentId ?? ''), label: studentLabel(s) }))
                        return (
                            <>
                                <div className="reject-class-summary">
                                    <div>💃 {inviteTarget.ModalityName ?? inviteTarget.modalityName ?? 'Coaching'}</div>
                                    <div>📅 {fmtDateLong((inviteTarget.StartDatetime ?? inviteTarget.startDatetime ?? '').slice(0, 10))} · {fmtTime(inviteTarget.StartDatetime ?? inviteTarget.startDatetime)} – {fmtTime(inviteTarget.EndDatetime ?? inviteTarget.endDatetime)}</div>
                                </div>
                                <div className="modal-field">
                                    <label className="modal-label">Selecionar Aluno *</label>
                                    {filteredStudents.length === 0 ? (
                                        <p style={{ color: 'var(--text-2)', fontSize: '0.875rem' }}>
                                            Não tem alunos inscritos na modalidade desta aula.
                                        </p>
                                    ) : (
                                        <Select
                                            value={inviteStudentId}
                                            onChange={setInviteStudentId}
                                            placeholder="Selecione o aluno"
                                            options={filteredStudents}
                                        />
                                    )}
                                </div>
                            </>
                        )
                    })()}
                    {inviteError && <p className="admin-error">{inviteError}</p>}
                    <div className="modal-actions">
                        <Button type="button" variant="secondary" onClick={() => setInviteTarget(null)}>
                            Cancelar
                        </Button>
                        <Button type="submit" variant="primary" disabled={inviteSubmitting}>
                            {inviteSubmitting ? 'A adicionar...' : 'Adicionar'}
                        </Button>
                    </div>
                </form>
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
                            <div>💃 {enrollTarget.ModalityName ?? 'Coaching'}</div>
                            <div>📅 {fmtDateLong((enrollTarget.StartDatetime ?? '').slice(0, 10))} · {fmtTime(enrollTarget.StartDatetime)} – {fmtTime(enrollTarget.EndDatetime)}</div>
                            {enrollTarget.CoachName  && <div>👨‍🏫 {enrollTarget.CoachName}</div>}
                            {enrollTarget.StudioName && <div>📍 {enrollTarget.StudioName}</div>}
                            <div>Vagas restantes: {enrollTarget.SpotsAvailable}/{enrollTarget.MaxParticipants}</div>
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
