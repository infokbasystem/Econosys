import { useEffect, useId, useState } from 'react'

export default function useSearchKeyboardNavigation({ matches, isOpen, setIsOpen, popupRef, onPick }) {
    const listId = useId()
    const [activeIndex, setActiveIndex] = useState(-1)

    useEffect(() => {
        if (isOpen && activeIndex >= 0) {
            popupRef.current?.querySelector(`[data-search-option-index="${activeIndex}"]`)
                ?.scrollIntoView({ block: 'nearest' })
        }
    }, [activeIndex, isOpen, popupRef])

    function handleKeyDown(event) {
        if (event.nativeEvent.isComposing) return

        if (event.key === 'Escape' && isOpen) {
            event.preventDefault()
            setIsOpen(false)
            setActiveIndex(-1)
        } else if ((event.key === 'ArrowDown' || event.key === 'ArrowUp') && matches.length > 0) {
            event.preventDefault()
            setIsOpen(true)
            setActiveIndex((current) => event.key === 'ArrowDown'
                ? (current + 1) % matches.length
                : (current <= 0 ? matches.length - 1 : current - 1))
        } else if (event.key === 'Enter' && isOpen && activeIndex >= 0 && activeIndex < matches.length) {
            event.preventDefault()
            onPick(matches[activeIndex])
        }
    }

    return { listId, activeIndex, setActiveIndex, handleKeyDown }
}
