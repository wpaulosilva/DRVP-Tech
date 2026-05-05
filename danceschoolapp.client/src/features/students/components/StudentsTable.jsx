import StatusBadge from '../../../components/common/StatusBadge'
import Button from '../../../components/common/Button'

function StudentsTable({ students, loading, onEditRejected }) {
    const getName = (student) => {
        const firstName =
            student.firstName ??
            student.personInfo?.firstName ??
            ''

        const lastName =
            student.lastName ??
            student.personInfo?.lastName ??
            ''

        return `${firstName} ${lastName}`.trim() || '—'
    }

    const getStatusLabel = (status) => {
        if (status === 0) return 'Pendente'
        if (status === 1) return 'Aceite'
        if (status === 2) return 'Rejeitado'
        return '—'
    }

    return (
        <div className="table-wrap">
            <table className="app-table">
                <thead>
                    <tr>
                        <th>Nome</th>
                        <th>Estado</th>
                        <th>Validação</th>
                        <th>Ações</th>
                    </tr>
                </thead>

                <tbody>
                    {loading ? (
                        <tr>
                            <td colSpan={4} className="table-empty">
                                A carregar...
                            </td>
                        </tr>
                    ) : students.length === 0 ? (
                        <tr>
                            <td colSpan={4} className="table-empty">
                                Nenhum estudante encontrado.
                            </td>
                        </tr>
                    ) : (
                        students.map((student) => (
                            <tr key={student.studentId}>
                                <td>{getName(student)}</td>

                                <td>
                                    {student.acceptanceStatus === 1 ? (
                                        <StatusBadge active={student.isActive} />
                                    ) : (
                                        <span className="muted-status">—</span>
                                    )}
                                </td>

                                <td>{getStatusLabel(student.acceptanceStatus)}</td>

                                <td>
                                    {student.acceptanceStatus === 2 && (
                                        <Button
                                            variant="secondary"
                                            onClick={() => onEditRejected?.(student)}
                                        >
                                            Editar / Reenviar
                                        </Button>
                                    )}
                                </td>
                            </tr>
                        ))
                    )}
                </tbody>
            </table>
        </div>
    )
}

export default StudentsTable