import React from 'react'
import { useEffect, useRef, useState } from 'react'

const defaultToolbarState = {
    bold: false,
    italic: false,
    underline: false,
    insertUnorderedList: false,
    insertOrderedList: false,
};

const RichTextEditor = ({ value, onChange, placeholder = '', height = 'min-h-24 max-h-56' }) => {
    const editorRef = useRef(null);
    const selectionRef = useRef(null);
    const [toolbarState, setToolbarState] = useState(defaultToolbarState);

    useEffect(() => {
        if (!editorRef.current) return;
        const current = editorRef.current.innerHTML;
        const next = value || '';
        if (current !== next) {
            editorRef.current.innerHTML = next;
        }
    }, [value]);

    const saveSelection = () => {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0 || !editorRef.current) return;

        const range = selection.getRangeAt(0);
        if (!editorRef.current.contains(range.commonAncestorContainer)) return;

        selectionRef.current = range.cloneRange();
    };

    const isSelectionInsideEditor = () => {
        const selection = window.getSelection();
        if (!selection || selection.rangeCount === 0 || !editorRef.current) return false;

        const range = selection.getRangeAt(0);
        return editorRef.current.contains(range.commonAncestorContainer);
    };

    const updateToolbarState = () => {
        if (!isSelectionInsideEditor()) {
            setToolbarState(defaultToolbarState);
            return;
        }

        setToolbarState({
            bold: document.queryCommandState('bold'),
            italic: document.queryCommandState('italic'),
            underline: document.queryCommandState('underline'),
            insertUnorderedList: document.queryCommandState('insertUnorderedList'),
            insertOrderedList: document.queryCommandState('insertOrderedList'),
        });
    };

    const handleSelectionUpdate = () => {
        saveSelection();
        updateToolbarState();
    };

    const getButtonClassName = (isActive, extraClasses = '') => (
        `px-2 py-1 text-xs rounded ${isActive ? 'bg-yellow-200 text-gray-900' : 'hover:bg-gray-200'} ${extraClasses}`.trim()
    );

    useEffect(() => {
        const handleSelectionChange = () => {
            updateToolbarState();
        };

        document.addEventListener('selectionchange', handleSelectionChange);

        return () => {
            document.removeEventListener('selectionchange', handleSelectionChange);
        };
    }, []);

    const placeCaretAtEnd = () => {
        if (!editorRef.current) return;

        const range = document.createRange();
        range.selectNodeContents(editorRef.current);
        range.collapse(false);

        const selection = window.getSelection();
        if (!selection) return;

        selection.removeAllRanges();
        selection.addRange(range);
        selectionRef.current = range.cloneRange();
    };

    const restoreSelection = () => {
        const selection = window.getSelection();
        if (!selection) return;

        if (!selectionRef.current) {
            placeCaretAtEnd();
            return;
        }

        selection.removeAllRanges();
        selection.addRange(selectionRef.current);
    };

    const applyCommand = (command, commandValue = null) => {
        if (!editorRef.current) return;
        editorRef.current.focus();
        restoreSelection();
        document.execCommand(command, false, commandValue);
        saveSelection();
        updateToolbarState();
        onChange(editorRef.current.innerHTML);
    };

    return (
        <div className="w-full border border-gray-300 rounded-sm bg-white">
            <div className="flex items-center gap-1 border-b border-gray-200 p-1 bg-gray-50">
                <button type="button" className={getButtonClassName(toolbarState.bold)} onMouseDown={(e) => e.preventDefault()} onClick={() => applyCommand('bold')} title="Fet">B</button>
                <button type="button" className={getButtonClassName(toolbarState.italic, 'italic')} onMouseDown={(e) => e.preventDefault()} onClick={() => applyCommand('italic')} title="Kursiv">I</button>
                <button type="button" className={getButtonClassName(toolbarState.underline, 'underline')} onMouseDown={(e) => e.preventDefault()} onClick={() => applyCommand('underline')} title="Understruken">U</button>
                <button type="button" className={getButtonClassName(toolbarState.insertUnorderedList)} onMouseDown={(e) => e.preventDefault()} onClick={() => applyCommand('insertUnorderedList')} title="Punktlista">•</button>
                <button type="button" className={getButtonClassName(toolbarState.insertOrderedList)} onMouseDown={(e) => e.preventDefault()} onClick={() => applyCommand('insertOrderedList')} title="Numrerad lista">1.</button>
                <select
                    className="text-xs border border-gray-300 rounded px-1 py-[2px] ml-2"
                    defaultValue="3"
                    onChange={(e) => applyCommand('fontSize', e.target.value)}
                    title="Textstorlek"
                >
                    <option value="2">Liten</option>
                    <option value="3">Normal</option>
                    <option value="4">Stor</option>
                    <option value="5">X-Stor</option>
                </select>
                <button type="button" className="ml-auto px-2 py-1 text-xs rounded hover:bg-gray-200" onMouseDown={(e) => e.preventDefault()} onClick={() => applyCommand('removeFormat')} title="Rensa formatering">Rensa format</button>
            </div>
            <div
                ref={editorRef}
                contentEditable
                suppressContentEditableWarning
                className={`${height} overflow-y-auto p-2 text-xs focus:outline-none [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:list-decimal [&_ol]:pl-5 [&_li]:ml-1`}
                onInput={(e) => {
                    handleSelectionUpdate();
                    onChange(e.currentTarget.innerHTML);
                }}
                onMouseUp={handleSelectionUpdate}
                onKeyUp={handleSelectionUpdate}
                onFocus={handleSelectionUpdate}
                data-placeholder={placeholder}
            />
        </div>
    );
};

export default RichTextEditor