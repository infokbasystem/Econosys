import { useCallback, useEffect, useRef, useState } from 'react';

const EMPTY_MESSAGES = [];
const INFO_PANEL_WIDE_QUERY = '(min-width: 96rem)';

const useOrderDetailState = ({ initialInfoPanelExpanded } = {}) => {
    const [messages, setStoredMessages] = useState(EMPTY_MESSAGES);
    const [isWideScreen, setIsWideScreen] = useState(() => window.matchMedia(INFO_PANEL_WIDE_QUERY).matches);
    const [infoPanelExpandedOverride, setInfoPanelExpandedOverride] = useState(initialInfoPanelExpanded ?? null);
    const isInfoPanelExpanded = infoPanelExpandedOverride ?? isWideScreen;
    const infoPanelAutoCloseRef = useRef(null);
    const messagesRef = useRef(EMPTY_MESSAGES);
    const isInfoPanelExpandedRef = useRef(isInfoPanelExpanded);

    useEffect(() => {
        const mediaQuery = window.matchMedia(INFO_PANEL_WIDE_QUERY);
        const handleChange = (event) => setIsWideScreen(event.matches);
        mediaQuery.addEventListener('change', handleChange);
        return () => mediaQuery.removeEventListener('change', handleChange);
    }, []);

    useEffect(() => {
        isInfoPanelExpandedRef.current = isInfoPanelExpanded;
    }, [isInfoPanelExpanded]);

    useEffect(() => () => {
        if (infoPanelAutoCloseRef.current) {
            clearTimeout(infoPanelAutoCloseRef.current);
        }
    }, []);

    const setMessages = useCallback((update) => {
        const nextMessages = typeof update === 'function' ? update(messagesRef.current) : update;
        const resolvedMessages = Array.isArray(nextMessages) ? nextMessages : [];
        messagesRef.current = resolvedMessages;
        setStoredMessages(resolvedMessages);

        const latestMessage = resolvedMessages[resolvedMessages.length - 1];
        if (!latestMessage) return;
        if (infoPanelAutoCloseRef.current) {
            clearTimeout(infoPanelAutoCloseRef.current);
            infoPanelAutoCloseRef.current = null;
        }

        const wasExpanded = isInfoPanelExpandedRef.current;
        isInfoPanelExpandedRef.current = true;
        setInfoPanelExpandedOverride(true);

        const hasIssue = latestMessage.type === 'error' || latestMessage.type === 'warning';
        if (!hasIssue && !wasExpanded) {
            infoPanelAutoCloseRef.current = setTimeout(() => {
                isInfoPanelExpandedRef.current = false;
                setInfoPanelExpandedOverride(false);
                infoPanelAutoCloseRef.current = null;
            }, 2000);
        }
    }, []);

    const toggleInfoPanel = useCallback(() => {
        setInfoPanelExpandedOverride((previousOverride) => {
            const next = !(previousOverride ?? isWideScreen);
            isInfoPanelExpandedRef.current = next;
            return next;
        });
    }, [isWideScreen]);

    return {
        messages,
        setMessages,
        isInfoPanelExpanded,
        isInfoPanelUsingResponsiveDefault: infoPanelExpandedOverride === null,
        toggleInfoPanel,
    };
};

export default useOrderDetailState;