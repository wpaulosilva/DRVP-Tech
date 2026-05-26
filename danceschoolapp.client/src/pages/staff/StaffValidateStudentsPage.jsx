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

function StaffValidateStudentsPage() {
    const [tab, setTab] = useState('pending')
    const [students, setStudents] = useState([])
    const [loading, setLoading] = useState(true)
    const [sortBy, setSortBy] = useState('name')
    const [sortDir, setSortDir] = useState('asc')

    const [showRejectModal, setShowRejectModal] = useState(false)
    const [rejectReason, setRejectReason] = useState('')
    const [selectedStudentId, setSelectedStudentId] = useState(null)

    // Edit student modal state
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

    useEffect(() => {
        fetchStudents()
    }, [tab])

    useEffect(() => {
        getModalities().then(data => {
            const list = Array.isArray(data) ? data : (data?.items ?? data?.Items ?? [])
            setModalities(list.filter(m => m.isActive ?? m.IsActive ?? true))
        }).catch(() => {})
    }, [])

    const openEditModal = async (student) => {
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
            const existingIds = (data.modalities ?? []).map(m => m.modalityId ?? m.ModalityId)
            setEditModalityIds(existingIds)
            setShowEditModal(true)
        } catch (err) {
            alert(err.message)
            setEditingStudent(null)
        } finally {
            setEditLoading(false)
        }
    }

    const handleEditSubmit = async () => {
        if (!editFirstName.trim() || !editLastName.trim()) {
            setEditError('Nome e apelido são obrigatórios.')
            return
        }
        if (!editBirthDate) {
            setEditError('Data de nascimento é obrigatória.')
            return
        }
        if (editNif && editNif.length !== 9) {
            setEditError('NIF deve ter 9 dígitos.')
            return
        }
        setEditLoading(true)
        setEditError('')
        try {
            await updateStudent(editingStudent.studentId, {
                parentId:    editingStudent.parentId ?? 0,
                firstName:   editFirstName.trim(),
                lastName:    editLastName.trim(),
                birthDate:   editBirthDate,
                phone:       editPhone.trim() || null,
                address:     editAddress.trim() || null,
                nif:         editNif.trim() || null,
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

    const toggleEditModality = (id) => {
        setEditModalityIds(prev =>
            prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
        )
    }

    const handleSort = (field) => {
        if (sortBy === field) {
            setSortDir((current) => current === 'asc' ? 'desc' : 'asc')
        } else {
            setSortBy(field)
            setSortDir('asc')
        }
    }

    const renderSortIcon = (field) => {
        if (sortBy !== field) return ''
        return sortDir === 'asc' ? ' ▲' : ' ▼'
    }

    const handleAccept = async (id) => {
        await acceptStudent(id)
        fetchStudents()
    }

    const openRejectModal = (id) => {
        setSelectedStudentId(id)
        setRejectReason('')
        setShowRejectModal(true)
    }

    const confirmReject = async () => {
        if (!rejectReason.trim()) return

        await rejectStudent(selectedStudentId, rejectReason.trim())

        setShowRejectModal(false)
        setSelectedStudentId(null)
        setRejectReason('')
        fetchStudents()
    }

    const handleDeactivate = async (id) => {
        if (!window.confirm('Desativar este estudante?')) return
        await deactivateStudent(id)
        fetchStudents()
    }

    const handleActivate = async (id) => {
        if (!window.confirm('Reativar este estudante?')) return
        await activateStudent(id)
        fetchStudents()
    }

    const getName = (s) => {
        const first = s.firstName ?? s.personInfo?.firstName ?? ''
        const last = s.lastName ?? s.personInfo?.lastName ?? ''
        return `${first} ${last}`.trim() || '—'
    }

    const getValidation = (status) => {
        if (status === 0) return 'Pendente'
        if (status === 1) return 'Aceite'
        if (status === 2) return 'Rejeitado'
        return '—'
    }

    const shouldShowActiveStatus = (s) => s.acceptanceStatus === 1

    const sortedStudents = useMemo(() => {
        return [...students].sort((a, b) => {
            let valueA = ''
            let valueB = ''

            if (sortBy === 'name') {
                valueA = getName(a).toLowerCase()
                valueB = getName(b).toLowerCase()
            }

            if (sortBy === 'status') {
                valueA = a.acceptanceStatus === 1 ? (a.isActive ? 'ativo' : 'inativo') : ''
                valueB = b.acceptanceStatus === 1 ? (b.isActive ? 'ativo' : 'inativo') : ''
            }

            if (sortBy === 'validation') {
                valueA = getValidation(a.acceptanceStatus).toLowerCase()
                valueB = getValidation(b.acceptanceStatus).toLowerCase()
            }

            if (valueA < valueB) return sortDir === 'asc' ? -1 : 1
            if (valueA > valueB) return sortDir === 'asc' ? 1 : -1
            return 0
        })
    }, [students, sortBy, sortDir])

    return (
        <section className="dashboard-page-card">
            <h2>Estudantes</h2>
            <p>Aprovar ou gerir estudantes introduzidos pelos encarregados.</p>

            <div className="staff-tabs">
                <button
                    className={tab === 'pending' ? 'staff-tab active' : 'staff-tab'}
                    onClick={() => setTab('pending')}
                >
                    Pendentes
                </button>

                <button
                    className={tab === 'all' ? 'staff-tab active' : 'staff-tab'}
                    onClick={() => setTab('all')}
                >
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
                                    <p><strong>Modalidades:</strong> {(s.modalities).join(', ')}</p>
                                )}

                                <div className="validate-actions">
                                    <Button
                                        variant="secondary"
                                        onClick={() => openRejectModal(s.studentId)}
                                    >
                                        Rejeitar
                                    </Button>

                                    <Button
                                        variant="primary"
                                        onClick={() => handleAccept(s.studentId)}
                                    >
                                        Aceitar
                                    </Button>
                                </div>
                            </div>
                        ))}
                    </div>
                )
            ) : (
                <div className="table-wrap">
                    <table className="app-table">
                        <thead>
                            <tr>
                                <th onClick={() => handleSort('name')} style={{ cursor: 'pointer' }}>
                                    Nome{renderSortIcon('name')}
                                </th>

                                <th onClick={() => handleSort('status')} style={{ cursor: 'pointer' }}>
                                    Estado{renderSortIcon('status')}
                                </th>

                                <th onClick={() => handleSort('validation')} style={{ cursor: 'pointer' }}>
                                    Validação{renderSortIcon('validation')}
                                </th>

                                <th>Ações</th>
                                <th></th>
                            </tr>
                        </thead>

                        <tbody>
                            {sortedStudents.length === 0 ? (
                                <tr>
                                    <td colSpan={5} className="table-empty">
                                        Nenhum estudante encontrado.
                                    </td>
                                </tr>
                            ) : (
                                sortedStudents.map((s) => (
                                    <tr key={s.studentId}>
                                        <td>{getName(s)}</td>

                                        <td>
                                            {shouldShowActiveStatus(s) ? (
                                                <StatusBadge active={s.isActive} />
                                            ) : (
                                                <span className="muted-status">—</span>
                                            )}
                                        </td>

                                        <td>{getValidation(s.acceptanceStatus)}</td>

                                        <td>
                                            {s.acceptanceStatus === 1 && (
                                                s.isActive ? (
                                                    <Button
                                                        variant="danger"
                                                        onClick={() => handleDeactivate(s.studentId)}
                                                    >
                                                        Desativar
                                                    </Button>
                                                ) : (
                                                    <Button
                                                        variant="secondary"
                                                        onClick={() => handleActivate(s.studentId)}
                                                    >
                                                        Reativar
                                                    </Button>
                                                )
                                            )}
                                        </td>
                                        <td>
                                            <Button
                                                variant="secondary"
                                                onClick={() => openEditModal(s)}
                                            >
                                                Editar
                                            </Button>
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            )}

            <Modal
                open={showRejectModal}
                title="Rejeitar Estudante"
                onClose={() => setShowRejectModal(false)}
            >
                <p>Indique o motivo da rejeição:</p>

                <Input
                    type="text"
                    value={rejectReason}
                    placeholder="Motivo da rejeição..."
                    onChange={setRejectReason}
                />

                <div className="modal-actions">
                    <Button variant="secondary" onClick={() => setShowRejectModal(false)}>
                        Cancelar
                    </Button>

                    <Button variant="danger" onClick={confirmReject}>
                        Confirmar Rejeição
                    </Button>
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