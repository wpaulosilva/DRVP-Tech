import { useEffect, useState } from 'react'
import { useAuth } from '@/context/useAuth'
import {
    getMyStudents,
    createStudent,
    updateStudent,
    getStudent,
} from '@/services/studentsService'
import Button from '@/components/common/Button'
import AddStudentModal from '@/features/students/components/AddStudentModal'
import StudentsTable from '@/features/students/components/StudentsTable'
import '@/styles/AdminPage.css'

function ParentStudentsPage() {
    const { user } = useAuth()

    const [students, setStudents] = useState([])
    const [loadingStudents, setLoadingStudents] = useState(true)
    const [showModal, setShowModal] = useState(false)
    const [editingStudent, setEditingStudent] = useState(null)

    const [firstName, setFirstName] = useState('')
    const [lastName, setLastName] = useState('')
    const [birthDate, setBirthDate] = useState('')
    const [phone, setPhone] = useState('')
    const [address, setAddress] = useState('')
    const [nif, setNif] = useState('')
    const [error, setError] = useState('')
    const [loading, setLoading] = useState(false)

    const fetchStudents = async () => {
        if (!user?.userId) return

        setLoadingStudents(true)

        try {
            const data = await getMyStudents()
            setStudents(Array.isArray(data) ? data : [])
        } catch {
            setStudents([])
        } finally {
            setLoadingStudents(false)
        }
    }

    useEffect(() => {
        fetchStudents()
    }, [user])

    const resetForm = () => {
        setFirstName('')
        setLastName('')
        setBirthDate('')
        setPhone('')
        setAddress('')
        setNif('')
        setError('')
    }

    const openModal = () => {
        setEditingStudent(null)
        resetForm()
        setShowModal(true)
    }

    const openEditModal = async (student) => {
        setEditingStudent(student)
        setError('')
        setLoading(true)

        try {
            const data = await getStudent(student.studentId)

            setFirstName(data.personInfo?.firstName ?? '')
            setLastName(data.personInfo?.lastName ?? '')
            setBirthDate(data.personInfo?.birthDate ?? '')
            setPhone(data.personInfo?.phone ?? '')
            setAddress(data.personInfo?.address ?? '')
            setNif(data.personInfo?.nif ?? '')

            setShowModal(true)
        } catch (err) {
            alert(err.message)
            setEditingStudent(null)
        } finally {
            setLoading(false)
        }
    }

    const handleSubmit = async () => {
        // Nome
        if (!firstName.trim() || !lastName.trim()) {
            setError('Nome e apelido são obrigatórios.')
            return
        }

        // Data
        if (!birthDate) {
            setError('Data de nascimento é obrigatória.')
            return
        }

        // Data futura
        const today = new Date().toISOString().split('T')[0]
        if (birthDate > today) {
            setError('Data de nascimento não pode ser no futuro.')
            return
        }

        // NIF (opcional mas se tiver validar)
        if (nif && nif.length !== 9) {
            setError('NIF deve ter 9 dígitos.')
            return
        }

        setLoading(true)
        setError('')

        const payload = {
            parentId: user.userId,
            firstName: firstName.trim(),
            lastName: lastName.trim(),
            birthDate,
            phone: phone.trim() || null,
            address: address.trim() || null,
            nif: nif.trim() || null,
        }

        try {
            if (editingStudent) {
                await updateStudent(editingStudent.studentId, payload)
            } else {
                await createStudent(payload)
            }

            setShowModal(false)
            setEditingStudent(null)
            resetForm()
            fetchStudents()
        } catch (err) {
            setError(err.message)
        } finally {
            setLoading(false)
        }
    }

    return (
        <section className="dashboard-page-card">
            <div className="admin-page-header">
                <div>
                    <h2>Meus Estudantes</h2>
                    <p>Gerir informações dos estudantes a seu cargo.</p>
                </div>

                <Button variant="primary" onClick={openModal}>
                    Adicionar Estudante
                </Button>
            </div>

            <StudentsTable
                students={students}
                loading={loadingStudents}
                onEditRejected={openEditModal}
            />

            <AddStudentModal
                open={showModal}
                title={editingStudent ? 'Editar Estudante' : 'Adicionar Novo Estudante'}
                description={
                    editingStudent
                        ? 'Corrija os dados do estudante e envie novamente para validação.'
                        : 'Preencha as informações do estudante.'
                }
                confirmLabel={editingStudent ? 'Enviar Novamente' : 'Adicionar Estudante'}
                loadingLabel={editingStudent ? 'A enviar...' : 'A adicionar...'}
                onClose={() => {
                    setShowModal(false)
                    setEditingStudent(null)
                    resetForm()
                }}
                onConfirm={handleSubmit}
                firstName={firstName}
                lastName={lastName}
                birthDate={birthDate}
                phone={phone}
                address={address}
                nif={nif}
                onFirstNameChange={setFirstName}
                onLastNameChange={setLastName}
                onBirthDateChange={setBirthDate}
                onPhoneChange={setPhone}
                onAddressChange={setAddress}
                onNifChange={setNif}
                error={error}
                loading={loading}
            />
        </section>
    )
}

export default ParentStudentsPage