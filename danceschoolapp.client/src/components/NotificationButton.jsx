import React, { useEffect, useState } from 'react'
import NotificationModal from './NotificationModal'
import { useAuth } from '../context/useAuth'
import { get } from '../api/client'

export default function NotificationButton() {
  const { user } = useAuth()
  const userId = user?.UserId ?? user?.userId ?? user?.id ?? null

  const [unreadCount, setUnreadCount] = useState(0)
  const [open, setOpen] = useState(false)

  // Fetch just enough to get TotalUnread — page 1, pageSize 1.
  // The API returns TotalUnread as a top-level field regardless of pageSize.
  const fetchUnread = async () => {
    if (!userId) { setUnreadCount(0); return }
    try {
      const data = await get(`/api/notifications/user/${userId}?page=1&pageSize=1`)
      setUnreadCount(data.TotalUnread ?? data.totalUnread ?? 0)
    } catch {
      // silent — badge just won't update
    }
  }

  useEffect(() => {
    let mounted = true
    let interval = null

    const run = async () => {
      if (!mounted) return
      await fetchUnread()
      if (!mounted) return
      interval = setInterval(fetchUnread, 60_000)
    }
    run()

    return () => {
      mounted = false
      clearInterval(interval)
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId])

  const handleClose = () => {
    setOpen(false)
    // Re-check unread count after user dismisses the modal
    setTimeout(fetchUnread, 200)
  }

  return (
    <>
      <div style={{ position: 'relative', marginLeft: 12 }}>
        <button
          aria-label="Notificações"
          onClick={() => setOpen(true)}
          className="notification-btn"
          style={{
            width: 44,
            height: 44,
            borderRadius: 10,
            background: 'var(--surface)',
            border: '1px solid var(--border)',
            boxShadow: 'var(--shadow-sm)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
            position: 'relative',
            color: 'var(--text-2)',
            transition: 'background 0.15s',
          }}
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M15 17H9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <path d="M12 22a2.5 2.5 0 0 0 2.5-2.5h-5A2.5 2.5 0 0 0 12 22z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <path d="M18 8a6 6 0 1 0-12 0c0 7-3 8-3 8h18s-3-1-3-8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>

          {unreadCount > 0 && (
            <span
              style={{
                position: 'absolute',
                top: -4,
                right: -4,
                background: 'var(--danger)',
                color: '#fff',
                borderRadius: 999,
                minWidth: 18,
                height: 18,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: 11,
                padding: '0 5px',
                boxShadow: '0 2px 6px rgba(0,0,0,0.15)',
                pointerEvents: 'none',
              }}
            >
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
        </button>
      </div>

      {open && (
        <NotificationModal
          userId={userId}
          onClose={handleClose}
          onUnreadChange={setUnreadCount}
        />
      )}
    </>
  )
}
