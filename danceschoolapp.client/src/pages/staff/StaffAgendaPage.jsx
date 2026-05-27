import AgendaCalendar from '../../components/common/AgendaCalendar'
import { getAgenda } from '../../services/staffService'

const fetchAgenda = (from, to) => getAgenda({ from, to })

function StaffAgendaPage() {
    return (
        <AgendaCalendar
            title="Agenda Global"
            subtitle="Visualize todas as aulas marcadas na semana."
            fetchFn={fetchAgenda}
            showCoach
        />
    )
}

export default StaffAgendaPage