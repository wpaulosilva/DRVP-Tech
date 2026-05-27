import AgendaCalendar from '../../components/common/AgendaCalendar'
import { getCoachAgenda } from '../../services/coachClassesService'

const fetchAgenda = (from, to) => getCoachAgenda({ from, to })

function CoachAgendaPage() {
    return (
        <AgendaCalendar
            title="Minha Agenda"
            subtitle="Visualize os seus coachings agendados para a semana."
            fetchFn={fetchAgenda}
        />
    )
}

export default CoachAgendaPage