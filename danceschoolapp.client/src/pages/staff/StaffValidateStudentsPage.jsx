import { useEffect, useMemo, useState } from 'react'
import {
    getValidateStudents,
    acceptStudent,
    rejectStudent,
    deactivateStudent,
    activateStudent,
    getStudent,
    updateStudent,
} from '@/services/studentsService'
import { getModalities } from '@/services/modalitiesService'
import { get } from '@/api/client'
import Button from '@/components/common/Button'
import StatusBadge from '@/components/common/StatusBadge'
import Modal from '@/components/common/Modal'
import Input from '@/components/common/Input'
import AddStudentModal from '@/features/students/components/AddStudentModal'
import '@/styles/AdminPage.css'

// ── helpers ──────────────────────────────────────────────────────────────────

const getName = (s) => {
    const first = s.firstName ?? s.personInfo?.firstName ?? ''
    const last  = s.lastName  ?? s.personInfo?.lastName  ?? ''
    return `${first} ${last}`.trim() || '—'
}

function ValidationBadge({ status }) {
    if (status === 0) return <span className="val-badge val-badge--pending">Pendente</span>
    if (status === 1) return <span className="val-badge val-badge--accepted">Aceite</span>
    if (status === 2) return <span className="val-badge val-badge--rejected">Rejeitado</span>
    return <span className="muted-status">—</span>
}

function SensitiveField({ label, value }) {
    const [shown, setShown] = useState(false)
    const masked = value ? '•'.repeat(Math.min(value.length, 9)) : '—'
    return (
        <div className="student-detail-field">
            <label>{label}</label>
            <div className="sensitive-wrap">
                <span className="sensitive-value">{shown ? (value || '—') : masked}</span>
                {value && (
                    <button
                        type="button"
                        className="sensitive-toggle"
                        onClick={e => { e.stopPropagation(); setShown(v => !v) }}
                        title={shown ? 'Ocultar' : 'Mostrar'}
                    >
                        {shown ? '🙈' : '👁'}
                    </button>
                )}
            </div>
        </div>
    )
}

function StaffValidateStudentsPage() {
    const [tab, setTab] = useState('pending')
    const [students, setStudents] = useState([])
    const [loading, setLoading] = useState(true)
    const [sortBy, setSortBy] = useState('name')
    const [sortDir, setSortDir] = useState('asc')

    // All-tab filters
    const [search, setSearch] = useState('')
    const [filterModality, setFilterModality] = useState('')
    const [expandedIds, setExpandedIds] = useState(new Set())

    const [showRejectModal, setShowRejectModal] = useState(false)
    const [rejectReason, setRejectReason] = useState('')
    const [selectedStudentId, setSelectedStudentId] = useState(null)

    // Edit modal state
    const [modalities, setModalities] = useState([])
    const [showEditModal, setShowEditModal] = useState(false)
    const [editingStudent, setEditingStudent] = useState(null)
    const [editFirstName, setEditFirstName] = useState('')
    const [editLastName, setEditLastName] = useState('')
    const [editBirthDate, setEditBirthDate] = useState('')
    const [editPhone, setEditPhone] = useState('')
    const [editAddress, setEditAddress] = useState('')
    const [editNif, setEditNif] = useState('')
    const [editModalityIds, setEditModalityIds] = useState([])
    const [editError, setEditError] = useState('')
    const [editLoading, setEditLoading] = useState(false)

    const fetchStudents = async () => {
        setLoading(true)
        try {
            if (tab === 'pending') {
                const data = await getValidateStudents({ status: 'pending' })
                setStudents(data.items ?? [])
            } else {
                const data = await get('/api/students')
                setStudents(Array.isArray(data) ? data : [])
            }
        } catch {
            setStudents([])
        } finally {
            setLoading(false)
        }
    }

    useEffect(() => { fetchStudents() }, [tab])

    useEffect(() => {
        getModalities().then(data => {
            const list = Array.isArray(data) ? data : (data?.items ?? data?.Items ?? [])
            setModalities(list.filter(m => m.isActive ?? m.IsActive ?? true))
        }).catch(() => {})
    }, [])

    const toggleExpand = (id, e) => {
        // Don't expand when clicking buttons
        if (e.target.closest('button')) return
        setExpandedIds(prev => {
            const next = new Set(prev)
            next.has(id) ? next.delete(id) : next.add(id)
            return next
        })
    }

    const openEditModal = async (student, e) => {
        e?.stopPropagation()
        setEditingStudent(student)
        setEditError('')
        setEditLoading(true)
        try {
            const data = await getStudent(student.studentId)
            setEditFirstName(data.personInfo?.firstName ?? '')
            setEditLastName(data.personInfo?.lastName ?? '')
            setEditBirthDate(data.personInfo?.birthDate ?? '')
            setEditPhone(data.personInfo?.phone ?? '')
            setEditAddress(data.personInfo?.address ?? '')
            setEditNif(data.personInfo?.nif ?? '')
            setEditModalityIds((data.modalities ?? []).map(m => m.modalityId ?? m.ModalityId))
            setShowEditModal(true)
        } catch (err) {
            alert(err.message)
            setEditingStudent(null)
        } finally {
            setEditLoading(false)
        }
    }

    const handleEditSubmit = async () => {
        if (!editFirstName.trim() || !editLastName.trim()) { setEditError('Nome e apelido são obrigatórios.'); return }
        if (!editBirthDate) { setEditError('Data de nascimento é obrigatória.'); return }
        if (editNif && editNif.length !== 9) { setEditError('NIF deve ter 9 dígitos.'); return }
        setEditLoading(true)
        setEditError('')
        try {
            await updateStudent(editingStudent.studentId, {
                parentId: editingStudent.parentId ?? 0,
                firstName: editFirstName.trim(),
                lastName: editLastName.trim(),
                birthDate: editBirthDate,
                phone: editPhone.trim() || null,
                address: editAddress.trim() || null,
                nif: editNif.trim() || null,
                modalityIds: editModalityIds,
            })
            setShowEditModal(false)
            setEditingStudent(null)
            fetchStudents()
        } catch (err) {
            setEditError(err.message)
        } finally {
            setEditLoading(false)
        }
    }

    const toggleEditModality = (id) =>
        setEditModalityIds(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id])

    const handleSort = (field) => {
        if (sortBy === field) setSortDir(d => d === 'asc' ? 'desc' : 'asc')
        else { setSortBy(field); setSortDir('asc') }
    }

    const sortIcon = (field) => sortBy === field ? (sortDir === 'asc' ? ' ▲' : ' ▼') : ''

    const handleAccept = async (id) => { await acceptStudent(id); fetchStudents() }

    const confirmReject = async () => {
        if (!rejectReason.trim()) return
        await rejectStudent(selectedStudentId, rejectReason.trim())
        setShowRejectModal(false); setSelectedStudentId(null); setRejectReason('')
        fetchStudents()
    }

    const handleDeactivate = async (id, e) => {
        e.stopPropagation()
        if (!window.confirm('Desativar este estudante?')) return
        await deactivateStudent(id); fetchStudents()
    }

    const handleActivate = async (id, e) => {
        e.stopPropagation()
        if (!window.confirm('Reativar este estudante?')) return
        await activateStudent(id); fetchStudents()
    }

    const fmtDate = v => {
        if (!v) return '—'
        const d = new Date(v)
        return isNaN(d) ? v : d.toLocaleDateString('pt-PT')
    }

    const filteredSorted = useMemo(() => {
        let list = [...students]

        if (search.trim()) {
            const q = search.trim().toLowerCase()
            list = list.filter(s => getName(s).toLowerCase().includes(q))
        }

        if (filterModality) {
            const mid = Number(filterModality)
            list = list.filter(s => (s.modalities ?? []).some(m => (m.modalityId ?? m.ModalityId) === mid))
        }

        return list.sort((a, b) => {
            let va = '', vb = ''
            if (sortBy === 'name')       { va = getName(a).toLowerCase(); vb = getName(b).toLowerCase() }
            if (sortBy === 'validation') { va = String(a.acceptanceStatus); vb = String(b.acceptanceStatus) }
            if (sortBy === 'status')     { va = a.isActive ? '1' : '0'; vb = b.isActive ? '1' : '0' }
            if (va < vb) return sortDir === 'asc' ? -1 : 1
            if (va > vb) return sortDir === 'asc' ?  1 : -1
            return 0
        })
    }, [students, search, filterModality, sortBy, sortDir])

    return (
        <section className="dashboard-page-card">
            <h2>Estudantes</h2>
            <p>Aprovar ou gerir estudantes introduzidos pelos encarregados.</p>

            <div className="staff-tabs">
                <button className={tab === 'pending' ? 'staff-tab active' : 'staff-tab'} onClick={() => setTab('pending')}>
                    Pendentes
                </button>
                <button className={tab === 'all' ? 'staff-tab active' : 'staff-tab'} onClick={() => setTab('all')}>
                    Todos
                </button>
            </div>

            {loading ? (
                <p>A carregar...</p>
            ) : tab === 'pending' ? (
                students.length === 0 ? (
                    <p>Sem estudantes pendentes.</p>
                ) : (
                    <div className="validate-students-grid">
                        {students.map((s) => (
                            <div key={s.studentId} className="validate-card">
                                <h3>{getName(s)}</h3>
                                <p><strong>Data Nascimento:</strong> {s.birthDate || '—'}</p>
                                <p><strong>NIF:</strong> {s.nif || '—'}</p>
                                <p><strong>Telefone:</strong> {s.phone || '—'}</p>
                                <p><strong>Morada:</strong> {s.address || '—'}</p>
                                <p><strong>Encarregado:</strong> {s.parentName || '—'}</p>
                                <p><strong>Email:</strong> {s.parentEmail || '—'}</p>
                                {(s.modalities ?? []).length > 0 && (
                                    <p><strong>Modalidades:</strong> {s.modalities.map(m => m.name ?? m).join(', ')}</p>
                                )}
                                <div className="validate-actions">
                                    <Button variant="secondary" onClick={() => { setSelectedStudentId(s.studentId); setRejectReason(''); setShowRejectModal(true) }}>
                                        Rejeitar
                                    </Button>
                                    <Button variant="primary" onClick={() => handleAccept(s.studentId)}>
                                        Aceitar
                                    </Button>
                                </div>
                            </div>
                        ))}
                    </div>
                )
            ) : (
                <>
                    <div className="students-filters">
                        <input
                            type="text"
                            placeholder="Pesquisar por nome..."
                            value={search}
                            onChange={e => setSearch(e.target.value)}
                        />
                        <select value={filterModality} onChange={e => setFilterModality(e.target.value)}>
                            <option value="">Todas as modalidades</option>
                            {modalities.map(m => (
                                <option key={m.modalityId} value={m.modalityId}>{m.name}</option>
                            ))}
                        </select>
                    </div>

                    <div className="table-wrap">
                        <table className="app-table">
                            <thead>
                                <tr>
                                    <th onClick={() => handleSort('name')} style={{ cursor: 'pointer' }}>Nome{sortIcon('name')}</th>
                                    <th>Modalidades</th>
                                    <th onClick={() => handleSort('validation')} style={{ cursor: 'pointer' }}>Validação{sortIcon('validation')}</th>
                                    <th onClick={() => handleSort('status')} style={{ cursor: 'pointer' }}>Estado{sortIcon('status')}</th>
                                    <th></th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredSorted.length === 0 ? (
                                    <tr><td colSpan={5} className="table-empty">Nenhum estudante encontrado.</td></tr>
                                ) : (
                                    filteredSorted.map((s) => {
                                        const expanded = expandedIds.has(s.studentId)
                                        const p = s.personInfo ?? {}
                                        return (
                                            <>
                                                <tr
                                                    key={s.studentId}
                                                    className={`student-row${expanded ? ' expanded' : ''}`}
                                                    onClick={e => toggleExpand(s.studentId, e)}
                                                    title="Clique para ver detalhes"
                                                >
                                                    <td>
                                                        <span style={{ marginRight: 6, color: 'var(--text-3)', fontSize: '0.8rem' }}>
                                                            {expanded ? '▲' : '▼'}
                                                        </span>
                                                        {getName(s)}
                                                    </td>

                                                    <td>
                                                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                                                            {(s.modalities ?? []).map(m => (
                                                                <span key={m.modalityId ?? m.ModalityId} className="mc-tag" style={{ fontSize: '0.75rem' }}>
                                                                    {m.name}
                                                                </span>
                                                            ))}
                                                        </div>
                                                    </td>

                                                    <td><ValidationBadge status={s.acceptanceStatus} /></td>

                                                    <td>
                                                        <div className="status-cell">
                                                            {s.acceptanceStatus === 1 && <StatusBadge active={s.isActive} />}
                                                            {s.acceptanceStatus === 1 && (
                                                                s.isActive
                                                                    ? <Button variant="danger" onClick={e => handleDeactivate(s.studentId, e)}>Desativar</Button>
                                                                    : <Button variant="secondary" onClick={e => handleActivate(s.studentId, e)}>Reativar</Button>
                                                            )}
                                                        </div>
                                                    </td>

                                                    <td>
                                                        <Button variant="secondary" onClick={e => openEditModal(s, e)}>Editar</Button>
                                                    </td>
                                                </tr>

                                                {expanded && (
                                                    <tr key={`${s.studentId}-detail`} className="student-detail-row">
                                                        <td colSpan={5}>
                                                            <div className="student-detail-panel">
                                                                <div className="student-detail-field">
                                                                    <label>Data de Nascimento</label>
                                                                    <span>{fmtDate(p.birthDate)}</span>
                                                                </div>
                                                                <div className="student-detail-field">
                                                                    <label>Morada</label>
                                                                    <span>{p.address || '—'}</span>
                                                                </div>
                                                                <SensitiveField label="NIF" value={p.nif} />
                                                                <SensitiveField label="Telefone" value={p.phone} />
                                                                <div className="student-detail-field">
                                                                    <label>Encarregado</label>
                                                                    <span>{s.parentName || '—'}</span>
                                                                </div>
                                                                <div className="student-detail-field">
                                                                    <label>Email do Encarregado</label>
                                                                    <span>{s.parentEmail || '—'}</span>
                                                                </div>
                                                            </div>
                                                        </td>
                                                    </tr>
                                                )}
                                            </>
                                        )
                                    })
                                )}
                            </tbody>
                        </table>
                    </div>
                </>
            )}

            <Modal open={showRejectModal} title="Rejeitar Estudante" onClose={() => setShowRejectModal(false)}>
                <p>Indique o motivo da rejeição:</p>
                <Input type="text" value={rejectReason} placeholder="Motivo da rejeição..." onChange={setRejectReason} />
                <div className="modal-actions">
                    <Button variant="secondary" onClick={() => setShowRejectModal(false)}>Cancelar</Button>
                    <Button variant="danger" onClick={confirmReject}>Confirmar Rejeição</Button>
                </div>
            </Modal>

            <AddStudentModal
                open={showEditModal}
                title="Editar Estudante"
                description="Altere os dados do estudante e as modalidades atribuídas."
                confirmLabel="Guardar Alterações"
                loadingLabel="A guardar..."
                onClose={() => { setShowEditModal(false); setEditingStudent(null) }}
                onConfirm={handleEditSubmit}
                firstName={editFirstName}
                lastName={editLastName}
                birthDate={editBirthDate}
                phone={editPhone}
                address={editAddress}
                nif={editNif}
                modalities={modalities}
                selectedModalityIds={editModalityIds}
                onModalityToggle={toggleEditModality}
                onFirstNameChange={setEditFirstName}
                onLastNameChange={setEditLastName}
                onBirthDateChange={setEditBirthDate}
                onPhoneChange={setEditPhone}
                onAddressChange={setEditAddress}
                onNifChange={setEditNif}
                error={editError}
                loading={editLoading}
            />
        </section>
    )
}

export default StaffValidateStudentsPage