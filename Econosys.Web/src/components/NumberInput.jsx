import React, { useLayoutEffect, useRef, useState } from "react";

const NumberInput = ({
    rowId,
    field,
    value,
    disabled = false,
    decimals = 2,
    maxDecimals,
    hideZeroDecimals = false,
    suffix = '',
    className,
    onChange,
    ...props
}) => {
    const [displayValue, setDisplayValue] = useState("");
    const [isFocused, setIsFocused] = useState(false);
    const editedSinceFocus = useRef(false);
    const inputRef = useRef(null);

    useLayoutEffect(() => {
        if (isFocused) inputRef.current?.select();
    }, [isFocused]);

    const getDecimalsFor = (num) => {
        if (!hideZeroDecimals || decimals === 0) return decimals;
        const factor = 10 ** decimals;
        return Math.round(num * factor) % factor === 0 ? 0 : decimals;
    };

    // Format number for display when blurred
    const formatNumber = (num) => {
        if (num == null || num === "") return "";
        const displayDecimals = getDecimalsFor(num);
        const parts = num.toFixed(displayDecimals).split(".");
        let intPart = parts[0];
        let decPart = parts[1];
        // Add space as thousand separator
        intPart = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, " ");
        const formattedValue = displayDecimals > 0 ? `${intPart},${decPart}` : intPart;
        return suffix ? `${formattedValue} ${suffix}` : formattedValue;
    };

    // Prepare number for editing (Swedish comma but no spaces)
    const formatForEdit = (num) => {
        if (num == null || num === "") return "";
        const editDecimals = getDecimalsFor(num);
        return editDecimals > 0
            ? num.toFixed(editDecimals).replace(".", ",")
            : Math.round(num).toString();
    };

    const handleFocus = () => {
        editedSinceFocus.current = false;
        setIsFocused(true);
        setDisplayValue(formatForEdit(value));
    };

    const handleBlur = () => {
        setIsFocused(false);
        if (!editedSinceFocus.current) return;
        editedSinceFocus.current = false;

        // If input is empty, set to null
        if (displayValue === "" || displayValue == null) {
            if (value != null && value !== "") onChange(rowId, field, null);
            setDisplayValue("");
            return;
        }

        // Parse and format again for display
        const parsed = parseInput(displayValue);
        if (!isNaN(parsed)) {
            if (parsed !== value) onChange(rowId, field, parsed);
            setDisplayValue(formatNumber(parsed));
        } else {
            setDisplayValue(formatNumber(value));
        }
    };

    const parseInput = (str) => {
        if (typeof str !== "string") return NaN;
        // Replace comma with dot, remove spaces
        const normalized = str.replace(/\s+/g, "").replace(",", ".");
        return parseFloat(normalized);
    };

    const handleChange = (e) => {
        editedSinceFocus.current = true;
        let newVal = e.target.value;

        // Only allow digits, comma, and spaces while typing
        newVal = newVal.replace(/[^\d,]/g, "");

        if (maxDecimals != null && newVal.includes(",")) {
            const [integerPart, ...fractionParts] = newVal.split(",");
            newVal = `${integerPart},${fractionParts.join("").slice(0, maxDecimals)}`;
        }

        // Prevent decimals if decimals = 0
        if (decimals === 0) {
            newVal = newVal.replace(/,.*$/, "");
        }

        setDisplayValue(newVal);

        const parsed = parseInput(newVal);
        if (!isNaN(parsed) && parsed !== value) onChange(rowId, field, parsed);
    };

    return (
        <input
            ref={inputRef}
            type="text"
            value={isFocused ? displayValue : formatNumber(value)}
            disabled={disabled}
            onFocus={handleFocus}
            onBlur={handleBlur}
            onChange={handleChange}
            className={`text-right px-2 py-1 focus:outline-none ${className}`}
            {...props}
        />
    );
};

export default NumberInput;
