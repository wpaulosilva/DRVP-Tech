export const sidebarMenus = {
    admin: [
        { label: 'Dashboard', to: '/admin' },
        { label: 'Utilizadores', to: '/admin/utilizadores' },
    ],

    staff: [
        { label: 'Dashboard', to: '/staff' },
        { label: 'Utilizadores', to: '/staff/utilizadores' },
        { label: 'Estudantes', to: '/staff/validar-estudantes' },
        { label: 'Aulas', to: '/staff/validar-aulas' },
        { label: 'Modalidades', to: '/staff/modalidades' },
        { label: 'Estúdios', to: '/staff/estudios' },
        { label: 'Eventos', to: '/staff/eventos' },
        { label: 'Inventário', to: '/staff/inventario' },
        { label: 'Agenda Global', to: '/staff/agenda' },
        { label: 'Faturação', to: '/staff/faturacao' },
        { label: 'Bloqueios', to: '/staff/bloqueios' },
    ],

    coach: [
        { label: 'Dashboard', to: '/coach' },
        { label: 'Disponibilidade', to: '/coach/disponibilidade' },
        { label: 'Validar Aulas', to: '/coach/validar-aulas' },
        { label: 'Agenda', to: '/coach/agenda' },
        { label: 'Eventos', to: '/coach/eventos' },
    ],

    parent: [
        { label: 'Dashboard', to: '/parent' },
        { label: 'Aulas', to: '/parent/aulas' },
        { label: 'Meus Estudantes', to: '/parent/estudantes' },
        { label: 'Inventário', to: '/parent/inventario' },
        { label: 'Eventos', to: '/parent/eventos' },
    ],
}
