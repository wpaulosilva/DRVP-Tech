import { useEffect, useMemo, useState } from 'react'
import ClassValidationCard from '../../components/common/ClassValidationCard'
import Modal from '../../components/common/Modal'
import Button from '../../components/common/Button'
import Select from '../../components/common/Select'
import MonthCalendar, { isoDate, getMonthRange, fmtDateLong } from '../../components/common/MonthCalendar'
import { getCoachValidate, coachAccept, coachReject, coachValidate, getStudentsByModality, coachCreateClass, getMaxParticipants, getCoachAgenda } from '../../services/coachClassesService'
import { getMyCoachProfile, getCoachAvailability } from '../../services/coachAvailabilityService'
import '../../styles/ValidateClasses.css'
import '../../styles/ParentClasses.css'

function CoachValidateClassesPage() {
    const [activeTab, setActiveTab] = useState('requests')
    const [aulas, setAulas] = useState([])
    const [loading, setLoading] = useState(false)
    const [stats, setStats] = useState({ requests: 0, validations: 0 })

    // Reject modal state
    const [rejectTarget, setRejectTarget] = useState(null)
    const [rejectReason, setRejectReason] = useState('')
    const [rejecting, setRejecting] = useState(false)

    // Filter bar state (for requests + validations tabs)
    const [search, setSearch]       = useState('')
    const [sortOrder, setSortOrder] = useState('asc')

    // ===== Coach-create class state =====
    const [modalities, setModalities]         = useState([])  // coach's own assigned modalities
    const [coachAvailability, setCoachAvailability] = useState([])  // CoachAvailabilityListResponse[]
    const [agendaByDate, setAgendaByDate]     = useState({})  // "YYYY-MM-DD" → AgendaClassItem[]
    const [agendaLoading, setAgendaLoading]   = useState(false)
    const [createModality, setCreateModality] = useState('')
    const [createMonth, setCreateMonth]       = useState(new Date())
    const [createSelectedDate, setCreateSelectedDate] = useState(null)  // "YYYY-MM-DD"
    const [createStart, setCreateStart]       = useState('10:00')
    const [createEnd, setCreateEnd]           = useState('11:00')
    const [createMaxParts, setCreateMaxParts] = useState(1)
    const [allStudents, setAllStudents]       = useState([])
    const [studentSearch, setStudentSearch]   = useState('')
    const [selectedStudents, setSelectedStudents] = useState(new Set())
    const [studentsLoading, setStudentsLoading]   = useState(false)
    const [createSubmitting, setCreateSubmitting] = useState(false)
    const [createError, setCreateError]           = useState('')
    const [createSuccess, setCreateSuccess]       = useState(false)
    const [maxParticipants, setMaxParticipants]   = useState(8)

    const fetchAulas = async (tab) => {
        setLoading(true)
        try {
            const res = await getCoachValidate({ tab, page: 1, pageSize: 20 })
            const data = res?.Items ?? res?.items ?? res ?? []
            setAulas(data)
            setStats(prev => ({
                ...prev,
                requests: res?.RequestsCount ?? res?.requestsCount ?? (tab === 'requests' ? data.length : prev.requests),
                validations: res?.ValidationsCount ?? res?.validationsCount ?? (tab === 'validations' ? data.length : prev.validations),
            }))
        } catch (e) {
            console.error(e)
            setAulas([])
        } finally {
            setLoading(false)
        }
    }

    useEffect(() => {
        const prefetchValidationsCount = async () => {
            try {
                const other = await getCoachValidate({ tab: 'validations', page: 1, pageSize: 1 })
                const otherData = other?.Items ?? other?.items ?? other ?? []
                setStats(prev => ({
                    ...prev,
                    validations: other?.ValidationsCount ?? other?.validationsCount ?? other?.totalCount ?? otherData.length,
                }))
            } catch {
                // ignore
            }
        }
        prefetchValidationsCount()
    }, [])

    useEffect(() => {
        setSearch('')
        fetchAulas(activeTab)
    }, [activeTab])

    const handleAccept = async (id) => {
        try {
            await coachAccept(id)
            fetchAulas(activeTab)
        } catch (e) {
            console.error(e)
        }
    }

    const openRejectModal = (id) => {
        const aula = aulas.find(a => (a.ClassId ?? a.classId ?? a.id) === id)
        setRejectTarget(aula || { id })
        setRejectReason('')
    }

    const submitReject = async (e) => {
        e.preventDefault()
        const id = rejectTarget?.ClassId ?? rejectTarget?.classId ?? rejectTarget?.id
        if (!id) return

        setRejecting(true)
        try {
            await coachReject(id, rejectReason || undefined)
            setRejectTarget(null)
            setRejectReason('')
            fetchAulas(activeTab)
        } catch (e) {
            console.error(e)
        } finally {
            setRejecting(false)
        }
    }

    const handleValidar = async (id, didTeach) => {
        try {
            await coachValidate(id, didTeach)
            fetchAulas(activeTab)
        } catch (e) {
            console.error(e)
        }
    }

    // Load coach profile (own modalities + coachId), availability windows, and max group size once
    useEffect(() => {
        getMaxParticipants().then(data => {
            const v = data?.maxParticipants ?? data?.MaxParticipants
            if (v) setMaxParticipants(v)
        }).catch(() => {})

        getMyCoachProfile().then(profile => {
            const mods = profile?.Modalities ?? profile?.modalities ?? []
            setModalities(mods)
            const coachId = profile?.CoachId ?? profile?.coachId
            if (coachId) {
                getCoachAvailability(coachId)
                    .then(avList => setCoachAvailability(Array.isArray(avList) ? avList : []))
                    .catch(() => {})
            }
        }).catch(() => {})
    }, [])

    // Fetch agenda (booked classes) for the current create-month so we can show conflicts in the calendar
    useEffect(() => {
        if (activeTab !== 'criar') return
        let cancelled = false
        setAgendaLoading(true)
        const { from, to } = getMonthRange(createMonth)
        getCoachAgenda({ from, to })
            .then(data => {
                if (cancelled) return
                const byDate = {}
                const items = Array.isArray(data) ? data : []
                for (const c of items) {
                    const key = (c.StartDatetime ?? c.startDatetime ?? '').slice(0, 10)
                    if (key) (byDate[key] ||= []).push(c)
                }
                setAgendaByDate(byDate)
            })
            .catch(() => {})
            .finally(() => { if (!cancelled) setAgendaLoading(false) })
        return () => { cancelled = true }
    }, [activeTab, createMonth])

    // Load students when modality changes
    useEffect(() => {
        if (!createModality) { setAllStudents([]); setStudentSearch(''); return }
        setStudentsLoading(true); setSelectedStudents(new Set()); setStudentSearch('')
        getStudentsByModality(Number(createModality))
            .then(data => setAllStudents(Array.isArray(data) ? data : []))
            .catch(() => setAllStudents([]))
            .finally(() => setStudentsLoading(false))
    }, [createModality])

    const modalityOptions = useMemo(() =>
        modalities.map(m => ({ value: String(m.ModalityId ?? m.modalityId ?? ''), label: m.Name ?? m.name ?? '' }))
    , [modalities])

    const toggleStudent = (id) => {
        setSelectedStudents(prev => {
            const next = new Set(prev)
            next.has(id) ? next.delete(id) : next.add(id)
            return next
        })
    }

    const prevCreateMonth = () => setCreateMonth(prev => {
        const d = new Date(prev); d.setDate(1); d.setMonth(d.getMonth() - 1); return d
    })
    const nextCreateMonth = () => setCreateMonth(prev => {
        const d = new Date(prev); d.setDate(1); d.setMonth(d.getMonth() + 1); return d
    })

    const handleCoachCreate = async (e) => {
        e.preventDefault()
        if (!createModality)             { setCreateError('Selecione uma modalidade.'); return }
        if (!createSelectedDate)         { setCreateError('Selecione um dia no calendário.'); return }
        if (selectedStudents.size === 0) { setCreateError('Selecione pelo menos um aluno.'); return }
        if (createEnd <= createStart)    { setCreateError('A hora de fim deve ser depois da hora de início.'); return }
        setCreateSubmitting(true); setCreateError('')
        try {
            // determine maxParticipants from selected students (use configured max as cap)
            const computedMax = Math.max(1, Math.min((selectedStudents.size || 1), Number(maxParticipants || 1)))
            await coachCreateClass({
                modalityId:      Number(createModality),
                startDatetime:   `${createSelectedDate}T${createStart}:00`,
                endDatetime:     `${createSelectedDate}T${createEnd}:00`,
                maxParticipants: computedMax,
                studentIds:      [...selectedStudents],
            })
            setCreateSuccess(true)
            setSelectedStudents(new Set())
            setCreateSelectedDate(null)
        } catch (err) {
            setCreateError(err.message)
        } finally {
            setCreateSubmitting(false)
        }
    }

    const isRequests = activeTab === 'requests'

    const filteredAulas = useMemo(() => {
        let result = [...aulas]
        if (search.trim()) {
            const q = search.trim().toLowerCase()
            result = result.filter(a => {
                const modality = (a.ModalityName ?? a.modalityName ?? '').toLowerCase()
                const students = (a.StudentNames ?? a.studentNames ?? []).join(' ').toLowerCase()
                const participants = (a.Participants ?? a.participants ?? [])
                    .map(p => p.StudentName ?? p.studentName ?? '').join(' ').toLowerCase()
                return modality.includes(q) || students.includes(q) || participants.includes(q)
            })
        }
        result.sort((a, b) => {
            const da = new Date(a.StartDatetime ?? a.startDatetime ?? a.PreferredDatetime ?? a.preferredDatetime ?? 0).getTime()
            const db = new Date(b.StartDatetime ?? b.startDatetime ?? b.PreferredDatetime ?? b.preferredDatetime ?? 0).getTime()
            return sortOrder === 'asc' ? da - db : db - da
        })
        return result
    }, [aulas, search, sortOrder])

    return (
        <section className="dashboard-page-card">
            <h2>Validar Coachings</h2>
            <p>Aceite pedidos de coaching aprovados pela direção e valide coachings pendentes após o prazo de 48h.</p>

            <div className="validate-kpi-row" style={{ marginTop: '20px' }}>
                <div className="validate-kpi">
                    <span className="validate-kpi-label">Pedidos por aceitar</span>
                    <span className="validate-kpi-value validate-kpi-value--purple">
                        {loading ? '\u2014' : stats.requests}
                    </span>
                </div>
                <div className="validate-kpi">
                    <span className="validate-kpi-label">Validações pendentes</span>
                    <span className="validate-kpi-value validate-kpi-value--orange">
                        {loading ? '\u2014' : stats.validations}
                    </span>
                </div>
            </div>

            <div className="validate-tabs">
                <button
                    type="button"
                    className={`validate-tab ${activeTab === 'requests' ? 'validate-tab--active' : ''}`}
                    onClick={() => setActiveTab('requests')}
                >
                    Pedidos de Coaching ({stats.requests})
                </button>
                <button
                    type="button"
                    className={`validate-tab ${activeTab === 'validations' ? 'validate-tab--active-orange' : ''}`}
                    onClick={() => setActiveTab('validations')}
                >
                    Validações ({stats.validations})
                </button>
                <button
                    type="button"
                    className={`validate-tab ${activeTab === 'criar' ? 'validate-tab--active' : ''}`}
                    onClick={() => { setActiveTab('criar'); setCreateSuccess(false); setCreateError('') }}
                >
                    Criar Coaching
                </button>
            </div>

            {/* ===== Requests / Validations tab content ===== */}
            {activeTab !== 'criar' && (
                <>
                    {/* Filter bar */}
                    {!loading && aulas.length > 0 && (
                        <div className="class-filter-bar">
                            <div className="class-filter-search">
                                <svg width="15" height="15" viewBox="0 0 24 24" fill="none">
                                    <path d="M21 21l-4.35-4.35" stroke="var(--text-3)" strokeWidth="1.5" strokeLinecap="round"/>
                                    <circle cx="11" cy="11" r="8" stroke="var(--text-3)" strokeWidth="1.5"/>
                                </svg>
                                <input
                                    placeholder="Pesquisar por modalidade, aluno..."
                                    value={search}
                                    onChange={e => setSearch(e.target.value)}
                                />
                            </div>
                            <button
                                type="button"
                                className={`class-filter-sort${sortOrder === 'desc' ? ' class-filter-sort--active' : ''}`}
                                onClick={() => setSortOrder(o => o === 'asc' ? 'desc' : 'asc')}
                            >
                                {sortOrder === 'asc' ? '↑ Mais antigos' : '↓ Mais recentes'}
                            </button>
                            {search && (
                                <span className="class-filter-count">
                                    {filteredAulas.length} / {aulas.length}
                                </span>
                            )}
                        </div>
                    )}

                    {loading && (
                        <div className="validate-empty">
                            <p>Carregando...</p>
                        </div>
                    )}

                    {!loading && filteredAulas.length === 0 && (
                        <div className="validate-empty">
                            <div className="validate-empty-icon">{'\u2713'}</div>
                            <h3>{search ? 'Sem resultados' : isRequests ? 'Sem pedidos pendentes' : 'Sem validações pendentes'}</h3>
                            <p>
                                {search
                                    ? `Nenhum coaching corresponde a "${search}".`
                                    : isRequests
                                        ? 'Não há coachings aprovados pela direção a aguardar a sua resposta.'
                                        : 'Não há coachings em estado pendente para validar.'}
                            </p>
                        </div>
                    )}

                    {!loading && filteredAulas.map(aula => {
                        const classId = aula.ClassId ?? aula.classId ?? aula.id
                        return (
                            <ClassValidationCard
                                key={classId}
                                aula={aula}
                                tipo={isRequests ? 'coach-request' : 'professor'}
                                variant={isRequests ? 'purple' : 'orange'}
                                showParticipants
                                showCoachValidation={!isRequests}
                                showParentTally={!isRequests}
                                onConfirm={() => isRequests ? handleAccept(classId) : handleValidar(classId, true)}
                                onReject={() => isRequests ? openRejectModal(classId) : handleValidar(classId, false)}
                                confirmLabel={isRequests ? 'Aceitar Coaching' : 'Realizada'}
                                rejectLabel={isRequests ? 'Recusar' : 'Não realizada'}
                            />
                        )
                    })}
                </>
            )}

            {/* ===== Criar Coaching tab content ===== */}
            {activeTab === 'criar' && (
                <div style={{ marginTop: '16px' }}>
                    {createSuccess ? (
                        <div className="validate-empty" style={{ padding: '24px' }}>
                            <div className="validate-empty-icon">✓</div>
                            <h3>Coaching criado!</h3>
                            <p>Os EE dos alunos serão notificados para aprovar a inscrição.</p>
                            <Button variant="secondary" onClick={() => setCreateSuccess(false)} style={{ marginTop: '12px' }}>
                                Criar outro coaching
                            </Button>
                        </div>
                    ) : (
                        <>
                            <p className="tab-description">
                                Selecione a modalidade e clique num dia para definir o horário e os alunos. Os EE serão notificados para confirmar.
                            </p>

                            {/* Modality filter */}
                            <div className="pc-filter-bar">
                                <div className="pc-filter-group">
                                    <label className="pc-filter-label">Modalidade *</label>
                                    <Select
                                        value={createModality}
                                        onChange={v => { setCreateModality(v); setCreateSelectedDate(null); setCreateError('') }}
                                        placeholder="Selecione a modalidade"
                                        options={modalityOptions}
                                    />
                                </div>
                            </div>

                            {/* Month calendar — available days show time windows + any booked classes */}
                            {(() => {
                                const todayIso = isoDate(new Date())
                                const renderDay = (key, dayNum) => {
                                    const isPast     = key < todayIso
                                    const isSelected = key === createSelectedDate

                                    // Availability windows for this day-of-week (Weekday enum matches JS getDay())
                                    const dow = new Date(key + 'T00:00:00').getDay()
                                    const dayWindows = isPast ? [] : coachAvailability.filter(av => {
                                        const weekday    = av.Weekday    ?? av.weekday
                                        const validFrom  = av.ValidFrom  ?? av.validFrom  ?? null
                                        const validUntil = av.ValidUntil ?? av.validUntil ?? null
                                        if (weekday !== dow) return false
                                        if (validFrom  && key < validFrom)  return false
                                        if (validUntil && key > validUntil) return false
                                        return true
                                    })
                                    const hasSlot   = dayWindows.length > 0
                                    const booked    = agendaByDate[key] ?? []

                                    // Build chip list: availability windows first, booked classes after
                                    const chips = [
                                        ...dayWindows.map(av => ({
                                            type: 'avail',
                                            label: `${(av.StartTime ?? av.startTime ?? '').slice(0, 5)}–${(av.EndTime ?? av.endTime ?? '').slice(0, 5)}`,
                                        })),
                                        ...booked.map(c => ({
                                            type: 'booked',
                                            label: `${(c.StartDatetime ?? c.startDatetime ?? '').slice(11, 16)} ocupado`,
                                        })),
                                    ]

                                    return (
                                        <div
                                            key={key}
                                            className={[
                                                'pc-day-cell',
                                                isPast || !hasSlot ? 'pc-day-cell--past' : 'pc-day-cell--has-slot',
                                                isSelected ? 'pc-day-cell--selected' : '',
                                            ].join(' ').trim()}
                                            onClick={() => {
                                                if (isPast || !hasSlot) return
                                                setCreateSelectedDate(isSelected ? null : key)
                                                setCreateError('')
                                            }}
                                        >
                                            <span className="pc-day-num">{dayNum}</span>
                                            {chips.slice(0, 2).map((chip, ci) => (
                                                <div key={ci} className={`pc-slot-chip${chip.type === 'booked' ? ' pc-slot-chip--booked' : ''}`}>
                                                    {chip.label}
                                                </div>
                                            ))}
                                            {chips.length > 2 && <div className="pc-event-more">+{chips.length - 2}</div>}
                                        </div>
                                    )
                                }
                                return (
                                    <MonthCalendar
                                        month={createMonth}
                                        onPrev={() => { prevCreateMonth(); setCreateSelectedDate(null) }}
                                        onNext={() => { nextCreateMonth(); setCreateSelectedDate(null) }}
                                        renderDay={renderDay}
                                        loading={agendaLoading}
                                    />
                                )
                            })()}

                            {!createSelectedDate && (
                                <div className="validate-empty" style={{ padding: '20px' }}>
                                    <p style={{ color: '#6b7280' }}>Clique num dia para definir o horário do coaching.</p>
                                </div>
                            )}

                            {/* Form shown after day is selected */}
                            {createSelectedDate && (() => {
                                // Availability windows and booked classes for the selected day
                                const selDow = new Date(createSelectedDate + 'T00:00:00').getDay()
                                const selWindows = coachAvailability.filter(av => {
                                    const weekday    = av.Weekday    ?? av.weekday
                                    const validFrom  = av.ValidFrom  ?? av.validFrom  ?? null
                                    const validUntil = av.ValidUntil ?? av.validUntil ?? null
                                    if (weekday !== selDow) return false
                                    if (validFrom  && createSelectedDate < validFrom)  return false
                                    if (validUntil && createSelectedDate > validUntil) return false
                                    return true
                                })
                                const selBooked = agendaByDate[createSelectedDate] ?? []

                                // Sorted + filtered students
                                const sortedStudents = [...allStudents].sort((a, b) =>
                                    (a.StudentName ?? a.studentName ?? '').localeCompare(b.StudentName ?? b.studentName ?? '', 'pt')
                                )
                                const filteredStudents = studentSearch.trim()
                                    ? sortedStudents.filter(s =>
                                        (s.StudentName ?? s.studentName ?? '').toLowerCase().includes(studentSearch.toLowerCase())
                                    )
                                    : sortedStudents

                                return (
                                <form onSubmit={handleCoachCreate} className="modal-form" style={{ marginTop: '16px', maxWidth: '520px' }}>
                                    <h3 className="validate-section-heading" style={{ marginBottom: '8px' }}>
                                        {fmtDateLong(createSelectedDate)}
                                    </h3>

                                    {/* Day schedule summary */}
                                    <div style={{ marginBottom: '16px', fontSize: '0.85rem', display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                        {selWindows.length > 0 && (
                                            <div style={{ color: 'var(--success)' }}>
                                                <strong>Disponibilidade:</strong>{' '}
                                                {selWindows.map((av, i) =>
                                                    `${(av.StartTime ?? av.startTime ?? '').slice(0, 5)}–${(av.EndTime ?? av.endTime ?? '').slice(0, 5)}`
                                                ).join(', ')}
                                            </div>
                                        )}
                                        {selBooked.length > 0 && (
                                            <div style={{ color: 'var(--warning)' }}>
                                                <strong>Já marcado:</strong>{' '}
                                                {selBooked.map(c =>
                                                    `${(c.StartDatetime ?? c.startDatetime ?? '').slice(11, 16)}–${(c.EndDatetime ?? c.endDatetime ?? '').slice(11, 16)} (${c.ModalityName ?? c.modalityName ?? ''})`
                                                ).join(', ')}
                                            </div>
                                        )}
                                    </div>

                                    {/* Students */}
                                    <div className="modal-field">
                                        <label className="modal-label">Alunos *</label>
                                        {!createModality ? (
                                            <p style={{ fontSize: '0.875rem', color: 'var(--text-2)' }}>Selecione uma modalidade primeiro.</p>
                                        ) : studentsLoading ? (
                                            <p style={{ fontSize: '0.875rem', color: 'var(--text-2)' }}>Carregando alunos...</p>
                                        ) : allStudents.length === 0 ? (
                                            <p style={{ fontSize: '0.875rem', color: 'var(--text-2)' }}>Nenhum aluno inscrito nesta modalidade.</p>
                                        ) : (
                                            <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                                                <div style={{ padding: '6px 10px', borderBottom: '1px solid var(--border)', background: 'var(--surface-2)' }}>
                                                    <input
                                                        type="search"
                                                        placeholder="Pesquisar aluno..."
                                                        value={studentSearch}
                                                        onChange={e => setStudentSearch(e.target.value)}
                                                        style={{ border: 'none', outline: 'none', background: 'transparent', color: 'var(--text-1)', fontFamily: 'inherit', fontSize: '0.875rem', width: '100%' }}
                                                    />
                                                </div>
                                                <div style={{ display: 'flex', flexDirection: 'column', maxHeight: '180px', overflowY: 'auto', padding: '4px 10px' }}>
                                                    {filteredStudents.length === 0 ? (
                                                        <p style={{ fontSize: '0.8rem', color: 'var(--text-3)', padding: '6px 0' }}>Sem resultados.</p>
                                                    ) : filteredStudents.map(s => {
                                                        const sid = s.StudentId ?? s.studentId
                                                        return (
                                                            <label key={sid} style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', fontSize: '0.9rem', padding: '4px 0' }}>
                                                                <input
                                                                    type="checkbox"
                                                                    checked={selectedStudents.has(sid)}
                                                                    onChange={() => toggleStudent(sid)}
                                                                />
                                                                {s.StudentName ?? s.studentName}
                                                            </label>
                                                        )
                                                    })}
                                                </div>
                                            </div>
                                        )}
                                    </div>

                                    {/* Max participants removed: computed from selected students */}

                                    {/* Time range */}
                                    <div style={{ display: 'flex', gap: '12px' }}>
                                        <div className="modal-field" style={{ flex: 1 }}>
                                            <label className="modal-label">Hora início *</label>
                                            <input
                                                type="time"
                                                className="input"
                                                value={createStart}
                                                onChange={e => setCreateStart(e.target.value)}
                                                required
                                            />
                                        </div>
                                        <div className="modal-field" style={{ flex: 1 }}>
                                            <label className="modal-label">Hora fim *</label>
                                            <input
                                                type="time"
                                                className="input"
                                                value={createEnd}
                                                onChange={e => setCreateEnd(e.target.value)}
                                                required
                                            />
                                        </div>
                                    </div>

                                    {createError && <p className="admin-error">{createError}</p>}

                                    <div className="modal-actions" style={{ marginTop: '16px' }}>
                                        <Button type="button" variant="secondary" onClick={() => setCreateSelectedDate(null)}>
                                            Cancelar
                                        </Button>
                                        <Button type="submit" variant="primary" disabled={createSubmitting}>
                                            {createSubmitting ? 'A criar...' : 'Criar Coaching'}
                                        </Button>
                                    </div>
                                </form>
                                )
                            })()}
                        </>
                    )}
                </div>
            )}

            <Modal
                open={!!rejectTarget}
                title="Recusar pedido de coaching"
                onClose={() => {
                    setRejectTarget(null)
                    setRejectReason('')
                }}
            >
                <form onSubmit={submitReject}>
                    {rejectTarget && (
                        <div className="reject-class-summary">
                            <div>
                                Modalidade: {rejectTarget?.ModalityName ?? rejectTarget?.modalityName ?? rejectTarget?.Modality ?? rejectTarget?.modality ?? '\u2014'}
                            </div>
                            <div>
                                Data: {(rejectTarget?.StartDatetime ?? rejectTarget?.startDatetime ?? rejectTarget?.PreferredDatetime ?? rejectTarget?.preferredDatetime)
                                    ? new Date(rejectTarget.StartDatetime ?? rejectTarget.startDatetime ?? rejectTarget.PreferredDatetime ?? rejectTarget.preferredDatetime).toLocaleString('pt-PT')
                                    : '\u2014'}
                            </div>
                            <div>
                                Alunos: {(rejectTarget?.Participants ?? rejectTarget?.participants ?? []).length}/
                                {rejectTarget?.MaxParticipants ?? rejectTarget?.maxParticipants ?? '?'}
                            </div>
                        </div>
                    )}

                    <label
                        style={{
                            display: 'block',
                            marginBottom: '8px',
                            fontSize: '0.92rem',
                            fontWeight: 600,
                            color: '#475569',
                        }}
                    >
                        Motivo *
                    </label>

                    <textarea
                        required
                        rows={4}
                        value={rejectReason}
                        onChange={(e) => setRejectReason(e.target.value)}
                        placeholder="Explique o motivo da recusa..."
                        className="reject-textarea"
                    />

                    <div className="modal-actions" style={{ marginTop: '16px' }}>
                        <button
                            type="button"
                            className="btn btn-secondary"
                            onClick={() => {
                                setRejectTarget(null)
                                setRejectReason('')
                            }}
                        >
                            Voltar
                        </button>
                        <button
                            type="submit"
                            className="btn btn-danger"
                            disabled={rejecting}
                        >
                            {rejecting ? 'A recusar...' : 'Confirmar recusa'}
                        </button>
                    </div>
                </form>
            </Modal>
        </section>
    )
}

export default CoachValidateClassesPage