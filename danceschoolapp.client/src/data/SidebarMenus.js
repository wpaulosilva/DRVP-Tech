export const sidebarMenus = {
    admin: [
        { label: 'Dashboard',     to: '/admin',               icon: 'dashboard'  },
        { label: 'Utilizadores', to: '/admin/utilizadores', icon: 'direction' },
        { label: 'Configurações', to: '/admin/configuracoes', icon: 'info' },
    ],

    staff: [
        { label: 'Dashboard',     to: '/staff',                       icon: 'dashboard'    },
        { label: 'Utilizadores',  to: '/staff/utilizadores',          icon: 'users'        },
        { label: 'Estudantes',    to: '/staff/validar-estudantes',    icon: 'students'     },
        { label: 'Coachings',     to: '/staff/validar-aulas',         icon: 'validate'     },
        { label: 'Modalidades',   to: '/staff/modalidades',           icon: 'modalities'   },
        { label: 'Estúdios',      to: '/staff/estudios',              icon: 'studios'      },
        { label: 'Eventos',       to: '/staff/eventos',               icon: 'events'       },
        { label: 'Inventário',    to: '/staff/inventario',            icon: 'inventory'    },
        { label: 'Agenda Global', to: '/staff/agenda',                icon: 'agenda'       },
        { label: 'Faturação',     to: '/staff/faturacao',             icon: 'billing'      },
        { label: 'Bloqueios',     to: '/staff/bloqueios',             icon: 'blocked'      },
    ],

    coach: [
        { label: 'Dashboard',      to: '/coach',                  icon: 'dashboard'    },
        { label: 'Disponibilidade', to: '/coach/disponibilidade', icon: 'availability' },
        { label: 'Validar Coachings',   to: '/coach/validar-aulas',  icon: 'validate'     },
        { label: 'Agenda',          to: '/coach/agenda',          icon: 'agenda'       },
        { label: 'Eventos',         to: '/coach/eventos',         icon: 'events'       },
    ],

    parent: [
        { label: 'Dashboard',       to: '/parent',               icon: 'dashboard'  },
        { label: 'Coachings',       to: '/parent/aulas',         icon: 'classes'    },
        { label: 'Meus Estudantes', to: '/parent/estudantes',    icon: 'students'   },
        { label: 'Inventário',      to: '/parent/inventario',    icon: 'inventory'  },
        { label: 'Eventos',         to: '/parent/eventos',       icon: 'events'     },
    ],
}
