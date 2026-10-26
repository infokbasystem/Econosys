import { useLayoutEffect } from 'react'

export default function useSearchPopupPosition(isOpen, containerRef, popupRef) {
    useLayoutEffect(() => {
        if (!isOpen) return

        function updatePosition() {
            const anchor = containerRef.current
            const popup = popupRef.current
            if (!anchor || !popup) return

            const rect = anchor.getBoundingClientRect()
            const margin = 8
            const width = Math.min(400, document.documentElement.clientWidth - margin * 2)
            const maxHeight = Math.min(360, window.innerHeight - margin * 2)
            const preferredLeft = rect.right + margin + width <= document.documentElement.clientWidth - margin
                ? rect.right + margin
                : rect.left - margin - width
            const left = Math.max(margin, Math.min(preferredLeft, document.documentElement.clientWidth - width - margin))
            const top = Math.max(margin, Math.min(rect.top, window.innerHeight - maxHeight - margin))

            Object.assign(popup.style, {
                left: `${left}px`,
                top: `${top}px`,
                width: `${width}px`,
                maxHeight: `${maxHeight}px`,
            })
        }

        updatePosition()
        window.addEventListener('resize', updatePosition)
        window.addEventListener('scroll', updatePosition, true)
        const observer = new ResizeObserver(updatePosition)
        observer.observe(containerRef.current)

        return () => {
            window.removeEventListener('resize', updatePosition)
            window.removeEventListener('scroll', updatePosition, true)
            observer.disconnect()
        }
    }, [isOpen, containerRef, popupRef])
}
