import React, { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import { get, patch, del } from '../api/client'
import './notification.css'

const PAGE_SIZE = 20

// Human-readable labels for each EntityType value from the API
const ENTITY_LABELS = {
  coachclass:       'Coachings',
  itemrequisition:  'Inventário',
  student:          'Utilizadores',
}

// Destination route for a notification based on EntityType + current user's roles
function resolveLink(entityType, roles) {
  if (!entityType) return null
  const et = entityType.toLowerCase()
  if (et === 'coachclass') {
    if (roles.includes('coach'))  return { path: '/coach/validar-aulas', label: 'Validar Coachings' }
    if (roles.includes('parent')) return { path: '/parent/aulas', label: 'Ver Coachings' }
    if (roles.includes('staff') || roles.includes('admin')) return { path: '/staff/validar-aulas', label: 'Validar Coachings' }
  }
  if (et === 'itemrequisition') {
    if (roles.includes('parent'))                        return { path: '/parent/inventario',         label: 'Inventário' }
    if (roles.includes('staff') || roles.includes('admin')) return { path: '/staff/inventario',      label: 'Inventário' }
  }
  if (et === 'student') {
    if (roles.includes('staff') || roles.includes('admin')) return { path: '/staff/validar-estudantes', label: 'Validar Estudantes' }
    if (roles.includes('parent'))                        return { path: '/parent/estudantes',         label: 'Os Meus Estudantes' }
  }
  return null
}

export default function NotificationModal({ userId, onClose, onUnreadChange }) {
  const { user }  = useAuth()
  const navigate  = useNavigate()
  const roles     = (user?.Roles || user?.roles || []).map(r => r.toString().toLowerCase())

  const [items,       setItems]       = useState([])
  const [loading,     setLoading]     = useState(false)
  const [page,        setPage]        = useState(1)
  const [hasMore,     setHasMore]     = useState(false)
  const [tab,         setTab]         = useState('all')
  const [expandedId,  setExpandedId]  = useState(null)
  const [markingAll,  setMarkingAll]  = useState(false)

  // Stable ref so onClose is never a useEffect dependency
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  //  Data fetching 

  const fetchPage = useCallback(async (pageNum) => {
    if (!userId) return
    setLoading(true)
    try {
      const data = await get(`/api/notifications/user/${userId}?page=${pageNum}&pageSize=${PAGE_SIZE}`)
      const list  = Array.isArray(data) ? data : (data.Items ?? data.items ?? [])

      // Replace backend text occurrences of 'aula'/'aulas' with 'coaching'/'coachings' for UI consistency
      const replaceAula = (s) => {
        if (!s || typeof s !== 'string') return s

        // Replace common feminine articles + 'aula(s)' -> masculine equivalents
        // e.g. 'a aula' -> 'o coaching', 'As aulas' -> 'Os coachings'
        s = s.replace(/\b([Aa]|[Aa]s)\s+(aula|aulas)\b/g, (match, art, noun) => {
          const isPlural = noun.toLowerCase() === 'aulas'
          const artIsCapital = art[0] === art[0].toUpperCase()
          const repArticle = artIsCapital ? (isPlural ? 'Os' : 'O') : (isPlural ? 'os' : 'o')
          const repNoun = isPlural ? 'coachings' : 'coaching'
          return `${repArticle} ${repNoun}`
        })

        // Replace remaining standalone 'aula'/'aulas' → 'coaching'/'coachings', preserving case
        s = s.replace(/\b(aulas|aula)\b/gi, (m) => {
          const lower = m.toLowerCase()
          const rep = lower === 'aulas' ? 'coachings' : 'coaching'
          if (m[0] === m[0].toUpperCase()) return rep.charAt(0).toUpperCase() + rep.slice(1)
          return rep
        })

        // Convert common feminine adjective forms to masculine (singular and plural)
        const genderMap = {
          'aprovada': 'aprovado', 'aprovadas': 'aprovados',
          'confirmada': 'confirmado', 'confirmadas': 'confirmados',
          'cancelada': 'cancelado', 'canceladas': 'cancelados',
          'marcada': 'marcado', 'marcadas': 'marcados',
          'realizada': 'realizado', 'realizadas': 'realizados',
          'agendada': 'agendado', 'agendadas': 'agendados',
          'solicitada': 'solicitado', 'solicitadas': 'solicitados',
          'requisitada': 'requisitado', 'requisitadas': 'requisitados'
        }
        s = s.replace(/\b(aprovadas|aprovada|confirmadas|confirmada|canceladas|cancelada|marcadas|marcada|realizadas|realizada|agendadas|agendada|solicitadas|solicitada|requisitadas|requisitada)\b/gi, (m) => {
          const lower = m.toLowerCase()
          const rep = genderMap[lower] ?? m
          // preserve capitalization
          if (m[0] === m[0].toUpperCase()) return rep.charAt(0).toUpperCase() + rep.slice(1)
          return rep
        })

        return s
      }

      const mapped = list.map(n => ({
        id:          n.NotificationId ?? n.notificationId ?? n.id ?? null,
        title:       replaceAula(n.Title   ?? n.title   ?? ''),
        message:     replaceAula(n.Message ?? n.message ?? ''),
        createdAt:   n.CreatedAt ?? n.createdAt ?? null,
        displayDate: (n.CreatedAt ?? n.createdAt)
                       ? new Date(n.CreatedAt ?? n.createdAt).toLocaleString('pt-PT')
                       : '',
        isRead:      n.IsRead ?? n.isRead ?? !!(n.ReadAt ?? n.readAt),
        entityType:  n.EntityType ?? n.entityType ?? null,
        entityId:    n.EntityId   ?? n.entityId   ?? null,
      }))

      setItems(prev => pageNum === 1 ? mapped : [...prev, ...mapped])
      setHasMore(data.HasNext ?? data.hasNext ?? false)

      const totalUnread = data.TotalUnread ?? data.totalUnread
      if (totalUnread !== undefined && onUnreadChange) onUnreadChange(totalUnread)
    } catch (err) {
      console.error('Erro fetchPage', err)
    } finally {
      setLoading(false)
    }
  }, [userId, onUnreadChange])

  useEffect(() => { fetchPage(1) }, [fetchPage])

  useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') onCloseRef.current() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  //  Actions 

  const loadMore = () => {
    const next = page + 1
    setPage(next)
    fetchPage(next)
  }

  const markAsRead = async (id) => {
    try {
      await patch(`/api/notifications/${id}/read`)
      setItems(prev => prev.map(n => n.id === id ? { ...n, isRead: true } : n))
      if (onUnreadChange) onUnreadChange(c => Math.max(0, c - 1))
    } catch (err) { console.error('Erro markAsRead', err) }
  }

  const markAllRead = async () => {
    if (!userId || markingAll) return
    setMarkingAll(true)
    try {
      await patch(`/api/notifications/user/${userId}/read-all`)
      setItems(prev => prev.map(n => ({ ...n, isRead: true })))
      if (onUnreadChange) onUnreadChange(0)
    } catch (err) { console.error('Erro markAllRead', err) }
    finally { setMarkingAll(false) }
  }

  const deleteNotification = async (e, id) => {
    e.stopPropagation()
    if (!id || !window.confirm('Deseja eliminar esta notificação?')) return
    try {
      await del(`/api/notifications/${id}`)
      const removed = items.find(n => n.id === id)
      setItems(prev => prev.filter(n => n.id !== id))
      if (expandedId === id) setExpandedId(null)
      if (!removed?.isRead && onUnreadChange) onUnreadChange(c => Math.max(0, c - 1))
    } catch (err) { console.error('Erro deleteNotification', err) }
  }

  const toggleExpand = (n) => {
    const isOpening = expandedId !== n.id
    setExpandedId(isOpening ? n.id : null)
    if (isOpening && !n.isRead) markAsRead(n.id)
  }

  const goToPage = (path) => {
    onCloseRef.current()
    navigate(path)
  }

  //  Derived tabs (dynamic — only show tabs for entity types that exist) 

  const entityTypesPresent = [...new Set(
    items.map(n => n.entityType?.toLowerCase()).filter(Boolean)
  )]
  const dynamicTabs = entityTypesPresent
    .map(et => ({ key: et, label: ENTITY_LABELS[et] }))
    .filter(t => t.label)   // only show tabs for known entity types

  // Reset to 'all' if current tab no longer has items
  const tabFiltered = items.filter(n =>
    tab === 'all' ? true : (n.entityType ?? '').toLowerCase() === tab
  )

  const unreadVisible = items.filter(n => !n.isRead).length

  //  Render 

  const content = (
    <div
      className="notif-backdrop"
      onMouseDown={(e) => { if (e.target === e.currentTarget) onCloseRef.current() }}
    >
      <div className="notif-modal" role="dialog" aria-modal="true">
        <div className="notif-topline" />

        <div className="notif-header">
          <h2>Notificações</h2>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            {unreadVisible > 0 && (
              <button
                className="mark-read"
                onClick={markAllRead}
                disabled={markingAll}
                style={{ fontSize: 12, padding: '6px 10px' }}
              >
                {markingAll ? 'A marcar...' : 'Marcar todas lidas'}
              </button>
            )}
            <button className="notif-close" onClick={() => onCloseRef.current()} aria-label="Fechar">✕</button>
          </div>
        </div>

        {/* Dynamic entity-type tabs */}
        {dynamicTabs.length > 0 && (
          <div className="notif-tabs">
            <button
              className={`pill ${tab === 'all' ? 'active' : ''}`}
              onClick={() => setTab('all')}
            >
              Todas
            </button>
            {dynamicTabs.map(t => (
              <button
                key={t.key}
                className={`pill ${tab === t.key ? 'active' : ''}`}
                onClick={() => setTab(t.key)}
              >
                {t.label}
              </button>
            ))}
          </div>
        )}

        <div className="notif-body">
          {loading && items.length === 0 && <div className="empty">A carregar...</div>}

          {!loading && tabFiltered.length === 0 && (
            <div className="empty">
              <svg width="48" height="48" viewBox="0 0 24 24" fill="none">
                <path d="M18 8a6 6 0 1 0-12 0c0 7-3 8-3 8h18s-3-1-3-8"
                  stroke="#9CA3AF" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round"/>
                <path d="M15 17H9" stroke="#9CA3AF" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round"/>
                <path d="M12 22a2.5 2.5 0 0 0 2.5-2.5h-5A2.5 2.5 0 0 0 12 22z"
                  stroke="#9CA3AF" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round"/>
              </svg>
              <div className="empty-title">Sem notificações</div>
            </div>
          )}

          {tabFiltered.length > 0 && (
            <div className="list" role="list">
              {tabFiltered.map((n, idx) => {
                const dest      = resolveLink(n.entityType, roles)
                const isExpanded = expandedId === n.id

                return (
                  <div
                    key={n.id ?? idx}
                    className={`notif-item ${n.isRead ? 'read' : 'unread'} notif-item--clickable`}
                    role="listitem"
                    onClick={() => toggleExpand(n)}
                  >
                    <div className="notif-main">
                      <div className="notif-title">{n.title}</div>

                      {/* Collapsed: truncated message preview */}
                      {!isExpanded && (
                        <div className="notif-message notif-message--preview">{n.message}</div>
                      )}

                      {/* Expanded: full message + optional navigation */}
                      {isExpanded && (
                        <div className="notif-detail" onClick={e => e.stopPropagation()}>
                          <p className="notif-detail-message">{n.message}</p>
                          {dest && (
                            <button
                              className="notif-goto-btn"
                              onClick={() => goToPage(dest.path)}
                            >
                              Ir para {dest.label} →
                            </button>
                          )}
                        </div>
                      )}
                    </div>

                    <div className="notif-meta">
                      <div className="notif-date">{n.displayDate}</div>
                      <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                        {!n.isRead && (
                          <button
                            className="mark-read"
                            onClick={(e) => { e.stopPropagation(); markAsRead(n.id) }}
                          >
                            Marcar lida
                          </button>
                        )}
                        <button
                          className="delete-btn"
                          onClick={(e) => deleteNotification(e, n.id)}
                          aria-label="Eliminar notificação"
                          title="Eliminar"
                        >
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                            <path d="M3 6h18" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                            <path d="M8 6v14a2 2 0 0 0 2 2h4a2 2 0 0 0 2-2V6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                            <path d="M10 11v6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                            <path d="M14 11v6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                            <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                          </svg>
                        </button>
                      </div>
                    </div>
                  </div>
                )
              })}

              {hasMore && (
                <div style={{ textAlign: 'center', padding: '8px 0' }}>
                  <button className="load-more-btn" onClick={loadMore} disabled={loading}>
                    {loading ? 'A carregar...' : 'Carregar mais'}
                  </button>
                </div>
              )}
            </div>
          )}
        </div>

        <div className="notif-footer">
          <button className="btn-close" onClick={() => onCloseRef.current()}>Fechar</button>
        </div>
      </div>
    </div>
  )

  return createPortal(content, document.body)
}
