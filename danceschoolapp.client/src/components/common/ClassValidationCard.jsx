import { useState } from 'react'
import '../../styles/ValidateClasses.css'

function formatDate(iso) {
    if (!iso) return ''
    try {
        const d = new Date(iso)
        return d.toLocaleDateString('pt-PT')
    } catch {
        return String(iso)
    }
}

function formatTime(iso) {
    if (!iso) return ''
    try {
        const d = new Date(iso)
        return d.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' })
    } catch {
        return String(iso)
    }
}

function ValidationDot({ state, label }) {
    const map = {
        1: { cls: 'validation-dot-icon--green', symbol: '\u2713' },
        2: { cls: 'validation-dot-icon--red', symbol: '\u2717' },
        0: { cls: 'validation-dot-icon--amber', symbol: '\u23F3' },
    }
    const v = map[state ?? 0]
    return (
        <span className="validation-dot">
            <span className={`validation-dot-icon ${v.cls}`}>{v.symbol}</span>
            {label && <span>{label}</span>}
        </span>
    )
}

function ParentTally({ participants }) {
    const validated = (participants || []).filter(p => (p.ValidationStatus ?? p.validationStatus) === 1).length
    const rejected = (participants || []).filter(p => (p.ValidationStatus ?? p.validationStatus) === 2).length
    const pending = (participants || []).filter(p => (p.ValidationStatus ?? p.validationStatus ?? 0) === 0).length
    const total = (participants || []).length

    return (
        <span className="tally-badge">
            <span className="tally-badge-label">EE</span>
            <span className="tally-dot">
                <span className="tally-dot-circle tally-dot-circle--green" />
                {validated}
            </span>
            <span className="tally-dot">
                <span className="tally-dot-circle tally-dot-circle--red" />
                {rejected}
            </span>
            <span className="tally-dot">
                <span className="tally-dot-circle tally-dot-circle--amber" />
                {pending}
            </span>
            <span className="tally-count">/ {total}</span>
        </span>
    )
}

function CoachBadge({ state }) {
    return (
        <span className="tally-badge">
            <span className="tally-badge-label">Coach</span>
            <ValidationDot state={state} />
        </span>
    )
}

function ClassValidationCard({
    aula,
    tipo,
    variant = 'purple',
    onConfirm,
    onReject,
    onEdit,
    showParticipants = true,
    showCoachValidation = false,
    showParentTally = false,
    confirmLabel,
    rejectLabel,
}) {
    const [expanded, setExpanded] = useState(false)

    const id = aula?.ClassId ?? aula?.classId ?? aula?.id ?? null
    const modality = aula?.ModalityName ?? aula?.modalityName ?? aula?.Modality ?? aula?.modality ?? ''
    const studio = aula?.StudioName ?? aula?.studioName ?? aula?.Studio ?? aula?.studio ?? ''
    const coach = aula?.CoachName ?? aula?.coachName ?? aula?.Coach ?? aula?.coach ?? ''
    const createdBy = aula?.CreatedBy ?? aula?.createdBy ?? aula?.ParentName ?? aula?.parentName ?? ''
    const startDt = aula?.StartDatetime ?? aula?.startDatetime ?? aula?.PreferredDatetime ?? aula?.preferredDatetime ?? null
    const endDt = aula?.EndDatetime ?? aula?.endDatetime ?? null
    const maxParticipants = aula?.MaxParticipants ?? aula?.maxParticipants ?? aula?.max_participants ?? aula?.MaxStudents ?? aula?.maxStudents ?? '?'
    const participants = aula?.Participants ?? aula?.participants ?? []
    const studentNames = aula?.StudentNames ?? aula?.studentNames ?? aula?.students ?? []
    const totalParticipants = aula?.TotalParticipants ?? aula?.totalParticipants ?? null
    const enrolled = totalParticipants ?? (participants.length || studentNames.length || 0)
    const coachValidationStatus = aula?.CoachValidationStatus ?? aula?.coachValidationStatus ?? null
    const coachValidatedAt = aula?.CoachValidatedAt ?? aula?.coachValidatedAt ?? null

    const date = formatDate(startDt)
    const startTime = formatTime(startDt)
    const endTime = formatTime(endDt)
    const timeStr = endTime ? `${startTime}\u2013${endTime}` : startTime

    const hasDisputed = participants.some(p => (p.ValidationStatus ?? p.validationStatus ?? 0) === 2)
    const coachDenied = coachValidationStatus === 2

    // Contested state overrides the normal border on the staff pending view
    const isContested = showCoachValidation && (hasDisputed || coachDenied)
    const borderClass = isContested ? 'class-card--contested'
        : variant === 'amber' ? 'class-card--amber'
            : variant === 'orange' ? 'class-card--orange'
                : variant === 'green' ? 'class-card--green' : ''

    const capacityClass = variant === 'amber' ? 'class-card-capacity--amber'
        : variant === 'orange' ? 'class-card-capacity--orange' : ''

    const expandedClass = isContested ? 'class-card-expanded--contested'
        : variant === 'amber' ? 'class-card-expanded--amber'
            : variant === 'orange' ? 'class-card-expanded--orange' : ''

    const confirmText = confirmLabel
        || (tipo === 'coach-request' ? 'Aceitar Coaching' : 'Realizada')
    const rejectText = rejectLabel
        || (tipo === 'coach-request' ? 'Recusar Coaching' : 'Não realizada')

    return (
        <div className={`class-card ${borderClass}`}>
            <button
                type="button"
                className="class-card-header"
                onClick={() => setExpanded(prev => !prev)}
            >
                <div style={{ flex: '1 1 auto', minWidth: 0 }}>
                    <div className="class-card-title-row">
                        <h3 className="class-card-title">
                            {modality || (studentNames[0] ?? 'Coaching')}
                        </h3>
                        <span className={`class-card-capacity ${capacityClass}`}>
                            Alunos {enrolled}/{maxParticipants}
                        </span>
                        {/* Contested badge — shown on staff pending view when anyone disputed */}
                        {showCoachValidation && (hasDisputed || coachDenied) && (
                            <span className="status-pill status-pill--rejected">Contestada</span>
                        )}
                        {/* Awaiting validation badge — parent view */}
                        {tipo === 'professor' && (
                            <span className="status-pill status-pill--pending">Aguarda validação</span>
                        )}
                    </div>

                    <div className="class-card-info-grid">
                        {date && (
                            <div>
                                <span className="label">Data: </span>
                                {date} · {timeStr}
                            </div>
                        )}
                        {studio && (
                            <div>
                                <span className="label">Estúdio: </span>
                                {studio}
                            </div>
                        )}
                        {coach && (
                            <div>
                                <span className="label">Coach: </span>
                                {coach}
                            </div>
                        )}
                        {createdBy && (
                            <div>
                                <span className="label">Criada por: </span>
                                {createdBy}
                            </div>
                        )}
                    </div>

                    {(showParentTally || showCoachValidation) && (
                        <div className="tally-row">
                            {showParentTally && <ParentTally participants={participants} />}
                            {showCoachValidation && <CoachBadge state={coachValidationStatus} />}
                        </div>
                    )}
                </div>

                <svg
                    className={`class-card-chevron ${expanded ? 'class-card-chevron--open' : ''}`}
                    viewBox="0 0 20 20"
                    fill="currentColor"
                >
                    <path
                        fillRule="evenodd"
                        d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z"
                        clipRule="evenodd"
                    />
                </svg>
            </button>

            {expanded && (
                <div className={`class-card-expanded ${expandedClass}`}>
                    {/* Participant list (Alunos label preserved) */}
                    {showParticipants && participants.length > 0 && (
                        <div>
                            <p className="class-card-section-title">
                                Participantes ({enrolled}/{maxParticipants})
                            </p>
                            {participants.map((p, idx) => {
                                const pId = p.ParticipantId ?? p.participantId ?? p.id ?? idx
                                const studentName = p.StudentName ?? p.studentName ?? p.student_name ?? ''
                                const parentName = p.ParentName ?? p.parentName ?? p.parent_name ?? ''
                                const vs = p.ValidationStatus ?? p.validationStatus ?? p.validation_status ?? 0

                                return (
                                    <div key={pId} className="participant-row">
                                        <div>
                                            <p className="participant-name">{studentName}</p>
                                            {parentName && <p className="participant-parent">EE: {parentName}</p>}
                                        </div>
                                        <div className="participant-right">
                                            {tipo === 'professor' && !showCoachValidation ? (
                                                vs === 0 ? (
                                                    <div className="participant-actions">
                                                        <button
                                                            className="btn-participant-confirm"
                                                            onClick={(e) => { e.stopPropagation(); onConfirm && onConfirm(pId) }}
                                                        >
                                                            {'\u2713'} Realizada
                                                        </button>
                                                        <button
                                                            className="btn-participant-reject"
                                                            onClick={(e) => { e.stopPropagation(); onReject && onReject(pId) }}
                                                        >
                                                            {'\u2717'} Não realizada
                                                        </button>
                                                    </div>
                                                ) : (
                                                    <ValidationDot
                                                        state={vs}
                                                        label={vs === 1 ? 'Realizada' : 'Não realizada'}
                                                    />
                                                )
                                            ) : showCoachValidation ? (
                                                <ValidationDot
                                                    state={vs}
                                                    label={vs === 1 ? 'Realizada' : vs === 2 ? 'Não realizada' : 'Aguarda'}
                                                />
                                            ) : (
                                                <span>Inscrito</span>
                                            )}
                                        </div>
                                    </div>
                                )
                            })}
                        </div>
                    )}

                    {/* Student names fallback (when no participant objects) */}
                    {showParticipants && participants.length === 0 && studentNames.length > 0 && (
                        <div>
                            <p className="class-card-section-title">Alunos</p>
                            {studentNames.map((name, idx) => (
                                <div key={idx} className="participant-row">
                                    <p className="participant-name">{name}</p>
                                </div>
                            ))}
                        </div>
                    )}

                    {/* Coach + Parent validation summary for pending tab */}
                    {showCoachValidation && (
                        <div className="validation-summary-row">
                            <div className="validation-summary-card">
                                <span className="validation-summary-card-label">Validação Coach</span>
                                <div className="validation-summary-card-content">
                                    <ValidationDot
                                        state={coachValidationStatus}
                                        label={coachValidationStatus === 1 ? 'Realizada' : coachValidationStatus === 2 ? 'Não realizada' : 'Aguarda'}
                                    />
                                    {coachValidatedAt && (
                                        <span className="validation-summary-card-date">
                                            {new Date(coachValidatedAt).toLocaleString('pt-PT')}
                                        </span>
                                    )}
                                </div>
                            </div>
                            <div className="validation-summary-card">
                                <span className="validation-summary-card-label">Resumo Encarregados</span>
                                <ParentTally participants={participants} />
                            </div>
                        </div>
                    )}

                    {/* Actions — hidden for parent view (per-participant buttons handle it), always shown for coach */}
                    {!(tipo === 'professor' && !showCoachValidation && participants.length > 0) && (
                        <div className="class-card-actions">
                            {onEdit && (
                                <button
                                    className="btn btn-secondary"
                                    onClick={(e) => { e.stopPropagation(); onEdit(aula) }}
                                >
                                    ✏ Editar
                                </button>
                            )}
                            <button
                                className="btn btn-accept"
                                onClick={() => id && onConfirm(id)}
                            >
                                {confirmText}
                            </button>
                            <button
                                className="btn btn-reject"
                                onClick={() => id && onReject(id)}
                            >
                                {rejectText}
                            </button>
                        </div>
                    )}
                </div>
            )}
        </div>
    )
}

export default ClassValidationCard
