import { useEffect, useState } from 'react'
import {
    getValidateStudents,
    acceptStudent,
    rejectStudent
} from '@/services/studentsService'
import Button from '@/components/common/Button'

function StaffValidateStudentsPage() {
    const [students, setStudents] = useState([])
    const [loading, setLoading] = useState(true)

    const fetchStudents = async () => {
        setLoading(true)

        try {
            const data = await getValidateStudents({ status: 'pending' })
            setStudents(data.items ?? [])
        } catch {
            setStudents([])
        } finally {
            setLoading(false)
        }
    }

    useEffect(() => {
        fetchStudents()
    }, [])

    const handleAccept = async (id) => {
        await acceptStudent(id)
        fetchStudents()
    }

    const handleReject = async (id) => {
        const reason = prompt('Motivo da rejeição:')
        if (!reason) return

        await rejectStudent(id, reason)
        fetchStudents()
    }

    return (
        <section className="dashboard-page-card">
            <h2>Validar Estudantes</h2>
            <p>Aprovar ou recusar estudantes introduzidos pelos encarregados.</p>

            {loading ? (
                <p>A carregar...</p>
            ) : students.length === 0 ? (
                <p>Sem estudantes pendentes.</p>
            ) : (
                <div className="validate-students-grid">
                    {students.map((s) => (
                        <div key={s.studentId} className="validate-card">
                            <h3>{s.firstName} {s.lastName}</h3>

                            <p><strong>Data Nascimento:</strong> {s.birthDate}</p>
                            <p><strong>NIF:</strong> {s.nif}</p>
                            <p><strong>Telefone:</strong> {s.phone}</p>
                            <p><strong>Morada:</strong> {s.address}</p>

                            <p><strong>Encarregado:</strong> {s.parentName}</p>
                            <p><strong>Email:</strong> {s.parentEmail}</p>

                            <div className="validate-actions">
                                <Button
                                    variant="secondary"
                                    onClick={() => handleReject(s.studentId)}
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
            )}
        </section>
    )
}

export default StaffValidateStudentsPage